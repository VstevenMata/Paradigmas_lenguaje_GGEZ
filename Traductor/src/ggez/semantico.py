"""Paso 3: analisis semantico con tabla de simbolos (tipos, alcances, herencia)."""

from __future__ import annotations

from dataclasses import dataclass

from . import arbol
from .errores import ErrorImportacion, ErrorSemantico
from .tokens import TIPOS_SIMPLES

# score se promueve a aim solo; al reves no, se perderian decimales
NUMERICOS = ("score", "aim")

# (cantidad de args, tipo del arg, retorno); "T" = tipo interno, None = no devuelve
METODOS_COLECCION: dict[str, dict[str, tuple[int, str | None, str | None]]] = {
    "squad": {
        "recruit": (1, "T", None),
        "kick":    (1, "score", None),
    },
}


# Con espacios a proposito: no se puede escribir en un programa y no choca con un inventory
FALLA = "error de revive"
CAMPOS_FALLA = {"reason": "lore", "line": "score"}


def tipo_interno(tipo: str) -> str | None:
    for constructor in METODOS_COLECCION:
        if tipo.startswith(constructor + "[") and tipo.endswith("]"):
            return tipo[len(constructor) + 1:-1]
    return None


def constructor_de(tipo: str) -> str | None:
    for constructor in METODOS_COLECCION:
        if tipo.startswith(constructor + "["):
            return constructor
    return None


# De mas cerrado a mas abierto
APERTURA = {"solo": 0, "party": 1, "open": 2}


@dataclass
class Simbolo:
    nombre: str
    tipo: str
    constante: bool
    linea: int
    # Un local SI puede tapar un miembro -> D-25
    miembro: bool = False
    dueno: str | None = None
    modificador: str | None = None
    # Solo las variables con stash arrancan en False: parametros, me y el error de revive no avisan
    leido: bool = True


@dataclass
class Firma:
    nombre: str
    parametros: list[tuple[str, str]]
    retorno: str | None
    linea: int
    modificador: str | None = None


@dataclass
class Campo:
    tipo: str
    modificador: str
    linea: int


@dataclass
class InfoGuild:
    nombre: str
    campos: dict[str, Campo]
    metodos: dict[str, Firma]
    constructor: Firma | None
    linea: int
    padre: str | None = None

# Sentencias tras las que el resto del bloque no corre
SALIDAS = {arbol.Reward: "reward", arbol.Ragequit: "ragequit", arbol.Respawn: "respawn", arbol.Wipe: "wipe"}


class Alcance:

    def __init__(self, padre: "Alcance | None" = None) -> None:
        self.simbolos: dict[str, Simbolo] = {}
        self.padre = padre

    def declarar(self, simbolo: Simbolo) -> None:
        self.simbolos[simbolo.nombre] = simbolo

    def buscar(self, nombre: str) -> Simbolo | None:
        alcance: Alcance | None = self
        while alcance is not None:
            if nombre in alcance.simbolos:
                return alcance.simbolos[nombre]
            alcance = alcance.padre
        return None


class Semantico:
    MODULOS = {"chat"}

    def __init__(
        self,
        archivo: str = "<entrada>",
        registros: dict[str, list[tuple[str, str]]] | None = None,
        guilds: dict[str, "InfoGuild"] | None = None,
        modulo: str | None = None,
        importa: dict[str, "Semantico"] | None = None,
    ) -> None:
        self.archivo = archivo
        self.global_ = Alcance()
        self.alcance = self.global_
        # Copia: cada archivo tiene sus propios travel
        self.modulos = set(self.MODULOS)
        self.modulo = modulo
        self.importa = importa or {}
        self.puntos_entrada = 0
        self.profundidad_ciclo = 0
        self.combos: dict[str, Firma] = {}
        # Compartido entre archivos: utilidades.Jugador es uno solo
        self.registros = registros if registros is not None else {}
        self.guilds = guilds if guilds is not None else {}
        self.combo_actual: Firma | None = None
        self.guild_actual: str | None = None
        self.advertencias: list[str] = []
        self.llamados: set[str] = set()

    def _advertir(self, mensaje: str, linea: int, columna: int = 1) -> None:
        self.advertencias.append(f"{self.archivo}:{linea}:{columna}: advertencia: {mensaje}")

    def _error(self, mensaje: str, nodo: arbol.Nodo) -> ErrorSemantico:
        return ErrorSemantico(mensaje, nodo.linea, nodo.columna, self.archivo)

    def analizar(self, programa: arbol.Programa, principal: bool = True) -> arbol.Programa:
        # Dos vueltas: primero se registran los combos para poder llamar a uno escrito mas abajo
        for declaracion in programa.declaraciones:
            if isinstance(declaracion, arbol.Travel):
                self.registrar_travel(declaracion)
        for declaracion in programa.declaraciones:
            if isinstance(declaracion, arbol.DeclInventory):
                self.registrar_inventory(declaracion)
        for declaracion in programa.declaraciones:
            if isinstance(declaracion, arbol.DeclGuild):
                self.registrar_guild(declaracion)
        # Herencia despues de registrar todo (el padre puede estar mas abajo); primero los ciclos
        guilds = [d for d in programa.declaraciones if isinstance(d, arbol.DeclGuild)]
        for declaracion in guilds:
            self.validar_padre(declaracion)
        for declaracion in guilds:
            self.validar_herencia(declaracion)
        for declaracion in programa.declaraciones:
            if isinstance(declaracion, arbol.DeclCombo):
                self.registrar_combo(declaracion)

        for declaracion in programa.declaraciones:
            self.declaracion(declaracion)

        if principal:
            # Solo en el principal: un archivo importado puede tener combos que este programa no usa
            for d in programa.declaraciones:
                if isinstance(d, arbol.DeclCombo) and d.nombre not in self.llamados:
                    self._advertir(f"el combo '{d.nombre}' nunca se llama", d.linea, d.columna)

        if principal and self.puntos_entrada == 0:
            raise ErrorSemantico(
                "el programa no tiene punto de entrada: falta lobby.launch{start THE GAME}",
                1, 1, self.archivo,
            )
        return programa

    def registrar_travel(self, nodo: arbol.Travel) -> None:
        if nodo.modulo in self.importa:
            self.global_.declarar(Simbolo(nodo.modulo, f"archivo:{nodo.modulo}", True, nodo.linea))
        else:
            self.modulos.add(nodo.modulo)

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
        if nodo.nombre in self.guilds:
            raise self._error(f"ya existe un guild llamado '{nodo.nombre}'", nodo)
        if nodo.nombre in self.importa:
            raise self._error(f"'{nodo.nombre}' ya es el nombre de un modulo importado", nodo)

        self.combos[nodo.nombre] = self._firma(nodo)
        self.global_.declarar(Simbolo(nodo.nombre, "combo", True, nodo.linea))

    def _firma(self, nodo: arbol.DeclCombo) -> Firma:
        vistos: set[str] = set()
        for parametro in nodo.parametros:
            if parametro.nombre in vistos:
                raise self._error(
                    f"el parametro '{parametro.nombre}' esta repetido", parametro
                )
            vistos.add(parametro.nombre)

        return Firma(
            nodo.nombre,
            [(str(p.tipo), p.nombre) for p in nodo.parametros],
            str(nodo.retorno) if nodo.retorno is not None else None,
            nodo.linea,
            nodo.modificador or "open",
        )

    def registrar_guild(self, nodo: arbol.DeclGuild) -> None:
        if nodo.nombre in self.guilds or nodo.nombre in self.registros:
            raise self._error(f"ya existe un tipo llamado '{nodo.nombre}'", nodo)

        campos: dict[str, Campo] = {}
        for campo in nodo.campos:
            if campo.nombre in campos:
                raise self._error(f"el campo '{campo.nombre}' esta repetido", campo)
            campos[campo.nombre] = Campo(str(campo.tipo), campo.modificador, campo.linea)

        metodos: dict[str, Firma] = {}
        constructor: Firma | None = None
        for metodo in nodo.metodos:
            if metodo.nombre == nodo.nombre:

                if constructor is not None:
                    raise self._error(f"el guild '{nodo.nombre}' ya tiene constructor", metodo)
                if metodo.retorno is not None:
                    raise self._error(
                        f"el constructor de '{nodo.nombre}' debe ser 'drops noloot'", metodo
                    )
                constructor = self._firma(metodo)
                continue
            if metodo.nombre in metodos:
                raise self._error(f"el metodo '{metodo.nombre}' esta repetido", metodo)
            if metodo.nombre in campos:
                raise self._error(
                    f"'{metodo.nombre}' ya es un campo de '{nodo.nombre}'", metodo
                )
            metodos[metodo.nombre] = self._firma(metodo)

        self.guilds[nodo.nombre] = InfoGuild(
            nodo.nombre, campos, metodos, constructor, nodo.linea, nodo.padre
        )

    def validar_padre(self, nodo: arbol.DeclGuild) -> None:
        if nodo.padre is None:
            return
        if nodo.padre in self.registros:
            raise self._error(
                f"'{nodo.padre}' es un inventory: solo se puede evolucionar de un guild", nodo
            )
        if nodo.padre not in self.guilds:
            raise self._error(f"no existe un guild llamado '{nodo.padre}'", nodo)
        self._verificar_tipo(nodo.padre, nodo)

        vistos = {nodo.nombre}
        actual: str | None = nodo.padre
        while actual is not None:
            if actual in vistos:
                raise self._error(
                    f"herencia en circulo: '{nodo.nombre}' termina evolucionando de si mismo",
                    nodo,
                )
            vistos.add(actual)
            actual = self.guilds[actual].padre

    def validar_herencia(self, nodo: arbol.DeclGuild) -> None:
        info = self.guilds[nodo.nombre]
        if info.padre is None:
            return

        for campo in nodo.campos:
            heredado = self._buscar_miembro(info.padre, campo.nombre)
            if heredado is not None:
                raise self._error(
                    f"'{campo.nombre}' ya existe en {heredado[0]}: un guild hijo no puede "
                    f"repetir lo que hereda",
                    campo,
                )

        for metodo in nodo.metodos:
            if metodo.nombre == nodo.nombre:
                continue
            heredado = self._buscar_miembro(info.padre, metodo.nombre)
            if heredado is None:
                continue
            dueno, original = heredado
            if isinstance(original, Campo):
                raise self._error(f"'{metodo.nombre}' ya es un campo de {dueno}", metodo)

            # Override: misma firma y no mas cerrado, como en Java
            nuevo = info.metodos[metodo.nombre]
            if nuevo.parametros != original.parametros or nuevo.retorno != original.retorno:
                raise self._error(
                    f"'{metodo.nombre}' reemplaza al de {dueno} y tiene que respetar su "
                    f"firma: {self._describir_firma(original)}",
                    metodo,
                )
            if APERTURA[nuevo.modificador] < APERTURA[original.modificador]:   # type: ignore[index]
                raise self._error(
                    f"'{metodo.nombre}' es {original.modificador} en {dueno}: al reemplazarlo "
                    f"no se puede cerrar mas",
                    metodo,
                )

    def _describir_firma(self, firma: Firma) -> str:
        parametros = " | ".join(f"{t} {n}" for t, n in firma.parametros)
        return f"{{{parametros}}} drops {firma.retorno or 'noloot'}"

    def _buscar_miembro(self, guild: str | None, nombre: str) -> tuple[str, Campo | Firma] | None:
        while guild is not None:
            info = self.guilds[guild]
            if nombre in info.campos:
                return guild, info.campos[nombre]
            if nombre in info.metodos:
                return guild, info.metodos[nombre]
            guild = info.padre
        return None

    def _linaje(self, guild: str) -> list[str]:
        cadena: list[str] = []
        actual: str | None = guild
        while actual is not None:
            cadena.append(actual)
            actual = self.guilds[actual].padre
        return cadena[::-1]

    def decl_combo(self, nodo: arbol.DeclCombo) -> None:
        self._cuerpo_combo(nodo, self.combos[nodo.nombre], self.global_)

    def _cuerpo_combo(
        self, nodo: arbol.DeclCombo, firma: Firma, base: Alcance, guild: str | None = None
    ) -> None:
        # OJO: arranca desde el alcance global, no desde donde se llama
        anterior, ciclos, combo_previo = self.alcance, self.profundidad_ciclo, self.combo_actual
        self.alcance = Alcance(base)
        self.profundidad_ciclo = 0
        self.combo_actual = firma

        # Aca y no al registrar: recien ahora estan anotados todos los guild
        if nodo.retorno is not None:
            self._verificar_tipo(str(nodo.retorno), nodo.retorno)
        for parametro in nodo.parametros:
            self._verificar_tipo(str(parametro.tipo), parametro.tipo)

        try:
            if guild is not None:
                self.alcance.declarar(Simbolo("me", guild, True, nodo.linea))
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
        """True si no hay salida sin reward. Un scan cuenta solo con fallback; los ciclos nunca."""
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

            if isinstance(sentencia, arbol.Clutch):
                if self._siempre_devuelve(sentencia.intento) \
                        and self._siempre_devuelve(sentencia.atrapa):
                    return True
                if sentencia.final is not None and self._siempre_devuelve(sentencia.final):
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

        self._verificar_asignable(esperado, self.expresion(nodo.valor), nodo, nodo.valor)

    def declaracion(self, nodo: arbol.Nodo) -> None:
        if isinstance(nodo, arbol.Travel):
            return

        if isinstance(nodo, arbol.DeclCombo):
            self.decl_combo(nodo)
            return

        if isinstance(nodo, arbol.DeclGuild):
            self.decl_guild(nodo)
            return

        if isinstance(nodo, arbol.DeclInventory):
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

    def decl_guild(self, nodo: arbol.DeclGuild) -> None:
        anterior = self.guild_actual
        self.guild_actual = nodo.nombre
        try:
            self._miembros_guild(nodo)
        finally:
            self.guild_actual = anterior

    def _miembros_guild(self, nodo: arbol.DeclGuild) -> None:
        info = self.guilds[nodo.nombre]

        # El valor inicial de un campo se evalua afuera de todo: ahi no hay me
        for campo in nodo.campos:
            self._verificar_tipo(str(campo.tipo), campo.tipo)
            if campo.valor is not None:
                obtenido = self.expresion(campo.valor)
                self._verificar_asignable(str(campo.tipo), obtenido, campo, campo.valor)

        # Orden de busqueda: locales -> miembros del guild (heredados incluidos) -> globales
        miembros = Alcance(self.global_)
        for guild in self._linaje(info.nombre):
            actual = self.guilds[guild]
            for nombre, campo in actual.campos.items():
                miembros.declarar(Simbolo(
                    nombre, campo.tipo, False, campo.linea,
                    miembro=True, dueno=guild, modificador=campo.modificador,
                ))
            for nombre, firma in actual.metodos.items():
                miembros.declarar(Simbolo(
                    nombre, f"miembro:{guild}:{nombre}", True, firma.linea,
                    miembro=True, dueno=guild, modificador=firma.modificador,
                ))

        for metodo in nodo.metodos:
            if metodo.nombre == nodo.nombre:
                firma = info.constructor
            else:
                firma = info.metodos[metodo.nombre]
            self._cuerpo_combo(metodo, firma, miembros, info.nombre)   # type: ignore[arg-type]

    def bloque(self, nodo: arbol.Bloque) -> None:
        self.alcance = Alcance(self.alcance)
        salida = None
        for sentencia in nodo.sentencias:
            if salida is not None:
                self._advertir(f"este codigo nunca se ejecuta: esta despues de un '{salida}'",
                               sentencia.linea, sentencia.columna)
                salida = ""                         # un solo aviso por bloque
            self.sentencia(sentencia)
            if salida is None and type(sentencia) in SALIDAS:
                salida = SALIDAS[type(sentencia)]
        for simbolo in self.alcance.simbolos.values():
            if not simbolo.leido:
                self._advertir(f"la variable '{simbolo.nombre}' se declaro pero su valor nunca se usa",
                               simbolo.linea)
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
        elif isinstance(nodo, arbol.Clutch):
            self.sent_clutch(nodo)
        elif isinstance(nodo, arbol.Wipe):
            self.sent_wipe(nodo)
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
        # El farm vive en el alcance que lo contiene: 'farm i set 0' reasigna una variable ya declarada
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

    def sent_clutch(self, nodo: arbol.Clutch) -> None:
        self.bloque(nodo.intento)

        previo = self.alcance.buscar(nodo.variable)
        if previo is not None:
            raise self._error(
                f"'{nodo.variable}' ya existe (linea {previo.linea}); el revive "
                f"necesita un nombre nuevo para el error",
                nodo,
            )

        self.alcance = Alcance(self.alcance)
        self.alcance.declarar(Simbolo(nodo.variable, FALLA, True, nodo.linea))
        try:
            self.bloque(nodo.atrapa)
        finally:
            self.alcance = self.alcance.padre          # type: ignore[assignment]

        if nodo.final is not None:
            self.bloque(nodo.final)

    def sent_wipe(self, nodo: arbol.Wipe) -> None:
        tipo = self.expresion(nodo.motivo)
        if tipo not in ("lore", "desconocido"):
            raise self._error(f"'wipe' recibe un lore y se obtuvo {tipo}", nodo)

    def _verificar_no_falla(self, destino: arbol.Nodo, nodo: arbol.Nodo) -> None:
        if isinstance(destino, arbol.Identificador) and self.expresion(destino) == FALLA:
            raise self._error(f"'{destino.nombre}' es el error del revive y no se puede reasignar", nodo)
        if isinstance(destino, arbol.Acceso) and self.expresion(destino.objeto) == FALLA:
            raise self._error(
                f"'.{destino.campo}' del error es de solo lectura", nodo
            )

    def decl_variable(self, nodo: arbol.DeclVariable) -> None:
        nombre = nodo.nombre
        self._verificar_tipo(str(nodo.tipo), nodo.tipo)

        if nombre in self.alcance.simbolos:
            raise self._error(f"'{nombre}' ya fue declarada en este bloque", nodo)

        previo = self.alcance.buscar(nombre)
        if previo is not None and not previo.miembro:
            # Prohibido tapar un nombre de afuera -> D-8; los campos si (se llega con me.) -> D-25
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
            self._verificar_asignable(declarado, obtenido, nodo, nodo.valor)

        self.alcance.declarar(Simbolo(nombre, declarado, nodo.constante, nodo.linea, leido=False))

    def asignacion(self, nodo: arbol.Asignacion) -> None:
        # Escribir no cuenta como usar: se restaura la marca que pone expresion()
        previo = self.alcance.buscar(nodo.destino.nombre) if isinstance(nodo.destino, arbol.Identificador) else None
        leido = previo.leido if previo else True
        destino = self.expresion(nodo.destino)
        if previo:
            previo.leido = leido
        valor = self.expresion(nodo.valor)
        self._verificar_no_falla(nodo.destino, nodo)
        if isinstance(nodo.destino, arbol.Me):
            raise self._error("'me' no se puede reasignar", nodo)

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

        self._verificar_asignable(destino, valor, nodo, nodo.valor)

    def incremento(self, nodo: arbol.Incremento) -> None:
        tipo = self.expresion(nodo.destino)
        self._verificar_no_falla(nodo.destino, nodo)
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

    def _verificar_asignable(
        self, destino: str, origen: str, nodo: arbol.Nodo, valor: arbol.Nodo | None = None
    ) -> None:
        if destino == origen or origen == "desconocido":
            return
        # afk es el null de Java: vale para lore, pixel, colecciones y objetos, no para score, aim ni win
        if origen == "afk" and destino not in ("score", "aim", "win"):
            return
        if self._es_hijo(origen, destino):
            return
        if destino == "aim" and origen == "score":
            if valor is not None:
                valor.a_aim = True                  # el paso 4 lo pasa a decimal
            return

        # El literal <a | b> toma el tipo del que lo recibe
        if origen.startswith("coleccion["):
            interno_destino = tipo_interno(destino)
            if interno_destino is None:
                raise self._error(
                    f"se esperaba {destino} y se obtuvo un literal de coleccion", nodo
                )
            if origen == "coleccion[]":
                return
            interno_origen = origen[len("coleccion["):-1]
            if interno_origen != interno_destino and not self._es_hijo(interno_origen, interno_destino):
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

    def expresion(self, nodo: arbol.Nodo) -> str:
        if isinstance(nodo, arbol.Literal):
            return nodo.tipo

        if isinstance(nodo, arbol.Identificador):
            simbolo = self.alcance.buscar(nodo.nombre)
            if simbolo is None:
                if nodo.nombre in self.modulos:
                    return "modulo"
                raise self._error(f"'{nodo.nombre}' no esta declarada", nodo)
            simbolo.leido = True
            if simbolo.miembro:
                self._verificar_visible(
                    simbolo.dueno, f"'{nodo.nombre}'", simbolo.modificador, nodo   # type: ignore[arg-type]
                )
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
            # Solo se revisa que sean del mismo tipo; el tipo real lo pone la declaracion
            if not nodo.elementos:
                return "coleccion[]"
            tipos = [self.expresion(e) for e in nodo.elementos]
            primero = tipos[0]
            for tipo, elemento in zip(tipos[1:], nodo.elementos[1:]):
                if tipo == primero:
                    continue
                comun = self._ancestro_comun(primero, tipo)
                if comun is None:
                    raise self._error(
                        f"los elementos de la coleccion deben ser del mismo tipo: "
                        f"empezo con {primero} y aparecio un {tipo}",
                        elemento,
                    )
                primero = comun
            return f"coleccion[{primero}]"

        if isinstance(nodo, arbol.Me):
            simbolo = self.alcance.buscar("me")
            if simbolo is None:
                raise self._error("'me' solo vale dentro de un combo de un guild", nodo)
            return simbolo.tipo

        raise self._error("expresion no reconocida", nodo)

    def _tipo_existe(self, tipo: str) -> bool:
        if not self._modulo_visible(tipo):
            return False
        if tipo in TIPOS_SIMPLES or tipo in self.registros or tipo in self.guilds:
            return True
        interno = tipo_interno(tipo)
        return interno is not None and self._tipo_existe(interno)

    def _modulo_visible(self, tipo: str) -> bool:
        if "." not in tipo or tipo_interno(tipo) is not None:
            return True
        modulo = tipo.split(".", 1)[0]
        return modulo == self.modulo or modulo in self.importa

    def _verificar_tipo(self, tipo: str, nodo: arbol.Nodo) -> None:
        if self._tipo_existe(tipo):
            return
        while tipo_interno(tipo) is not None:
            tipo = tipo_interno(tipo)                       # type: ignore[assignment]
        if not self._modulo_visible(tipo):
            modulo = tipo.split(".", 1)[0]
            raise self._error(
                f"para usar {tipo} hay que traer el modulo a este archivo: travel {modulo} from ...",
                nodo,
            )
        raise self._error(f"no existe el tipo {tipo}", nodo)

    def acceso(self, nodo: arbol.Acceso) -> str:
        objeto = self.expresion(nodo.objeto)

        if objeto in ("modulo", "desconocido"):
            return "desconocido"

        if objeto.startswith("archivo:"):
            modulo = objeto.split(":", 1)[1]
            importado = self.importa[modulo]
            if nodo.campo in importado.combos:
                return f"externo:{modulo}:{nodo.campo}"
            if f"{modulo}.{nodo.campo}" in self.guilds or f"{modulo}.{nodo.campo}" in self.registros:
                raise self._error(
                    f"{modulo}.{nodo.campo} es un tipo: se crea con spawn {modulo}.{nodo.campo}{{...}}",
                    nodo,
                )
            disponibles = ", ".join(sorted(importado.combos)) or "ningun combo"
            raise ErrorImportacion(
                f"el modulo {modulo} no tiene '{nodo.campo}'. Tiene: {disponibles}",
                nodo.linea, nodo.columna, self.archivo,
            )

        if nodo.campo == "size" and constructor_de(objeto) is not None:
            return "score"

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

        if objeto == FALLA:
            if nodo.campo in CAMPOS_FALLA:
                return CAMPOS_FALLA[nodo.campo]
            raise self._error(
                f"el error del revive no tiene '{nodo.campo}'. Tiene: reason, line", nodo
            )

        if objeto in self.guilds:
            encontrado = self._buscar_miembro(objeto, nodo.campo)
            if encontrado is None:
                todos: set[str] = set()
                for guild in self._linaje(objeto):
                    todos.update(self.guilds[guild].campos, self.guilds[guild].metodos)
                disponibles = ", ".join(sorted(todos)) or "nada"
                raise self._error(
                    f"el guild {objeto} no tiene '{nodo.campo}'. Tiene: {disponibles}", nodo
                )
            dueno, miembro = encontrado
            if isinstance(miembro, Campo):
                self._verificar_visible(dueno, f"el campo '{nodo.campo}'", miembro.modificador, nodo)
                return miembro.tipo
            self._verificar_visible(dueno, f"el combo '{nodo.campo}'", miembro.modificador, nodo)
            return f"miembro:{dueno}:{nodo.campo}"

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

    def _verificar_visible(
        self, dueno: str, que: str, modificador: str | None, nodo: arbol.Nodo
    ) -> None:
        """private/protected/public como Java -> D-24: se mira desde el guild que usa, no el objeto."""
        if modificador in (None, "open"):
            return
        if self.guild_actual == dueno:
            return
        if modificador == "party" and self._evoluciona_de(self.guild_actual, dueno):
            return

        if modificador == "solo":
            raise self._error(
                f"{que} es solo (privado) en {dueno}: solo se puede usar dentro de {dueno}",
                nodo,
            )
        raise self._error(
            f"{que} es party (protegido) en {dueno}: solo se puede usar dentro de "
            f"{dueno} o de los guild que evolucionan de el",
            nodo,
        )

    def _ancestro_comun(self, a: str, b: str) -> str | None:
        if a not in self.guilds or b not in self.guilds:
            return None
        for guild in reversed(self._linaje(a)):
            if self._evoluciona_de(b, guild):
                return guild
        return None

    def _es_hijo(self, origen: str, destino: str) -> bool:
        return origen in self.guilds and destino in self.guilds and self._evoluciona_de(origen, destino)

    def _evoluciona_de(self, hijo: str | None, padre: str) -> bool:
        while hijo is not None:
            if hijo == padre:
                return True
            hijo = self.guilds[hijo].padre
        return False

    def spawn(self, nodo: arbol.Spawn) -> str:
        if nodo.tipo in self.guilds or nodo.tipo in self.registros:
            self._verificar_tipo(nodo.tipo, nodo)
        if nodo.tipo in self.guilds:
            info = self.guilds[nodo.tipo]
            if info.constructor is None:
                if nodo.argumentos:
                    raise self._error(
                        f"el guild {nodo.tipo} no tiene constructor, asi que el spawn "
                        f"va sin valores: spawn {nodo.tipo}{{}}",
                        nodo,
                    )
                return nodo.tipo
            self._verificar_visible(
                nodo.tipo, f"el constructor de '{nodo.tipo}'", info.constructor.modificador, nodo
            )
            self._verificar_argumentos(info.constructor, nodo, f"el constructor de '{nodo.tipo}'")
            return nodo.tipo

        campos = self.registros.get(nodo.tipo)
        if campos is None:
            raise self._error(f"no existe un guild ni un inventory llamado '{nodo.tipo}'", nodo)

        if len(nodo.argumentos) != len(campos):
            raise self._error(
                f"{nodo.tipo} tiene {len(campos)} campo(s) y se pasaron "
                f"{len(nodo.argumentos)} valor(es)",
                nodo,
            )

        for (esperado, campo), argumento in zip(campos, nodo.argumentos):
            obtenido = self.expresion(argumento)
            self._verificar_asignable(esperado, obtenido, argumento, argumento)

        return nodo.tipo

    def llamada(self, nodo: arbol.Llamada) -> str:
        marca = None
        if isinstance(nodo.destino, arbol.Acceso):
            marca = self.expresion(nodo.destino)
            if marca.startswith("metodo:"):
                _, coleccion, metodo = marca.split(":", 2)
                return self.metodo_coleccion(nodo, coleccion, metodo)
            if marca.startswith("externo:"):
                _, modulo, nombre = marca.split(":", 2)
                firma = self.importa[modulo].combos[nombre]
                try:
                    self._verificar_argumentos(firma, nodo, f"el combo '{modulo}.{nombre}'")
                except ErrorSemantico as err:
                    raise ErrorImportacion(err.mensaje, err.linea, err.columna, err.archivo) from None
                return firma.retorno if firma.retorno is not None else "noloot"

        if isinstance(nodo.destino, arbol.Identificador):
            simbolo = self.alcance.buscar(nodo.destino.nombre)
            if simbolo is not None and simbolo.tipo.startswith("miembro:"):
                self._verificar_visible(
                    simbolo.dueno, f"el combo '{simbolo.nombre}'", simbolo.modificador, nodo   # type: ignore[arg-type]
                )
                marca = simbolo.tipo

        if marca is not None and marca.startswith("miembro:"):
            _, guild, metodo = marca.split(":", 2)
            firma = self.guilds[guild].metodos[metodo]
            self._verificar_argumentos(firma, nodo, f"el combo '{metodo}'")
            return firma.retorno if firma.retorno is not None else "noloot"

        tipos = [self.expresion(a) for a in nodo.argumentos]

        # chat.* todavia no tiene firma declarada -> bloque 6
        if not isinstance(nodo.destino, arbol.Identificador):
            self.expresion(nodo.destino)
            return "desconocido"

        firma = self.combos.get(nodo.destino.nombre)
        if firma is None:
            self.expresion(nodo.destino)
            return "desconocido"

        self._verificar_argumentos(firma, nodo, f"el combo '{firma.nombre}'", tipos)
        if self.combo_actual is None or self.combo_actual.nombre != firma.nombre:
            self.llamados.add(firma.nombre)     # llamarse a si mismo no cuenta como uso
        if firma.retorno is None:
            return "noloot"
        return firma.retorno

    def _verificar_argumentos(
        self, firma: Firma, nodo: arbol.Llamada | arbol.Spawn, quien: str,
        tipos: list[str] | None = None,
    ) -> None:
        if tipos is None:
            tipos = [self.expresion(a) for a in nodo.argumentos]

        if len(tipos) != len(firma.parametros):
            raise self._error(
                f"{quien} espera {len(firma.parametros)} argumento(s) y recibio {len(tipos)}",
                nodo,
            )

        for (esperado, nombre), obtenido, argumento in zip(firma.parametros, tipos, nodo.argumentos):
            if obtenido == "desconocido" or obtenido == esperado:
                continue
            if self._es_hijo(obtenido, esperado):
                continue
            if obtenido.startswith("coleccion["):
                self._verificar_asignable(esperado, obtenido, argumento, argumento)
                continue
            if esperado == "aim" and obtenido == "score":
                argumento.a_aim = True
                continue
            raise self._error(
                f"el parametro '{nombre}' de '{firma.nombre}' es {esperado} y se paso un {obtenido}",
                argumento,
            )

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
            self._verificar_asignable(esperado, obtenido, nodo.argumentos[0], nodo.argumentos[0])   # type: ignore[arg-type]

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
            if izq == "lore" or der == "lore":
                return "lore"
            return self._numerica(izq, der, op, nodo)

        # split entero como en C y Java (7 split 2 -> 3); para decimal, uno tiene que ser aim -> D-23
        if op in ("drain", "crit", "split", "scrap"):
            return self._numerica(izq, der, op, nodo)

        raise self._error(f"operador desconocido: {op}", nodo)

    def _numerica(self, izq: str, der: str, op: str, nodo: arbol.Nodo) -> str:
        if izq not in NUMERICOS or der not in NUMERICOS:
            raise self._error(f"'{op}' espera score o aim, y se obtuvo {izq} y {der}", nodo)
        return "aim" if "aim" in (izq, der) else "score"


def analizar(programa: arbol.Programa, archivo: str = "<entrada>") -> arbol.Programa:
    return Semantico(archivo).analizar(programa)


def analizar_archivos(archivos: list) -> list[str]:
    """Revisa cada archivo con su tabla; vienen en orden, lo importado ya esta revisado -> D-28."""
    registros: dict[str, list[tuple[str, str]]] = {}
    guilds: dict[str, InfoGuild] = {}
    revisados: dict[int, Semantico] = {}
    for archivo in archivos:
        importa = {modulo: revisados[id(otro)] for modulo, otro in archivo.importa.items()}
        semantico = Semantico(archivo.nombre, registros, guilds, archivo.modulo, importa)
        semantico.analizar(archivo.programa, principal=archivo.modulo is None)
        revisados[id(archivo)] = semantico
    return [a for s in revisados.values() for a in s.advertencias]
