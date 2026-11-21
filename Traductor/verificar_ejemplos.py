#!/usr/bin/env python3
"""Corre cada ejemplo y compara su salida con ejemplos/esperado/<nombre>.txt (lo usa el CI).

    python verificar_ejemplos.py               # verifica
    python verificar_ejemplos.py --actualizar  # reescribe lo esperado tras un cambio intencional (revisar el diff)
"""

import difflib
import subprocess
import sys
from pathlib import Path

AQUI = Path(__file__).parent
ESPERADO = AQUI / "ejemplos" / "esperado"
# Vitrina del resaltado: usa palabras que el traductor todavia no soporta
SALTAR = {"paleta_colores"}


def correr(ejemplo: Path) -> tuple[int, str]:
    r = subprocess.run([sys.executable, str(AQUI / "ggezc.py"), "run", str(ejemplo)],
                       stdin=subprocess.DEVNULL, capture_output=True, text=True, encoding="utf-8")
    return r.returncode, r.stdout + r.stderr


def main() -> int:
    actualizar = "--actualizar" in sys.argv
    ESPERADO.mkdir(exist_ok=True)
    fallos = 0
    for ejemplo in sorted((AQUI / "ejemplos").glob("*.ggez")):
        if ejemplo.stem in SALTAR:
            continue
        codigo, salida = correr(ejemplo)
        archivo = ESPERADO / f"{ejemplo.stem}.txt"
        if actualizar:
            archivo.write_text(salida, encoding="utf-8")
            continue
        esperado = archivo.read_text(encoding="utf-8") if archivo.exists() else None
        if codigo != 0 or salida != esperado:
            fallos += 1
            print(f"FALLA {ejemplo.name} (codigo {codigo})")
            if esperado is None:
                print(f"  falta {archivo.relative_to(AQUI)}: generalo con --actualizar")
            else:
                sys.stdout.writelines(difflib.unified_diff(
                    esperado.splitlines(True), salida.splitlines(True), "esperado", "obtenido"))
        else:
            print(f"ok    {ejemplo.name}")
    return 1 if fallos else 0


if __name__ == "__main__":
    sys.exit(main())
