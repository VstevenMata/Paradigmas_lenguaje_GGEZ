"""Traductor del lenguaje GGEZ.

Etapas (ver Problemas de traduccion de lenguajes.pdf):

    programa fuente
        -> analisis lexico     (lexer.py)
        -> analisis sintactico (sintactico.py)
        -> analisis semantico  (semantico.py)
        -> evaluacion          (evaluador.py)

Las cuatro corren de punta a punta sobre el subconjunto del lenguaje descrito en
el bloque 1 de TODO.md. Los modulos se llaman 'arbol' y 'sintactico' y no 'ast' y
'parser' porque esos dos nombres chocan con la biblioteca estandar de Python.
"""

from . import arbol
from .errores import (
    ErrorEjecucion,
    ErrorGgez,
    ErrorLexico,
    ErrorSemantico,
    ErrorSintactico,
)
from .evaluador import Evaluador, ejecutar
from .lexer import Lexer, tokenizar
from .semantico import Semantico
from .sintactico import Sintactico, parsear
from .tokens import TipoToken, Token

__all__ = [
    "ErrorEjecucion",
    "ErrorGgez",
    "ErrorLexico",
    "ErrorSemantico",
    "ErrorSintactico",
    "Evaluador",
    "Lexer",
    "Semantico",
    "Sintactico",
    "TipoToken",
    "Token",
    "arbol",
    "ejecutar",
    "parsear",
    "tokenizar",
]
