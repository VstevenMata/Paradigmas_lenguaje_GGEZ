"""Traductor de GGEZ.

    fuente -> lexer.py -> sintactico.py -> semantico.py -> evaluador.py

OJO: se llaman arbol.py y sintactico.py, no ast.py ni parser.py, porque esos
dos nombres chocan con modulos de Python y el import falla raro.
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
