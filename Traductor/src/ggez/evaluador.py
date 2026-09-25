"""Evaluador de GGEZ: recorre el AST y ejecuta el programa.

Ultima fase. Se eligio interpretar directo sobre el arbol (y no transpilar ni
generar bytecode) porque es lo mas simple y el contrato con el IDE -- 'ggezc
run archivo.ggez' -- no cambia si algun dia se reemplaza por dentro.

La salida del programa GGEZ va a stdout; chat.rage escribe en stderr. Eso es lo
que separa la terminal del IDE en sus pestanas Terminal y Errores.
"""

from __future__ import annotations

import sys

from . import arbol
from .errores import ErrorEjecucion


class Entorno:
    """Los valores en tiempo de ejecucion. Un nivel por cada glhf...gg."""

    def __init__(self, padre: "Entorno | None" = None) -> None:
        self.valores: dict[str, object] = {}
        self.padre = padre

    def declarar(self, nombre: str, valor: object) -> None:
        self.valores[nombre] = valor

    def obtener(self, nombre: str) -> object:
        entorno: Entorno | None = self
        while entorno is not None:
            if nombre in entorno.valores:
                return entorno.valores[nombre]
            entorno = entorno.padre
        raise KeyError(nombre)

    def asignar(self, nombre: str, valor: object) -> None:
        entorno: Entorno | None = self
        while entorno is not None:
            if nombre in entorno.valores:
                entorno.valores[nombre] = valor
                return
            entorno = entorno.padre
        raise KeyError(nombre)


class Modulo:
    """Un modulo de la libreria estandar, como chat."""

    def __init__(self, nombre: str, funciones: dict[str, object]) -> None:
        self.nombre = nombre
        self.funciones = funciones


class Evaluador:
    def __init__(self, archivo: str = "<entrada>") -> None:
        self.archivo = archivo
        self.entorno = Entorno()
        self.entorno.declarar("chat", Modulo("chat", {
            "send": self._chat_send,
            "rage": self._chat_rage,
            "load": self._chat_load,
        }))

    def _error(self, mensaje: str, nodo: arbol.Nodo) -> ErrorEjecucion:
        return ErrorEjecucion(mensaje, nodo.linea, nodo.columna, self.archivo)

    # ------------------------------------------------------- libreria estandar

    def _texto(self, valor: object) -> str:
        """Como se ve un valor GGEZ impreso."""
        if valor is None:
            return "afk"
        if isinstance(valor, bool):
            return "hit" if valor else "miss"
        if isinstance(valor, float):
            # sin el .0 sobrante cuando el aim cayo en un entero exacto
            return f"{valor:g}"
        return str(valor)

    def _chat_send(self, argumentos: list[object]) -> None:
        print(" ".join(self._texto(a) for a in argumentos))

    def _chat_rage(self, argumentos: list[object]) -> None:
        print(" ".join(self._texto(a) for a in argumentos), file=sys.stderr)

    def _chat_load(self, argumentos: list[object]) -> str:
        return input()

    # ---------------------------------------------------------------- programa

    def ejecutar(self, programa: arbol.Programa) -> None:
        for declaracion in programa.declaraciones:
            if isinstance(declaracion, arbol.PuntoEntrada):
                self.bloque(declaracion.cuerpo)
            # los travel no hacen nada todavia: la resolucion de modulos entre
            # archivos es el bloque 6 del TODO

    def bloque(self, nodo: arbol.Bloque) -> None:
        anterior = self.entorno
        self.entorno = Entorno(anterior)
        try:
            for sentencia in nodo.sentencias:
                self.sentencia(sentencia)
        finally:
            self.entorno = anterior

    def sentencia(self, nodo: arbol.Nodo) -> None:
        if isinstance(nodo, arbol.DeclVariable):
            valor = self.expresion(nodo.valor) if nodo.valor is not None else None
            if str(nodo.tipo) == "aim" and isinstance(valor, int) and not isinstance(valor, bool):
                valor = float(valor)               # promocion score -> aim
            self.entorno.declarar(nodo.nombre, valor)

        elif isinstance(nodo, arbol.Asignacion):
            self.asignacion(nodo)

        elif isinstance(nodo, arbol.Incremento):
            actual = self.expresion(nodo.destino)
            paso = 1 if nodo.operador == "buff" else -1
            self._guardar(nodo.destino, self._numero(actual, nodo) + paso)

        elif isinstance(nodo, arbol.SentenciaExpresion):
            self.expresion(nodo.expresion)

        elif isinstance(nodo, arbol.Bloque):
            self.bloque(nodo)

        else:
            raise self._error("sentencia no soportada todavia", nodo)

    def asignacion(self, nodo: arbol.Asignacion) -> None:
        valor = self.expresion(nodo.valor)

        if nodo.operador != "set":
            actual = self._numero(self.expresion(nodo.destino), nodo)
            incremento = self._numero(valor, nodo)
            valor = actual + incremento if nodo.operador == "boost" else actual - incremento

        self._guardar(nodo.destino, valor)

    def _guardar(self, destino: arbol.Nodo, valor: object) -> None:
        if isinstance(destino, arbol.Identificador):
            try:
                self.entorno.asignar(destino.nombre, valor)
            except KeyError:
                raise self._error(f"'{destino.nombre}' no esta declarada", destino) from None
            return
        raise self._error("destino de asignacion no soportado todavia", destino)

    def _numero(self, valor: object, nodo: arbol.Nodo) -> int | float:
        if isinstance(valor, bool) or not isinstance(valor, (int, float)):
            raise self._error(f"se esperaba un numero y se obtuvo {self._texto(valor)}", nodo)
        return valor

    # -------------------------------------------------------------- expresiones

    def expresion(self, nodo: arbol.Nodo) -> object:
        if isinstance(nodo, arbol.Literal):
            return nodo.valor

        if isinstance(nodo, arbol.Identificador):
            try:
                return self.entorno.obtener(nodo.nombre)
            except KeyError:
                raise self._error(f"'{nodo.nombre}' no esta declarada", nodo) from None

        if isinstance(nodo, arbol.Unaria):
            valor = self.expresion(nodo.operando)
            if nodo.operador == "dodge":
                return not valor
            return -self._numero(valor, nodo)

        if isinstance(nodo, arbol.Binaria):
            return self.binaria(nodo)

        if isinstance(nodo, arbol.LiteralColeccion):
            return [self.expresion(e) for e in nodo.elementos]

        if isinstance(nodo, arbol.Acceso):
            objeto = self.expresion(nodo.objeto)
            if isinstance(objeto, Modulo):
                if nodo.campo not in objeto.funciones:
                    raise self._error(
                        f"el modulo {objeto.nombre} no tiene '{nodo.campo}'", nodo
                    )
                return objeto.funciones[nodo.campo]
            if isinstance(objeto, list) and nodo.campo == "size":
                return len(objeto)
            raise self._error(f"no se puede acceder a '{nodo.campo}'", nodo)

        if isinstance(nodo, arbol.Indice):
            objeto = self.expresion(nodo.objeto)
            indice = self.expresion(nodo.indice)
            if not isinstance(objeto, list):
                raise self._error("solo las colecciones se pueden indexar", nodo)
            if not isinstance(indice, int) or isinstance(indice, bool):
                raise self._error("el indice debe ser un score", nodo)
            if not 0 <= indice < len(objeto):
                raise self._error(
                    f"indice fuera de rango: {indice} (la coleccion tiene {len(objeto)})", nodo
                )
            return objeto[indice]

        if isinstance(nodo, arbol.Llamada):
            funcion = self.expresion(nodo.destino)
            argumentos = [self.expresion(a) for a in nodo.argumentos]
            if not callable(funcion):
                raise self._error("esto no es una funcion", nodo)
            return funcion(argumentos)

        raise self._error("expresion no soportada todavia", nodo)

    def binaria(self, nodo: arbol.Binaria) -> object:
        op = nodo.operador

        # chain y swap cortocircuitan: el lado derecho solo se evalua si hace falta
        if op == "chain":
            return bool(self.expresion(nodo.izquierda)) and bool(self.expresion(nodo.derecha))
        if op == "swap":
            return bool(self.expresion(nodo.izquierda)) or bool(self.expresion(nodo.derecha))

        izq = self.expresion(nodo.izquierda)
        der = self.expresion(nodo.derecha)

        if op == "matches":
            return izq == der
        if op == "mismatch":
            return izq != der

        if op == "gain":
            # suma o concatena: si un lado es texto, el otro se convierte
            if isinstance(izq, str) or isinstance(der, str):
                return self._texto(izq) + self._texto(der)
            return self._numero(izq, nodo) + self._numero(der, nodo)

        a = self._numero(izq, nodo)
        b = self._numero(der, nodo)

        if op == "drain":
            return a - b
        if op == "crit":
            return a * b
        if op == "split":
            if b == 0:
                raise self._error("division entre cero", nodo)
            return a / b                            # siempre aim (D "4.2")
        if op == "scrap":
            if b == 0:
                raise self._error("modulo entre cero", nodo)
            return a % b
        if op == "over":
            return a > b
        if op == "under":
            return a < b
        if op == "atleast":
            return a >= b
        if op == "atmost":
            return a <= b

        raise self._error(f"operador no soportado: {op}", nodo)


def ejecutar(programa: arbol.Programa, archivo: str = "<entrada>") -> None:
    Evaluador(archivo).ejecutar(programa)
