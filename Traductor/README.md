# Traductor de GGEZ

Código fuente (**Python**) del traductor/intérprete de GGEZ. Para la definición del
lenguaje (gramática BNF, precedencia, semántica) ver [`GRAMATICA.md`](GRAMATICA.md) — este
README cubre otra cosa: el **contrato de invocación**, o sea qué comando exacto va a correr
el IDE en C#, con qué argumentos, y qué espera recibir de vuelta. Esto existe para que la
persona que escriba el traductor y la persona que lo conecte al IDE puedan trabajar en
paralelo sin pisarse: mientras se respete este contrato, no importa cómo esté implementado
adentro.

> **Nota:** el traductor se iba a escribir en Pascal, pero el equipo decidió hacerlo en
> Python. El contrato de más abajo **no cambió** — es agnóstico del lenguaje de
> implementación. `src/traductor.pas` quedó vacío del plan anterior y se puede borrar.

## Estructura

```
Traductor/
├── README.md            ← este archivo (contrato con el IDE)
├── GRAMATICA.md         ← definición formal del lenguaje (BNF/EBNF)
├── Makefile             ← atajos para correr desde terminal
├── ggezc.py             ← punto de entrada: el "binario" que invoca el IDE
├── ejemplos/
│   ├── clasificador.ggez  ← ejemplo imperativo del documento
│   └── poo.ggez           ← ejemplo de POO del documento
└── src/ggez/
    ├── tokens.py        ← tipos de token + las 60 palabras reservadas
    ├── lexer.py         ← análisis léxico             [LISTO]
    ├── errores.py       ← errores con archivo:línea:columna
    ├── cli.py           ← argumentos y códigos de salida
    ├── parser.py        ← análisis sintáctico         [pendiente]
    ├── semantico.py     ← análisis semántico          [pendiente]
    └── evaluador.py     ← ejecución                   [pendiente]
```

No hace falta compilar nada ni instalar dependencias: solo Python 3.10 o superior.

## Correrlo a mano

```bash
cd Traductor
python ggezc.py run ejemplos/clasificador.ggez
python ggezc.py tokens ejemplos/poo.ggez     # vuelca los tokens, para depurar
```

O con el Makefile:

```bash
make run ARCHIVO=ejemplos/clasificador.ggez
make test                                    # pasa los dos ejemplos
```

**Estado actual:** el analizador léxico está terminado y pasa los dos ejemplos del
documento (190 y 121 tokens). El analizador sintáctico todavía no existe, así que `run`
reporta cuántos tokens reconoció y sale con código 1.

## El contrato (esto es lo que no puede cambiar sin avisar del otro lado)

### Comando

```
ggezc run <archivo.ggez>
```

- `ggezc` es el punto de entrada (`ggezc.py`). El lado C# lo va a invocar por su **ruta
  completa** (ej. `python <raíz-del-repo>/Traductor/ggezc.py`), no por nombre en el PATH —
  no hay que instalarlo en ningún lado.
- Un solo subcomando obligatorio: `run`, que interpreta y ejecuta directo. (`tokens` existe
  solo para depurar; el IDE no lo usa.)
- Working directory: la carpeta del proyecto abierto en el IDE (la misma que ya usa la
  terminal — ver `Explorer.CarpetaRaiz` / `TerminalInstanceViewModel.DirectorioActual`), así
  que `<archivo.ggez>` puede ir como ruta relativa a eso.

### stdout / stderr / código de salida

- **stdout**: la salida real del programa GGEZ (lo que el usuario debe ver correr).
- **stderr**: errores — uno por línea, con el formato
  `archivo:línea:columna: etapa: mensaje` (estilo gcc), donde *etapa* es `error lexico`,
  `error sintactico`, `error semantico` o `error en ejecucion`. Ya está implementado en
  `src/ggez/errores.py`.
- **Código de salida**: `0` = corrió sin errores. Distinto de `0` = algo falló (léxico,
  sintáctico o en ejecución) — no importa cuál, alcanza con no-cero.

Estas tres cosas ya las consume la terminal del IDE tal cual están, sin cambios adicionales:
`TerminalProcessService` (`ParadigmasLenguajePropio/src/ParadigmasLenguajePropio/Services/TerminalProcessService.cs`)
separa stdout/stderr por línea de forma asíncrona, y
`TerminalInstanceViewModel.RevisarProcesoTerminado`
(`ParadigmasLenguajePropio/src/ParadigmasLenguajePropio/ViewModels/TerminalInstanceViewModel.cs:91`)
ya marca en rojo `[el comando terminó con código X]` cuando el código no es 0, y ya copia cada
línea de stderr a la pestaña "Errores" (`OnError`, línea 87 del mismo archivo) sin que haga
falta escribir nada nuevo para eso.

El panel **Problemas** además parsea ese formato con la expresión regular de
`TerminalViewModel.cs:89` y arma una fila clickeable que salta a la línea del error.

### Condiciones antes de invocarlo (responsabilidad del lado C#)

1. **El archivo tiene que estar guardado en disco** — el traductor lee del filesystem, no del
   buffer del editor. `ConstruirAsync()` ya llama `Editor.GuardarTodoAsync()` antes de nada
   (ver más abajo), así que esto ya está cubierto.
2. **Tiene que haber un `.ggez` para pasarle** — si no hay ningún archivo abierto, no hay nada
   que ejecutar; no lanzar el proceso en ese caso.
3. **Tiene que estar Python disponible** — si `python` no está en el PATH, conviene fallar con
   un mensaje claro en la terminal en vez de un error críptico de "comando no encontrado".

## Cómo se conecta con el botón Play/Construir del IDE

**Ya está cableado.** El menú **Ejecutar → Construir** (`MenuBarViewModel.cs:24`) dispara
`ManejarOpcionMenu("Construir")`, que llama `ConstruirAsync()` en `MainWindowViewModel`.
Esa función ahora:

1. Guarda todos los archivos (`Editor.GuardarTodoAsync()`), porque el traductor lee del disco.
2. Abre el panel de la terminal en la pestaña **Terminal**, que es donde cae la salida del
   programa. Los errores se copian solos a la pestaña **Errores**.
3. Toma el archivo activo (`Editor.TabActiva?.RutaCompleta`) y verifica que sea un `.ggez`.
4. Localiza el traductor con `RutaDelTraductor()`, que **sube desde `AppContext.BaseDirectory`
   buscando `Traductor/ggezc.py`**. Con `dotnet run` el binario queda en `bin/Debug/netX/`, seis
   niveles debajo de la raíz del repo; subir hasta encontrarlo evita configurar la ruta a mano
   en cada máquina.
5. Lanza `python "<ruta>/Traductor/ggezc.py" run "<archivo>"` en la terminal.

Si algo falta (archivo sin guardar, no es `.ggez`, no aparece el traductor) escribe un aviso en
la terminal en vez de fallar en silencio.

Piezas que se agregaron del lado C# para esto, por si hay que tocarlas:

| Miembro | Dónde | Para qué |
|---|---|---|
| `TerminalInstanceViewModel.EjecutarComando(string)` | público | corre un comando que no tecleó el usuario, con eco del prompt |
| `TerminalInstanceViewModel.EscribirAviso(string)` | público | un mensaje del IDE, no del proceso |
| `TerminalViewModel.InstanciaParaComando` | propiedad | a qué terminal mandarlo: la enfocada, o la primera visible; salta las PTY reales de la extensión de Claude |
| `TerminalViewModel.EnfocarInstancia(...)` | pasó de privado a público | |

**Pendiente:** **Ejecutar → Pausar/Reanudar** y el botón de pausa de `PanelJuegoViewModel` ya
alternan `PanelJuego.EnPausa`, pero ese toggle todavía no suspende el proceso del traductor
(`SIGSTOP`/`SIGCONT` en Linux, la alternativa de `ntdll.dll` en Windows).

"Botón play" y "comando de terminal" son la misma ruta: ambos terminan llamando
`TerminalProcessService.Lanzar(comando, directorioActual)` con el mismo `comando`.
