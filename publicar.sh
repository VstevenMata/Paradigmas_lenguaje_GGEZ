#!/usr/bin/env bash
# Genera el IDE como un solo ejecutable con Python adentro: ./publicar.sh [linux|windows|todo]  (por defecto linux)
set -euo pipefail
cd "$(dirname "$0")"

# dotnet no esta en el PATH: se vuelve a lanzar dentro del entorno de nix
command -v dotnet >/dev/null || exec nix develop -c "$0" "$@"

# Python portatil de python-build-standalone; cambiar version = cambiar estas dos lineas
PY_VERSION=3.12.7
PY_FECHA=20241016

publicar() {
    local rid=$1 triple=$2 nombre=$3
    local python=".cache-python/cpython-$PY_VERSION+$PY_FECHA-$triple-install_only_stripped.tar.gz"
    if [ ! -f "$python" ]; then
        mkdir -p .cache-python
        curl -fL -o "$python.tmp" "https://github.com/astral-sh/python-build-standalone/releases/download/$PY_FECHA/cpython-$PY_VERSION%2B$PY_FECHA-$triple-install_only_stripped.tar.gz"
        mv "$python.tmp" "$python"
    fi

    # El .NET de nix trae un lanzador parcheado que apunta a /nix/store y no corre en otras PCs:
    # con NetCoreTargetingPackRoot vacio se descargan de NuGet los paquetes oficiales de Microsoft
    mkdir -p obj/packs-vacio
    dotnet publish ParadigmasLenguajePropio/src/ParadigmasLenguajePropio -c Release -r "$rid" \
        -p:PythonEmbebido="$PWD/$python" -o "obj/publicar/$rid" \
        -p:NetCoreTargetingPackRoot="$PWD/obj/packs-vacio" -p:AllowMissingPrunePackageData=true

    mkdir -p dist
    cp "obj/publicar/$rid/ParadigmasLenguajePropio$4" "dist/$nombre"
    echo "Listo: dist/$nombre ($(du -h "dist/$nombre" | cut -f1))"
}

case "${1:-linux}" in
    linux)   publicar linux-x64 x86_64-unknown-linux-gnu GGEZ "" ;;
    windows) publicar win-x64 x86_64-pc-windows-msvc GGEZ.exe .exe ;;
    todo)    "$0" linux; "$0" windows ;;
    *)       echo "uso: $0 [linux|windows|todo]" >&2; exit 1 ;;
esac
