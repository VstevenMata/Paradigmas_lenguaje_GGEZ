"""ggezc play: consola interactiva. Cada entrada se ejecuta y muestra su salida o su error.

Las sentencias se envuelven en un lobby.launch por debajo. Memoria sin tocar el evaluador:
los combo/guild/inventory/travel que compilan se agregan antes de las entradas siguientes, y las
sentencias que no usan chat.* (stash, set, buff...) se repiten en silencio antes de cada entrada.
"""

from __future__ import annotations

import os
import re
import sys
import tempfile
import threading
import time
from pathlib import Path

from . import semantico
from .cargador import cargar
from .errores import ErrorGgez, posible_causa
from .evaluador import Evaluador
from .lexer import tokenizar
from .tokens import RESERVADAS, TipoToken

PASOS = 5_000_000
ENVOLTORIO = "lobby.launch{start THE GAME} glhf"
DECLARACION = re.compile(r"^\s*(combo|guild|inventory|questline|travel)\b\s*(\w+)?")
UBICACION = re.compile(r"^(.*?):(\d+):(\d+): (.*)$", re.S)

E = "\x1b["
R = E + "0m"
ACENTO, ROJO, AMARILLO, VERDE = E + "38;2;0;175;175m", E + "38;2;255;95;95m", E + "38;2;230;200;60m", E + "38;2;90;200;120m"
TENUE, NEGRITA = E + "2m", E + "1m"

# Fantasma en medios bloques sobre el fondo de la terminal: W cuerpo, E ojo, C mejilla, . transparente
FANTASMA = [
    "....WWWW....", "..WWWWWWWW..", ".WWWWWWWWWW.", ".WWEEWWEEWW.", ".WWEEWWEEWW.",
    ".WWWWWWWWWW.", ".WCWWWWWWCW.", ".WWWWWWWWWW.", ".WW.WWWW.WW.", "............",
]
PX = {"W": "255;255;255", "E": "0;0;128", "C": "0;175;175"}


def _visible(texto: str) -> int:
    return len(re.sub(r"\x1b\[[0-9;]*m", "", texto))


def _celda(arriba: str, abajo: str) -> str:
    if arriba == abajo == ".":
        return " "
    if arriba == ".":
        return f"{E}38;2;{PX[abajo]}m▄{R}"
    if abajo == "." or arriba == abajo:
        return f"{E}38;2;{PX[arriba]}m" + ("▀" if abajo == "." else "█") + R
    return f"{E}38;2;{PX[arriba]}m{E}48;2;{PX[abajo]}m▀{R}"


def _banner() -> str:
    fantasma = ["".join(_celda(a, b) for a, b in zip(FANTASMA[r], FANTASMA[r + 1]))
                for r in range(0, len(FANTASMA), 2)]
    texto = [
        f"{NEGRITA}GGEZ{R} {TENUE}consola{R}",
        f"{TENUE}Un lenguaje con alma de videojuego{R}",
        "",
        "Escribe código y presiona Enter.",
        f"{TENUE}/ayuda · /limpiar · /salir{R}",
    ]
    filas = [f" {f}  {t}" for f, t in zip(fantasma, texto)]
    ancho = max(_visible(f) for f in filas) + 2
    cuerpo = [f"{ACENTO}│{R}{f}{' ' * (ancho - _visible(f))}{ACENTO}│{R}" for f in filas]
    return "\n".join([f"{ACENTO}╭{'─' * ancho}╮{R}", *cuerpo, f"{ACENTO}╰{'─' * ancho}╯{R}"])


AYUDA = f"""
{NEGRITA}Cómo se usa{R}
  Escribe una sentencia y presiona Enter; no hace falta lobby.launch.
  Un bloque (farm, scan, combo…) se ejecuta al cerrar su último gg.
  Las variables, combo, guild e inventory se recuerdan entre entradas.
  {TENUE}Lo que declara una línea con chat.* (como chat.load) no se recuerda.{R}

{NEGRITA}Comandos{R}
  /ayuda     esta ayuda
  /limpiar   olvida todo y limpia la pantalla
  /salir     cierra la consola {TENUE}(también Ctrl+D){R}
  {TENUE}Ctrl+C     descarta lo que estás escribiendo{R}
"""


class _Contador:
    """Deja pasar la salida y anota si el programa imprimio algo."""

    def __init__(self, destino) -> None:
        self.destino, self.escribio = destino, False

    def write(self, texto: str) -> int:
        self.escribio = self.escribio or bool(texto)
        return self.destino.write(texto)

    def flush(self) -> None:
        self.destino.flush()


def _bloques_abiertos(texto: str) -> int:
    """glhf abiertos sin su gg; si el lexer falla se devuelve 0 para que el error se muestre."""
    try:
        tokens = tokenizar(texto, "<consola>")
    except ErrorGgez:
        return 0
    cuenta = 0
    for token in tokens:
        if token.tipo is TipoToken.RESERVADA and token.lexema in ("glhf", "gg"):
            cuenta += 1 if token.lexema == "glhf" else -1
    return cuenta


class Consola:
    def __init__(self) -> None:
        self.declaraciones: dict[str, str] = {}   # clave (tipo nombre) -> codigo
        self.memoria: list[str] = []              # sentencias sin chat.* que ya corrieron bien
        self.carpeta = Path(tempfile.mkdtemp(prefix="ggez-consola-"))

    def armar(self, codigo: str) -> tuple[str, int, int, str | None]:
        """Devuelve (fuente, lineas antes de lo escrito, columnas agregadas, clave si es declaracion)."""
        previas = "".join(c + "\n\n" for c in self.declaraciones.values())
        antes = previas.count("\n")
        if "lobby.launch" in codigo:
            return codigo + "\n", 0, 0, None
        if decl := DECLARACION.match(codigo):
            clave = f"{decl[1]} {decl[2] or codigo.strip()}"
            return f"{previas}{codigo}\n\n{ENVOLTORIO}\ngg\n", antes, 0, clave
        cuerpo = "\n".join(self.memoria + [codigo])
        sangrado = "\n".join("    " + linea for linea in cuerpo.splitlines())
        repetidas = sum(len(m.splitlines()) for m in self.memoria)
        return f"{previas}{ENVOLTORIO}\n{sangrado}\ngg\n", antes + 1 + repetidas, 4, None

    def ejecutar(self, codigo: str) -> None:
        fuente, dlinea, dcolumna, clave = self.armar(codigo)
        ruta = self.carpeta / "consola.ggez"
        ruta.write_text(fuente, encoding="utf-8")

        def ubicar(linea: int, columna: int) -> str:
            linea -= dlinea
            columna = columna - dcolumna if columna > dcolumna else columna
            return f"línea {max(linea, 1)}, columna {columna}"

        salida = _Contador(sys.stdout)
        error: list[BaseException] = []

        def correr() -> None:
            sys.stdout = salida
            try:
                archivos = cargar(ruta)
                for aviso in semantico.analizar_archivos(archivos):
                    m = UBICACION.match(str(aviso))
                    # En consola "nunca se usa/llama" es ruido, y lo de entradas anteriores ya se mostro
                    if not m or "nunca se" in m[4] or int(m[2]) <= dlinea:
                        continue
                    print(f"{AMARILLO}⚠ {ubicar(int(m[2]), int(m[3]))}: {m[4]}{R}", file=sys.stderr)
                Evaluador("consola.ggez", limite_pasos=PASOS).ejecutar_archivos(archivos)
            except (ErrorGgez, RecursionError) as err:
                error.append(err)
            finally:
                sys.stdout = salida.destino

        # Hilo con pila grande, igual que ggezc run, para la recursion profunda
        hilo = threading.Thread(target=correr)
        hilo.start()
        hilo.join()

        if error:
            err = error[0]
            if isinstance(err, RecursionError):
                print(f"{ROJO}✖ el programa se anidó demasiado para ejecutarse{R}")
                return
            print(f"{ROJO}✖ {err.etapa} · {ubicar(err.linea, err.columna)}{R}\n  {err.mensaje}")
            print(f"  {TENUE}{posible_causa(err.etapa, err.mensaje)}{R}")
            return
        if clave:
            self.declaraciones[clave] = codigo
            print(f"{VERDE}✔{R} {TENUE}{clave} guardado{R}")
            return
        if "lobby.launch" not in codigo and "chat." not in codigo:
            self.memoria.append(codigo)
        if not salida.escribio:
            print(f"{VERDE}✔{R} {TENUE}listo{R}")


def _preparar_terminal(prompt: str) -> str:
    """Historial y Tab con readline cuando existe; en Windows input() ya trae historial con flechas."""
    if os.name == "nt":
        os.system("")   # activa los colores ANSI en la consola de Windows
    try:
        import readline
    except ImportError:
        return prompt
    palabras = sorted(RESERVADAS | {"chat.send", "chat.load", "chat.rage", "physics", "rng", "clock", "save"})

    def completar(texto: str, estado: int) -> str | None:
        opciones = [p for p in palabras if p.startswith(texto)]
        return opciones[estado] if estado < len(opciones) else None

    readline.set_completer(completar)
    readline.set_completer_delims(" \t\n{}<>[]|")
    readline.parse_and_bind("tab: complete")
    # Lo pegado llega entero y espera Enter (GNU readline; libedit lo ignora y queda _hay_mas)
    readline.parse_and_bind("set enable-bracketed-paste on")
    # \001 \002: readline no cuenta los colores como ancho del prompt
    return re.sub(r"(\x1b\[[0-9;]*m)", "\001\\1\002", prompt)


def _hay_mas() -> bool:
    """Si queda texto esperando justo despues de leer una linea, es que se esta pegando."""
    if not sys.stdin.isatty():   # entrada por tuberia: cada linea va sola
        return False
    if os.name == "nt":
        import msvcrt
        return msvcrt.kbhit()
    import select
    return bool(select.select([sys.stdin], [], [], 0)[0])


def iniciar() -> int:
    primero = _preparar_terminal(f"{ACENTO}{NEGRITA}›{R} ")
    seguir = _preparar_terminal(f"{TENUE}·{R} ")
    print(_banner() + "\n")
    consola = Consola()
    buffer: list[str] = []
    pegado = False
    while True:
        try:
            inicio = time.monotonic()
            linea = input(seguir if buffer else primero)
            rapido = time.monotonic() - inicio < 0.05
            linea = linea.replace("\x1b[200~", "").replace("\x1b[201~", "")
        except EOFError:
            print()
            return 0
        except KeyboardInterrupt:
            print(f"\n{TENUE}(descartado){R}" if buffer else "")
            buffer, pegado = [], False
            continue

        # Pegado sin bracketed paste: se junta todo y se ejecuta recien con el Enter de la persona.
        # Una linea que llega en menos de 50 ms ya estaba en la entrada (la ultima de lo pegado)
        if _hay_mas() or (pegado and rapido):
            buffer.append(linea)
            pegado = True
            continue
        if pegado:
            if linea.strip():
                buffer.append(linea)
            codigo, buffer, pegado = "\n".join(buffer), [], False
            if codigo.strip():
                _correr(consola, codigo)
            continue

        if not buffer:
            comando = linea.strip().lower().lstrip("/")
            if comando in ("salir", "exit", "quit"):
                return 0
            if comando in ("ayuda", "help"):
                print(AYUDA)
                continue
            if comando in ("limpiar", "clear"):
                consola.declaraciones.clear()
                consola.memoria.clear()
                print("\x1b[2J\x1b[H" + _banner() + "\n")
                continue
            if not comando:
                continue

        buffer.append(linea)
        codigo = "\n".join(buffer)
        if _bloques_abiertos(codigo) > 0:
            continue
        buffer = []
        _correr(consola, codigo)


def _correr(consola: Consola, codigo: str) -> None:
    try:
        consola.ejecutar(codigo)
    except KeyboardInterrupt:
        print(f"\n{TENUE}(interrumpido){R}")
