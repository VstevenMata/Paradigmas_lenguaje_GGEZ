"""El comando ggezc, que es lo que lanza el IDE.

    ggezc run <archivo.ggez>     ejecuta
    ggezc tokens <archivo.ggez>  solo vuelca tokens, para depurar

stdout = salida del programa, stderr = errores, codigo 0 = ok.
Contrato completo en Traductor/README.md.
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

from . import semantico
from .errores import ErrorGgez
from .evaluador import ejecutar
from .lexer import tokenizar
from .sintactico import parsear
from .tokens import TipoToken

EXITO = 0
FALLO_TRADUCCION = 1
FALLO_ARCHIVO = 2


def _leer(ruta: Path) -> str:
    # utf-8-sig: se come el BOM que meten algunos editores en Windows
    return ruta.read_text(encoding="utf-8-sig")


def _comando_tokens(ruta: Path) -> int:
    for token in tokenizar(_leer(ruta), ruta.name):
        if token.tipo is TipoToken.FIN_ARCHIVO:
            print(f"{token.linea:>4}:{token.columna:<3} {token}")
            continue
        print(f"{token.linea:>4}:{token.columna:<3} {token}")
    return EXITO


def _comando_run(ruta: Path) -> int:
    """Las cuatro etapas: lexico -> sintactico -> semantico -> ejecucion."""
    tokens = tokenizar(_leer(ruta), ruta.name)
    arbol = parsear(tokens, ruta.name)
    semantico.analizar(arbol, ruta.name)
    ejecutar(arbol, ruta.name)
    return EXITO


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
        # este formato lo parsea el panel Problemas del IDE
        print(err.formatear(ruta.name), file=sys.stderr)
        return FALLO_TRADUCCION
    except OSError as err:
        print(f"ggezc: no se pudo leer {ruta}: {err}", file=sys.stderr)
        return FALLO_ARCHIVO


if __name__ == "__main__":
    sys.exit(main())
