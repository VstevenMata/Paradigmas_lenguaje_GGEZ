BIN := bin/ggezc
SRC := src/traductor.pas

.PHONY: build run clean

build: $(BIN)

$(BIN): $(SRC)
	mkdir -p bin
	fpc -FEbin -FUbin -o$(BIN) $(SRC)

# make run ARCHIVO=ruta/al/archivo.ggez
run: build
	@test -n "$(ARCHIVO)" || (echo "uso: make run ARCHIVO=ruta/al/archivo.ggez" >&2; exit 1)
	./$(BIN) run "$(ARCHIVO)"

clean:
	rm -rf bin
