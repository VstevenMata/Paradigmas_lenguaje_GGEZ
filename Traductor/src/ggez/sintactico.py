"""Paso 2: con los tokens arma el arbol.

Una funcion por cada regla de GRAMATICA.md, con el mismo nombre, asi se pueden
ir leyendo los dos al lado.

Que el por vaya antes que el mas no esta escrito en ningun lado: sale solo del
orden en que las funciones de expresion se llaman entre si.
"""

from __future__ import annotations

from . import arbol
from .errores import ErrorSintactico
from .tokens import (
    COLECCIONES_RETIRADAS,
    CONSTRUCTORES_COLECCION,
    MODIFICADORES,
    OP_ADITIVOS,
    OP_ASIGNACION,
    OP_INCREMENTO,
    OP_MULTIPLICATIVOS,
    OP_RELACIONALES,
    TIPOS_SIMPLES,
    TipoToken,
    Token,
)


class Sintactico:
    def __init__(self, tokens: list[Token], archivo: str = "<entrada>") -> None:
        self.tokens = tokens
        self.archivo = archivo
        self.pos = 0

    # ---------------------------------------------------------------- helpers

    @property
    def actual(self) -> Token:
        return self.tokens[self.pos]

    def _avanzar(self) -> Token:
        token = self.tokens[self.pos]
        if token.tipo is not TipoToken.FIN_ARCHIVO:
            self.pos += 1
        return token

    def _error(self, mensaje: str, token: Token | None = None) -> ErrorSintactico:
        t = token or self.actual
        return ErrorSintactico(mensaje, t.linea, t.columna, self.archivo)

    def _describir(self, token: Token) -> str:
        if token.tipo is TipoToken.FIN_ARCHIVO:
            return "el final del archivo"
        if token.tipo is TipoToken.FIN_LINEA:
            return "un salto de linea"
        return f"'{token.lexema}'"

    def _consumir_reservada(self, palabra: str) -> Token:
        if not self.actual.es(palabra):
            raise self._error(
                f"se esperaba '{palabra}' y se encontro {self._describir(self.actual)}"
            )
        return self._avanzar()

    def _consumir(self, tipo: TipoToken, descripcion: str) -> Token:
        if self.actual.tipo is not tipo:
            raise self._error(
                f"se esperaba {descripcion} y se encontro {self._describir(self.actual)}"
            )
        return self._avanzar()

    def _fin_linea(self) -> None:
        """Cierra la sentencia. En GGEZ no hay ';', es el salto de linea.

        Un gg tambien la cierra, sin consumirlo -> D-19. Eso permite escribir
        un bloque en una linea:  pick 10 | 9 glhf reward "Pro" gg
        """
        if self.actual.tipo is TipoToken.FIN_ARCHIVO or self.actual.es("gg"):
            return
        if self.actual.tipo is not TipoToken.FIN_LINEA:
            raise self._error(
                f"se esperaba el final de la sentencia y se encontro "
                f"{self._describir(self.actual)}"
            )
        while self.actual.tipo is TipoToken.FIN_LINEA:
            self._avanzar()

    def _saltar_lineas(self) -> None:
        while self.actual.tipo is TipoToken.FIN_LINEA:
            self._avanzar()

    # --------------------------------------------------------------- programa

    def analizar(self) -> arbol.Programa:
        inicio = self.actual
        self._saltar_lineas()
        declaraciones: list[arbol.Nodo] = []

        while self.actual.tipo is not TipoToken.FIN_ARCHIVO:
            declaraciones.append(self.decl_superior())
            self._saltar_lineas()

        return arbol.Programa(declaraciones, linea=inicio.linea, columna=inicio.columna)

    def decl_superior(self) -> arbol.Nodo:
        if self.actual.es("travel"):
            return self.decl_travel()
        if self.actual.es("lobby"):
            return self.punto_entrada()
        if self.actual.es("combo") or self.actual.es(*MODIFICADORES):
            return self.decl_combo()
        if self.actual.es("inventory"):
            return self.decl_inventory()
        raise self._error(
            f"aqui se esperaba una declaracion de nivel superior (travel, combo o "
            f"lobby.launch) y se encontro {self._describir(self.actual)}"
        )

    def decl_combo(self) -> arbol.DeclCombo:
        t = self.actual
        modificador = self._avanzar().lexema if self.actual.es(*MODIFICADORES) else None

        self._consumir_reservada("combo")
        nombre = self._consumir(TipoToken.IDENTIFICADOR, "el nombre del combo").lexema
        self._consumir(TipoToken.LLAVE_IZQ, "'{' con los parametros del combo")

        parametros: list[arbol.Parametro] = []
        if self.actual.tipo is not TipoToken.LLAVE_DER:
            parametros.append(self.parametro())
            while self.actual.tipo is TipoToken.BARRA:
                self._avanzar()
                parametros.append(self.parametro())
        self._consumir(TipoToken.LLAVE_DER, "'}' para cerrar los parametros")

        # drops es obligatorio siempre; sin retorno se escribe drops noloot -> D-12
        self._consumir_reservada("drops")
        if self.actual.es("noloot"):
            self._avanzar()
            retorno = None
        else:
            retorno = self.tipo()

        cuerpo = self.bloque()
        return arbol.DeclCombo(
            nombre, parametros, retorno, cuerpo, modificador,
            linea=t.linea, columna=t.columna,
        )

    def decl_inventory(self) -> arbol.DeclInventory:
        t = self._consumir_reservada("inventory")
        nombre = self._consumir(TipoToken.IDENTIFICADOR, "el nombre del inventory").lexema
        self._consumir_reservada("glhf")
        self._saltar_lineas()

        campos: list[arbol.Parametro] = []
        while not self.actual.es("gg"):
            if self.actual.tipo is TipoToken.FIN_ARCHIVO:
                raise self._error("inventory sin cerrar: falta 'gg'", t)
            campos.append(self.parametro())
            self._fin_linea()
            self._saltar_lineas()

        self._consumir_reservada("gg")
        return arbol.DeclInventory(nombre, campos, linea=t.linea, columna=t.columna)

    def parametro(self) -> arbol.Parametro:
        t = self.actual
        tipo = self.tipo()
        nombre = self._consumir(TipoToken.IDENTIFICADOR, "el nombre del parametro").lexema
        return arbol.Parametro(tipo, nombre, linea=t.linea, columna=t.columna)

    def decl_travel(self) -> arbol.Travel:
        t = self._consumir_reservada("travel")
        modulo = self._consumir(TipoToken.IDENTIFICADOR, "el nombre del modulo").lexema
        self._consumir_reservada("from")
        paquete = self._consumir(TipoToken.IDENTIFICADOR, "el nombre del paquete").lexema
        self._fin_linea()
        return arbol.Travel(modulo, paquete, linea=t.linea, columna=t.columna)

    def punto_entrada(self) -> arbol.PuntoEntrada:
        # 'start THE GAME' es texto fijo, no son parametros -> D-8
        t = self._consumir_reservada("lobby")
        self._consumir(TipoToken.PUNTO, "'.' despues de lobby")
        self._consumir_reservada("launch")
        self._consumir(TipoToken.LLAVE_IZQ, "'{' despues de lobby.launch")
        for palabra in ("start", "THE", "GAME"):
            self._consumir_reservada(palabra)
        self._consumir(TipoToken.LLAVE_DER, "'}' para cerrar lobby.launch")
        return arbol.PuntoEntrada(self.bloque(), linea=t.linea, columna=t.columna)

    def bloque(self) -> arbol.Bloque:
        cuerpo = self._cuerpo_bloque()
        self._consumir_reservada("gg")
        return cuerpo

    def _cuerpo_bloque(self) -> arbol.Bloque:
        """glhf + sentencias, frenando ANTES del gg sin consumirlo.

        Separado de bloque() porque hay casos donde el gg no cierra nada:
        'gg fallback' (el else) y 'gg survive' (el grind).
        """
        t = self._consumir_reservada("glhf")
        self._saltar_lineas()   # opcional, el bloque puede seguir en la misma linea
        sentencias: list[arbol.Nodo] = []

        while not self.actual.es("gg"):
            if self.actual.tipo is TipoToken.FIN_ARCHIVO:
                raise self._error("bloque sin cerrar: falta 'gg'", t)
            sentencias.append(self.sentencia())
            self._saltar_lineas()

        return arbol.Bloque(sentencias, linea=t.linea, columna=t.columna)

    # ------------------------------------------------------------- sentencias

    def sentencia(self) -> arbol.Nodo:
        if self.actual.es("stash", "hardcoded"):
            return self.decl_variable()
        if self.actual.es("scan"):
            return self.sent_scan()
        if self.actual.es("farm"):
            return self.sent_farm()
        if self.actual.es("survive"):
            return self.sent_survive()
        if self.actual.es("grind"):
            return self.sent_grind()
        if self.actual.es("loadout"):
            return self.sent_loadout()
        if self.actual.es("ragequit"):
            t = self._avanzar()
            self._fin_linea()
            return arbol.Ragequit(linea=t.linea, columna=t.columna)
        if self.actual.es("respawn"):
            t = self._avanzar()
            self._fin_linea()
            return arbol.Respawn(linea=t.linea, columna=t.columna)
        if self.actual.es("reward"):
            return self.sent_reward()

        # el resto arranca con una expresion. Si despues aparece set/boost/sap
        # o buff/nerf, era el destino de una asignacion
        inicio = self.actual
        expresion = self.expresion()

        if self.actual.es(*OP_ASIGNACION):
            operador = self._avanzar().lexema
            valor = self.expresion()
            self._fin_linea()
            self._verificar_designador(expresion, inicio)
            return arbol.Asignacion(
                expresion, operador, valor, linea=inicio.linea, columna=inicio.columna
            )

        if self.actual.es(*OP_INCREMENTO):
            operador = self._avanzar().lexema
            self._fin_linea()
            self._verificar_designador(expresion, inicio)
            return arbol.Incremento(
                expresion, operador, linea=inicio.linea, columna=inicio.columna
            )

        self._fin_linea()
        return arbol.SentenciaExpresion(
            expresion, linea=inicio.linea, columna=inicio.columna
        )

    def _verificar_designador(self, nodo: arbol.Nodo, token: Token) -> None:
        """A la izquierda del set solo va un nombre, un campo o un indice."""
        if not isinstance(nodo, (arbol.Identificador, arbol.Acceso, arbol.Indice, arbol.Me)):
            raise self._error("el lado izquierdo de una asignacion no es asignable", token)

    def sent_reward(self) -> arbol.Reward:
        t = self._consumir_reservada("reward")
        # si el tipo esta bien lo revisa el paso 3; aca solo miro si hay valor
        vacio = (
            self.actual.tipo in (TipoToken.FIN_LINEA, TipoToken.FIN_ARCHIVO)
            or self.actual.es("gg")
        )
        valor = None if vacio else self.expresion()
        self._fin_linea()
        return arbol.Reward(valor, linea=t.linea, columna=t.columna)

    # ------------------------------------------------------ control de flujo

    def sent_scan(self) -> arbol.Scan:
        t = self._consumir_reservada("scan")
        condicion = self.expresion()
        entonces = self._cuerpo_bloque()
        self._consumir_reservada("gg")

        # OJO: el unico lugar donde hay que mirar el token que sigue. Ese gg
        # puede cerrar el scan, o puede venir un fallback despues
        sino: arbol.Bloque | arbol.Scan | None = None
        if self.actual.es("fallback"):
            self._avanzar()
            sino = self.sent_scan() if self.actual.es("scan") else self.bloque()

        return arbol.Scan(condicion, entonces, sino, linea=t.linea, columna=t.columna)

    def sent_farm(self) -> arbol.Farm:
        t = self._consumir_reservada("farm")
        inicio = self.asig_simple()
        self._consumir(TipoToken.BARRA, "'|' despues de la inicializacion del farm")
        condicion = self.expresion()
        self._consumir(TipoToken.BARRA, "'|' despues de la condicion del farm")
        paso = self.paso()
        cuerpo = self.bloque()
        return arbol.Farm(inicio, condicion, paso, cuerpo, linea=t.linea, columna=t.columna)

    def asig_simple(self) -> arbol.Asignacion:
        inicio = self.actual
        destino = self.expresion()
        self._consumir_reservada("set")
        valor = self.expresion()
        self._verificar_designador(destino, inicio)
        return arbol.Asignacion(destino, "set", valor, linea=inicio.linea, columna=inicio.columna)

    def paso(self) -> arbol.Nodo:
        inicio = self.actual
        destino = self.expresion()

        if self.actual.es(*OP_INCREMENTO):
            operador = self._avanzar().lexema
            self._verificar_designador(destino, inicio)
            return arbol.Incremento(destino, operador, linea=inicio.linea, columna=inicio.columna)

        if self.actual.es(*OP_ASIGNACION):
            operador = self._avanzar().lexema
            valor = self.expresion()
            self._verificar_designador(destino, inicio)
            return arbol.Asignacion(destino, operador, valor, linea=inicio.linea, columna=inicio.columna)

        raise self._error(
            f"el paso del farm debe modificar la variable (buff, nerf, set, boost o sap) "
            f"y se encontro {self._describir(self.actual)}"
        )

    def sent_survive(self) -> arbol.Survive:
        t = self._consumir_reservada("survive")
        condicion = self.expresion()
        cuerpo = self.bloque()
        return arbol.Survive(condicion, cuerpo, linea=t.linea, columna=t.columna)

    def sent_grind(self) -> arbol.Grind:
        # la condicion va al FINAL: por eso el cuerpo corre minimo una vez -> D-10
        t = self._consumir_reservada("grind")
        cuerpo = self._cuerpo_bloque()
        self._consumir_reservada("gg")
        self._consumir_reservada("survive")
        self._consumir(TipoToken.LLAVE_IZQ, "'{' con la condicion del grind")
        condicion = self.expresion()
        self._consumir(TipoToken.LLAVE_DER, "'}' para cerrar la condicion del grind")
        self._fin_linea()
        return arbol.Grind(cuerpo, condicion, linea=t.linea, columna=t.columna)

    def sent_loadout(self) -> arbol.Loadout:
        t = self._consumir_reservada("loadout")
        expresion = self.expresion()
        self._consumir_reservada("glhf")
        self._fin_linea()

        ramas: list[arbol.RamaPick] = []
        defecto: arbol.Bloque | None = None

        while not self.actual.es("gg"):
            if self.actual.tipo is TipoToken.FIN_ARCHIVO:
                raise self._error("loadout sin cerrar: falta 'gg'", t)

            if self.actual.es("pick"):
                if defecto is not None:
                    raise self._error("el 'noob' debe ser la ultima rama del loadout")
                tp = self._avanzar()
                valores = self.lista_valores()
                ramas.append(arbol.RamaPick(valores, self.bloque(), linea=tp.linea, columna=tp.columna))
            elif self.actual.es("noob"):
                if defecto is not None:
                    raise self._error("el loadout ya tiene un 'noob'")
                self._avanzar()
                defecto = self.bloque()
            else:
                raise self._error(
                    f"dentro de un loadout solo van ramas 'pick' o 'noob', y se "
                    f"encontro {self._describir(self.actual)}"
                )

            self._saltar_lineas()

        self._consumir_reservada("gg")
        return arbol.Loadout(expresion, ramas, defecto, linea=t.linea, columna=t.columna)

    def lista_valores(self) -> list[arbol.Nodo]:
        valores = [self._valor_pick()]
        while self.actual.tipo is TipoToken.BARRA:
            self._avanzar()
            valores.append(self._valor_pick())
        return valores

    def _valor_pick(self) -> arbol.Nodo:
        # solo numeros/textos sueltos: el pick compara y ya, no calcula nada
        t = self.actual
        nodo = self.primario()
        if not isinstance(nodo, arbol.Literal):
            raise self._error("un 'pick' solo admite valores literales", t)
        return nodo

    def decl_variable(self) -> arbol.DeclVariable:
        t = self._avanzar()                       # stash o hardcoded
        constante = t.lexema == "hardcoded"
        tipo = self.tipo()
        nombre = self._consumir(TipoToken.IDENTIFICADOR, "el nombre de la variable").lexema

        valor = None
        if self.actual.es("set"):
            self._avanzar()
            valor = self.expresion()

        self._fin_linea()
        return arbol.DeclVariable(
            tipo, nombre, valor, constante, linea=t.linea, columna=t.columna
        )

    def tipo(self) -> arbol.Tipo:
        t = self.actual

        # stack y queue quedaron reservadas solo para dar este mensaje -> D-21
        if t.es(*COLECCIONES_RETIRADAS):
            raise self._error(
                f"GGEZ no tiene '{t.lexema}': la unica coleccion es squad. "
                f"Use squad[T] en su lugar."
            )

        if t.es(*CONSTRUCTORES_COLECCION):
            self._avanzar()
            self._consumir(TipoToken.CORCHETE_IZQ, f"'[' con el tipo interno de {t.lexema}")
            interno = self.tipo()
            self._consumir(TipoToken.CORCHETE_DER, "']' para cerrar el tipo generico")
            return arbol.Tipo(t.lexema, interno, linea=t.linea, columna=t.columna)

        if t.es(*TIPOS_SIMPLES) or t.tipo is TipoToken.IDENTIFICADOR:
            self._avanzar()
            return arbol.Tipo(t.lexema, linea=t.linea, columna=t.columna)

        raise self._error(f"se esperaba un tipo y se encontro {self._describir(t)}")

    # ------------------------------------------------------------ expresiones
    # el orden de estas funciones es lo que decide que va primero

    def expresion(self) -> arbol.Nodo:
        return self.expr_or()

    def expr_or(self) -> arbol.Nodo:
        izq = self.expr_and()
        while self.actual.es("swap"):
            t = self._avanzar()
            izq = arbol.Binaria(izq, t.lexema, self.expr_and(), linea=t.linea, columna=t.columna)
        return izq

    def expr_and(self) -> arbol.Nodo:
        izq = self.expr_rel()
        while self.actual.es("chain"):
            t = self._avanzar()
            izq = arbol.Binaria(izq, t.lexema, self.expr_rel(), linea=t.linea, columna=t.columna)
        return izq

    def expr_rel(self) -> arbol.Nodo:
        izq = self.expr_adit()
        # no asociativa a proposito: "a under b under c" no significa nada
        if self.actual.es(*OP_RELACIONALES):
            t = self._avanzar()
            izq = arbol.Binaria(izq, t.lexema, self.expr_adit(), linea=t.linea, columna=t.columna)
        return izq

    def expr_adit(self) -> arbol.Nodo:
        izq = self.expr_mult()
        while self.actual.es(*OP_ADITIVOS):
            t = self._avanzar()
            izq = arbol.Binaria(izq, t.lexema, self.expr_mult(), linea=t.linea, columna=t.columna)
        return izq

    def expr_mult(self) -> arbol.Nodo:
        izq = self.expr_unaria()
        while self.actual.es(*OP_MULTIPLICATIVOS):
            t = self._avanzar()
            izq = arbol.Binaria(izq, t.lexema, self.expr_unaria(), linea=t.linea, columna=t.columna)
        return izq

    def expr_unaria(self) -> arbol.Nodo:
        if self.actual.es("dodge", "drain"):
            t = self._avanzar()
            return arbol.Unaria(t.lexema, self.expr_unaria(), linea=t.linea, columna=t.columna)
        return self.expr_postfija()

    def expr_postfija(self) -> arbol.Nodo:
        nodo = self.primario()

        while True:
            t = self.actual

            if t.tipo is TipoToken.PUNTO:
                self._avanzar()
                campo = self._consumir(TipoToken.IDENTIFICADOR, "el nombre del campo").lexema
                nodo = arbol.Acceso(nodo, campo, linea=t.linea, columna=t.columna)

            elif t.tipo is TipoToken.MENOR:
                # aca el < es un indice, porque viene DESPUES de algo
                self._avanzar()
                indice = self.expresion()
                self._consumir(TipoToken.MAYOR, "'>' para cerrar el indice")
                nodo = arbol.Indice(nodo, indice, linea=t.linea, columna=t.columna)

            elif t.tipo is TipoToken.LLAVE_IZQ:
                # y el { es una llamada, por lo mismo
                self._avanzar()
                nodo = arbol.Llamada(
                    nodo, self.lista_argumentos(), linea=t.linea, columna=t.columna
                )

            else:
                return nodo

    def lista_argumentos(self) -> list[arbol.Nodo]:
        argumentos: list[arbol.Nodo] = []
        if self.actual.tipo is not TipoToken.LLAVE_DER:
            argumentos.append(self.expresion())
            while self.actual.tipo is TipoToken.BARRA:
                self._avanzar()
                argumentos.append(self.expresion())
        self._consumir(TipoToken.LLAVE_DER, "'}' para cerrar la llamada")
        return argumentos

    def primario(self) -> arbol.Nodo:
        t = self.actual

        if t.tipo is TipoToken.ENTERO:
            self._avanzar()
            return arbol.Literal(t.valor, "score", linea=t.linea, columna=t.columna)

        if t.tipo is TipoToken.REAL:
            self._avanzar()
            return arbol.Literal(t.valor, "aim", linea=t.linea, columna=t.columna)

        if t.tipo is TipoToken.CADENA:
            self._avanzar()
            return arbol.Literal(t.valor, "lore", linea=t.linea, columna=t.columna)

        if t.tipo is TipoToken.CARACTER:
            self._avanzar()
            return arbol.Literal(t.valor, "pixel", linea=t.linea, columna=t.columna)

        if t.es("hit", "miss"):
            self._avanzar()
            return arbol.Literal(t.lexema == "hit", "win", linea=t.linea, columna=t.columna)

        if t.es("afk"):
            self._avanzar()
            return arbol.Literal(None, "afk", linea=t.linea, columna=t.columna)

        if t.es("me"):
            self._avanzar()
            return arbol.Me(linea=t.linea, columna=t.columna)

        if t.es("spawn"):
            self._avanzar()
            tipo = self._consumir(TipoToken.IDENTIFICADOR, "el tipo a instanciar").lexema
            self._consumir(TipoToken.LLAVE_IZQ, "'{' con los valores iniciales")
            argumentos = self.lista_argumentos()
            return arbol.Spawn(tipo, argumentos, linea=t.linea, columna=t.columna)

        if t.tipo is TipoToken.IDENTIFICADOR:
            self._avanzar()
            return arbol.Identificador(t.lexema, linea=t.linea, columna=t.columna)

        if t.tipo is TipoToken.LLAVE_IZQ:
            # aca el { agrupa, porque va DONDE TENDRIA QUE HABER un valor
            self._avanzar()
            interna = self.expresion()
            self._consumir(TipoToken.LLAVE_DER, "'}' para cerrar la agrupacion")
            return interna

        if t.tipo is TipoToken.MENOR:
            # y el < arma un vector, por lo mismo
            self._avanzar()
            elementos: list[arbol.Nodo] = []
            if self.actual.tipo is not TipoToken.MAYOR:
                elementos.append(self.expresion())
                while self.actual.tipo is TipoToken.BARRA:
                    self._avanzar()
                    elementos.append(self.expresion())
            self._consumir(TipoToken.MAYOR, "'>' para cerrar el literal de coleccion")
            return arbol.LiteralColeccion(elementos, linea=t.linea, columna=t.columna)

        raise self._error(f"se esperaba una expresion y se encontro {self._describir(t)}")


def parsear(tokens: list[Token], archivo: str = "<entrada>") -> arbol.Programa:
    return Sintactico(tokens, archivo).analizar()
