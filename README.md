# Paradigmas de Lenguaje - GGEZ / C#

Proyecto comparativo de paradigmas de programación. Este README cubre la parte en **C#** (`csharp/`), con gráficos vía [Raylib-cs](https://github.com/ChrisDill/Raylib-cs).

## Estructura del proyecto

```
Paradigmas_lenguaje_GGEZ/
├── flake.nix                    # entorno reproducible para Linux (SDK .NET + nixGL)
├── flake.lock                   # versiones fijadas, generado por nix
├── .gitignore
└── csharp/
    ├── ParadigmasLenguajePropio.sln     # solución, agrupa proyectos
    └── src/ParadigmasLenguajePropio/
        ├── ParadigmasLenguajePropio.csproj   # dependencias y config del proyecto
        ├── Ggez.cs                   ← código de la app (top-level statements, es el "main")
        ├── bin/                      ← GENERADO por dotnet, no se versiona
        └── obj/                      ← GENERADO por dotnet, no se versiona
```

Para agregar más código, sumá archivos `.cs` dentro de `csharp/src/ParadigmasLenguajePropio/` — el SDK de .NET los incluye automáticamente en la compilación, no hace falta listarlos en ningún lado.

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

Este único comando compila el proyecto y abre la ventana gráfica con OpenGL nativo (usa `nixGLIntel` para resolver los drivers Mesa/Intel del sistema). La primera vez tarda más porque nix baja el SDK y las librerías gráficas desde su caché; las siguientes veces es rápido.

Para cerrar la app: click en la X de la ventana, o `Ctrl+C` en la terminal donde corre.

### Alternativa manual (para trabajar/debuggear)

```bash
nix develop              # entra al entorno con dotnet + libs graficas
cd csharp
dotnet build              # solo compila, chequea errores
nixGLIntel dotnet run --project src/ParadigmasLenguajePropio   # compila y corre con GL
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

Desde una terminal (PowerShell o cmd), parado en `csharp/`:

```powershell
cd csharp
dotnet run --project src\ParadigmasLenguajePropio
```

Eso alcanza — `Raylib-cs` ya trae los binarios nativos para Windows (`raylib.dll`), así que no hace falta ningún wrapper extra tipo `nixGLIntel` (eso solo existe para resolver un problema específico de Linux+nix). `dotnet run` compila y abre la ventana directo.

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
