"""Paso 4: recorre el arbol y lo ejecuta. chat.send va a stdout y chat.rage a stderr."""

from __future__ import annotations

import math
import sys

from . import arbol
from .errores import ErrorEjecucion
from .tokens import CONSTRUCTORES_COLECCION


class Entorno:
    """Valores en ejecucion, un nivel por glhf...gg."""

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

    def __init__(self, nombre: str, funciones: dict[str, object]) -> None:
        self.nombre = nombre
        self.funciones = funciones


class Lugar:
    """Nivel global y nombre del archivo donde esta escrito algo: un combo ve su propio archivo -> D-28."""

    def __init__(self, archivo: str, global_: Entorno) -> None:
        self.archivo = archivo
        self.global_ = global_


class Coleccion:

    def __init__(self, constructor: str = "squad") -> None:
        self.constructor = constructor
        self.elementos: list[object] = []

    def __repr__(self) -> str:
        return "<" + " | ".join(str(e) for e in self.elementos) + ">"


class Registro:

    def __init__(self, tipo: str, campos: dict[str, object]) -> None:
        self.tipo = tipo
        self.campos = campos

    def __repr__(self) -> str:
        pares = " | ".join(f"{k}: {v}" for k, v in self.campos.items())
        return f"{self.tipo}{{{pares}}}"


class Clase:

    def __init__(self, declaracion: arbol.DeclGuild) -> None:
        self.nombre = declaracion.nombre
        self.padre = declaracion.padre
        self.campos = list(declaracion.campos)
        self.constructor: arbol.DeclCombo | None = None
        self.metodos: dict[str, arbol.DeclCombo] = {}
        for metodo in declaracion.metodos:
            if metodo.nombre == declaracion.nombre:
                self.constructor = metodo
            else:
                self.metodos[metodo.nombre] = metodo
        self.con_herencia = self.padre is None

    def heredar(self, padre: "Clase") -> None:
        """Copia lo del padre abajo de lo propio; los metodos del hijo pisan y el constructor NO se hereda -> D-27."""
        self.campos = padre.campos + self.campos
        metodos = dict(padre.metodos)
        metodos.update(self.metodos)
        self.metodos = metodos
        self.con_herencia = True


class Objeto(Registro):

    def __init__(self, clase: Clase, campos: dict[str, object]) -> None:
        super().__init__(clase.nombre, campos)
        self.clase = clase


# ragequit, respawn y reward son excepciones para cortar desde cualquier profundidad
class _SenalRagequit(Exception):
    pass


class _SenalRespawn(Exception):
    pass


class _SenalReward(Exception):

    def __init__(self, valor: object) -> None:
        super().__init__()
        self.valor = valor


class Combo:

    def __init__(
        self, declaracion: arbol.DeclCombo, evaluador: "Evaluador", objeto: Objeto | None = None
    ) -> None:
        self.declaracion = declaracion
        self.evaluador = evaluador
        self.objeto = objeto

    @property
    def nombre(self) -> str:
        return self.declaracion.nombre

    def __call__(self, argumentos: list[object]) -> object:
        return self.evaluador.invocar(self, argumentos)


class Evaluador:
    def __init__(self, archivo: str = "<entrada>") -> None:
        self.archivo = archivo
        self.entorno = Entorno()
        # Estos dos son de todo el programa
        self.registros: dict[str, list[str]] = {}
        self.clases: dict[str, Clase] = {}
        self.lugares: dict[int, Lugar] = {}

    def _error(self, mensaje: str, nodo: arbol.Nodo) -> ErrorEjecucion:
        return ErrorEjecucion(mensaje, nodo.linea, nodo.columna, self.archivo)

    def _texto(self, valor: object) -> str:
        if valor is None:
            return "afk"
        if isinstance(valor, bool):
            return "hit" if valor else "miss"
        if isinstance(valor, float):
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

    def ejecutar(self, programa: arbol.Programa) -> None:
        _, lugar = self._cargar(programa, self.archivo, None, {})
        self._arrancar(programa, lugar)

    def ejecutar_archivos(self, archivos: list) -> None:
        exporta: dict[int, Modulo] = {}
        lugar = None
        for archivo in archivos:
            importa = {modulo: exporta[id(otro)] for modulo, otro in archivo.importa.items()}
            exporta[id(archivo)], lugar = self._cargar(
                archivo.programa, archivo.nombre, archivo.modulo, importa
            )
        self._arrancar(archivos[-1].programa, lugar)   # type: ignore[arg-type]

    def _cargar(
        self, programa: arbol.Programa, archivo: str, modulo: str | None, importa: dict[str, Modulo]
    ) -> tuple[Modulo, Lugar]:
        lugar = Lugar(archivo, Entorno())
        lugar.global_.declarar("chat", Modulo("chat", {
            "send": self._chat_send,
            "rage": self._chat_rage,
            "load": self._chat_load,
        }))
        for nombre, importado in importa.items():
            lugar.global_.declarar(nombre, importado)

        # Primero se registran todos los combo para que se llamen sin importar el orden
        funciones: dict[str, object] = {}
        for declaracion in programa.declaraciones:
            if isinstance(declaracion, arbol.DeclInventory):
                self.registros[declaracion.nombre] = [c.nombre for c in declaracion.campos]
            elif isinstance(declaracion, arbol.DeclCombo):
                combo = Combo(declaracion, self)
                self.lugares[id(declaracion)] = lugar
                lugar.global_.declarar(declaracion.nombre, combo)
                funciones[declaracion.nombre] = combo
            elif isinstance(declaracion, arbol.DeclGuild):
                self.clases[declaracion.nombre] = Clase(declaracion)
                for miembro in [*declaracion.campos, *declaracion.metodos]:
                    self.lugares[id(miembro)] = lugar

        return Modulo(modulo or archivo, funciones), lugar

    def _arrancar(self, programa: arbol.Programa, lugar: Lugar) -> None:
        # Recien con todo cargado: un guild puede heredar de otro archivo
        for clase in self.clases.values():
            self._armar_herencia(clase)

        self.archivo = lugar.archivo
        self.entorno = lugar.global_
        for declaracion in programa.declaraciones:
            if isinstance(declaracion, arbol.PuntoEntrada):
                self.bloque(declaracion.cuerpo)

    def invocar(self, combo: Combo, argumentos: list[object]) -> object:
        declaracion = combo.declaracion
        lugar = self.lugares[id(declaracion)]

        # OJO: arranca desde el global de SU archivo, no desde quien llamo
        anterior, archivo_anterior = self.entorno, self.archivo
        base = self._base_metodo(combo.objeto, lugar.global_) if combo.objeto else lugar.global_
        self.entorno = Entorno(base)
        self.archivo = lugar.archivo
        if combo.objeto is not None:
            self.entorno.declarar("me", combo.objeto)
        for parametro, valor in zip(declaracion.parametros, argumentos):
            if str(parametro.tipo) == "aim" and isinstance(valor, int) and not isinstance(valor, bool):
                valor = float(valor)
            self.entorno.declarar(parametro.nombre, valor)

        try:
            self.bloque(declaracion.cuerpo)
        except _SenalReward as senal:
            return senal.valor
        finally:
            self.entorno, self.archivo = anterior, archivo_anterior

        if declaracion.retorno is not None:
            # El paso 3 lo revisa, pero un ciclo sin vueltas se le puede escapar
            raise ErrorEjecucion(
                f"el combo '{declaracion.nombre}' termino sin ejecutar 'reward'",
                declaracion.linea, declaracion.columna, lugar.archivo,
            )
        return None

    def _armar_herencia(self, clase: Clase) -> None:
        # El padre primero, si no el hijo copiaria un padre a medio armar
        if clase.con_herencia:
            return
        padre = self.clases[clase.padre]            # type: ignore[index]
        self._armar_herencia(padre)
        clase.heredar(padre)

    def _base_metodo(self, objeto: Objeto, global_: Entorno) -> Entorno:
        """Alcance de un metodo: campos del objeto -> metodos del guild -> combos globales de su archivo."""
        metodos = Entorno(global_)
        for nombre, declaracion in objeto.clase.metodos.items():
            metodos.declarar(nombre, Combo(declaracion, self, objeto))
        campos = Entorno(metodos)
        # Los campos del objeto SON este nivel: asignar a secas ya cambia el objeto
        campos.valores = objeto.campos
        return campos

    def _crear_objeto(self, clase: Clase, argumentos: list[object]) -> Objeto:
        # El valor inicial se evalua en el global del archivo del campo (puede ser heredado de otro)
        anterior, archivo_anterior = self.entorno, self.archivo
        campos: dict[str, object] = {}
        try:
            for campo in clase.campos:
                if campo.valor is None:
                    campos[campo.nombre] = self._por_defecto(campo.tipo)
                    continue
                lugar = self.lugares[id(campo)]
                self.entorno, self.archivo = lugar.global_, lugar.archivo
                campos[campo.nombre] = self.expresion(campo.valor)
        finally:
            self.entorno, self.archivo = anterior, archivo_anterior

        objeto = Objeto(clase, campos)
        if clase.constructor is not None:
            self.invocar(Combo(clase.constructor, self, objeto), argumentos)
        return objeto

    def _por_defecto(self, tipo: arbol.Tipo) -> object:
        # Como en Java: numeros en 0, win en miss, el resto en afk -> D-26
        return {"score": 0, "aim": 0.0, "win": False}.get(tipo.nombre)

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
                valor = float(valor)

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

        elif isinstance(nodo, arbol.Clutch):
            self.sent_clutch(nodo)

        elif isinstance(nodo, arbol.Wipe):
            motivo = self.expresion(nodo.motivo)
            raise self._error(self._texto(motivo), nodo)

        else:
            raise self._error("sentencia no soportada todavia", nodo)

    def sent_scan(self, nodo: arbol.Scan) -> None:
        if self.expresion(nodo.condicion):
            self.bloque(nodo.entonces)
        elif isinstance(nodo.sino, arbol.Scan):
            self.sent_scan(nodo.sino)
        elif nodo.sino is not None:
            self.bloque(nodo.sino)

    def _vuelta(self, cuerpo: arbol.Bloque) -> bool:
        try:
            self.bloque(cuerpo)
        except _SenalRespawn:
            pass
        except _SenalRagequit:
            return False
        return True

    def sent_farm(self, nodo: arbol.Farm) -> None:
        self.sentencia(nodo.inicio)
        while self.expresion(nodo.condicion):
            if not self._vuelta(nodo.cuerpo):
                break
            # El paso corre igual tras un respawn, si no el farm se quedaria colgado
            self.sentencia(nodo.paso)

    def sent_survive(self, nodo: arbol.Survive) -> None:
        while self.expresion(nodo.condicion):
            if not self._vuelta(nodo.cuerpo):
                break

    def sent_grind(self, nodo: arbol.Grind) -> None:
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
                return

        if nodo.defecto is not None:
            self.bloque(nodo.defecto)

    def sent_clutch(self, nodo: arbol.Clutch) -> None:
        # reward/ragequit/respawn no son ErrorEjecucion y atraviesan el revive, pero el checkpoint corre igual -> D-22
        try:
            try:
                self.bloque(nodo.intento)
            except ErrorEjecucion as error:
                self._revive(nodo, error)
        finally:
            if nodo.final is not None:
                self.bloque(nodo.final)

    def _revive(self, nodo: arbol.Clutch, error: ErrorEjecucion) -> None:

        falla = Registro("error de revive", {"reason": error.mensaje, "line": error.linea})
        anterior = self.entorno
        self.entorno = Entorno(anterior)
        self.entorno.declarar(nodo.variable, falla)
        try:
            self.bloque(nodo.atrapa)
        finally:
            self.entorno = anterior

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

        if isinstance(destino, arbol.Acceso):
            objeto = self.expresion(destino.objeto)
            if objeto is None:
                raise self._error(
                    f"no se puede asignar a '{destino.campo}': el valor es afk", destino
                )
            if not isinstance(objeto, Registro):
                raise self._error(
                    f"no se puede asignar a '{destino.campo}': no es un inventory ni un objeto",
                    destino,
                )
            if destino.campo not in objeto.campos:
                raise self._error(
                    f"{self._que_es(objeto)} no tiene el campo '{destino.campo}'", destino
                )
            objeto.campos[destino.campo] = valor
            return

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

    def expresion(self, nodo: arbol.Nodo) -> object:
        valor = self._calcular(nodo)
        if nodo.a_aim and isinstance(valor, int) and not isinstance(valor, bool):
            return float(valor)
        return valor

    def _calcular(self, nodo: arbol.Nodo) -> object:
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
            coleccion = Coleccion("squad")
            coleccion.elementos = [self.expresion(e) for e in nodo.elementos]
            return coleccion

        if isinstance(nodo, arbol.Me):
            return self.entorno.obtener("me")

        if isinstance(nodo, arbol.Spawn):
            if nodo.tipo in self.clases:
                argumentos = [self.expresion(a) for a in nodo.argumentos]
                return self._crear_objeto(self.clases[nodo.tipo], argumentos)
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

        if isinstance(objeto, Objeto) and nodo.campo in objeto.clase.metodos:
            return Combo(objeto.clase.metodos[nodo.campo], self, objeto)

        if isinstance(objeto, Registro):
            if nodo.campo not in objeto.campos:
                raise self._error(
                    f"{self._que_es(objeto)} no tiene el campo '{nodo.campo}'", nodo
                )
            return objeto.campos[nodo.campo]

        if objeto is None:
            raise self._error(f"no se puede acceder a '{nodo.campo}': el valor es afk", nodo)

        raise self._error(f"no se puede acceder a '{nodo.campo}'", nodo)

    def _que_es(self, registro: Registro) -> str:
        if isinstance(registro, Objeto):
            return f"el guild {registro.tipo}"
        return f"el inventory {registro.tipo}"

    def _metodo_coleccion(self, coleccion: Coleccion, nombre: str, nodo: arbol.Nodo):
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
            if isinstance(izq, str) or isinstance(der, str):
                return self._texto(izq) + self._texto(der)
            return self._numero(izq, nodo) + self._numero(der, nodo)

        a = self._numero(izq, nodo)
        b = self._numero(der, nodo)

        if op == "drain":
            return a - b
        if op == "crit":
            return a * b
        enteros = isinstance(a, int) and isinstance(b, int)

        if op == "split":
            if b == 0:
                raise self._error("division entre cero", nodo)
            if enteros:
                return self._division_entera(a, b)
            return a / b
        if op == "scrap":
            if b == 0:
                raise self._error("modulo entre cero", nodo)
            if enteros:
                return a - self._division_entera(a, b) * b
            return math.fmod(a, b)
        if op == "over":
            return a > b
        if op == "under":
            return a < b
        if op == "atleast":
            return a >= b
        if op == "atmost":
            return a <= b

        raise self._error(f"operador no soportado: {op}", nodo)

    def _division_entera(self, a: int, b: int) -> int:
        # Trunca como C y Java (-7 split 2 = -3); el // de Python daria -4
        cociente = abs(a) // abs(b)
        return cociente if (a < 0) == (b < 0) else -cociente


def ejecutar(programa: arbol.Programa, archivo: str = "<entrada>") -> None:
    Evaluador(archivo).ejecutar(programa)


def ejecutar_archivos(archivos: list) -> None:
    Evaluador().ejecutar_archivos(archivos)
