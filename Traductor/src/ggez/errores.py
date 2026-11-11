"""Errores como archivo.ggez:LINEA:COLUMNA: etapa: mensaje. El panel Problemas del IDE parsea este formato."""

from __future__ import annotations


class ErrorGgez(Exception):
    """Error con linea y columna. Cada subclase solo cambia la etapa."""

    etapa = "error"

    def __init__(self, mensaje: str, linea: int, columna: int, archivo: str = "") -> None:
        super().__init__(mensaje)
        self.mensaje = mensaje
        self.linea = linea
        self.columna = columna
        self.archivo = archivo

    def formatear(self, archivo: str | None = None) -> str:
        nombre = archivo or self.archivo or "<entrada>"
        return (f"{nombre}:{self.linea}:{self.columna}: {self.etapa}: {self.mensaje}\n"
                f"    posible causa: {posible_causa(self.etapa, self.mensaje)}")


# (fragmento del mensaje, causa); gana el primero que aparezca en el mensaje
CAUSAS = [
    ("sin cerrar: falta 'gg'", "Cada glhf necesita su gg. Revise que no se haya borrado un gg al final de un bloque."),
    ("cadena sin cerrar", "Falta la comilla doble que cierra el texto, en esta misma linea."),
    ("comentario de bloque", "Un /all necesita su /end."),
    ("parentesis", "GGEZ usa { } para agrupar y llamar, no ( )."),
    ("';'", "En GGEZ cada sentencia termina con el salto de linea; borre el ';'."),
    ("tildes", "Los nombres solo usan letras sin tilde, digitos y _."),
    ("empezar con un digito", "Un nombre debe empezar con una letra o _."),
    ("caracter inesperado", "Ese simbolo no existe en GGEZ; revise si quiso escribir una palabra como gain, over o matches."),
    ("comillas dobles", "Las comillas simples son solo para un pixel ('a'); el texto va entre comillas dobles."),
    ("no esta declarada", "Declarela con stash antes de usarla, o revise si escribio mal el nombre o si quedo en otro bloque."),
    ("ya fue declarada", "Para cambiar su valor use set sin stash."),
    ("no permite taparla", "Use otro nombre: GGEZ no deja repetir el nombre de una variable de un bloque exterior."),
    ("argumento(s)", "Revise la firma del combo: la cantidad de valores entre { } debe ser igual a la de parametros."),
    ("se paso un", "Pase un valor del tipo que declara el parametro."),
    ("punto de entrada", "Agregue lobby.launch{start THE GAME} glhf ... gg en el archivo principal."),
    ("solo puede haber un lobby.launch", "Deje un solo lobby.launch; las demas acciones van en combos."),
    ("biblioteca", "Quite el lobby.launch del archivo importado o importe otro archivo."),
    ("circulo", "Elimine uno de los travel del ciclo para que ningun archivo dependa de si mismo."),
    ("no existe", "Revise el nombre del archivo o tipo y la carpeta indicada en el from."),
    ("no tiene '", "Revise el nombre: el mensaje lista lo que si existe."),
    ("reward", "Todo camino del combo debe terminar en reward, o declare drops noloot."),
    ("hardcoded", "Una constante no cambia; use stash si necesita modificarla."),
    ("no entra en esta version", "Esa palabra esta reservada para una version futura de GGEZ; use otra construccion."),
    ("se obtuvo", "Los tipos no coinciden: use un valor del tipo declarado (score, aim, lore, win, pixel...)."),
    ("se esperaba", "Revise el orden de las palabras en esta linea comparandolo con la sintaxis en Ayuda."),
    ("solo vale dentro", "Muevala al lugar donde tiene sentido (ciclo, combo o guild)."),
    ("entre cero", "Verifique que el divisor no sea 0 antes de dividir (scan divisor mismatch 0)."),
    ("fuera de rango", "Los indices van de 0 a .size gain -1; revise el limite del ciclo."),
    ("recursion", "Agregue un caso base que haga reward sin volver a llamarse."),
    ("sin mana", "Probablemente un ciclo nunca termina; revise que la condicion llegue a ser miss."),
    ("afk", "El valor esta vacio (afk); creelo con spawn antes de usarlo."),
]

CAUSA_POR_ETAPA = {
    "error lexico": "Hay un caracter o simbolo que GGEZ no reconoce en esta linea.",
    "error sintactico": "La linea no sigue la forma de GGEZ: revise glhf/gg, llaves { } y separadores |.",
    "error semantico": "La linea esta bien escrita pero no tiene sentido: revise tipos, nombres y declaraciones.",
    "error en ejecucion": "El programa fallo mientras corria: revise los valores que llegan a esta linea.",
}


def posible_causa(etapa: str, mensaje: str) -> str:
    for fragmento, causa in CAUSAS:
        if fragmento in mensaje:
            return causa
    return CAUSA_POR_ETAPA.get(etapa, "Revise la linea indicada.")


class ErrorLexico(ErrorGgez):
    etapa = "error lexico"


class ErrorSintactico(ErrorGgez):
    etapa = "error sintactico"


class ErrorSemantico(ErrorGgez):
    etapa = "error semantico"


# Hereda de semantico: el cargador y el semantico la lanzan donde antes iba un ErrorSemantico
class ErrorImportacion(ErrorSemantico):
    etapa = "error de importacion"


class ErrorEjecucion(ErrorGgez):
    etapa = "error en ejecucion"


# No hereda de ErrorEjecucion para que revive no lo atrape: un ciclo sin fin no se rescata
class ErrorLimite(ErrorGgez):
    etapa = "error en ejecucion"
