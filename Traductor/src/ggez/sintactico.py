"""Analizador sintactico de GGEZ.

Segunda fase de la traduccion. Descenso recursivo: hay UNA FUNCION POR
NO-TERMINAL de GRAMATICA.md, con el mismo nombre, de modo que se puede leer la
gramatica y el codigo en paralelo. La cascada de expresiones de la seccion 8 ya
codifica la precedencia, asi que no hace falta resolver ambiguedades aparte.

Alcance actual (bloque 1 del TODO): punto de entrada, bloques, declaraciones de
variables y constantes, asignaciones y la cascada completa de expresiones.
Control de flujo, funciones, colecciones y POO son los bloques 2 a 5.
"""

from __future__ import annotations

from . import arbol
from .errores import ErrorSintactico
from .tokens import (
    CONSTRUCTORES_COLECCION,
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
        """Consume el cierre de sentencia. En GGEZ no hay ';': es el salto de linea."""
        if self.actual.tipo is TipoToken.FIN_ARCHIVO:
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
        raise self._error(
            f"aqui se esperaba una declaracion de nivel superior (travel o "
            f"lobby.launch) y se encontro {self._describir(self.actual)}"
        )

    def decl_travel(self) -> arbol.Travel:
        t = self._consumir_reservada("travel")
        modulo = self._consumir(TipoToken.IDENTIFICADOR, "el nombre del modulo").lexema
        self._consumir_reservada("from")
        paquete = self._consumir(TipoToken.IDENTIFICADOR, "el nombre del paquete").lexema
        self._fin_linea()
        return arbol.Travel(modulo, paquete, linea=t.linea, columna=t.columna)

    def punto_entrada(self) -> arbol.PuntoEntrada:
        # lobby.launch{start THE GAME} -- 'start THE GAME' es texto fijo (D-8)
        t = self._consumir_reservada("lobby")
        self._consumir(TipoToken.PUNTO, "'.' despues de lobby")
        self._consumir_reservada("launch")
        self._consumir(TipoToken.LLAVE_IZQ, "'{' despues de lobby.launch")
        for palabra in ("start", "THE", "GAME"):
            self._consumir_reservada(palabra)
        self._consumir(TipoToken.LLAVE_DER, "'}' para cerrar lobby.launch")
        return arbol.PuntoEntrada(self.bloque(), linea=t.linea, columna=t.columna)

    def bloque(self) -> arbol.Bloque:
        t = self._consumir_reservada("glhf")
        self._fin_linea()
        sentencias: list[arbol.Nodo] = []

        while not self.actual.es("gg"):
            if self.actual.tipo is TipoToken.FIN_ARCHIVO:
                raise self._error("bloque sin cerrar: falta 'gg'", t)
            sentencias.append(self.sentencia())
            self._saltar_lineas()

        self._consumir_reservada("gg")
        return arbol.Bloque(sentencias, linea=t.linea, columna=t.columna)

    # ------------------------------------------------------------- sentencias

    def sentencia(self) -> arbol.Nodo:
        if self.actual.es("stash", "hardcoded"):
            return self.decl_variable()

        # Cualquier otra sentencia empieza por una expresion. Si despues de esa
        # expresion viene set/boost/sap o buff/nerf, era en realidad el
        # designador de una asignacion.
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
        """Solo un identificador, un campo o un indice pueden ir a la izquierda."""
        if not isinstance(nodo, (arbol.Identificador, arbol.Acceso, arbol.Indice, arbol.Me)):
            raise self._error("el lado izquierdo de una asignacion no es asignable", token)

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
    # La cascada reproduce la tabla de precedencia de GRAMATICA.md seccion 8.

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
        # no asociativa a proposito: 'a under b under c' no significa nada
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
                # aca '<' es un indice: el literal de coleccion solo aparece
                # donde se espera un operando, no despues de uno
                self._avanzar()
                indice = self.expresion()
                self._consumir(TipoToken.MAYOR, "'>' para cerrar el indice")
                nodo = arbol.Indice(nodo, indice, linea=t.linea, columna=t.columna)

            elif t.tipo is TipoToken.LLAVE_IZQ:
                # y aca '{' es una llamada, por la misma razon
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

        if t.tipo is TipoToken.IDENTIFICADOR:
            self._avanzar()
            return arbol.Identificador(t.lexema, linea=t.linea, columna=t.columna)

        if t.tipo is TipoToken.LLAVE_IZQ:
            # agrupacion: '{' donde se espera un operando
            self._avanzar()
            interna = self.expresion()
            self._consumir(TipoToken.LLAVE_DER, "'}' para cerrar la agrupacion")
            return interna

        if t.tipo is TipoToken.MENOR:
            # literal de coleccion: '<' donde se espera un operando
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
