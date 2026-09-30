"""Nodos del arbol, uno por regla de GRAMATICA.md. Se llama arbol.py porque ast.py choca con Python."""

from __future__ import annotations

from dataclasses import dataclass, field


@dataclass
class Nodo:
    linea: int = field(default=0, kw_only=True)
    columna: int = field(default=0, kw_only=True)
    # Lo prende el paso 3 cuando un score va a un aim; si no, 3 split 2 daria 1
    a_aim: bool = field(default=False, kw_only=True)



@dataclass
class Tipo(Nodo):
    nombre: str                          # score, aim, lore, win, pixel, o un nombre propio
    interno: "Tipo | None" = None        # el T de squad[T]

    def __str__(self) -> str:
        return f"{self.nombre}[{self.interno}]" if self.interno else self.nombre



@dataclass
class Programa(Nodo):
    declaraciones: list[Nodo]


@dataclass
class Travel(Nodo):
    """travel modulo from paquete"""
    modulo: str
    paquete: str


@dataclass
class PuntoEntrada(Nodo):
    """lobby.launch{start THE GAME} glhf ... gg -- el punto de entrada"""
    cuerpo: "Bloque"


@dataclass
class Bloque(Nodo):
    """glhf ... gg. Cada bloque abre un alcance nuevo."""
    sentencias: list[Nodo]



@dataclass
class DeclVariable(Nodo):
    """stash tipo nombre [set valor]   /   hardcoded tipo nombre set valor"""
    tipo: Tipo
    nombre: str
    valor: Nodo | None = None
    constante: bool = False


@dataclass
class Asignacion(Nodo):
    """destino set|boost|sap valor"""
    destino: Nodo
    operador: str
    valor: Nodo


@dataclass
class Incremento(Nodo):
    """destino buff|nerf"""
    destino: Nodo
    operador: str


@dataclass
class SentenciaExpresion(Nodo):
    expresion: Nodo



@dataclass
class Scan(Nodo):
    """scan cond glhf ... gg [fallback glhf ... gg]; sino es Bloque o Scan (fallback scan)."""
    condicion: Nodo
    entonces: "Bloque"
    sino: "Bloque | Scan | None" = None


@dataclass
class Farm(Nodo):
    """farm inicio | cond | paso glhf ... gg"""
    inicio: Nodo
    condicion: Nodo
    paso: Nodo
    cuerpo: "Bloque"


@dataclass
class Survive(Nodo):
    """survive cond glhf ... gg. Chequea la condicion ANTES de cada vuelta."""
    condicion: Nodo
    cuerpo: "Bloque"


@dataclass
class Grind(Nodo):
    """grind glhf ... gg survive {cond}. Chequea al final: corre minimo una vez."""
    cuerpo: "Bloque"
    condicion: Nodo


@dataclass
class RamaPick(Nodo):
    """pick v1 | v2 glhf ... gg"""
    valores: list[Nodo]
    cuerpo: "Bloque"


@dataclass
class Loadout(Nodo):
    """loadout expr glhf ramas [noob] gg. Sin caida entre ramas."""
    expresion: Nodo
    ramas: list[RamaPick]
    defecto: "Bloque | None" = None


@dataclass
class Ragequit(Nodo):
    """ragequit: corta el ciclo."""


@dataclass
class Respawn(Nodo):
    """respawn: salta a la siguiente vuelta."""



@dataclass
class Clutch(Nodo):
    """clutch glhf ... gg revive {e} glhf ... gg [checkpoint glhf ... gg]"""
    intento: "Bloque"
    variable: str                        # la e de revive {e}
    atrapa: "Bloque"
    final: "Bloque | None" = None        # el checkpoint, corre siempre


@dataclass
class Wipe(Nodo):
    """wipe {motivo}: lanza un error que agarra el revive."""
    motivo: Nodo



@dataclass
class Parametro(Nodo):
    """Un parametro de combo, o un campo de inventory."""
    tipo: Tipo
    nombre: str


@dataclass
class DeclCombo(Nodo):
    """[open|party|solo] combo nombre{params} drops tipo glhf ... gg"""
    nombre: str
    parametros: list[Parametro]
    retorno: Tipo | None          # None = drops noloot
    cuerpo: "Bloque"
    modificador: str | None = None


@dataclass
class Reward(Nodo):
    """reward [expr]: corta el combo y devuelve."""
    valor: Nodo | None = None



@dataclass
class DeclInventory(Nodo):
    """inventory Nombre glhf  tipo campo  ... gg"""
    nombre: str
    campos: list[Parametro]


@dataclass
class Spawn(Nodo):
    """spawn Tipo{args}: crea un registro o un objeto de un guild."""
    tipo: str
    argumentos: list[Nodo]



@dataclass
class DeclCampo(Nodo):
    """open|party|solo tipo nombre [set valor], dentro de un guild"""
    modificador: str
    tipo: Tipo
    nombre: str
    valor: Nodo | None = None


@dataclass
class DeclGuild(Nodo):
    """guild Nombre [evolves Padre] glhf  campos y combos  gg"""
    nombre: str
    padre: str | None
    campos: list[DeclCampo]
    metodos: list[DeclCombo]             # el que se llama igual es el constructor



@dataclass
class Literal(Nodo):
    valor: object
    tipo: str                            # score, aim, lore, pixel, win, afk


@dataclass
class Identificador(Nodo):
    nombre: str


@dataclass
class Me(Nodo):
    """me: la instancia actual, dentro de un guild."""


@dataclass
class Binaria(Nodo):
    izquierda: Nodo
    operador: str
    derecha: Nodo


@dataclass
class Unaria(Nodo):
    operador: str                        # dodge (NOT) o drain (menos unario)
    operando: Nodo


@dataclass
class Acceso(Nodo):
    """objeto.campo"""
    objeto: Nodo
    campo: str


@dataclass
class Indice(Nodo):
    """objeto<indice>"""
    objeto: Nodo
    indice: Nodo


@dataclass
class Llamada(Nodo):
    """destino{args}"""
    destino: Nodo
    argumentos: list[Nodo]


@dataclass
class LiteralColeccion(Nodo):
    """<a | b | c>"""
    elementos: list[Nodo]
