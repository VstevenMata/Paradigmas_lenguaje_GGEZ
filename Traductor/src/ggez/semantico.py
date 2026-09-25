"""Analizador semantico de GGEZ.

Tercera fase de la traduccion. Mantiene la TABLA DE SIMBOLOS -- la misma que
aparece al costado del diagrama de etapas de Problemas de traduccion de
lenguajes.pdf -- y con ella verifica lo que la sintaxis no puede:

  - que toda variable este declarada antes de usarse
  - que los tipos calcen (con promocion score -> aim, nunca al reves)
  - que no se reasigne un hardcoded
  - que no se tape (shadowing) un nombre de un bloque exterior  (D-8)
  - que exista exactamente un lobby.launch                      (D-13)
  - que una variable de tipo simple se declare con 'set'        (D-9)
"""

from __future__ import annotations

from dataclasses import dataclass

from . import arbol
from .errores import ErrorSemantico
from .tokens import TIPOS_SIMPLES

# score se promueve a aim automaticamente; al reves es error (D "4.3" del txt
# de pendientes): perder decimales tiene que ser explicito.
NUMERICOS = ("score", "aim")


@dataclass
class Simbolo:
    nombre: str
    tipo: str
    constante: bool
    linea: int


class Alcance:
    """Un nivel de la tabla de simbolos. Cada glhf...gg abre uno nuevo."""

    def __init__(self, padre: "Alcance | None" = None) -> None:
        self.simbolos: dict[str, Simbolo] = {}
        self.padre = padre

    def declarar(self, simbolo: Simbolo) -> None:
        self.simbolos[simbolo.nombre] = simbolo

    def buscar(self, nombre: str) -> Simbolo | None:
        """Busca en este alcance y hacia afuera: un bloque interno ve los externos."""
        alcance: Alcance | None = self
        while alcance is not None:
            if nombre in alcance.simbolos:
                return alcance.simbolos[nombre]
            alcance = alcance.padre
        return None


class Semantico:
    # modulos de la libreria estandar que existen sin importar (4.6 del txt)
    MODULOS = {"chat"}

    def __init__(self, archivo: str = "<entrada>") -> None:
        self.archivo = archivo
        self.alcance = Alcance()
        self.puntos_entrada = 0

    def _error(self, mensaje: str, nodo: arbol.Nodo) -> ErrorSemantico:
        return ErrorSemantico(mensaje, nodo.linea, nodo.columna, self.archivo)

    # ---------------------------------------------------------------- recorrido

    def analizar(self, programa: arbol.Programa) -> arbol.Programa:
        for declaracion in programa.declaraciones:
            self.declaracion(declaracion)

        if self.puntos_entrada == 0:
            # Un archivo sin punto de entrada es valido como biblioteca (D-13),
            # pero no se puede ejecutar, que es lo que pide 'ggezc run'.
            raise ErrorSemantico(
                "el programa no tiene punto de entrada: falta lobby.launch{start THE GAME}",
                1, 1, self.archivo,
            )
        return programa

    def declaracion(self, nodo: arbol.Nodo) -> None:
        if isinstance(nodo, arbol.Travel):
            self.MODULOS.add(nodo.modulo)
            return

        if isinstance(nodo, arbol.PuntoEntrada):
            self.puntos_entrada += 1
            if self.puntos_entrada > 1:
                raise self._error("solo puede haber un lobby.launch en el programa", nodo)
            self.bloque(nodo.cuerpo)
            return

        raise self._error("declaracion de nivel superior no reconocida", nodo)

    def bloque(self, nodo: arbol.Bloque) -> None:
        self.alcance = Alcance(self.alcance)
        for sentencia in nodo.sentencias:
            self.sentencia(sentencia)
        self.alcance = self.alcance.padre          # type: ignore[assignment]

    def sentencia(self, nodo: arbol.Nodo) -> None:
        if isinstance(nodo, arbol.DeclVariable):
            self.decl_variable(nodo)
        elif isinstance(nodo, arbol.Asignacion):
            self.asignacion(nodo)
        elif isinstance(nodo, arbol.Incremento):
            self.incremento(nodo)
        elif isinstance(nodo, arbol.SentenciaExpresion):
            self.expresion(nodo.expresion)
        elif isinstance(nodo, arbol.Bloque):
            self.bloque(nodo)
        else:
            raise self._error("sentencia no reconocida", nodo)

    def decl_variable(self, nodo: arbol.DeclVariable) -> None:
        nombre = nodo.nombre

        if nombre in self.alcance.simbolos:
            raise self._error(f"'{nombre}' ya fue declarada en este bloque", nodo)

        previo = self.alcance.buscar(nombre)
        if previo is not None:
            # D-8: prohibido tapar un nombre de afuera. Evita bugs sutiles y es
            # mas facil de implementar que resolver el shadowing bien.
            raise self._error(
                f"'{nombre}' ya existe en un bloque exterior (linea {previo.linea}); "
                f"GGEZ no permite taparla",
                nodo,
            )

        declarado = str(nodo.tipo)

        if nodo.valor is None:
            if nodo.tipo.nombre in TIPOS_SIMPLES:
                raise self._error(
                    f"una variable de tipo {declarado} debe inicializarse con 'set'", nodo
                )
        else:
            obtenido = self.expresion(nodo.valor)
            self._verificar_asignable(declarado, obtenido, nodo)

        self.alcance.declarar(Simbolo(nombre, declarado, nodo.constante, nodo.linea))

    def asignacion(self, nodo: arbol.Asignacion) -> None:
        destino = self.expresion(nodo.destino)
        valor = self.expresion(nodo.valor)

        if isinstance(nodo.destino, arbol.Identificador):
            simbolo = self.alcance.buscar(nodo.destino.nombre)
            if simbolo is not None and simbolo.constante:
                raise self._error(
                    f"'{simbolo.nombre}' es hardcoded y no se puede reasignar", nodo
                )

        if nodo.operador in ("boost", "sap") and destino not in NUMERICOS:
            raise self._error(
                f"'{nodo.operador}' solo aplica a score o aim, no a {destino}", nodo
            )

        self._verificar_asignable(destino, valor, nodo)

    def incremento(self, nodo: arbol.Incremento) -> None:
        tipo = self.expresion(nodo.destino)
        if tipo not in NUMERICOS:
            raise self._error(
                f"'{nodo.operador}' solo aplica a score o aim, no a {tipo}", nodo
            )
        if isinstance(nodo.destino, arbol.Identificador):
            simbolo = self.alcance.buscar(nodo.destino.nombre)
            if simbolo is not None and simbolo.constante:
                raise self._error(
                    f"'{simbolo.nombre}' es hardcoded y no se puede modificar", nodo
                )

    def _verificar_asignable(self, destino: str, origen: str, nodo: arbol.Nodo) -> None:
        if destino == origen or origen == "desconocido":
            return
        if destino == "aim" and origen == "score":
            return                                  # promocion permitida
        if destino == "score" and origen == "aim":
            raise self._error(
                "no se puede asignar un aim a un score: se perderian los decimales", nodo
            )
        raise self._error(f"se esperaba {destino} y se obtuvo {origen}", nodo)

    # -------------------------------------------------------------- expresiones

    def expresion(self, nodo: arbol.Nodo) -> str:
        """Verifica la expresion y devuelve su tipo."""
        if isinstance(nodo, arbol.Literal):
            return nodo.tipo

        if isinstance(nodo, arbol.Identificador):
            simbolo = self.alcance.buscar(nodo.nombre)
            if simbolo is None:
                if nodo.nombre in self.MODULOS:
                    return "modulo"
                raise self._error(f"'{nodo.nombre}' no esta declarada", nodo)
            return simbolo.tipo

        if isinstance(nodo, arbol.Unaria):
            tipo = self.expresion(nodo.operando)
            if nodo.operador == "dodge":
                if tipo != "win":
                    raise self._error(f"'dodge' espera un win y se obtuvo {tipo}", nodo)
                return "win"
            if tipo not in NUMERICOS:
                raise self._error(f"el menos unario espera score o aim y se obtuvo {tipo}", nodo)
            return tipo

        if isinstance(nodo, arbol.Binaria):
            return self.binaria(nodo)

        if isinstance(nodo, arbol.Acceso):
            objeto = self.expresion(nodo.objeto)
            if objeto == "modulo":
                return "desconocido"                # lo resuelve el evaluador
            return "desconocido"                    # campos: bloque 5

        if isinstance(nodo, arbol.Llamada):
            for argumento in nodo.argumentos:
                self.expresion(argumento)
            self.expresion(nodo.destino)
            return "desconocido"                    # firmas: bloque 3

        if isinstance(nodo, (arbol.Indice, arbol.LiteralColeccion, arbol.Me)):
            return "desconocido"                    # colecciones y POO: bloques 4 y 5

        raise self._error("expresion no reconocida", nodo)

    def binaria(self, nodo: arbol.Binaria) -> str:
        izq = self.expresion(nodo.izquierda)
        der = self.expresion(nodo.derecha)
        op = nodo.operador

        if "desconocido" in (izq, der):
            return "desconocido"

        if op in ("chain", "swap"):
            if izq != "win" or der != "win":
                raise self._error(f"'{op}' espera dos win y se obtuvo {izq} y {der}", nodo)
            return "win"

        if op in ("matches", "mismatch"):
            return "win"

        if op in ("over", "under", "atleast", "atmost"):
            if izq not in NUMERICOS or der not in NUMERICOS:
                raise self._error(
                    f"'{op}' compara score o aim, y se obtuvo {izq} y {der}", nodo
                )
            return "win"

        if op == "gain":
            # gain suma Y concatena: si un lado es lore, el otro se convierte a
            # texto (4.1 del txt de pendientes)
            if izq == "lore" or der == "lore":
                return "lore"
            return self._numerica(izq, der, op, nodo)

        if op == "split":
            # la division siempre da aim, aunque los dos lados sean score (4.2)
            self._numerica(izq, der, op, nodo)
            return "aim"

        if op in ("drain", "crit", "scrap"):
            return self._numerica(izq, der, op, nodo)

        raise self._error(f"operador desconocido: {op}", nodo)

    def _numerica(self, izq: str, der: str, op: str, nodo: arbol.Nodo) -> str:
        if izq not in NUMERICOS or der not in NUMERICOS:
            raise self._error(f"'{op}' espera score o aim, y se obtuvo {izq} y {der}", nodo)
        return "aim" if "aim" in (izq, der) else "score"


def analizar(programa: arbol.Programa, archivo: str = "<entrada>") -> arbol.Programa:
    return Semantico(archivo).analizar(programa)
