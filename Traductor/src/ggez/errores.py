"""Errores del traductor, en el formato que el IDE ya sabe leer.

El contrato con el IDE en C# (ver README.md) pide que cada error salga por stderr
como:

    archivo.ggez:LINEA:COLUMNA: error: mensaje

Ese formato es el de gcc. El panel Problemas del IDE lo parsea con la expresion
regular de TerminalViewModel.cs y arma una fila clickeable que salta a la linea.
"""

from __future__ import annotations


class ErrorGgez(Exception):
    """Error con posicion en el archivo fuente.

    Las subclases solo cambian la etapa que se reporta, para que al leer la
    terminal quede claro si fallo el analisis lexico, el sintactico, el
    semantico o la ejecucion.
    """

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
