{
  description = "Entorno de desarrollo C# (.NET) para el proyecto de Paradigmas de Lenguaje";

  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";
    flake-utils.url = "github:numtide/flake-utils";
  };

  outputs = { self, nixpkgs, flake-utils }:
    flake-utils.lib.eachDefaultSystem (system:
      let
        pkgs = import nixpkgs {
          inherit system;
          config.allowUnfree = true;
        };

        # X11/Wayland: ventana e input de Avalonia.
        # fontconfig/freetype: los usa SkiaSharp (motor de render de Avalonia) para texto.
        # libice/libsm: protocolo de sesión X11 que Avalonia inicializa al arrancar.
        graphicsLibPath = pkgs.lib.makeLibraryPath [
          pkgs.libX11
          pkgs.libxcursor
          pkgs.libxi
          pkgs.libxrandr
          pkgs.libxinerama
          pkgs.libxkbcommon
          pkgs.wayland
          pkgs.alsa-lib
          pkgs.fontconfig
          pkgs.freetype
          pkgs.libice
          pkgs.libsm
        ];

        up = pkgs.writeShellApplication {
          name = "up";
          runtimeInputs = [ pkgs.dotnet-sdk_10 pkgs.git ];
          text = ''
            repo_root="$(git rev-parse --show-toplevel)"
            cd "$repo_root/ParadigmasLenguajePropio"
            export DOTNET_CLI_TELEMETRY_OPTOUT=1
            export LD_LIBRARY_PATH="${graphicsLibPath}''${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
            echo "Compilando y levantando ParadigmasLenguajePropio..."
            dotnet run --project src/ParadigmasLenguajePropio
          '';
        };
      in
      rec {
        devShells.default = pkgs.mkShell {
          packages = [
            pkgs.dotnet-sdk_10
          ];

          LD_LIBRARY_PATH = graphicsLibPath;

          shellHook = ''
            export DOTNET_CLI_TELEMETRY_OPTOUT=1
            echo "dotnet SDK: $(dotnet --version)"
          '';
        };

        # Compila y levanta la app grafica en un solo comando:
        #   nix run .#up
        apps.up = flake-utils.lib.mkApp { drv = up; };
        apps.default = apps.up;
      });
}
