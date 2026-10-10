"""ggezc check: corre las pruebas de nivel.json contra el .ggez del estudiante."""

from __future__ import annotations

import io
import json
import sys
from contextlib import redirect_stderr, redirect_stdout
from pathlib import Path

from . import arbol, semantico
from .cargador import _recorrer, cargar
from .errores import ErrorGgez
from .evaluador import Evaluador
from .lexer import tokenizar
from .sintactico import parsear

ARCHIVO_NIVEL = "nivel.json"
ORIGEN_LLAMADA = "<prueba>"


class ErrorNivel(Exception):
    pass


def leer_nivel(ruta: Path) -> dict:
    """nivel.json puede venir de otra persona: se revisa la forma antes de usarlo."""
    try:
        datos = json.loads(ruta.read_text(encoding="utf-8-sig"))
    except json.JSONDecodeError as err:
        raise ErrorNivel(f"JSON invalido en la linea {err.lineno}: {err.msg}") from None
    if not isinstance(datos, dict):
        raise ErrorNivel("debe ser un objeto { ... }")
    if not isinstance(datos.get("titulo", ""), str):
        raise ErrorNivel("'titulo' debe ser texto")
    meta = datos.get("meta_pasos", 0)
    if not isinstance(meta, int) or isinstance(meta, bool) or meta < 0:
        raise ErrorNivel("'meta_pasos' debe ser un entero positivo (0 = sin meta)")

    pruebas = datos.get("pruebas")
    if not isinstance(pruebas, list) or not pruebas:
        raise ErrorNivel("necesita una lista 'pruebas' con al menos una prueba")
    for i, prueba in enumerate(pruebas, 1):
        if not isinstance(prueba, dict) or not isinstance(prueba.get("salida"), str):
            raise ErrorNivel(f"la prueba {i} necesita 'salida' de texto")
        for campo in ("entrada", "llamada"):
            if not isinstance(prueba.get(campo, ""), str):
                raise ErrorNivel(f"'{campo}' de la prueba {i} debe ser texto")
        if "\n" in prueba.get("llamada", ""):
            raise ErrorNivel(f"la 'llamada' de la prueba {i} debe ser de una sola linea")
    return datos


def _con_llamada(archivo, llamada: str) -> None:
    """Cambia el lobby.launch del estudiante por uno que solo imprime la llamada."""
    texto = f"lobby.launch{{start THE GAME}} glhf\n    chat.send{{{llamada}}}\ngg\n"
    entrada = parsear(tokenizar(texto, ORIGEN_LLAMADA), ORIGEN_LLAMADA).declaraciones
    # Linea 0 marca los errores de la llamada: no son del archivo del estudiante
    for nodo in _recorrer(entrada[0]):
        nodo.linea = nodo.columna = 0
    propias = [d for d in archivo.programa.declaraciones if not isinstance(d, arbol.PuntoEntrada)]
    archivo.programa.declaraciones = propias + entrada


def _correr(ruta: Path, prueba: dict, limite_pasos: int) -> tuple[str, int, str | None]:
    """Devuelve (salida, pasos, error). Los errores de compilacion del estudiante suben: sin compilar no hay pruebas."""
    # Se recarga en cada prueba porque el semantico anota el arbol
    archivos = cargar(ruta)
    if "llamada" in prueba:
        _con_llamada(archivos[-1], prueba["llamada"])
    evaluador = Evaluador(limite_pasos=limite_pasos)
    salida = io.StringIO()
    stdin, sys.stdin = sys.stdin, io.StringIO(prueba.get("entrada", ""))
    error = None
    try:
        semantico.analizar_archivos(archivos)
        with redirect_stdout(salida), redirect_stderr(io.StringIO()):
            evaluador.ejecutar_archivos(archivos)
    except ErrorGgez as err:
        if err.linea == 0:
            error = f"la llamada {prueba['llamada']} fallo: {err.mensaje}"
        elif err.etapa != "error en ejecucion":
            raise
        else:
            error = err.formatear()
    except EOFError:
        error = "chat.load pidio mas lineas de las que trae la prueba"
    finally:
        sys.stdin = stdin
    return salida.getvalue(), evaluador.pasos, error


def _normalizar(texto: str) -> str:
    # Espacios al final de linea y lineas vacias al final no cuentan
    return "\n".join(linea.rstrip() for linea in texto.strip("\n").splitlines())


def comprobar(ruta: Path, limite_pasos: int, eventos: bool) -> bool:
    if not (ruta.parent / ARCHIVO_NIVEL).is_file():
        raise ErrorNivel(f"no hay {ARCHIVO_NIVEL} en la carpeta de {ruta.name}: no es un nivel")
    nivel = leer_nivel(ruta.parent / ARCHIVO_NIVEL)
    titulo = nivel.get("titulo") or ruta.parent.name
    meta = nivel.get("meta_pasos", 0)
    pruebas = nivel["pruebas"]

    print(f"Nivel: {titulo}", flush=True)
    pasadas, pasos_max = 0, 0
    reportado = False
    for i, prueba in enumerate(pruebas, 1):
        salida, pasos, error = _correr(ruta, prueba, limite_pasos)
        pasos_max = max(pasos_max, pasos)
        nombre = prueba.get("llamada") or (f"entrada {prueba['entrada']!r}" if "entrada" in prueba else "programa completo")
        if error is None and _normalizar(salida) == _normalizar(prueba["salida"]):
            pasadas += 1
            print(f"  [OK]    Prueba {i}: {nombre}")
            continue
        print(f"  [FALLO] Prueba {i}: {nombre}")
        if error is not None:
            print(f"          error:    {error}")
            # El primero va a stderr para que el panel Problemas salte a la linea
            if not reportado and not error.startswith("la llamada") and ": error en ejecucion: " in error:
                print(error, file=sys.stderr, flush=True)
                reportado = True
        else:
            print(f"          esperaba: {_normalizar(prueba['salida'])!r}")
            print(f"          obtuvo:   {_normalizar(salida)!r}")

    meta_texto = f" (meta {meta:,})" if meta else ""
    print(f"Resultado: {pasadas} de {len(pruebas)} pruebas. Pasos de la prueba mas cara: {pasos_max:,}{meta_texto}")
    if eventos:
        print(f"@ggez pasos {pasos_max} {limite_pasos}")
        print(f"@ggez nivel {pasadas} {len(pruebas)} {pasos_max} {meta} {titulo}", flush=True)
    return pasadas == len(pruebas)
