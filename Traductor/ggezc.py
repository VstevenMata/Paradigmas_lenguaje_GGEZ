#!/usr/bin/env python3
"""Punto de entrada del traductor: el 'ggezc' que invoca el IDE.

    python ggezc.py run ejemplos/clasificador.ggez
"""

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent / "src"))

from ggez.cli import main  # noqa: E402

if __name__ == "__main__":
    sys.exit(main())
