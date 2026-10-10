"""Logros por lo que usa el codigo. Aca solo se detectan; nombres y guardado los tiene el IDE."""

from __future__ import annotations

from . import arbol
from .cargador import _recorrer


def _se_llama_a_si_mismo(combo: arbol.DeclCombo) -> bool:
    return any(
        isinstance(n, arbol.Llamada) and isinstance(n.destino, arbol.Identificador) and n.destino.nombre == combo.nombre
        for n in _recorrer(combo.cuerpo)
    )


def detectar(archivos: list) -> list[str]:
    nodos = [n for archivo in archivos for n in _recorrer(archivo.programa)]
    tipos = {type(n) for n in nodos}
    combos = [n for n in nodos if isinstance(n, arbol.DeclCombo)]
    guilds = [n for n in nodos if isinstance(n, arbol.DeclGuild)]

    logros = {
        "primer-programa": True,
        "combo": bool(combos),
        "recursion": any(_se_llama_a_si_mismo(c) for c in combos),
        "squad": any(isinstance(n, arbol.Tipo) and n.interno is not None for n in nodos),
        "loadout": arbol.Loadout in tipos,
        "tres-ciclos": {arbol.Farm, arbol.Survive, arbol.Grind} <= tipos,
        "clutch": arbol.Clutch in tipos,
        "guild": bool(guilds),
        "herencia": any(g.padre is not None for g in guilds),
        "modulos": len(archivos) > 1,
    }
    return [nombre for nombre, ganado in logros.items() if ganado]


def emitir(archivos: list) -> None:
    for logro in detectar(archivos):
        print(f"@ggez logro {logro}")
