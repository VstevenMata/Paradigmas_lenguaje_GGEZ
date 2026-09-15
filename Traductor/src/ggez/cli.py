"""Linea de comandos del traductor: el binario 'ggezc' que invoca el IDE.

Respeta el contrato acordado con el lado C# (ver Traductor/README.md):

    ggezc run <archivo.ggez>     interpreta y ejecuta
    ggezc tokens <archivo.ggez>  vuelca la lista de tokens (para depurar)

    stdout -> la salida del programa GGEZ
    stderr -> los errores, uno por linea, como archivo:linea:columna: etapa: mensaje
    codigo -> 0 si corrio bien, distinto de 0 si fallo
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

from .errores import ErrorGgez
from .lexer import tokenizar
from .tokens import TipoToken

EXITO = 0
FALLO_TRADUCCION = 1
FALLO_ARCHIVO = 2


def _leer(ruta: Path) -> str:
    # utf-8-sig para tolerar el BOM que dejan algunos editores en Windows
    return ruta.read_text(encoding="utf-8-sig")


def _comando_tokens(ruta: Path) -> int:
    for token in tokenizar(_leer(ruta), ruta.name):
        if token.tipo is TipoToken.FIN_ARCHIVO:
            print(f"{token.linea:>4}:{token.columna:<3} {token}")
            continue
        print(f"{token.linea:>4}:{token.columna:<3} {token}")
    return EXITO


def _comando_run(ruta: Path) -> int:
    tokens = tokenizar(_leer(ruta), ruta.name)
    # TODO: parser -> analisis semantico -> evaluador.
    # Mientras tanto se reporta el avance real en vez de fingir que ejecuta.
    reales = [t for t in tokens if t.tipo not in (TipoToken.FIN_LINEA, TipoToken.FIN_ARCHIVO)]
    print(f"Analisis lexico correcto: {len(reales)} tokens en {ruta.name}.")
    print("El analizador sintactico todavia no esta implementado.")
    return FALLO_TRADUCCION


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(
        prog="ggezc",
        description="Traductor del lenguaje GGEZ.",
    )
    sub = parser.add_subparsers(dest="comando", required=True)

    p_run = sub.add_parser("run", help="interpreta y ejecuta un archivo .ggez")
    p_run.add_argument("archivo")

    p_tok = sub.add_parser("tokens", help="vuelca la lista de tokens (depuracion)")
    p_tok.add_argument("archivo")

    args = parser.parse_args(argv)
    ruta = Path(args.archivo)

    if not ruta.is_file():
        print(f"ggezc: no existe el archivo: {ruta}", file=sys.stderr)
        return FALLO_ARCHIVO

    try:
        if args.comando == "tokens":
            return _comando_tokens(ruta)
        return _comando_run(ruta)
    except ErrorGgez as err:
        # el formato que el panel Problemas del IDE sabe parsear
        print(err.formatear(ruta.name), file=sys.stderr)
        return FALLO_TRADUCCION
    except OSError as err:
        print(f"ggezc: no se pudo leer {ruta}: {err}", file=sys.stderr)
        return FALLO_ARCHIVO


if __name__ == "__main__":
    sys.exit(main())
