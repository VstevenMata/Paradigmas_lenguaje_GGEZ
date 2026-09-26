"""Arbol de analisis sintactico (AST) de GGEZ.

Cada nodo corresponde a un no-terminal de GRAMATICA.md y guarda su linea y
columna, para que el analizador semantico y el evaluador puedan reportar
errores con posicion exacta (ver errores.py).

El modulo se llama 'arbol' y no 'ast' para no chocar con el modulo 'ast' de la
biblioteca estandar de Python.
"""

from __future__ import annotations

from dataclasses import dataclass, field


@dataclass
class Nodo:
    linea: int = field(default=0, kw_only=True)
    columna: int = field(default=0, kw_only=True)


# --------------------------------------------------------------------- tipos

@dataclass
class Tipo(Nodo):
    """Nombre de tipo tal como aparece escrito en el programa."""
    nombre: str                          # score, aim, lore, win, pixel o un identificador
    interno: "Tipo | None" = None        # el T de squad[T], stack[T], queue[T]

    def __str__(self) -> str:
        return f"{self.nombre}[{self.interno}]" if self.interno else self.nombre


# ------------------------------------------------------------------ programa

@dataclass
class Programa(Nodo):
    declaraciones: list[Nodo]


@dataclass
class Travel(Nodo):
    """travel <modulo> from <paquete>"""
    modulo: str
    paquete: str


@dataclass
class PuntoEntrada(Nodo):
    """lobby.launch{start THE GAME} glhf ... gg"""
    cuerpo: "Bloque"


@dataclass
class Bloque(Nodo):
    """glhf ... gg  -- abre un alcance nuevo"""
    sentencias: list[Nodo]


# --------------------------------------------------------------- sentencias

@dataclass
class DeclVariable(Nodo):
    """stash <tipo> <nombre> [set <valor>]  /  hardcoded <tipo> <nombre> set <valor>"""
    tipo: Tipo
    nombre: str
    valor: Nodo | None = None
    constante: bool = False


@dataclass
class Asignacion(Nodo):
    """<designador> set|boost|sap <expresion>"""
    destino: Nodo
    operador: str
    valor: Nodo


@dataclass
class Incremento(Nodo):
    """<designador> buff|nerf"""
    destino: Nodo
    operador: str


@dataclass
class SentenciaExpresion(Nodo):
    """Una expresion usada como sentencia, tipicamente una llamada."""
    expresion: Nodo


# -------------------------------------------------------------- expresiones

@dataclass
class Literal(Nodo):
    valor: object
    tipo: str                            # score, aim, lore, pixel, win, afk


@dataclass
class Identificador(Nodo):
    nombre: str


@dataclass
class Me(Nodo):
    """me -- la instancia actual dentro de un guild"""


@dataclass
class Binaria(Nodo):
    izquierda: Nodo
    operador: str
    derecha: Nodo


@dataclass
class Unaria(Nodo):
    operador: str                        # dodge (NOT) o drain (menos unario)
    operando: Nodo


@dataclass
class Acceso(Nodo):
    """<objeto> . <campo>"""
    objeto: Nodo
    campo: str


@dataclass
class Indice(Nodo):
    """<objeto> < <indice> >"""
    objeto: Nodo
    indice: Nodo


@dataclass
class Llamada(Nodo):
    """<destino> { <argumentos> }"""
    destino: Nodo
    argumentos: list[Nodo]


@dataclass
class LiteralColeccion(Nodo):
    """< a | b | c >"""
    elementos: list[Nodo]
