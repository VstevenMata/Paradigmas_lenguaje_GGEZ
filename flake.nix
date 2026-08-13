{
  description = "Entorno de desarrollo C# (.NET) para el proyecto de Paradigmas de Lenguaje";

  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";
    flake-utils.url = "github:numtide/flake-utils";
    nixgl.url = "github:nix-community/nixGL";
  };

  outputs = { self, nixpkgs, flake-utils, nixgl }:
    flake-utils.lib.eachDefaultSystem (system:
      let
        pkgs = import nixpkgs {
          inherit system;
          config.allowUnfree = true;
          overlays = [ nixgl.overlay ];
        };

        # X11/audio: nixGLIntel ya resuelve GL/DRI por su cuenta y antepone
        # su propio LD_LIBRARY_PATH, dejando este al final de la cadena.
        graphicsLibPath = pkgs.lib.makeLibraryPath [
          pkgs.libX11
          pkgs.libxcursor
          pkgs.libxi
          pkgs.libxrandr
          pkgs.libxinerama
          pkgs.libxkbcommon
          pkgs.wayland
          pkgs.alsa-lib
        ];

        up = pkgs.writeShellApplication {
          name = "up";
          runtimeInputs = [ pkgs.dotnet-sdk_9 pkgs.nixgl.nixGLIntel pkgs.git ];
          text = ''
            repo_root="$(git rev-parse --show-toplevel)"
            cd "$repo_root/ParadigmasLenguajePropio"
            export DOTNET_CLI_TELEMETRY_OPTOUT=1
            export LD_LIBRARY_PATH="${graphicsLibPath}''${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
            echo "Compilando y levantando ParadigmasLenguajePropio..."
            nixGLIntel dotnet run --project src/ParadigmasLenguajePropio
          '';
        };
      in
      rec {
        # nixGL resuelve el OpenGL nativo del sistema (Mesa/Intel en este caso)
        # dentro del devShell de nix en una distro no-NixOS como Fedora.
        # Uso: nixGLIntel dotnet run --project ...
        devShells.default = pkgs.mkShell {
          packages = [
            pkgs.dotnet-sdk_9
            pkgs.nixgl.nixGLIntel
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
