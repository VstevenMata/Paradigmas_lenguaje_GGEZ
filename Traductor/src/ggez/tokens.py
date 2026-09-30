"""Tokens y palabras reservadas (GRAMATICA.md seccion 1). Una palabra del lenguaje se cambia SOLO aca."""

from __future__ import annotations

from dataclasses import dataclass
from enum import Enum, auto


class TipoToken(Enum):
    ENTERO = auto()
    REAL = auto()
    CADENA = auto()
    CARACTER = auto()

    IDENTIFICADOR = auto()
    RESERVADA = auto()

    LLAVE_IZQ = auto()      # {  agrupacion, parametros, llamadas
    LLAVE_DER = auto()
    MENOR = auto()          # <  indices y literales de coleccion
    MAYOR = auto()
    CORCHETE_IZQ = auto()   # [  genericos
    CORCHETE_DER = auto()
    BARRA = auto()          # |  separador de elementos
    PUNTO = auto()          # .  acceso a miembro

    FIN_LINEA = auto()      # cierra la sentencia (GGEZ no usa ';')
    FIN_ARCHIVO = auto()


# chain/swap/dodge (AND/OR/NOT) no estaban en la propuesta original -> D-1
RESERVADAS: frozenset[str] = frozenset({
    "lobby", "launch", "start", "THE", "GAME", "travel", "from",
    "server", "region", "quit",
    "stash", "hardcoded", "noloot", "afk",
    "score", "aim", "lore", "win", "pixel",
    "squad", "stack", "queue", "inventory",
    "hit", "miss",
    "scan", "fallback", "farm", "survive", "grind",
    "loadout", "pick", "noob", "ragequit", "respawn",
    "combo", "reward", "drops",
    "clutch", "revive", "checkpoint", "wipe",
    "guild", "questline", "evolves", "spawn", "me", "open", "party", "solo",
    "glhf", "gg",
    "gain", "drain", "crit", "split", "scrap",
    "matches", "mismatch", "over", "under", "atleast", "atmost",
    "set", "boost", "sap", "buff", "nerf",
    "chain", "swap", "dodge",
})

TIPOS_SIMPLES: frozenset[str] = frozenset({"score", "aim", "lore", "win", "pixel"})
# Solo vectores -> D-21; stack y queue siguen reservadas para dar buen error
CONSTRUCTORES_COLECCION: frozenset[str] = frozenset({"squad"})
COLECCIONES_RETIRADAS: frozenset[str] = frozenset({"stack", "queue"})
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
    # literales: el valor ya convertido. None en los demas tokens
    valor: object = None

    def es(self, *reservadas: str) -> bool:
        return self.tipo is TipoToken.RESERVADA and self.lexema in reservadas

    def __str__(self) -> str:
        if self.tipo in (TipoToken.FIN_LINEA, TipoToken.FIN_ARCHIVO):
            return self.tipo.name
        return f"{self.tipo.name}({self.lexema!r})"
