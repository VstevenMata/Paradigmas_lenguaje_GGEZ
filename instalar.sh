#!/usr/bin/env bash
# Instalador de GGEZ con tematica Win95: curl -fsSL https://raw.githubusercontent.com/VstevenMata/Paradigmas_lenguaje_GGEZ/main/instalar.sh | bash
set -euo pipefail
shopt -s extglob
export LC_ALL=C.UTF-8
URL=https://github.com/VstevenMata/Paradigmas_lenguaje_GGEZ/releases/latest/download

# NixOS no puede correr el ejecutable suelto (no tiene /lib64/ld-linux); el flake lo envuelve
if [ -e /etc/NIXOS ]; then
    echo "En NixOS, GGEZ se abre con nix:"
    echo "  nix run github:VstevenMata/Paradigmas_lenguaje_GGEZ"
    exit 0
fi
DIR=$(dirname "$0")

# Paleta de Windows 95 en color verdadero; T vuelve al texto normal sobre la ventana gris
E=$'\e'; B="$E[1m"; I="$E[3m"; S="$E[4m"
T="$E[0m$E[48;2;192;192;192m$E[38;2;0;0;0m"
TEAL="$E[0m$E[48;2;0;128;128m"
BLANCO="$E[38;2;255;255;255m"; GRIS="$E[38;2;128;128;128m"; OSC="$E[38;2;64;64;64m"
NAVY="$E[38;2;0;0;128m"; VERDE="$E[38;2;0;128;0m"; ROJO="$E[38;2;170;0;0m"
FONDO_NAVY="$E[48;2;0;0;128m"; FONDO_BLANCO="$E[48;2;255;255;255m"
ESP=$(printf '%300s' "")
LINEA=$(printf '─%.0s' $(seq 74))
W=74 ALTO=24

# Fantasma pixelado con medios bloques (2 pixeles por caracter): W cuerpo, E ojo, C mejilla
declare -A PX=([.]="192;192;192" [W]="255;255;255" [E]="0;0;128" [C]="0;128;128")
FANTASMA=(
"......WWWW......"
"....WWWWWWWW...."
"...WWWWWWWWWW..."
"..WWWWWWWWWWWW.."
"..WWWWWWWWWWWW.."
".WWWEEWWWWEEWWW."
".WWWEEWWWWEEWWW."
".WWWWWWWWWWWWWW."
".WWCCWWWWWWCCWW."
".WWWWWWWWWWWWWW."
".WWWWWWWWWWWWWW."
".WWWWWWWWWWWWWW."
)
FALDAS=(".WW.WWW..WWW.WW." ".W.WWW.WW.WWW.W.")
VACIO="................"

# 16 cuadros precalculados: flota, la falda ondea y parpadea en el ultimo
CUADROS=()
for f in {0..15}; do
    px=("${FANTASMA[@]}" "${FALDAS[(f / 2) % 2]}")
    if [ $f = 15 ]; then px[5]=${px[5]//E/W}; fi
    if [ $(((f / 4) % 2)) = 0 ]; then px+=("$VACIO"); else px=("$VACIO" "${px[@]}"); fi
    for ((r = 0; r < 14; r += 2)); do
        out=""
        for ((c = 0; c < 16; c++)); do
            a=${px[r]:c:1} b=${px[r + 1]:c:1}
            if [ "$a" = "$b" ]; then out+="$E[48;2;${PX[$a]}m "; else out+="$E[38;2;${PX[$a]}m$E[48;2;${PX[$b]}m▀"; fi
        done
        CUADROS+=("$out$T")
    done
done

TEXTO=(
""
"$NAVY${B}GGEZ$T  ${GRIS}Programa de instalación"
""
"Un lenguaje de programación con alma de videojuego."
""
"Steven Mata · Maikel Segura · Angel Ureña"
"${GRIS}Paradigmas de Programación · UNA · 2026"
)
ETAPAS=("Sistema" "Carpeta" "Instalación" "Listo")
OPCIONES=("Linux" "Windows" "Salir")
DETALLES=(
"Instala el comando ggez y lo agrega al menú de aplicaciones."
"Descarga GGEZ.exe para Windows."
"Cierra el instalador sin cambiar nada."
)
SPIN=(⠋ ⠙ ⠹ ⠸ ⠼ ⠴ ⠦ ⠧ ⠇ ⠏)

case "$(uname -s)" in
    MINGW* | MSYS* | CYGWIN*) detectado=1 ;;
    *) detectado=0 ;;
esac
sel=$detectado f=0 PASO=0 RUTA="" CARPETA="" AVISO="" ERROR=""
PROG=0 TOTAL=0 BAJADO=0 NUEVA_TERMINAL=0 destino=""
PASOS=() ESTADOS=()

# Largo visible: quita las secuencias de color (\e[...m) sin usar extglob, que es lento
vis() {
    local s=$1 n=0 p
    while [[ $s == *$'\e['* ]]; do p=${s%%$'\e['*}; n=$((n + ${#p})); s=${s#*$'\e['}; s=${s#*m}; done
    N=$((n + ${#s}))
}
pad() { vis "$1"; local n=$(($2 - N)); if [ $n -lt 0 ]; then n=0; fi; REPLY="$1$T${ESP:0:n}"; }
fila() { pad "$1" $W; LINEAS+=("$T$BLANCO│$REPLY$T$OSC│"); }
# Rutas largas se recortan por la izquierda para que quepan
recortar() { if [ ${#1} -gt $2 ]; then REPLY="…${1: -$(($2 - 1))}"; else REPLY=$1; fi; }

boton() {
    case ${2:-} in
        foco) REPLY="$BLANCO▐$T$B $S$1$T$B $T$OSC▌$T" ;;
        off) REPLY="$BLANCO▐$T$GRIS $1 $T$OSC▌$T" ;;
        *) REPLY="$BLANCO▐$T $1 $OSC▌$T" ;;
    esac
}

contenido() {
    local i marca icono
    C=()
    case $PASO in
        0)
            C+=("  $B¿En qué sistema operativo vas a instalar GGEZ?" "")
            for i in 0 1 2; do
                marca=""; if [ $i = $detectado ]; then marca="(detectado)"; fi
                if [ $i = $sel ]; then
                    pad "$FONDO_NAVY$BLANCO (•) ${OPCIONES[i]}  $marca" 30
                    C+=("    ${REPLY//$T/$FONDO_NAVY$BLANCO}$T")
                else
                    C+=("     ( ) ${OPCIONES[i]}  $GRIS$marca")
                fi
            done
            C+=("" "  $GRIS${DETALLES[sel]}")
            ;;
        1)
            C+=("  $B¿Dónde quieres guardar GGEZ?" "" "  Carpeta de destino:")
            local cursor=" "
            if [ $((f % 8)) -lt 4 ]; then cursor="$NAVY█"; fi
            recortar "$RUTA" 64
            pad "$FONDO_BLANCO$E[38;2;0;0;0m $REPLY$cursor" 68
            C+=("  $OSC▐${REPLY//$T/$FONDO_BLANCO}$T$BLANCO▌")
            C+=("" "  ${GRIS}Escribe otra ruta o deja la sugerida; si no existe se crea.")
            if [ "$AVISO" ]; then C+=("  $ROJO$AVISO"); fi
            ;;
        2)
            recortar "$CARPETA" 50
            C+=("  ${B}Instalando GGEZ en$T $NAVY$REPLY" "")
            for i in "${!PASOS[@]}"; do
                case ${ESTADOS[i]} in
                    pendiente) icono="$GRIS◌" ;;
                    activo) icono="$NAVY${SPIN[f % 10]}" ;;
                    ok) icono="$VERDE✔" ;;
                    error) icono="$ROJO✖" ;;
                esac
                C+=("    $icono$T ${PASOS[i]}")
            done
            while [ ${#C[@]} -lt 5 ]; do C+=(""); done
            local n=$((PROG * 56 / 100)) lleno
            lleno=$(printf '%*s' $n "")
            pad "$FONDO_BLANCO$NAVY${lleno// /▊}" 56
            C+=("" "  $OSC▐${REPLY//$T/$FONDO_BLANCO}$T$BLANCO▌$T  $PROG%")
            if [ "$ERROR" ]; then C+=("  $ROJO$ERROR")
            elif [ $TOTAL -gt 0 ]; then C+=("  ${GRIS}Descargado $((BAJADO >> 20)) de $((TOTAL >> 20)) MB"); fi
            ;;
        3)
            C+=("  $VERDE$B✔ ¡GGEZ se instaló correctamente!" "" "  Quedó guardado en:")
            recortar "$destino" 66
            C+=("    $NAVY$B$REPLY" "")
            if [ $sel = 0 ]; then
                C+=("  Escribe $NAVY${B}ggez$T en la terminal o búscalo en el menú de aplicaciones.")
                if [ $NUEVA_TERMINAL = 1 ]; then C+=("  ${GRIS}Abre una terminal nueva para que reconozca el comando."); fi
            elif [ $detectado = 0 ]; then
                C+=("  Cópialo a la PC con Windows y ábrelo con doble clic.")
            fi
            ;;
    esac
    while [ ${#C[@]} -lt 8 ]; do C+=(""); done
}

ventana() {
    local i etapas="  " izq botones b1 b2 b3
    LINEAS=("$T$BLANCO┌$LINEA$OSC┐")
    pad "$FONDO_NAVY$BLANCO$B ▣ GGEZ - Instalador" $((W - 9))
    fila "${REPLY//$T/$FONDO_NAVY}$T$B[_][□][×]"
    for i in {0..6}; do fila "  ${CUADROS[f * 7 + i]}   ${TEXTO[i]}"; done
    fila "  $GRIS$I«Todo lo puedo en Cristo que me fortalece.» — Filipenses 4:13"
    fila "$GRIS$LINEA"
    for i in 0 1 2 3; do
        if [ $i -lt $PASO ]; then etapas+="$VERDE✔ ${ETAPAS[i]}$T"
        elif [ $i = $PASO ]; then etapas+="$NAVY$B▶ ${ETAPAS[i]}$T"
        else etapas+="$GRIS○ ${ETAPAS[i]}$T"; fi
        if [ $i -lt 3 ]; then etapas+="$GRIS  ›  $T"; fi
    done
    fila "$etapas"
    fila ""
    contenido
    for i in {0..7}; do fila "${C[i]}"; done
    fila "$GRIS$LINEA"
    case $PASO in
        0) izq="↑↓ elegir · Enter · Esc"; boton "< Atrás" off; b1=$REPLY; boton "Siguiente >" foco; b2=$REPLY; boton "Cancelar"; b3=$REPLY ;;
        1) izq="Ctrl+U borra la ruta"; boton "< Atrás"; b1=$REPLY; boton "Siguiente >" foco; b2=$REPLY; boton "Cancelar"; b3=$REPLY ;;
        2) if [ "$ERROR" ]; then izq="Enter para cerrar"; boton "Cerrar" foco; else izq="Por favor espera…"; boton "Cancelar" off; fi; b1=""; b2=""; b3=$REPLY ;;
        3) izq="Enter para salir"; b1=""; b2=""; boton "Finalizar" foco; b3=$REPLY ;;
    esac
    botones="$b1 $b2 $b3 "
    vis "$botones"
    pad "  $GRIS$izq" $((W - N))
    fila "$REPLY$botones"
    LINEAS+=("$T$OSC└$LINEA┘")
}

# Escritorio verde azulado de fondo con la ventana centrada
pantalla() {
    ventana
    local i j margen=$(((COLS - W - 2) / 2)) arriba=$(((FILAS - ALTO) / 2)) out="$E[H"
    if [ $margen -lt 0 ]; then margen=0; fi
    if [ $arriba -lt 0 ]; then arriba=0; fi
    for ((i = 0; i < FILAS; i++)); do
        j=$((i - arriba))
        if [ $j -ge 0 ] && [ $j -lt $ALTO ]; then out+="$TEAL${ESP:0:margen}${LINEAS[j]}$TEAL$E[K"; else out+="$TEAL$E[K"; fi
        if [ $i -lt $((FILAS - 1)) ]; then out+=$'\n'; fi
    done
    printf '%s' "$out"
}

tamano() { read -r FILAS COLS < <(stty size <&3 2>/dev/null) || { FILAS=24; COLS=80; }; }

# Lee una tecla (o nada si pasa el tiempo) y la deja en K con nombre: ENTER, ESC, UP, DOWN, BS, CLR
leer() {
    local c r=""
    K=""
    IFS= read -rsn1 -t 0.12 -u 3 c || return 0
    case $c in
        "") K=ENTER ;;
        $'\e')
            IFS= read -rsn1 -t 0.01 -u 3 r || true
            if [ "$r" = "[" ] || [ "$r" = O ]; then
                IFS= read -rsn1 -t 0.01 -u 3 r || true
                case $r in
                    A) K=UP ;;
                    B) K=DOWN ;;
                    [0-9]) IFS= read -rsn1 -t 0.01 -u 3 _ || true; K=NADA ;;
                    *) K=NADA ;;
                esac
            elif [ -z "$r" ]; then K=ESC; else K=NADA; fi
            ;;
        $'\x7f' | $'\b') K=BS ;;
        $'\x15') K=CLR ;;
        [[:cntrl:]]) K=NADA ;;
        *) K=$c ;;
    esac
}

# Avanza la animacion; tambien sirve de pausa mientras algo corre en segundo plano
tick() { leer; f=$(((f + 1) % 16)); pantalla; }
paso() { ESTADOS[$1]=$2; pantalla; }

descargar() {
    TOTAL=$(curl -sIL --connect-timeout 10 "$1" | tr -d '\r' | awk 'tolower($1)=="content-length:"{n=$2} END{print n+0}') || TOTAL=0
    curl -fsL --connect-timeout 10 -o "$2" "$1" &
    local pid=$!
    while kill -0 $pid 2>/dev/null; do
        BAJADO=0
        if [ -f "$2" ]; then BAJADO=$(wc -c <"$2"); fi
        if [ $BAJADO -gt $TOTAL ]; then TOTAL=0; fi
        if [ $TOTAL -gt 0 ]; then PROG=$((BAJADO * 80 / TOTAL)); fi
        tick
    done
    TOTAL=0
    wait $pid
}

# ~/.local/bin ya esta en el PATH en la mayoria de distros; si no, se agrega para bash, zsh y fish
agregar_path() {
    case ":$PATH:" in *":$HOME/.local/bin:"*) return 0 ;; esac
    NUEVA_TERMINAL=1
    local linea='export PATH="$HOME/.local/bin:$PATH"' rc
    for rc in "$HOME/.bashrc" "$HOME/.zshrc"; do
        if [ -f "$rc" ] && ! grep -qF "$linea" "$rc"; then echo "$linea" >>"$rc"; fi
    done
    if command -v fish >/dev/null; then
        mkdir -p "$HOME/.config/fish/conf.d"
        echo 'fish_add_path ~/.local/bin' >"$HOME/.config/fish/conf.d/ggez.fish"
    fi
}

ruta_sugerida() {
    if [ $sel = 0 ]; then RUTA="$HOME/.local/bin"
    elif [ $detectado = 1 ]; then RUTA="$(cygpath -u "$LOCALAPPDATA" 2>/dev/null || echo "$LOCALAPPDATA")/GGEZ"
    else RUTA=$PWD; fi
    RUTA=${RUTA/#$HOME/\~}
}

# Normaliza la ruta escrita (~, relativa, / final) y revisa que se pueda escribir ahi
aceptar_ruta() {
    local r=${RUTA/#\~/$HOME}
    if [ -z "$r" ]; then AVISO="Escribe una carpeta."; return 1; fi
    case $r in /* | [A-Za-z]:*) ;; *) r="$PWD/$r" ;; esac
    if [ "$r" != / ]; then r=${r%%+(/)}; fi
    if ! mkdir -p "$r" 2>/dev/null || [ ! -w "$r" ]; then AVISO="No se puede escribir en esa carpeta; prueba con otra."; return 1; fi
    CARPETA=$r AVISO=""
}

fallar() {
    ESTADOS[$1]=error
    ERROR="✖ No se pudo descargar. ¿Hay internet? ¿Ya hay un release publicado?"
}

instalar() {
    PASO=2
    if [ $sel = 0 ]; then
        destino="$CARPETA/ggez"
        PASOS=("Descargando GGEZ" "Creando el comando ggez" "Agregándolo al menú de aplicaciones")
        ESTADOS=(pendiente pendiente pendiente)
        paso 0 activo
        if [ -f "$DIR/dist/GGEZ" ]; then
            PASOS[0]="Copiando GGEZ desde dist/"
            cp "$DIR/dist/GGEZ" "$destino.tmp"
        elif ! descargar "$URL/GGEZ" "$destino.tmp"; then
            rm -f "$destino.tmp"; fallar 0; return
        fi
        PROG=80; paso 0 ok

        paso 1 activo
        chmod +x "$destino.tmp"
        mv "$destino.tmp" "$destino"
        # Si se guardo en otra carpeta, el comando ggez apunta ahi desde ~/.local/bin
        if [ "$CARPETA" != "$HOME/.local/bin" ]; then
            mkdir -p "$HOME/.local/bin"
            ln -sf "$destino" "$HOME/.local/bin/ggez"
        fi
        agregar_path
        PROG=90; tick; paso 1 ok

        # Mismo acceso que crea la app al abrirse; el icono aparece tras la primera vez que se abre
        paso 2 activo
        local apps="${XDG_DATA_HOME:-$HOME/.local/share}/applications"
        mkdir -p "$apps"
        cat >"$apps/ggez.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=GGEZ
Comment=Un lenguaje de programación con alma de videojuego
Exec="$destino"
Icon=${XDG_CONFIG_HOME:-$HOME/.config}/ParadigmasLenguajePropio/ggez.png
Terminal=false
Categories=Development;IDE;
EOF
        PROG=100; tick; paso 2 ok
    else
        destino="$CARPETA/GGEZ.exe"
        PASOS=("Descargando GGEZ.exe")
        ESTADOS=(pendiente)
        paso 0 activo
        if ! descargar "$URL/GGEZ.exe" "$destino"; then rm -f "$destino"; fallar 0; return; fi
        PROG=100; paso 0 ok
    fi
    tick; tick
    PASO=3
}

salir_pantalla() { printf '%s[0m%s[?25h%s[?1049l' "$E" "$E" "$E"; }

# Con curl | bash la entrada es el script, por eso las teclas se leen de la terminal
exec 3</dev/tty
tamano
trap tamano WINCH
trap salir_pantalla EXIT
trap 'exit 130' INT
printf '%s[?1049h%s[?25l' "$E" "$E"
pantalla

while :; do
    leer
    case $PASO:$K in
        0:UP | 0:k) sel=$(((sel + 2) % 3)) ;;
        0:DOWN | 0:j) sel=$(((sel + 1) % 3)) ;;
        0:[1-3]) sel=$((K - 1)) ;;
        0:ESC | 0:q) sel=2; break ;;
        0:ENTER) if [ $sel = 2 ]; then break; fi; ruta_sugerida; PASO=1 ;;
        1:ESC) PASO=0 AVISO="" ;;
        1:BS) RUTA=${RUTA%?} ;;
        1:CLR) RUTA="" ;;
        1:ENTER) if aceptar_ruta; then instalar; fi ;;
        1:UP | 1:DOWN | 1:NADA | 1:) ;;
        1:*) RUTA+=$K ;;
        2:ENTER | 2:ESC) break ;;
        3:ENTER | 3:ESC) break ;;
    esac
    if [ -z "$K" ]; then f=$(((f + 1) % 16)); fi
    pantalla
done

salir_pantalla
if [ $sel = 2 ]; then
    echo "Instalación cancelada."
elif [ "$ERROR" ]; then
    echo "${ERROR}"
    exit 1
else
    echo "✔ GGEZ quedó guardado en: $destino"
    if [ $sel = 0 ]; then echo "  Escribe ggez en la terminal para abrirlo."; fi
fi
