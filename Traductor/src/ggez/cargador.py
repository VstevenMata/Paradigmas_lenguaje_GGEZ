"""Carga el principal y sus travel una sola vez cada uno, en orden de dependencias (principal al final) -> D-28."""

from __future__ import annotations

from dataclasses import dataclass, field, fields
from pathlib import Path

from . import arbol
from .errores import ErrorImportacion
from .lexer import tokenizar
from .sintactico import parsear

# travel physics from arsenal: la libreria, no un archivo (bloque 6)
LIBRERIA = "arsenal"
# travel utilidades from base: en la misma carpeta
MISMA_CARPETA = "base"
MODULOS_LIBRERIA = {"chat", "physics", "rng", "clock", "save"}


@dataclass
class Archivo:
    ruta: Path
    nombre: str
    modulo: str | None                # el X de 'travel X'; None en el principal
    programa: arbol.Programa
    importa: dict[str, "Archivo"] = field(default_factory=dict)


def cargar(ruta: Path) -> list[Archivo]:
    principal = ruta.resolve()
    raiz = principal.parent
    cargados: dict[Path, Archivo] = {}
    por_modulo: dict[str, Archivo] = {}
    orden: list[Archivo] = []
    en_camino: list[Path] = []        # los que estoy cargando ahora, para ver circulos

    def leer(ruta_archivo: Path, modulo: str | None) -> Archivo:
        try:
            nombre = ruta_archivo.relative_to(raiz).as_posix()
        except ValueError:
            # Solo pasa con accesos directos de carpeta; la ruta con C: rompe al IDE
            nombre = ruta_archivo.name
        texto = ruta_archivo.read_text(encoding="utf-8-sig")
        programa = parsear(tokenizar(texto, nombre), nombre)
        return Archivo(ruta_archivo, nombre, modulo, programa)

    def visitar(archivo: Archivo) -> None:
        en_camino.append(archivo.ruta)
        for travel in archivo.programa.declaraciones:
            if isinstance(travel, arbol.Travel) and travel.paquete != LIBRERIA:
                archivo.importa[travel.modulo] = traer(archivo, travel)
        en_camino.pop()
        orden.append(archivo)

    def traer(archivo: Archivo, travel: arbol.Travel) -> Archivo:
        def error(mensaje: str) -> ErrorImportacion:
            return ErrorImportacion(mensaje, travel.linea, travel.columna, archivo.nombre)

        if travel.modulo in MODULOS_LIBRERIA:
            raise error(f"'{travel.modulo}' ya es un modulo de la libreria: el archivo tiene que llamarse distinto")
        if travel.modulo in archivo.importa:
            raise error(f"'{travel.modulo}' ya se importo en este archivo")

        carpeta = archivo.ruta.parent
        if travel.paquete != MISMA_CARPETA:
            carpeta = carpeta / travel.paquete
        destino = (carpeta / f"{travel.modulo}.ggez").resolve()

        if destino in en_camino:
            vuelta = en_camino[en_camino.index(destino):] + [destino]
            nombres = " -> ".join(cargados[r].nombre for r in vuelta)
            raise error(f"importacion en circulo: {nombres}")

        if destino in cargados:
            return cargados[destino]

        if not destino.is_file():
            donde = "la misma carpeta" if travel.paquete == MISMA_CARPETA else f"la carpeta '{travel.paquete}'"
            raise error(f"no existe {travel.modulo}.ggez en {donde}")
        if travel.modulo in por_modulo:
            # utilidades es utilidades.X en todos lados: dos no pueden llamarse igual
            raise error(
                f"ya hay otro modulo llamado '{travel.modulo}' ({por_modulo[travel.modulo].nombre})"
            )

        nuevo = leer(destino, travel.modulo)
        for declaracion in nuevo.programa.declaraciones:
            if isinstance(declaracion, arbol.PuntoEntrada):
                raise ErrorImportacion(
                    "un archivo que se importa con travel es una biblioteca: no puede "
                    "tener lobby.launch",
                    declaracion.linea, declaracion.columna, nuevo.nombre,
                )   # -> D-29
        _con_nombre_de_modulo(nuevo.programa, travel.modulo)

        cargados[destino] = nuevo
        por_modulo[travel.modulo] = nuevo
        visitar(nuevo)
        return nuevo

    inicio = leer(principal, None)
    cargados[principal] = inicio
    visitar(inicio)
    return orden


def _con_nombre_de_modulo(programa: arbol.Programa, modulo: str) -> None:
    """Renombra Jugador a utilidades.Jugador para que los pasos 3 y 4 vean un solo nombre sin choques."""
    propios = {
        d.nombre for d in programa.declaraciones
        if isinstance(d, (arbol.DeclGuild, arbol.DeclInventory))
    }

    def con_modulo(nombre: str) -> str:
        return f"{modulo}.{nombre}" if nombre in propios else nombre

    for nodo in list(_recorrer(programa)):
        if isinstance(nodo, arbol.Tipo):
            nodo.nombre = con_modulo(nodo.nombre)
        elif isinstance(nodo, arbol.Spawn):
            nodo.tipo = con_modulo(nodo.tipo)
        elif isinstance(nodo, arbol.DeclInventory):
            nodo.nombre = con_modulo(nodo.nombre)
        elif isinstance(nodo, arbol.DeclGuild):
            # el constructor se llama igual que el guild: se renombran juntos
            for metodo in nodo.metodos:
                if metodo.nombre == nodo.nombre:
                    metodo.nombre = con_modulo(metodo.nombre)
            nodo.nombre = con_modulo(nodo.nombre)
            if nodo.padre is not None:
                nodo.padre = con_modulo(nodo.padre)


def _recorrer(nodo: arbol.Nodo):
    yield nodo
    for campo in fields(nodo):
        valor = getattr(nodo, campo.name)
        hijos = valor if isinstance(valor, list) else [valor]
        for hijo in hijos:
            if isinstance(hijo, arbol.Nodo):
                yield from _recorrer(hijo)
