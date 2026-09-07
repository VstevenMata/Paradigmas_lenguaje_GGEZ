# Paradigmas de Lenguaje - GGEZ / C#

Proyecto comparativo de paradigmas de programación. Este README cubre la parte en **C#** (`ParadigmasLenguajePropio/`), una app de escritorio con [Avalonia](https://avaloniaui.net/) (MVVM, con [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet)).

## Estructura del proyecto

```
Paradigmas_lenguaje_GGEZ/
├── flake.nix                    # entorno reproducible para Linux (SDK .NET)
├── flake.lock                   # versiones fijadas, generado por nix
├── .gitignore
└── ParadigmasLenguajePropio/
    ├── ParadigmasLenguajePropio.sln     # solución, agrupa proyectos
    └── src/ParadigmasLenguajePropio/
        ├── ParadigmasLenguajePropio.csproj   # dependencias y config del proyecto
        ├── Program.cs                 ← punto de entrada (Main)
        ├── App.axaml(.cs)             ← arranque de la app, carga el tema
        ├── Views/                     ← XAML + code-behind de cada panel/ventana
        ├── ViewModels/                ← estado y lógica de cada Vista (MVVM)
        ├── Models/                    ← datos puros (árbol de archivos, líneas de terminal, etc.)
        ├── Services/                  ← diálogos, selector de archivos, proceso de la terminal
        ├── Controls/                  ← controles custom (bisel Win95, editor de texto, etc.)
        ├── Converters/                ← conversores de binding (íconos, colores, etc.)
        ├── Themes/TemaBase.axaml      ← mecánica de temas (Templates/Style), compartida por todos
        ├── Themes/Paletas/            ← un .axaml por tema (Win95, Zelda, ...), solo colores/fuente
        ├── Assets/                    ← íconos y fuentes
        ├── bin/                       ← GENERADO por dotnet, no se versiona
        └── obj/                       ← GENERADO por dotnet, no se versiona
```

Para agregar más código, sumá archivos `.cs`/`.axaml` dentro de `ParadigmasLenguajePropio/src/ParadigmasLenguajePropio/` — el SDK de .NET los incluye automáticamente en la compilación, no hace falta listarlos en ningún lado.

## Qué hay en `.gitignore` y por qué

```
bin/
obj/
```

`bin/` (binarios compilados) y `obj/` (metadata intermedia de build, cache de NuGet) los regenera `dotnet` solo, en cualquier sistema operativo, cada vez que compilás. Son "basura" reproducible: distinta por máquina, no debe versionarse. **No hay que crearlas a mano** — aparecen solas la primera vez que alguien compila el proyecto, sea con nix, con el instalador de Microsoft, o como sea.

---

## Cómo correrlo en Linux (con nix)

Usamos `nix` para no instalar el SDK de .NET ni librerías gráficas a nivel de sistema — todo vive aislado en `/nix/store/`.

### 1. Prender el daemon de nix (una vez por sesión/reinicio)

Si tu Linux tiene instalación multi-usuario de nix, el daemon puede estar apagado. Comprobalo con:

```bash
systemctl is-active nix-daemon
```

Si dice `inactive`, arrancalo:

```bash
sudo systemctl start nix-daemon
```
```bash
Para apagar el deamon 
sudo systemctl stop nix-daemon

```


Esto **no es permanente** — si reiniciás la PC, hay que volver a correrlo. (Si preferís que arranque solo en cada boot: `sudo systemctl enable --now nix-daemon`.)

### 2. Compilar y levantar la app

Parado en la raíz del repo (o cualquier subcarpeta):

```bash
nix run .#up
```

Este único comando compila el proyecto y abre la ventana gráfica (Avalonia usa Skia para dibujar, no necesita OpenGL/`nixGLIntel` como la versión anterior con Raylib). La primera vez tarda más porque nix baja el SDK desde su caché; las siguientes veces es rápido.

Para cerrar la app: click en la X de la ventana, o `Ctrl+C` en la terminal donde corre.

### Alternativa manual (para trabajar/debuggear)

```bash
nix develop              # entra al entorno con dotnet + libs graficas
cd ParadigmasLenguajePropio
dotnet build              # solo compila, chequea errores
dotnet run --project src/ParadigmasLenguajePropio   # compila y corre
exit                       # sale del entorno nix
```

---

## Cómo correrlo en Windows

En Windows **no se usa nix** (no corre nativo fuera de WSL2). El camino simple:

### 1. Instalar el SDK de .NET

Descargar e instalar el [.NET SDK](https://dotnet.microsoft.com/download) (versión 9).  
Verificar instalación:

```powershell
dotnet --version
```

### 2. Compilar y correr

Desde una terminal (PowerShell o cmd), parado en `ParadigmasLenguajePropio/`:

```powershell
cd ParadigmasLenguajePropio
dotnet run --project src\ParadigmasLenguajePropio
```

Eso alcanza — Avalonia ya trae todo lo necesario para Windows vía NuGet, no hace falta ningún paso extra. `dotnet run` compila y abre la ventana directo.

### Notas

- No hace falta instalar Visual Studio completo, con el SDK y la terminal alcanza.
- `dotnet build` compila sin correr, igual que en Linux.
- `bin/` y `obj/` se generan igual que en Linux, dentro de `src\ParadigmasLenguajePropio\`.

---

## Resumen rápido

| | Linux (este repo) | Windows |
|---|---|---|
| Instalar SDK | `nix run .#up` lo resuelve solo | Instalador oficial de .NET |
| Daemon extra | `sudo systemctl start nix-daemon` (si está apagado) | No aplica |
| Compilar | `dotnet build` (dentro de `nix develop`) | `dotnet build` |
| Compilar + correr | `nix run .#up` | `dotnet run --project src\ParadigmasLenguajePropio` |
| `bin/`/`obj/` | Se generan solos, gitignoreados | Igual |
