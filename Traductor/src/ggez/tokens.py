"""Tipos de token y tabla de palabras reservadas de GGEZ.

Corresponde a la seccion 1 de GRAMATICA.md. Si se agrega o se renombra una
palabra reservada, este es el unico archivo que hay que tocar.
"""

from __future__ import annotations

from dataclasses import dataclass
from enum import Enum, auto


class TipoToken(Enum):
    # literales
    ENTERO = auto()
    REAL = auto()
    CADENA = auto()
    CARACTER = auto()

    IDENTIFICADOR = auto()
    RESERVADA = auto()

    # delimitadores
    LLAVE_IZQ = auto()      # {  agrupacion, parametros, llamadas
    LLAVE_DER = auto()      # }
    MENOR = auto()          # <  indices y literales de coleccion
    MAYOR = auto()          # >
    CORCHETE_IZQ = auto()   # [  genericos
    CORCHETE_DER = auto()   # ]
    BARRA = auto()          # |  separador de elementos
    PUNTO = auto()          # .  acceso a miembro

    FIN_LINEA = auto()      # cierra la sentencia (GGEZ no usa ';')
    FIN_ARCHIVO = auto()


# Las 60 palabras reservadas, en los mismos grupos que la Propuesta de Lenguaje.
# chain / swap / dodge no estaban en el documento original: son los operadores
# logicos AND / OR / NOT que faltaban (decision D-1 de GRAMATICA.md).
RESERVADAS: frozenset[str] = frozenset({
    # estructura
    "lobby", "launch", "start", "THE", "GAME", "travel", "from",
    "server", "region", "quit",
    # declaraciones
    "stash", "hardcoded", "noloot", "afk",
    # tipos
    "score", "aim", "lore", "win", "pixel",
    "squad", "stack", "queue", "inventory",
    # booleanos
    "hit", "miss",
    # flujo
    "scan", "fallback", "farm", "survive", "grind",
    "loadout", "pick", "noob", "ragequit", "respawn",
    # funciones
    "combo", "reward", "drops",
    # errores
    "clutch", "revive", "checkpoint", "wipe",
    # POO
    "guild", "questline", "evolves", "spawn", "me", "open", "party", "solo",
    # bloques
    "glhf", "gg",
    # operadores
    "gain", "drain", "crit", "split", "scrap",
    "matches", "mismatch", "over", "under", "atleast", "atmost",
    "set", "boost", "sap", "buff", "nerf",
    "chain", "swap", "dodge",
})

# Subconjuntos que el analizador sintactico consulta seguido.
TIPOS_SIMPLES: frozenset[str] = frozenset({"score", "aim", "lore", "win", "pixel"})
CONSTRUCTORES_COLECCION: frozenset[str] = frozenset({"squad", "stack", "queue"})
OP_RELACIONALES: frozenset[str] = frozenset({
    "matches", "mismatch", "over", "under", "atleast", "atmost",
})
OP_ADITIVOS: frozenset[str] = frozenset({"gain", "drain"})
OP_MULTIPLICATIVOS: frozenset[str] = frozenset({"crit", "split", "scrap"})
OP_ASIGNACION: frozenset[str] = frozenset({"set", "boost", "sap"})
OP_INCREMENTO: frozenset[str] = frozenset({"buff", "nerf"})
MODIFICADORES: frozenset[str] = frozenset({"open", "party", "solo"})

SIMBOLOS: dict[str, TipoToken] = {
    "{": TipoToken.LLAVE_IZQ,
    "}": TipoToken.LLAVE_DER,
    "<": TipoToken.MENOR,
    ">": TipoToken.MAYOR,
    "[": TipoToken.CORCHETE_IZQ,
    "]": TipoToken.CORCHETE_DER,
    "|": TipoToken.BARRA,
    ".": TipoToken.PUNTO,
}


@dataclass(frozen=True)
class Token:
    tipo: TipoToken
    lexema: str
    linea: int
    columna: int
    # valor ya convertido para los literales (int, float, str); None en el resto
    valor: object = None

    def es(self, *reservadas: str) -> bool:
        """True si es una palabra reservada y coincide con alguna de las dadas."""
        return self.tipo is TipoToken.RESERVADA and self.lexema in reservadas

    def __str__(self) -> str:
        if self.tipo in (TipoToken.FIN_LINEA, TipoToken.FIN_ARCHIVO):
            return self.tipo.name
        return f"{self.tipo.name}({self.lexema!r})"
