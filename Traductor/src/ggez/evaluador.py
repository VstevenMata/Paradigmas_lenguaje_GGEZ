"""Paso 4: recorre el arbol y lo ejecuta. El ultimo.

Va bajando por el arbol y resolviendo de abajo para arriba.

chat.send sale por la salida normal y chat.rage por la de errores. Por eso el
IDE los puede mostrar en pestanas distintas.
"""

from __future__ import annotations

import sys

from . import arbol
from .errores import ErrorEjecucion
from .tokens import CONSTRUCTORES_COLECCION


class Entorno:
    """Donde se guardan los valores mientras corre. Un nivel por glhf...gg.

    Es lo mismo que la tabla de simbolos del paso 3, pero con los valores en
    vez de los tipos.
    """

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
    """Un modulo de la libreria, tipo chat."""

    def __init__(self, nombre: str, funciones: dict[str, object]) -> None:
        self.nombre = nombre
        self.funciones = funciones


class Coleccion:
    """Un squad. Adentro es una lista de Python y nada mas."""

    def __init__(self, constructor: str = "squad") -> None:
        self.constructor = constructor
        self.elementos: list[object] = []

    def __repr__(self) -> str:
        return "<" + " | ".join(str(e) for e in self.elementos) + ">"


class Registro:
    """Un inventory ya creado con spawn."""

    def __init__(self, tipo: str, campos: dict[str, object]) -> None:
        self.tipo = tipo
        self.campos = campos

    def __repr__(self) -> str:
        pares = " | ".join(f"{k}: {v}" for k, v in self.campos.items())
        return f"{self.tipo}{{{pares}}}"


# ragequit, respawn y reward tienen que cortar desde donde sea (podes estar
# metido dentro de tres scan). Los hago con excepciones asi no tengo que andar
# devolviendo un aviso en cada sentencia. El ciclo o el combo las agarra.
class _SenalRagequit(Exception):
    pass


class _SenalRespawn(Exception):
    pass


class _SenalReward(Exception):
    """reward: corta el combo y se lleva el valor."""

    def __init__(self, valor: object) -> None:
        super().__init__()
        self.valor = valor


class Combo:
    """Un combo ya listo para llamarse."""

    def __init__(self, declaracion: arbol.DeclCombo, evaluador: "Evaluador") -> None:
        self.declaracion = declaracion
        self.evaluador = evaluador

    @property
    def nombre(self) -> str:
        return self.declaracion.nombre

    def __call__(self, argumentos: list[object]) -> object:
        return self.evaluador.invocar(self, argumentos)


class Evaluador:
    def __init__(self, archivo: str = "<entrada>") -> None:
        self.archivo = archivo
        self.global_ = Entorno()
        self.entorno = self.global_
        # los inventory: nombre -> campos, en orden
        self.registros: dict[str, list[str]] = {}
        self.entorno.declarar("chat", Modulo("chat", {
            "send": self._chat_send,
            "rage": self._chat_rage,
            "load": self._chat_load,
        }))

    def _error(self, mensaje: str, nodo: arbol.Nodo) -> ErrorEjecucion:
        return ErrorEjecucion(mensaje, nodo.linea, nodo.columna, self.archivo)

    # ------------------------------------------------------- libreria estandar

    def _texto(self, valor: object) -> str:
        """Como se ve cada valor cuando se imprime."""
        if valor is None:
            return "afk"
        if isinstance(valor, bool):
            return "hit" if valor else "miss"
        if isinstance(valor, float):
            # sin el .0 de mas cuando el decimal dio justo
            return f"{valor:g}"
        if isinstance(valor, Coleccion):
            return "<" + " | ".join(self._texto(e) for e in valor.elementos) + ">"
        if isinstance(valor, Registro):
            pares = " | ".join(f"{k}: {self._texto(v)}" for k, v in valor.campos.items())
            return f"{valor.tipo}{{{pares}}}"
        return str(valor)

    def _chat_send(self, argumentos: list[object]) -> None:
        print(" ".join(self._texto(a) for a in argumentos))

    def _chat_rage(self, argumentos: list[object]) -> None:
        print(" ".join(self._texto(a) for a in argumentos), file=sys.stderr)

    def _chat_load(self, _argumentos: list[object]) -> str:
        return input()

    # ---------------------------------------------------------------- programa

    def ejecutar(self, programa: arbol.Programa) -> None:
        # igual que en el paso 3: primero anoto todos los combo, asi pueden
        # llamarse entre si sin importar el orden en que esten escritos
        for declaracion in programa.declaraciones:
            if isinstance(declaracion, arbol.DeclInventory):
                self.registros[declaracion.nombre] = [c.nombre for c in declaracion.campos]
            elif isinstance(declaracion, arbol.DeclCombo):
                self.global_.declarar(declaracion.nombre, Combo(declaracion, self))

        for declaracion in programa.declaraciones:
            if isinstance(declaracion, arbol.PuntoEntrada):
                self.bloque(declaracion.cuerpo)
            # los travel todavia no hacen nada -> bloque 6

    def invocar(self, combo: Combo, argumentos: list[object]) -> object:
        declaracion = combo.declaracion

        # OJO: arranca desde el nivel de afuera de todo, no desde el de quien
        # llamo. Adentro solo estan los parametros y los otros combo
        anterior = self.entorno
        self.entorno = Entorno(self.global_)
        for parametro, valor in zip(declaracion.parametros, argumentos):
            if str(parametro.tipo) == "aim" and isinstance(valor, int) and not isinstance(valor, bool):
                valor = float(valor)                 # score -> aim
            self.entorno.declarar(parametro.nombre, valor)

        try:
            self.bloque(declaracion.cuerpo)
        except _SenalReward as senal:
            return senal.valor
        finally:
            self.entorno = anterior

        if declaracion.retorno is not None:
            # el paso 3 ya revisa esto, pero un ciclo que no da ni una vuelta
            # se le puede escapar. Mejor avisar que devolver afk y que siga
            raise self._error(
                f"el combo '{declaracion.nombre}' termino sin ejecutar 'reward'",
                declaracion,
            )
        return None

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
                valor = float(valor)               # score -> aim

            # el <a | b | c> llega sin tipo: se lo pone la declaracion
            constructor = nodo.tipo.nombre
            if constructor in CONSTRUCTORES_COLECCION:
                if valor is None:
                    valor = Coleccion(constructor)
                elif isinstance(valor, Coleccion):
                    valor.constructor = constructor

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

        elif isinstance(nodo, arbol.Scan):
            self.sent_scan(nodo)

        elif isinstance(nodo, arbol.Farm):
            self.sent_farm(nodo)

        elif isinstance(nodo, arbol.Survive):
            self.sent_survive(nodo)

        elif isinstance(nodo, arbol.Grind):
            self.sent_grind(nodo)

        elif isinstance(nodo, arbol.Loadout):
            self.sent_loadout(nodo)

        elif isinstance(nodo, arbol.Reward):
            raise _SenalReward(
                self.expresion(nodo.valor) if nodo.valor is not None else None
            )

        elif isinstance(nodo, arbol.Ragequit):
            raise _SenalRagequit()

        elif isinstance(nodo, arbol.Respawn):
            raise _SenalRespawn()

        else:
            raise self._error("sentencia no soportada todavia", nodo)

    # ----------------------------------------------------- control de flujo

    def sent_scan(self, nodo: arbol.Scan) -> None:
        if self.expresion(nodo.condicion):
            self.bloque(nodo.entonces)
        elif isinstance(nodo.sino, arbol.Scan):
            self.sent_scan(nodo.sino)
        elif nodo.sino is not None:
            self.bloque(nodo.sino)

    def _vuelta(self, cuerpo: arbol.Bloque) -> bool:
        """Una vuelta del ciclo. Devuelve False si hay que cortar."""
        try:
            self.bloque(cuerpo)
        except _SenalRespawn:
            pass                                    # sigue con la proxima
        except _SenalRagequit:
            return False
        return True

    def sent_farm(self, nodo: arbol.Farm) -> None:
        self.sentencia(nodo.inicio)
        while self.expresion(nodo.condicion):
            if not self._vuelta(nodo.cuerpo):
                break
            # despues de un respawn el paso corre igual, si no el farm no
            # avanzaria nunca y se quedaria colgado
            self.sentencia(nodo.paso)

    def sent_survive(self, nodo: arbol.Survive) -> None:
        while self.expresion(nodo.condicion):
            if not self._vuelta(nodo.cuerpo):
                break

    def sent_grind(self, nodo: arbol.Grind) -> None:
        # condicion al final: el cuerpo corre minimo una vez
        while True:
            if not self._vuelta(nodo.cuerpo):
                break
            if not self.expresion(nodo.condicion):
                break

    def sent_loadout(self, nodo: arbol.Loadout) -> None:
        seleccion = self.expresion(nodo.expresion)

        for rama in nodo.ramas:
            if any(self.expresion(v) == seleccion for v in rama.valores):
                self.bloque(rama.cuerpo)
                return                              # sin caida entre ramas

        if nodo.defecto is not None:
            self.bloque(nodo.defecto)

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

        # campo:  espada.dano set 20
        if isinstance(destino, arbol.Acceso):
            objeto = self.expresion(destino.objeto)
            if not isinstance(objeto, Registro):
                raise self._error(
                    f"no se puede asignar a '{destino.campo}': no es un inventory", destino
                )
            if destino.campo not in objeto.campos:
                raise self._error(
                    f"el inventory {objeto.tipo} no tiene el campo '{destino.campo}'", destino
                )
            objeto.campos[destino.campo] = valor
            return

        # posicion:  marcador<0> set 99
        if isinstance(destino, arbol.Indice):
            coleccion = self.expresion(destino.objeto)
            indice = self.expresion(destino.indice)
            if not isinstance(coleccion, Coleccion):
                raise self._error("solo las colecciones se pueden indexar", destino)
            if not isinstance(indice, int) or isinstance(indice, bool):
                raise self._error("el indice debe ser un score", destino)
            if not 0 <= indice < len(coleccion.elementos):
                raise self._error(
                    f"indice fuera de rango: {indice} (la coleccion tiene "
                    f"{len(coleccion.elementos)} elemento(s))",
                    destino,
                )
            coleccion.elementos[indice] = valor
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
            # el tipo se lo pone la declaracion que lo recibe
            coleccion = Coleccion("squad")
            coleccion.elementos = [self.expresion(e) for e in nodo.elementos]
            return coleccion

        if isinstance(nodo, arbol.Spawn):
            campos = self.registros.get(nodo.tipo)
            if campos is None:
                raise self._error(f"no existe un inventory llamado '{nodo.tipo}'", nodo)
            valores = [self.expresion(a) for a in nodo.argumentos]
            return Registro(nodo.tipo, dict(zip(campos, valores)))

        if isinstance(nodo, arbol.Acceso):
            return self.acceso(nodo)

        if isinstance(nodo, arbol.Indice):
            objeto = self.expresion(nodo.objeto)
            indice = self.expresion(nodo.indice)
            if not isinstance(objeto, Coleccion):
                raise self._error("solo las colecciones se pueden indexar", nodo)
            if not isinstance(indice, int) or isinstance(indice, bool):
                raise self._error("el indice debe ser un score", nodo)
            if not 0 <= indice < len(objeto.elementos):
                raise self._error(
                    f"indice fuera de rango: {indice} (la coleccion tiene "
                    f"{len(objeto.elementos)} elemento(s))",
                    nodo,
                )
            return objeto.elementos[indice]

        if isinstance(nodo, arbol.Llamada):
            funcion = self.expresion(nodo.destino)
            argumentos = [self.expresion(a) for a in nodo.argumentos]
            if not callable(funcion):
                raise self._error("esto no es una funcion", nodo)
            return funcion(argumentos)

        raise self._error("expresion no soportada todavia", nodo)

    def acceso(self, nodo: arbol.Acceso) -> object:
        objeto = self.expresion(nodo.objeto)

        if isinstance(objeto, Modulo):
            if nodo.campo not in objeto.funciones:
                raise self._error(
                    f"el modulo {objeto.nombre} no tiene '{nodo.campo}'", nodo
                )
            return objeto.funciones[nodo.campo]

        if isinstance(objeto, Coleccion):
            if nodo.campo == "size":
                return len(objeto.elementos)
            metodo = self._metodo_coleccion(objeto, nodo.campo, nodo)
            return metodo

        if isinstance(objeto, Registro):
            if nodo.campo not in objeto.campos:
                raise self._error(
                    f"el inventory {objeto.tipo} no tiene el campo '{nodo.campo}'", nodo
                )
            return objeto.campos[nodo.campo]

        raise self._error(f"no se puede acceder a '{nodo.campo}'", nodo)

    def _metodo_coleccion(self, coleccion: Coleccion, nombre: str, nodo: arbol.Nodo):
        """Devuelve el metodo ya pegado a su vector, listo para llamar."""
        metodos = {
            "recruit": lambda args: coleccion.elementos.append(args[0]),
            "kick":    lambda args: self._kick(coleccion, args[0], nodo),
        }

        if nombre not in metodos:
            disponibles = ", ".join(["size"] + sorted(metodos))
            raise self._error(f"squad no tiene '{nombre}'. Tiene: {disponibles}", nodo)
        return metodos[nombre]

    def _kick(self, coleccion: Coleccion, indice: object, nodo: arbol.Nodo) -> None:
        if not isinstance(indice, int) or isinstance(indice, bool):
            raise self._error("el indice debe ser un score", nodo)
        if not 0 <= indice < len(coleccion.elementos):
            raise self._error(
                f"indice fuera de rango: {indice} (la squad tiene "
                f"{len(coleccion.elementos)} elemento(s))",
                nodo,
            )
        coleccion.elementos.pop(indice)

    def binaria(self, nodo: arbol.Binaria) -> object:
        op = nodo.operador

        # si con el lado izquierdo ya alcanza, el derecho ni se mira
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
            # si hay texto de algun lado, pega. Si no, suma
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
            return a / b                            # siempre da aim
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
