"""El comando ggezc, que es lo que lanza el IDE.

    ggezc run <archivo.ggez>     ejecuta
    ggezc check <archivo.ggez>   corre las pruebas del nivel.json de su carpeta
    ggezc tokens <archivo.ggez>  solo vuelca tokens, para depurar

stdout = salida del programa, stderr = errores, codigo 0 = ok.
Contrato completo en Traductor/README.md.
"""

from __future__ import annotations

import argparse
import hashlib
import sys
import threading
from pathlib import Path

from . import semantico
from .cargador import cargar
from .errores import ErrorGgez
from .evaluador import Evaluador
from .lexer import tokenizar
from .logros import emitir as emitir_logros
from .nivel import ARCHIVO_NIVEL, ErrorNivel, comprobar
from .tokens import TipoToken

EXITO = 0
FALLO_TRADUCCION = 1
FALLO_ARCHIVO = 2

PASOS_POR_DEFECTO = 5_000_000   # unos 6 s: corta un ciclo sin fin sin colgar el IDE
PILA_HILO = 512 * 1024 * 1024   # la recursion de GGEZ usa ~7 marcos de Python por llamada


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


def _comando_run(ruta: Path, limite_pasos: int, eventos: bool) -> int:
    """Lexico y sintactico los hace el cargador por archivo; aca semantico y ejecucion."""
    archivos = cargar(ruta)
    semantico.analizar_archivos(archivos)
    evaluador = Evaluador(limite_pasos=limite_pasos)
    try:
        evaluador.ejecutar_archivos(archivos)
    finally:
        # Linea para el panel de juego del IDE, que no la muestra en la terminal
        if eventos:
            print(f"@ggez pasos {evaluador.pasos} {limite_pasos}", flush=True)
    # Solo llega aca si corrio sin errores
    if eventos:
        emitir_logros(archivos)
        print(f"@ggez programa {_huella(ruta)} {' '.join(sorted(evaluador.usos))}".rstrip(), flush=True)
    return EXITO


def _huella(ruta: Path) -> str:
    """Igual para el mismo programa aunque cambien espacios o comentarios: 'programas distintos' no se infla re-corriendo."""
    tokens = [(t.tipo.name, t.lexema) for t in tokenizar(_leer(ruta), ruta.name)]
    return hashlib.sha1(repr(tokens).encode()).hexdigest()[:16]


def main(argv: list[str] | None = None) -> int:
    """Corre en un hilo con pila grande: la del hilo principal no alcanza para recursion profunda."""
    resultado = [FALLO_TRADUCCION]

    def correr() -> None:
        # argparse sale con SystemExit (--help, argumentos malos) y en un hilo se perderia el codigo
        try:
            resultado[0] = _main(argv)
        except SystemExit as salida:
            resultado[0] = salida.code if isinstance(salida.code, int) else FALLO_TRADUCCION

    sys.setrecursionlimit(200_000)
    threading.stack_size(PILA_HILO)
    hilo = threading.Thread(target=correr)
    hilo.start()
    hilo.join()
    return resultado[0]


def _main(argv: list[str] | None) -> int:
    parser = argparse.ArgumentParser(
        prog="ggezc",
        description="Traductor del lenguaje GGEZ.",
    )
    sub = parser.add_subparsers(dest="comando", required=True)

    for comando, ayuda in (("run", "interpreta y ejecuta un archivo .ggez"),
                           ("check", f"corre las pruebas del {ARCHIVO_NIVEL} que esta junto al archivo")):
        p = sub.add_parser(comando, help=ayuda)
        p.add_argument("archivo")
        p.add_argument("--pasos", type=int, default=PASOS_POR_DEFECTO,
                       help=f"mana: maximo de pasos (por defecto {PASOS_POR_DEFECTO:,}; 0 = sin limite)")
        p.add_argument("--eventos", action="store_true", help="emite lineas @ggez para el panel de juego del IDE")

    p_tok = sub.add_parser("tokens", help="vuelca la lista de tokens (depuracion)")
    p_tok.add_argument("archivo")

    args = parser.parse_args(argv)
    ruta = Path(args.archivo)
    if args.comando != "tokens" and args.pasos < 0:
        parser.error("--pasos no puede ser negativo")

    if not ruta.is_file():
        print(f"ggezc: no existe el archivo: {ruta}", file=sys.stderr)
        return FALLO_ARCHIVO

    try:
        if args.comando == "tokens":
            return _comando_tokens(ruta)
        if args.comando == "check":
            return EXITO if comprobar(ruta, args.pasos, args.eventos) else FALLO_TRADUCCION
        return _comando_run(ruta, args.pasos, args.eventos)
    except ErrorGgez as err:
        # Formato que parsea el panel Problemas del IDE; el archivo puede ser uno importado
        print(err.formatear(err.archivo or ruta.name), file=sys.stderr)
        if getattr(args, "eventos", False):
            print("@ggez fallo", flush=True)
        return FALLO_TRADUCCION
    except RecursionError:
        # Red de seguridad: expresiones anidadas a lo bestia, nunca un traceback de Python
        print(f"{ruta.name}:1:1: error en ejecucion: el programa se anido demasiado para ejecutarse", file=sys.stderr)
        return FALLO_TRADUCCION
    except ErrorNivel as err:
        print(f"{ARCHIVO_NIVEL}: {err}", file=sys.stderr)
        return FALLO_ARCHIVO
    except OSError as err:
        print(f"ggezc: no se pudo leer {ruta}: {err}", file=sys.stderr)
        return FALLO_ARCHIVO


if __name__ == "__main__":
    sys.exit(main())
