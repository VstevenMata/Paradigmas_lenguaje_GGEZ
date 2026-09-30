"""Paso 3: revisa que el programa tenga sentido.

Aca esta la TABLA DE SIMBOLOS, la del dibujo de etapas del PDF del curso.

Agarra lo que el paso 2 no puede ver: usar una variable que no existe, meter
texto en un numero, tocar un hardcoded, un scan con una condicion que no es
win. O sea: esta bien escrito, pero no tiene sentido.
"""

from __future__ import annotations

from dataclasses import dataclass

from . import arbol
from .errores import ErrorSemantico
from .tokens import TIPOS_SIMPLES

# un entero se vuelve decimal solo. Al reves no, porque se perderian decimales
NUMERICOS = ("score", "aim")

# Metodos de squad, la unica coleccion -> D-21. Nombres propuestos -> D-20.
# Cada entrada: (cuantos args, de que tipo, que devuelve)
#   "T"  = el tipo interno (el score de squad[score])
#   None = no devuelve nada
METODOS_COLECCION: dict[str, dict[str, tuple[int, str | None, str | None]]] = {
    "squad": {                       # vector: se recluta y se echa gente
        "recruit": (1, "T", None),       # agregar al final
        "kick":    (1, "score", None),   # sacar por indice
    },
}


def tipo_interno(tipo: str) -> str | None:
    """squad[score] -> score. Si no es coleccion, None."""
    for constructor in METODOS_COLECCION:
        if tipo.startswith(constructor + "[") and tipo.endswith("]"):
            return tipo[len(constructor) + 1:-1]
    return None


def constructor_de(tipo: str) -> str | None:
    """squad[score] -> squad."""
    for constructor in METODOS_COLECCION:
        if tipo.startswith(constructor + "["):
            return constructor
    return None


@dataclass
class Simbolo:
    nombre: str
    tipo: str
    constante: bool
    linea: int


@dataclass
class Firma:
    """Lo que hay que saber de un combo para revisar como lo llaman."""
    nombre: str
    parametros: list[tuple[str, str]]     # (tipo, nombre), en orden
    retorno: str | None                   # None = drops noloot
    linea: int


class Alcance:
    """Un nivel de la tabla de simbolos. Cada glhf...gg abre uno nuevo."""

    def __init__(self, padre: "Alcance | None" = None) -> None:
        self.simbolos: dict[str, Simbolo] = {}
        self.padre = padre

    def declarar(self, simbolo: Simbolo) -> None:
        self.simbolos[simbolo.nombre] = simbolo

    def buscar(self, nombre: str) -> Simbolo | None:
        """Busca aca y si no esta va subiendo. De adentro se ve lo de afuera."""
        alcance: Alcance | None = self
        while alcance is not None:
            if nombre in alcance.simbolos:
                return alcance.simbolos[nombre]
            alcance = alcance.padre
        return None


class Semantico:
    # modulos que existen sin tener que importarlos
    MODULOS = {"chat"}

    def __init__(self, archivo: str = "<entrada>") -> None:
        self.archivo = archivo
        self.global_ = Alcance()
        self.alcance = self.global_
        self.puntos_entrada = 0
        # para saber si estoy dentro de un ciclo. Lo usan ragequit y respawn -> D-14
        self.profundidad_ciclo = 0
        # los combo que existen. Se llena antes de mirar ningun cuerpo
        self.combos: dict[str, Firma] = {}
        # los inventory: nombre -> [(tipo, campo)]
        self.registros: dict[str, list[tuple[str, str]]] = {}
        # en que combo estoy parado, para revisar sus reward
        self.combo_actual: Firma | None = None

    def _error(self, mensaje: str, nodo: arbol.Nodo) -> ErrorSemantico:
        return ErrorSemantico(mensaje, nodo.linea, nodo.columna, self.archivo)

    # ---------------------------------------------------------------- recorrido

    def analizar(self, programa: arbol.Programa) -> arbol.Programa:
        # DOS VUELTAS. En la primera solo anoto que combos existen, sin mirar
        # que hacen. Asi uno puede llamar a otro que esta escrito mas abajo
        for declaracion in programa.declaraciones:
            if isinstance(declaracion, arbol.DeclInventory):
                self.registrar_inventory(declaracion)
        for declaracion in programa.declaraciones:
            if isinstance(declaracion, arbol.DeclCombo):
                self.registrar_combo(declaracion)

        for declaracion in programa.declaraciones:
            self.declaracion(declaracion)

        if self.puntos_entrada == 0:
            # sin punto de entrada sirve como biblioteca, pero no se ejecuta -> D-13
            raise ErrorSemantico(
                "el programa no tiene punto de entrada: falta lobby.launch{start THE GAME}",
                1, 1, self.archivo,
            )
        return programa

    def registrar_inventory(self, nodo: arbol.DeclInventory) -> None:
        if nodo.nombre in self.registros:
            raise self._error(f"ya existe un inventory llamado '{nodo.nombre}'", nodo)

        vistos: set[str] = set()
        for campo in nodo.campos:
            if campo.nombre in vistos:
                raise self._error(f"el campo '{campo.nombre}' esta repetido", campo)
            vistos.add(campo.nombre)

        self.registros[nodo.nombre] = [(str(c.tipo), c.nombre) for c in nodo.campos]

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

        # OJO: arranca desde el nivel de afuera de todo, no desde donde estoy.
        # Adentro del combo no se ven las variables del que lo llamo
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
        """True si no hay forma de salir sin pasar por un reward.

        Un scan cuenta solo si tiene fallback y devuelven las dos ramas.
        Los ciclos nunca cuentan, porque pueden no dar ni una vuelta.
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

        if isinstance(nodo, arbol.DeclInventory):
            # los campos ya se validaron al registrar; falta ver que los tipos existan
            for tipo, campo in self.registros[nodo.nombre]:
                if not self._tipo_existe(tipo):
                    raise self._error(
                        f"el campo '{campo}' usa un tipo desconocido: {tipo}", nodo
                    )
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
            # prohibido tapar un nombre de afuera -> D-8
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
            return                                  # score -> aim, permitido

        # el <a | b | c> no sabe de que tipo es: se lo pone el que lo recibe
        if origen.startswith("coleccion["):
            interno_destino = tipo_interno(destino)
            if interno_destino is None:
                raise self._error(
                    f"se esperaba {destino} y se obtuvo un literal de coleccion", nodo
                )
            if origen == "coleccion[]":
                return                              # literal vacio: sirve para cualquiera
            interno_origen = origen[len("coleccion["):-1]
            if interno_origen != interno_destino:
                raise self._error(
                    f"la coleccion es de {interno_destino} y el literal trae "
                    f"{interno_origen}",
                    nodo,
                )
            return
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
            return self.acceso(nodo)

        if isinstance(nodo, arbol.Llamada):
            return self.llamada(nodo)

        if isinstance(nodo, arbol.Spawn):
            return self.spawn(nodo)

        if isinstance(nodo, arbol.Indice):
            coleccion = self.expresion(nodo.objeto)
            indice = self.expresion(nodo.indice)

            if indice not in ("score", "desconocido"):
                raise self._error(f"el indice debe ser un score y se obtuvo {indice}", nodo)
            if coleccion == "desconocido":
                return "desconocido"

            interno = tipo_interno(coleccion)
            if interno is None:
                raise self._error(f"{coleccion} no se puede indexar", nodo)
            return interno

        if isinstance(nodo, arbol.LiteralColeccion):
            # aca solo miro que sean todos del mismo tipo. El tipo de verdad
            # se lo pone despues la declaracion que lo recibe
            if not nodo.elementos:
                return "coleccion[]"
            tipos = [self.expresion(e) for e in nodo.elementos]
            primero = tipos[0]
            for tipo, elemento in zip(tipos[1:], nodo.elementos[1:]):
                if tipo != primero:
                    raise self._error(
                        f"los elementos de la coleccion deben ser del mismo tipo: "
                        f"empezo con {primero} y aparecio un {tipo}",
                        elemento,
                    )
            return f"coleccion[{primero}]"

        if isinstance(nodo, arbol.Me):
            return "desconocido"                    # falta: bloque 5

        raise self._error("expresion no reconocida", nodo)

    def _tipo_existe(self, tipo: str) -> bool:
        if tipo in TIPOS_SIMPLES or tipo in self.registros:
            return True
        interno = tipo_interno(tipo)
        return interno is not None and self._tipo_existe(interno)

    def acceso(self, nodo: arbol.Acceso) -> str:
        objeto = self.expresion(nodo.objeto)

        if objeto in ("modulo", "desconocido"):
            return "desconocido"

        # .size
        if nodo.campo == "size" and constructor_de(objeto) is not None:
            return "score"

        # devuelvo una marca y la Llamada de afuera se encarga
        constructor = constructor_de(objeto)
        if constructor is not None:
            if nodo.campo in METODOS_COLECCION[constructor]:
                return f"metodo:{objeto}:{nodo.campo}"
            disponibles = ", ".join(
                ["size"] + sorted(METODOS_COLECCION[constructor])
            )
            raise self._error(
                f"{constructor} no tiene '{nodo.campo}'. Tiene: {disponibles}", nodo
            )

        # campo de un inventory
        if objeto in self.registros:
            for tipo, campo in self.registros[objeto]:
                if campo == nodo.campo:
                    return tipo
            campos = ", ".join(c for _, c in self.registros[objeto])
            raise self._error(
                f"el inventory {objeto} no tiene el campo '{nodo.campo}'. Tiene: {campos}",
                nodo,
            )

        raise self._error(f"{objeto} no tiene campos a los que acceder", nodo)

    def spawn(self, nodo: arbol.Spawn) -> str:
        campos = self.registros.get(nodo.tipo)
        if campos is None:
            raise self._error(f"no existe un inventory llamado '{nodo.tipo}'", nodo)

        if len(nodo.argumentos) != len(campos):
            raise self._error(
                f"{nodo.tipo} tiene {len(campos)} campo(s) y se pasaron "
                f"{len(nodo.argumentos)} valor(es)",
                nodo,
            )

        for (esperado, campo), argumento in zip(campos, nodo.argumentos):
            obtenido = self.expresion(argumento)
            self._verificar_asignable(esperado, obtenido, argumento)

        return nodo.tipo

    def llamada(self, nodo: arbol.Llamada) -> str:
        # si es un metodo del vector, el Acceso ya dejo la marca
        if isinstance(nodo.destino, arbol.Acceso):
            marca = self.expresion(nodo.destino)
            if isinstance(marca, str) and marca.startswith("metodo:"):
                _, coleccion, metodo = marca.split(":", 2)
                return self.metodo_coleccion(nodo, coleccion, metodo)

        tipos = [self.expresion(a) for a in nodo.argumentos]

        # solo puedo revisar los combo nuestros. chat.send y esos todavia no
        # tienen firma declarada -> bloque 6
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
                continue                             # score -> aim, permitido
            raise self._error(
                f"el parametro '{nombre}' de '{firma.nombre}' es {esperado} y se "
                f"paso un {obtenido}",
                argumento,
            )

        if firma.retorno is None:
            return "noloot"
        return firma.retorno

    def metodo_coleccion(self, nodo: arbol.Llamada, coleccion: str, metodo: str) -> str:
        constructor = constructor_de(coleccion)
        interno = tipo_interno(coleccion)
        cantidad, tipo_arg, retorno = METODOS_COLECCION[constructor][metodo]   # type: ignore[index]

        if len(nodo.argumentos) != cantidad:
            raise self._error(
                f"'{metodo}' de {constructor} espera {cantidad} argumento(s) y "
                f"recibio {len(nodo.argumentos)}",
                nodo,
            )

        if cantidad:
            esperado = interno if tipo_arg == "T" else tipo_arg
            obtenido = self.expresion(nodo.argumentos[0])
            self._verificar_asignable(esperado, obtenido, nodo.argumentos[0])   # type: ignore[arg-type]

        if retorno is None:
            return "noloot"
        return interno if retorno == "T" else retorno       # type: ignore[return-value]

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
            # gain suma numeros, pero si un lado es texto los pega
            if izq == "lore" or der == "lore":
                return "lore"
            return self._numerica(izq, der, op, nodo)

        if op == "split":
            # split siempre da decimal, aunque dividas dos enteros
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
