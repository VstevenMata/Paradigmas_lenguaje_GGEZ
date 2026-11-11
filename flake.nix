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
          # sin libGL, Avalonia no puede abrir GLX y cae en silencio a render por CPU (~30 fps)
          pkgs.libGL
          pkgs.mesa
        ];

        # fuera de NixOS el libGL de nix no encuentra los drivers de mesa por si solo
        gpuEnv = ''
          export LIBGL_DRIVERS_PATH="${pkgs.mesa}/lib/dri"
          export __EGL_VENDOR_LIBRARY_FILENAMES="${pkgs.mesa}/share/glvnd/egl_vendor.d/50_mesa.json"
          export GBM_BACKENDS_PATH="${pkgs.mesa}/lib/gbm"
        '';

        up = pkgs.writeShellApplication {
          name = "up";
          runtimeInputs = [ pkgs.dotnet-sdk_10 pkgs.git ];
          text = ''
            repo_root="$(git rev-parse --show-toplevel)"
            cd "$repo_root/ParadigmasLenguajePropio"
            export DOTNET_CLI_TELEMETRY_OPTOUT=1
            export LD_LIBRARY_PATH="${graphicsLibPath}''${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
            ${gpuEnv}
            echo "Compilando y levantando ParadigmasLenguajePropio..."
            dotnet run --project src/ParadigmasLenguajePropio
          '';
        };

        # Ejecutable del release; al publicar otra version cambiar la url y el hash (nix-prefetch-url <url>)
        ggezRelease = pkgs.fetchurl {
          url = "https://github.com/VstevenMata/Paradigmas_lenguaje_GGEZ/releases/download/v1.0/GGEZ";
          sha256 = "045zwjvzqkrp9ck0sipfl2bw7dckj0qjf4mgwqmj4db0978bczjm";
        };
        ggezBin = pkgs.runCommand "ggez-bin" { } "install -Dm755 ${ggezRelease} $out/bin/GGEZ";

        # NixOS no tiene /lib64/ld-linux ni libicu donde el ejecutable los espera: se corre en un entorno FHS
        ggez = pkgs.buildFHSEnv {
          name = "ggez";
          targetPkgs = p: [
            p.icu p.zlib p.openssl p.stdenv.cc.cc.lib
            p.libX11 p.libxcursor p.libxi p.libxrandr p.libxext p.libxrender
            p.libice p.libsm p.fontconfig p.freetype p.libGL
          ];
          runScript = "${ggezBin}/bin/GGEZ";
        };
      in
      rec {
        # En NixOS (o con nix en cualquier distro): nix run github:VstevenMata/Paradigmas_lenguaje_GGEZ
        packages.default = ggez;
        apps.ggez = flake-utils.lib.mkApp { drv = ggez; };

        devShells.default = pkgs.mkShell {
          packages = [
            pkgs.dotnet-sdk_10
          ];

          LD_LIBRARY_PATH = graphicsLibPath;

          shellHook = ''
            export DOTNET_CLI_TELEMETRY_OPTOUT=1
            ${gpuEnv}
            echo "dotnet SDK: $(dotnet --version)"
          '';
        };

        # Compila y levanta la app grafica en un solo comando:
        #   nix run .#up
        apps.up = flake-utils.lib.mkApp { drv = up; };
        apps.default = apps.ggez;
      });
}
