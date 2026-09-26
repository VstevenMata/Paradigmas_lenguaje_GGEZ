"""Paso 1: parte el texto en palabras (tokens).

El programa llega como una tira de letras sueltas. Aca las agrupo y le pongo
etiqueta a cada una. Los comentarios y los espacios los tiro.
Lo que se puede escribir esta en GRAMATICA.md, seccion 1.
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
        # cuantas llaves quedan sin cerrar. Si hay alguna, los saltos de linea
        # no cuentan, y asi una llamada larga se puede partir en varias -> D-3
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

        # si el archivo no termina en salto, igual cierro la ultima sentencia
        if self.tokens and self.tokens[-1].tipo is not TipoToken.FIN_LINEA:
            self._agregar(TipoToken.FIN_LINEA, "\\n", self.linea, self.columna)

        self._agregar(TipoToken.FIN_ARCHIVO, "", self.linea, self.columna)
        return self.tokens

    def _siguiente_token(self) -> None:
        c = self._actual()

        # espacios: solo separan, no significan nada
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

        # los dos errores mas comunes si venis de C o Java
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

        # dentro de {} o <> sin cerrar el salto no corta nada
        if self.anidacion > 0:
            return

        # varios saltos seguidos = uno solo
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
        """Lo que viene despues de una barra invertida -> D-5."""
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

        # el punto es decimal solo si le sigue un digito:
        # 1.5 es un real, pero marcador.size son 3 tokens
        es_real = False
        if self._actual() == "." and self._espiar().isdigit():
            es_real = True
            self._avanzar()
            while self._actual().isdigit():
                self._avanzar()

        lexema = self.fuente[inicio:self.pos]

        # 5abc: seguro se equivocaron escribiendo un nombre
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

        # sin tildes ni ene -> D-6. Python las deja pasar, asi que las saco aca
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

        # solo cuento { y <. El [ no, porque un generico entra en una linea
        if c in "{<":
            self.anidacion += 1
        elif c in "}>":
            self.anidacion = max(0, self.anidacion - 1)

        self._agregar(tipo, c, linea, columna)


def tokenizar(fuente: str, archivo: str = "<entrada>") -> list[Token]:
    """Atajo, para no tener que crear el Lexer a mano."""
    return Lexer(fuente, archivo).analizar()
