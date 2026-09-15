PY := python
ENTRADA := ggezc.py

.PHONY: run tokens test clean

# make run ARCHIVO=ruta/al/archivo.ggez
run:
	@test -n "$(ARCHIVO)" || (echo "uso: make run ARCHIVO=ruta/al/archivo.ggez" >&2; exit 1)
	$(PY) $(ENTRADA) run "$(ARCHIVO)"

# make tokens ARCHIVO=ruta/al/archivo.ggez
tokens:
	@test -n "$(ARCHIVO)" || (echo "uso: make tokens ARCHIVO=ruta/al/archivo.ggez" >&2; exit 1)
	$(PY) $(ENTRADA) tokens "$(ARCHIVO)"

# pasa los dos ejemplos del documento por el traductor
test:
	$(PY) $(ENTRADA) run ejemplos/clasificador.ggez
	$(PY) $(ENTRADA) run ejemplos/poo.ggez

clean:
	rm -rf src/ggez/__pycache__ __pycache__
