"""Traductor del lenguaje GGEZ.

Etapas (ver Problemas de traduccion de lenguajes.pdf):

    programa fuente
        -> analisis lexico     (lexer.py)      [LISTO]
        -> analisis sintactico (parser.py)     [pendiente]
        -> analisis semantico  (semantico.py)  [pendiente]
        -> evaluacion          (evaluador.py)  [pendiente]
"""

from .errores import (
    ErrorEjecucion,
    ErrorGgez,
    ErrorLexico,
    ErrorSemantico,
    ErrorSintactico,
)
from .lexer import Lexer, tokenizar
from .tokens import TipoToken, Token

__all__ = [
    "ErrorEjecucion",
    "ErrorGgez",
    "ErrorLexico",
    "ErrorSemantico",
    "ErrorSintactico",
    "Lexer",
    "TipoToken",
    "Token",
    "tokenizar",
]
