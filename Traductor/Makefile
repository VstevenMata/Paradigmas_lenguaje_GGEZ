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

# corre las pruebas de pruebas/ (seccion 18 del enunciado) contra su esperado.txt
test:
	$(PY) pruebas/correr.py

clean:
	rm -rf src/ggez/__pycache__ __pycache__
