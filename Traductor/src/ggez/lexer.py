"""Analizador lexico de GGEZ.

Primera fase de la traduccion (ver "Etapas de traduccion" en
Problemas de traduccion de lenguajes.pdf): agrupa los caracteres del programa
fuente en sus constituyentes elementales -- identificadores, palabras
reservadas, literales, delimitadores -- y los entrega como una lista de tokens
al analizador sintactico.

Implementa la seccion 1 de GRAMATICA.md.
"""

from __future__ import annotations

from .errores import ErrorLexico
from .tokens import RESERVADAS, SIMBOLOS, TipoToken, Token


class Lexer:
    def __init__(self, fuente: str, archivo: str = "<entrada>") -> None:
        self.fuente = fuente
        self.archivo = archivo
        self.pos = 0
        self.linea = 1
        self.columna = 1
        self.tokens: list[Token] = []
        # Profundidad de '{' y '<' sin cerrar. Mientras sea > 0 los saltos de
        # linea se ignoran, y asi una llamada o un literal de coleccion puede
        # partirse en varias lineas (decision D-3 de GRAMATICA.md).
        self.anidacion = 0

    # ---------------------------------------------------------------- helpers

    def _fin(self) -> bool:
        return self.pos >= len(self.fuente)

    def _actual(self) -> str:
        return "" if self._fin() else self.fuente[self.pos]

    def _espiar(self, adelante: int = 1) -> str:
        i = self.pos + adelante
        return "" if i >= len(self.fuente) else self.fuente[i]

    def _avanzar(self) -> str:
        c = self.fuente[self.pos]
        self.pos += 1
        if c == "\n":
            self.linea += 1
            self.columna = 1
        else:
            self.columna += 1
        return c

    def _error(self, mensaje: str, linea: int | None = None, columna: int | None = None):
        return ErrorLexico(
            mensaje,
            linea if linea is not None else self.linea,
            columna if columna is not None else self.columna,
            self.archivo,
        )

    def _agregar(self, tipo: TipoToken, lexema: str, linea: int, columna: int, valor=None) -> None:
        self.tokens.append(Token(tipo, lexema, linea, columna, valor))

    # ----------------------------------------------------------------- tokens

    def analizar(self) -> list[Token]:
        while not self._fin():
            self._siguiente_token()

        # Un archivo que no termina en salto de linea igual cierra su ultima
        # sentencia: sin esto el parser se quedaria esperando el FIN_LINEA.
        if self.tokens and self.tokens[-1].tipo is not TipoToken.FIN_LINEA:
            self._agregar(TipoToken.FIN_LINEA, "\\n", self.linea, self.columna)

        self._agregar(TipoToken.FIN_ARCHIVO, "", self.linea, self.columna)
        return self.tokens

    def _siguiente_token(self) -> None:
        c = self._actual()

        # espacios y tabulaciones: separan tokens, no significan nada mas
        if c in " \t\r":
            self._avanzar()
            return

        if c == "\n":
            self._salto_de_linea()
            return

        if c == "/" and self._espiar() in ("w", "a"):
            self._comentario()
            return

        if c == '"':
            self._cadena()
            return

        if c == "'":
            self._caracter()
            return

        if c.isdigit():
            self._numero()
            return

        if c.isalpha() or c == "_":
            self._palabra()
            return

        if c in SIMBOLOS:
            self._simbolo()
            return

        # Mensaje explicito para los dos simbolos que GGEZ elimino a proposito:
        # es el error que mas van a cometer los que vienen de C o de Java.
        if c == ";":
            raise self._error(
                "GGEZ no usa ';': el salto de linea cierra la sentencia"
            )
        if c in "()":
            raise self._error(
                f"GGEZ no usa parentesis: use llaves en lugar de '{c}'"
            )

        raise self._error(f"caracter inesperado: {c!r}")

    def _salto_de_linea(self) -> None:
        linea, columna = self.linea, self.columna
        self._avanzar()

        # dentro de { } o < > sin cerrar, el salto de linea no corta la sentencia
        if self.anidacion > 0:
            return

        # varios saltos seguidos (o lineas en blanco) valen por uno solo, y un
        # salto al principio del archivo no cierra ninguna sentencia
        if not self.tokens or self.tokens[-1].tipo is TipoToken.FIN_LINEA:
            return

        self._agregar(TipoToken.FIN_LINEA, "\\n", linea, columna)

    def _comentario(self) -> None:
        linea, columna = self.linea, self.columna

        if self._espiar() == "w":              # /w comentario de una linea
            while not self._fin() and self._actual() != "\n":
                self._avanzar()
            return

        # /all ... /end  comentario de bloque
        if self.fuente.startswith("/all", self.pos):
            for _ in range(len("/all")):
                self._avanzar()
            while not self._fin():
                if self._actual() == "/" and self.fuente.startswith("/end", self.pos):
                    for _ in range(len("/end")):
                        self._avanzar()
                    return
                self._avanzar()
            raise self._error("comentario de bloque sin cerrar: falta '/end'", linea, columna)

        raise self._error(f"caracter inesperado: {self._actual()!r}")

    def _cadena(self) -> None:
        linea, columna = self.linea, self.columna
        self._avanzar()                         # comilla de apertura
        partes: list[str] = []

        while True:
            if self._fin() or self._actual() == "\n":
                raise self._error("cadena sin cerrar: falta la comilla doble", linea, columna)
            c = self._avanzar()
            if c == '"':
                break
            partes.append(self._escape() if c == "\\" else c)

        texto = "".join(partes)
        self._agregar(TipoToken.CADENA, f'"{texto}"', linea, columna, texto)

    def _caracter(self) -> None:
        linea, columna = self.linea, self.columna
        self._avanzar()                         # comilla de apertura

        if self._fin() or self._actual() == "\n":
            raise self._error("literal de caracter sin cerrar", linea, columna)

        c = self._avanzar()
        valor = self._escape() if c == "\\" else c

        if self._fin() or self._actual() != "'":
            raise self._error(
                "un literal de caracter (pixel) lleva exactamente un caracter; "
                "para texto use comillas dobles",
                linea, columna,
            )
        self._avanzar()                         # comilla de cierre
        self._agregar(TipoToken.CARACTER, f"'{valor}'", linea, columna, valor)

    def _escape(self) -> str:
        """Traduce la secuencia que sigue a una '\\' (decision D-5)."""
        if self._fin():
            raise self._error("secuencia de escape incompleta")
        linea, columna = self.linea, self.columna
        c = self._avanzar()
        tabla = {"n": "\n", "t": "\t", "\\": "\\", '"': '"', "'": "'"}
        if c not in tabla:
            raise self._error(f"secuencia de escape desconocida: '\\{c}'", linea, columna)
        return tabla[c]

    def _numero(self) -> None:
        linea, columna = self.linea, self.columna
        inicio = self.pos

        while self._actual().isdigit():
            self._avanzar()

        # El punto solo forma parte del numero si viene seguido de un digito.
        # Asi '1.5' es un real pero 'marcador.size' se parte en tres tokens.
        es_real = False
        if self._actual() == "." and self._espiar().isdigit():
            es_real = True
            self._avanzar()
            while self._actual().isdigit():
                self._avanzar()

        lexema = self.fuente[inicio:self.pos]

        # 'aim' es real y 'score' entero: un numero pegado a una letra casi
        # siempre es un identificador mal escrito, conviene avisarlo aca.
        if self._actual().isalpha() or self._actual() == "_":
            raise self._error(
                f"un identificador no puede empezar con un digito: {lexema}{self._actual()}",
                linea, columna,
            )

        if es_real:
            self._agregar(TipoToken.REAL, lexema, linea, columna, float(lexema))
        else:
            self._agregar(TipoToken.ENTERO, lexema, linea, columna, int(lexema))

    def _palabra(self) -> None:
        linea, columna = self.linea, self.columna
        inicio = self.pos

        while self._actual().isalnum() or self._actual() == "_":
            self._avanzar()

        lexema = self.fuente[inicio:self.pos]

        # El lenguaje es sensible a mayusculas (D-7) y no admite tildes ni 'n'
        # con virgulilla (D-6): .isalnum() de Python sí las acepta, así que hay
        # que rechazarlas a mano.
        if not lexema.isascii():
            raise self._error(
                f"los identificadores no admiten tildes ni 'ñ': {lexema}",
                linea, columna,
            )

        tipo = TipoToken.RESERVADA if lexema in RESERVADAS else TipoToken.IDENTIFICADOR
        self._agregar(tipo, lexema, linea, columna)

    def _simbolo(self) -> None:
        linea, columna = self.linea, self.columna
        c = self._avanzar()
        tipo = SIMBOLOS[c]

        # Solo '{' y '<' suspenden el corte por salto de linea. '[' no hace
        # falta: un generico siempre cabe en una linea.
        if c in "{<":
            self.anidacion += 1
        elif c in "}>":
            self.anidacion = max(0, self.anidacion - 1)

        self._agregar(tipo, c, linea, columna)


def tokenizar(fuente: str, archivo: str = "<entrada>") -> list[Token]:
    """Atajo: devuelve la lista de tokens de un programa GGEZ."""
    return Lexer(fuente, archivo).analizar()
