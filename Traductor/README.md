# Traductor de GGEZ

Acá va el código fuente (Pascal) del traductor/intérprete de GGEZ. Para el diseño interno
(lexer → parser → evaluador) ver [`../traductor-ggez.md`](../traductor-ggez.md) — este README
cubre otra cosa: el **contrato de invocación**, o sea qué comando exacto va a correr el IDE en
C#, con qué argumentos, y qué espera recibir de vuelta. Esto existe para que la persona que
escriba el traductor y la persona que lo conecte al IDE puedan trabajar en paralelo sin
pisarse: mientras se respete este contrato, no importa cómo esté implementado adentro.

## Estructura

```
Traductor/
├── README.md         ← este archivo
├── Makefile           ← build/run desde terminal
├── .gitignore          ← bin/ y los intermedios de fpc (*.o, *.ppu)
├── src/
│   └── traductor.pas   ← punto de entrada; ahí crece el lexer/parser/evaluador
└── bin/                ← GENERADO por `make build`, no se versiona
    └── ggezc
```

`bin/` es análogo al `bin/`/`obj/` de `ParadigmasLenguajePropio/` (ver
[`../README.md`](../README.md)): lo regenera `fpc`, no se toca a mano ni se commitea.

## Correrlo a mano

Necesita el [Free Pascal Compiler](https://www.freepascal.org/) (`fpc`) instalado.

```bash
cd Traductor
make build                          # compila src/traductor.pas -> bin/ggezc
make run ARCHIVO=ruta/al/archivo.ggez   # compila (si hace falta) y corre
```

O directo, una vez compilado:

```bash
./bin/ggezc run ruta/al/archivo.ggez
```

Ahora mismo `traductor.pas` es un **stub**: valida argumentos y que el archivo exista, pero
todavía no interpreta nada — sale con código 1 y un mensaje de "todavía no está implementado".
Es la base sobre la que se agrega el lexer/parser/evaluador de `../traductor-ggez.md`, sin
tocar el contrato de abajo.

## El contrato (esto es lo que no puede cambiar sin avisar del otro lado)

### Comando

```
ggezc run <archivo.ggez>
```

- `ggezc` es el binario compilado (`bin/ggezc`). El lado C# lo va a invocar por su **ruta
  completa** (ej. `<raíz-del-repo>/Traductor/bin/ggezc`), no por nombre en el PATH — no hay que
  instalarlo en ningún lado, solo compilarlo una vez.
- Un solo subcomando por ahora: `run`, que interpreta y ejecuta directo (opción 2 de las tres
  que lista `../traductor-ggez.md` — la más simple para arrancar; si más adelante se pasa a
  transpilar o a bytecode+VM, este comando puede quedar igual por fuera aunque cambie por
  dentro).
- Working directory: la carpeta del proyecto abierto en el IDE (la misma que ya usa la
  terminal — ver `Explorer.CarpetaRaiz` / `TerminalInstanceViewModel.DirectorioActual`), así
  que `<archivo.ggez>` puede ir como ruta relativa a eso.

### stdout / stderr / código de salida

- **stdout**: la salida real del programa GGEZ (lo que el usuario debe ver correr).
- **stderr**: errores — uno por línea. Mientras no exista un panel de problemas que parsee
  posición (ver `../ideas/panel-problemas.md`), el formato exacto no es crítico, pero conviene
  ir dejando `archivo:línea:columna: mensaje` (estilo gcc) como costumbre, para que a futuro se
  pueda parsear sin tocar el traductor.
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

### Condiciones antes de invocarlo (responsabilidad del lado C#)

1. **El archivo tiene que estar guardado en disco** — el traductor lee del filesystem, no del
   buffer del editor. `ConstruirAsync()` ya llama `Editor.GuardarTodoAsync()` antes de nada
   (ver más abajo), así que esto ya está cubierto.
2. **Tiene que haber un `.ggez` para pasarle** — si no hay ningún archivo abierto, no hay nada
   que ejecutar; no lanzar el proceso en ese caso.
3. **El binario tiene que existir** (`bin/ggezc`) — si `Traductor/` todavía no se compiló, se
   puede compilar al vuelo con `make -C Traductor build` antes de correr, o simplemente fallar
   con un mensaje claro en la terminal en vez de un error críptico de "comando no encontrado".

## Cómo se conecta con el botón Play/Construir del IDE

Ya existe el enganche, solo falta que llame al comando de arriba. Ahora mismo:

- El menú **Ejecutar → Construir** (`MenuBarViewModel.cs:24`) dispara
  `ManejarOpcionMenu("Construir")` (`MainWindowViewModel.cs:247`), que llama
  `ConstruirAsync()` (`MainWindowViewModel.cs:163`). Hoy esa función solo guarda todo y abre la
  terminal — ahí es donde hay que agregar el lanzamiento real, algo como:

  ```csharp
  async Task ConstruirAsync()
  {
      if (!HayLugarParaTerminal) return;
      await Editor.GuardarTodoAsync();
      TerminalVisible = true;
      Terminal.TabActiva = 1;
      // agregar acá: lanzar "<raíz>/Traductor/bin/ggezc run <archivo activo>"
      // en la instancia de terminal activa (mismo Process.Start que ya usa
      // TerminalProcessService.Lanzar para cualquier otro comando)
  }
  ```

- **Ejecutar → Pausar/Reanudar** y el botón de pausa de `PanelJuegoViewModel` ya están armados
  para alternar `PanelJuego.EnPausa`; falta que ese toggle también mande `SIGSTOP`/`SIGCONT` al
  proceso del traductor en Linux (o la alternativa de `ntdll.dll` en Windows) — el detalle
  completo de esa parte ya está en `../traductor-ggez.md`.
- **Ejecutar → Ver salida** ya cambia a la pestaña de terminal/errores — no necesita cambios.

Con esto, "botón play" y "comando de terminal" son la misma ruta: ambos terminan llamando
`TerminalProcessService.Lanzar(comando, directorioActual)` con el mismo `comando`. No hace
falta un camino separado para cada botón.
