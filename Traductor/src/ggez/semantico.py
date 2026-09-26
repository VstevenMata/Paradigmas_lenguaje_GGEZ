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


@dataclass
class Firma:
    """Lo que hay que saber de un combo para verificar sus llamadas."""
    nombre: str
    parametros: list[tuple[str, str]]     # (tipo, nombre) en orden
    retorno: str | None                   # None cuando es 'drops noloot'
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
        self.global_ = Alcance()
        self.alcance = self.global_
        self.puntos_entrada = 0
        # cuantos ciclos anidados estamos recorriendo: ragequit y respawn solo
        # valen adentro de uno (D-14)
        self.profundidad_ciclo = 0
        # firmas de todos los combo, recolectadas antes de recorrer los cuerpos
        self.combos: dict[str, Firma] = {}
        # el combo que estamos recorriendo, para verificar sus reward
        self.combo_actual: Firma | None = None

    def _error(self, mensaje: str, nodo: arbol.Nodo) -> ErrorSemantico:
        return ErrorSemantico(mensaje, nodo.linea, nodo.columna, self.archivo)

    # ---------------------------------------------------------------- recorrido

    def analizar(self, programa: arbol.Programa) -> arbol.Programa:
        # Primera pasada: registrar la firma de todos los combo antes de mirar
        # ningun cuerpo. Asi un combo puede llamar a otro declarado mas abajo, y
        # la recursion mutua funciona sin declaraciones adelantadas.
        for declaracion in programa.declaraciones:
            if isinstance(declaracion, arbol.DeclCombo):
                self.registrar_combo(declaracion)

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

    def registrar_combo(self, nodo: arbol.DeclCombo) -> None:
        if nodo.nombre in self.combos:
            raise self._error(
                f"ya existe un combo llamado '{nodo.nombre}' "
                f"(linea {self.combos[nodo.nombre].linea})",
                nodo,
            )

        vistos: set[str] = set()
        for parametro in nodo.parametros:
            if parametro.nombre in vistos:
                raise self._error(
                    f"el parametro '{parametro.nombre}' esta repetido", parametro
                )
            vistos.add(parametro.nombre)

        self.combos[nodo.nombre] = Firma(
            nodo.nombre,
            [(str(p.tipo), p.nombre) for p in nodo.parametros],
            str(nodo.retorno) if nodo.retorno is not None else None,
            nodo.linea,
        )
        self.global_.declarar(Simbolo(nodo.nombre, "combo", True, nodo.linea))

    def decl_combo(self, nodo: arbol.DeclCombo) -> None:
        firma = self.combos[nodo.nombre]

        # El cuerpo de un combo NO ve las variables de quien lo llama: su alcance
        # cuelga del global, no del alcance actual. Sin esto, un combo llamado
        # desde adentro del lobby podria usar las variables del lobby por accidente.
        anterior, ciclos, combo_previo = self.alcance, self.profundidad_ciclo, self.combo_actual
        self.alcance = Alcance(self.global_)
        self.profundidad_ciclo = 0
        self.combo_actual = firma

        try:
            for parametro in nodo.parametros:
                self.alcance.declarar(
                    Simbolo(parametro.nombre, str(parametro.tipo), False, parametro.linea)
                )
            self.bloque(nodo.cuerpo)
        finally:
            self.alcance, self.profundidad_ciclo, self.combo_actual = anterior, ciclos, combo_previo

        if firma.retorno is not None and not self._siempre_devuelve(nodo.cuerpo):
            raise self._error(
                f"el combo '{nodo.nombre}' declara 'drops {firma.retorno}' pero hay "
                f"caminos que terminan sin ejecutar 'reward'",
                nodo,
            )

    def _siempre_devuelve(self, bloque: arbol.Bloque) -> bool:
        """True si toda ejecucion del bloque pasa por un 'reward'.

        Es un analisis de caminos simple: alcanza con que alguna sentencia lo
        garantice. Un 'scan' solo cuenta si tiene fallback Y las dos ramas
        devuelven; un 'loadout' solo si tiene 'noob' y todas sus ramas devuelven.
        Los ciclos no cuentan: pueden dar cero vueltas.
        """
        for sentencia in bloque.sentencias:
            if isinstance(sentencia, arbol.Reward):
                return True

            if isinstance(sentencia, arbol.Scan):
                if sentencia.sino is None:
                    continue
                sino_ok = (
                    self._siempre_devuelve_scan(sentencia.sino)
                    if isinstance(sentencia.sino, arbol.Scan)
                    else self._siempre_devuelve(sentencia.sino)
                )
                if self._siempre_devuelve(sentencia.entonces) and sino_ok:
                    return True

            if isinstance(sentencia, arbol.Loadout) and sentencia.defecto is not None:
                if all(self._siempre_devuelve(r.cuerpo) for r in sentencia.ramas) \
                        and self._siempre_devuelve(sentencia.defecto):
                    return True

        return False

    def _siempre_devuelve_scan(self, nodo: arbol.Scan) -> bool:
        if nodo.sino is None:
            return False
        sino_ok = (
            self._siempre_devuelve_scan(nodo.sino)
            if isinstance(nodo.sino, arbol.Scan)
            else self._siempre_devuelve(nodo.sino)
        )
        return self._siempre_devuelve(nodo.entonces) and sino_ok

    def sent_reward(self, nodo: arbol.Reward) -> None:
        if self.combo_actual is None:
            raise self._error("'reward' solo vale dentro de un combo", nodo)

        esperado = self.combo_actual.retorno

        if esperado is None:
            if nodo.valor is not None:
                raise self._error(
                    f"el combo '{self.combo_actual.nombre}' declara 'drops noloot', "
                    f"asi que su 'reward' no puede devolver un valor",
                    nodo,
                )
            return

        if nodo.valor is None:
            raise self._error(
                f"el combo '{self.combo_actual.nombre}' declara 'drops {esperado}', "
                f"asi que 'reward' necesita un valor",
                nodo,
            )

        self._verificar_asignable(esperado, self.expresion(nodo.valor), nodo)

    def declaracion(self, nodo: arbol.Nodo) -> None:
        if isinstance(nodo, arbol.Travel):
            self.MODULOS.add(nodo.modulo)
            return

        if isinstance(nodo, arbol.DeclCombo):
            self.decl_combo(nodo)
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
        elif isinstance(nodo, arbol.Scan):
            self.sent_scan(nodo)
        elif isinstance(nodo, arbol.Farm):
            self.sent_farm(nodo)
        elif isinstance(nodo, arbol.Survive):
            self._ciclo(nodo.condicion, nodo.cuerpo, "survive")
        elif isinstance(nodo, arbol.Grind):
            self._ciclo(nodo.condicion, nodo.cuerpo, "grind")
        elif isinstance(nodo, arbol.Loadout):
            self.sent_loadout(nodo)
        elif isinstance(nodo, arbol.Reward):
            self.sent_reward(nodo)
        elif isinstance(nodo, (arbol.Ragequit, arbol.Respawn)):
            if self.profundidad_ciclo == 0:
                palabra = "ragequit" if isinstance(nodo, arbol.Ragequit) else "respawn"
                raise self._error(
                    f"'{palabra}' solo vale dentro de un ciclo (farm, survive o grind)", nodo
                )
        else:
            raise self._error("sentencia no reconocida", nodo)

    def _condicion(self, nodo: arbol.Nodo, construccion: str) -> None:
        tipo = self.expresion(nodo)
        if tipo not in ("win", "desconocido"):
            raise self._error(
                f"la condicion de '{construccion}' debe ser win y se obtuvo {tipo}", nodo
            )

    def sent_scan(self, nodo: arbol.Scan) -> None:
        self._condicion(nodo.condicion, "scan")
        self.bloque(nodo.entonces)
        if isinstance(nodo.sino, arbol.Scan):
            self.sent_scan(nodo.sino)
        elif nodo.sino is not None:
            self.bloque(nodo.sino)

    def _ciclo(self, condicion: arbol.Nodo, cuerpo: arbol.Bloque, construccion: str) -> None:
        self._condicion(condicion, construccion)
        self.profundidad_ciclo += 1
        try:
            self.bloque(cuerpo)
        finally:
            self.profundidad_ciclo -= 1

    def sent_farm(self, nodo: arbol.Farm) -> None:
        # Las tres partes del farm viven en el alcance del bloque que lo
        # contiene, no en uno propio: 'farm i set 0 | ...' reasigna una variable
        # que ya tiene que estar declarada.
        self.sentencia(nodo.inicio)
        self._condicion(nodo.condicion, "farm")
        self.sentencia(nodo.paso)

        self.profundidad_ciclo += 1
        try:
            self.bloque(nodo.cuerpo)
        finally:
            self.profundidad_ciclo -= 1

    def sent_loadout(self, nodo: arbol.Loadout) -> None:
        seleccion = self.expresion(nodo.expresion)

        vistos: dict[object, int] = {}
        for rama in nodo.ramas:
            for valor in rama.valores:
                tipo = self.expresion(valor)
                if seleccion != "desconocido" and tipo != seleccion:
                    raise self._error(
                        f"el loadout selecciona sobre {seleccion} y esta rama compara "
                        f"contra {tipo}",
                        valor,
                    )
                clave = valor.valor if isinstance(valor, arbol.Literal) else None
                if clave in vistos:
                    raise self._error(
                        f"el valor ya aparece en otra rama del loadout (linea {vistos[clave]})",
                        valor,
                    )
                vistos[clave] = valor.linea
            self.bloque(rama.cuerpo)

        if nodo.defecto is not None:
            self.bloque(nodo.defecto)

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
            return self.llamada(nodo)

        if isinstance(nodo, (arbol.Indice, arbol.LiteralColeccion, arbol.Me)):
            return "desconocido"                    # colecciones y POO: bloques 4 y 5

        raise self._error("expresion no reconocida", nodo)

    def llamada(self, nodo: arbol.Llamada) -> str:
        tipos = [self.expresion(a) for a in nodo.argumentos]

        # Solo se puede verificar cuando se llama a un combo por su nombre. Las
        # llamadas a modulos (chat.send, physics.avg) todavia no tienen firma
        # declarada, asi que pasan sin revisar -- eso es el bloque 6.
        if not isinstance(nodo.destino, arbol.Identificador):
            self.expresion(nodo.destino)
            return "desconocido"

        firma = self.combos.get(nodo.destino.nombre)
        if firma is None:
            self.expresion(nodo.destino)
            return "desconocido"

        if len(tipos) != len(firma.parametros):
            raise self._error(
                f"el combo '{firma.nombre}' espera {len(firma.parametros)} "
                f"argumento(s) y recibio {len(tipos)}",
                nodo,
            )

        for (esperado, nombre), obtenido, argumento in zip(firma.parametros, tipos, nodo.argumentos):
            if obtenido == "desconocido" or obtenido == esperado:
                continue
            if esperado == "aim" and obtenido == "score":
                continue                             # promocion permitida
            raise self._error(
                f"el parametro '{nombre}' de '{firma.nombre}' es {esperado} y se "
                f"paso un {obtenido}",
                argumento,
            )

        if firma.retorno is None:
            return "noloot"
        return firma.retorno

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
