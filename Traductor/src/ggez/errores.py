"""Errores del traductor.

Salen por stderr como  archivo.ggez:LINEA:COLUMNA: etapa: mensaje  (estilo gcc).
El panel Problemas del IDE parsea ese formato: si se cambia, se rompe alla.
"""

from __future__ import annotations


class ErrorGgez(Exception):
    """Error con linea y columna. Cada subclase solo cambia la etapa."""

    etapa = "error"

    def __init__(self, mensaje: str, linea: int, columna: int, archivo: str = "") -> None:
        super().__init__(mensaje)
        self.mensaje = mensaje
        self.linea = linea
        self.columna = columna
        self.archivo = archivo

    def formatear(self, archivo: str | None = None) -> str:
        nombre = archivo or self.archivo or "<entrada>"
        return f"{nombre}:{self.linea}:{self.columna}: {self.etapa}: {self.mensaje}"


class ErrorLexico(ErrorGgez):
    etapa = "error lexico"


class ErrorSintactico(ErrorGgez):
    etapa = "error sintactico"


class ErrorSemantico(ErrorGgez):
    etapa = "error semantico"


class ErrorEjecucion(ErrorGgez):
    etapa = "error en ejecucion"
