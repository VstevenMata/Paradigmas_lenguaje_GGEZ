# Instalador de GGEZ con tematica Win95: irm https://raw.githubusercontent.com/VstevenMata/Paradigmas_lenguaje_GGEZ/main/instalar.ps1 | iex
$ErrorActionPreference = 'Stop'
$URL = 'https://github.com/VstevenMata/Paradigmas_lenguaje_GGEZ/releases/latest/download'
[Console]::OutputEncoding = [Text.Encoding]::UTF8
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
Add-Type -AssemblyName System.Net.Http
$EsWindows = $env:OS -eq 'Windows_NT'

# La consola clasica de Windows necesita que se activen las secuencias de color (VT)
if ($EsWindows) {
    if (-not ('Ggez.Consola' -as [type])) {
        Add-Type -Namespace Ggez -Name Consola -MemberDefinition @'
[DllImport("kernel32.dll")] public static extern IntPtr GetStdHandle(int h);
[DllImport("kernel32.dll")] public static extern bool GetConsoleMode(IntPtr h, out int m);
[DllImport("kernel32.dll")] public static extern bool SetConsoleMode(IntPtr h, int m);
'@
    }
    $manejador = [Ggez.Consola]::GetStdHandle(-11); $modo = 0
    if ([Ggez.Consola]::GetConsoleMode($manejador, [ref]$modo)) { [void][Ggez.Consola]::SetConsoleMode($manejador, $modo -bor 4) }
}

# Paleta de Windows 95 en color verdadero; NORMAL vuelve al texto negro sobre la ventana gris
# (PowerShell no distingue mayusculas en variables, por eso los nombres largos)
$ESC = [char]27
$NORMAL = "$ESC[0m$ESC[48;2;192;192;192m$ESC[38;2;0;0;0m"
$NEGRITA = "$ESC[1m"; $CURSIVA = "$ESC[3m"; $SUBRAYA = "$ESC[4m"
$TEAL = "$ESC[0m$ESC[48;2;0;128;128m"
$BLANCO = "$ESC[38;2;255;255;255m"; $GRIS = "$ESC[38;2;128;128;128m"; $OSC = "$ESC[38;2;64;64;64m"
$NAVY = "$ESC[38;2;0;0;128m"; $VERDE = "$ESC[38;2;0;128;0m"; $ROJO = "$ESC[38;2;170;0;0m"
$FNAVY = "$ESC[48;2;0;0;128m"; $FBLANCO = "$ESC[48;2;255;255;255m"
$ANSI = [regex]"$ESC\[[0-9;]*m"
$ANCHO = 74; $ALTO = 24
$LINEA = [string]::new([char]0x2500, $ANCHO)

# Fantasma pixelado con medios bloques (2 pixeles por caracter): W cuerpo, E ojo, C mejilla
$PX = @{ '.' = '192;192;192'; 'W' = '255;255;255'; 'E' = '0;0;128'; 'C' = '0;128;128' }
$FANTASMA = @(
    '......WWWW......',
    '....WWWWWWWW....',
    '...WWWWWWWWWW...',
    '..WWWWWWWWWWWW..',
    '..WWWWWWWWWWWW..',
    '.WWWEEWWWWEEWWW.',
    '.WWWEEWWWWEEWWW.',
    '.WWWWWWWWWWWWWW.',
    '.WWCCWWWWWWCCWW.',
    '.WWWWWWWWWWWWWW.',
    '.WWWWWWWWWWWWWW.',
    '.WWWWWWWWWWWWWW.'
)
$FALDAS = @('.WW.WWW..WWW.WW.', '.W.WWW.WW.WWW.W.')
$VACIO = '................'

# 16 cuadros precalculados: flota, la falda ondea y parpadea en el ultimo
$CUADROS = New-Object System.Collections.Generic.List[string]
foreach ($fr in 0..15) {
    $pixeles = @($FANTASMA) + $FALDAS[[math]::Floor($fr / 2) % 2]
    if ($fr -eq 15) { $pixeles[5] = $pixeles[5].Replace('E', 'W') }
    if ([math]::Floor($fr / 4) % 2 -eq 0) { $pixeles += $VACIO } else { $pixeles = @($VACIO) + $pixeles }
    for ($r = 0; $r -lt 14; $r += 2) {
        $sb = New-Object Text.StringBuilder
        for ($col = 0; $col -lt 16; $col++) {
            $arriba = [string]$pixeles[$r][$col]; $abajo = [string]$pixeles[$r + 1][$col]
            if ($arriba -eq $abajo) { [void]$sb.Append("$ESC[48;2;$($PX[$arriba])m ") }
            else { [void]$sb.Append("$ESC[38;2;$($PX[$arriba])m$ESC[48;2;$($PX[$abajo])m" + [char]0x2580) }
        }
        $CUADROS.Add($sb.ToString() + $NORMAL)
    }
}

$TEXTO = @(
    '',
    "$NAVY${NEGRITA}GGEZ$NORMAL  ${GRIS}Programa de instalación",
    '',
    'Un lenguaje de programación con alma de videojuego.',
    '',
    'Steven Mata · Maikel Segura · Angel Ureña',
    "${GRIS}Paradigmas de Programación · UNA · 2026"
)
$ETAPAS = @('Sistema', 'Carpeta', 'Instalación', 'Listo')
$OPCIONES = @('Linux', 'Windows', 'Salir')
$DETALLES = @(
    'Descarga GGEZ para llevarlo a una PC con Linux.',
    'Instala GGEZ.exe, el comando ggez y un acceso en el menú Inicio.',
    'Cierra el instalador sin cambiar nada.'
)
$SPIN = @('⠋', '⠙', '⠹', '⠸', '⠼', '⠴', '⠦', '⠧', '⠇', '⠏')

$Detectado = if ($EsWindows) { 1 } else { 0 }
$Sel = $Detectado; $Cuadro = 0; $Paso = 0; $Ruta = ''; $Carpeta = ''; $Aviso = ''; $ErrorMsg = ''
$Prog = 0; $Total = 0; $Bajado = 0; $NuevaTerminal = $false; $Destino = ''; $Tecla = ''
$Pasos = @(); $Estados = @()

function Vis($s) { return $ANSI.Replace($s, '').Length }
function Pad($s, $w) { $n = $w - (Vis $s); if ($n -lt 0) { $n = 0 }; return "$s$NORMAL" + (' ' * $n) }
# Rutas largas se recortan por la izquierda para que quepan
function Recortar($s, $n) { if ($s.Length -gt $n) { return '…' + $s.Substring($s.Length - ($n - 1)) }; return $s }

function Boton($texto, $estado) {
    switch ($estado) {
        'foco' { return "$BLANCO▐$NORMAL$NEGRITA $SUBRAYA$texto$NORMAL$NEGRITA $NORMAL$OSC▌$NORMAL" }
        'off' { return "$BLANCO▐$NORMAL$GRIS $texto $NORMAL$OSC▌$NORMAL" }
        default { return "$BLANCO▐$NORMAL $texto $OSC▌$NORMAL" }
    }
}

function Contenido {
    $c = @()
    switch ($Paso) {
        0 {
            $c += "  $NEGRITA¿En qué sistema operativo vas a instalar GGEZ?"; $c += ''
            for ($j = 0; $j -lt 3; $j++) {
                $marca = if ($j -eq $Detectado) { '(detectado)' } else { '' }
                if ($j -eq $Sel) {
                    $fila = Pad "$FNAVY$BLANCO (•) $($OPCIONES[$j])  $marca" 30
                    $c += "    " + $fila.Replace($NORMAL, "$FNAVY$BLANCO") + $NORMAL
                } else { $c += "     ( ) $($OPCIONES[$j])  $GRIS$marca" }
            }
            $c += ''; $c += "  $GRIS$($DETALLES[$Sel])"
        }
        1 {
            $c += "  $NEGRITA¿Dónde quieres guardar GGEZ?"; $c += ''; $c += '  Carpeta de destino:'
            $cursor = if ($Cuadro % 8 -lt 4) { "$NAVY█" } else { ' ' }
            $campo = Pad "$FBLANCO$ESC[38;2;0;0;0m $(Recortar $Ruta 64)$cursor" 68
            $c += "  $OSC▐" + $campo.Replace($NORMAL, $FBLANCO) + "$NORMAL$BLANCO▌"
            $c += ''; $c += "  ${GRIS}Escribe otra ruta o deja la sugerida; si no existe se crea."
            if ($Aviso) { $c += "  $ROJO$Aviso" }
        }
        2 {
            $c += "  ${NEGRITA}Instalando GGEZ en$NORMAL $NAVY$(Recortar $Carpeta 50)"; $c += ''
            for ($j = 0; $j -lt $Pasos.Count; $j++) {
                $icono = switch ($Estados[$j]) {
                    'pendiente' { "$GRIS◌" }
                    'activo' { "$NAVY$($SPIN[$Cuadro % 10])" }
                    'ok' { "$VERDE✔" }
                    'error' { "$ROJO✖" }
                }
                $c += "    $icono$NORMAL $($Pasos[$j])"
            }
            while ($c.Count -lt 5) { $c += '' }
            $barra = Pad ("$FBLANCO$NAVY" + ('▊' * [math]::Floor($Prog * 56 / 100))) 56
            $c += ''; $c += "  $OSC▐" + $barra.Replace($NORMAL, $FBLANCO) + "$NORMAL$BLANCO▌$NORMAL  $Prog%"
            if ($ErrorMsg) { $c += "  $ROJO$ErrorMsg" }
            elseif ($Total -gt 0) { $c += "  ${GRIS}Descargado $([math]::Floor($Bajado / 1MB)) de $([math]::Floor($Total / 1MB)) MB" }
        }
        3 {
            $c += "  $VERDE$NEGRITA✔ ¡GGEZ se instaló correctamente!"; $c += ''; $c += '  Quedó guardado en:'
            $c += "    $NAVY$NEGRITA$(Recortar $Destino 66)"; $c += ''
            if ($Sel -eq 1 -and $EsWindows) {
                $c += "  Escribe $NAVY${NEGRITA}ggez$NORMAL en la terminal o búscalo en el menú Inicio."
                if ($NuevaTerminal) { $c += "  ${GRIS}Abre una terminal nueva para que reconozca el comando." }
            } elseif ($Sel -eq 0) { $c += '  Cópialo a la PC con Linux y ábrelo con ./GGEZ' }
            else { $c += '  Cópialo a la PC con Windows y ábrelo con doble clic.' }
        }
    }
    while ($c.Count -lt 8) { $c += '' }
    return , $c
}

function Ventana {
    $l = New-Object System.Collections.Generic.List[string]
    $fila = { param($s) $l.Add("$NORMAL$BLANCO│" + (Pad $s $ANCHO) + "$NORMAL$OSC│") }
    $l.Add("$NORMAL$BLANCO┌$LINEA$OSC┐")
    $titulo = Pad "$FNAVY$BLANCO$NEGRITA ▣ GGEZ - Instalador" ($ANCHO - 9)
    & $fila ($titulo.Replace($NORMAL, $FNAVY) + "$NORMAL$NEGRITA[_][□][×]")
    for ($j = 0; $j -lt 7; $j++) { & $fila ("  " + $CUADROS[$Cuadro * 7 + $j] + "   " + $TEXTO[$j]) }
    & $fila "  $GRIS$CURSIVA«Todo lo puedo en Cristo que me fortalece.» — Filipenses 4:13"
    & $fila "$GRIS$LINEA"
    $indicador = '  '
    for ($j = 0; $j -lt 4; $j++) {
        if ($j -lt $Paso) { $indicador += "$VERDE✔ $($ETAPAS[$j])$NORMAL" }
        elseif ($j -eq $Paso) { $indicador += "$NAVY$NEGRITA▶ $($ETAPAS[$j])$NORMAL" }
        else { $indicador += "$GRIS○ $($ETAPAS[$j])$NORMAL" }
        if ($j -lt 3) { $indicador += "$GRIS  ›  $NORMAL" }
    }
    & $fila $indicador
    & $fila ''
    foreach ($renglon in (Contenido)) { & $fila $renglon }
    & $fila "$GRIS$LINEA"
    switch ($Paso) {
        0 { $izq = '↑↓ elegir · Enter · Esc'; $botones = (Boton '< Atrás' 'off') + ' ' + (Boton 'Siguiente >' 'foco') + ' ' + (Boton 'Cancelar') }
        1 { $izq = 'Ctrl+U borra la ruta'; $botones = (Boton '< Atrás') + ' ' + (Boton 'Siguiente >' 'foco') + ' ' + (Boton 'Cancelar') }
        2 { if ($ErrorMsg) { $izq = 'Enter para cerrar'; $botones = Boton 'Cerrar' 'foco' } else { $izq = 'Por favor espera…'; $botones = Boton 'Cancelar' 'off' } }
        3 { $izq = 'Enter para salir'; $botones = Boton 'Finalizar' 'foco' }
    }
    $botones += ' '
    & $fila ((Pad "  $GRIS$izq" ($ANCHO - (Vis $botones))) + $botones)
    $l.Add("$NORMAL$OSC└$LINEA┘")
    return , $l
}

# Escritorio verde azulado de fondo con la ventana centrada
function Pantalla {
    $lineas = Ventana
    try { $filas = [Console]::WindowHeight; $cols = [Console]::WindowWidth } catch { $filas = 24; $cols = 80 }
    $margen = [math]::Max(0, [math]::Floor(($cols - $ANCHO - 2) / 2))
    $arriba = [math]::Max(0, [math]::Floor(($filas - $ALTO) / 2))
    $sb = New-Object Text.StringBuilder
    [void]$sb.Append("$ESC[H")
    for ($j = 0; $j -lt $filas; $j++) {
        $k = $j - $arriba
        if ($k -ge 0 -and $k -lt $ALTO) { [void]$sb.Append($TEAL + (' ' * $margen) + $lineas[$k] + "$TEAL$ESC[K") }
        else { [void]$sb.Append("$TEAL$ESC[K") }
        if ($j -lt $filas - 1) { [void]$sb.Append("`n") }
    }
    [Console]::Write($sb.ToString())
}

# Espera una tecla hasta 120 ms y la deja en Tecla: ENTER, ESC, UP, DOWN, BS, CLR, o el caracter
function Leer {
    $script:Tecla = ''
    $fin = [DateTime]::Now.AddMilliseconds(120)
    while (-not [Console]::KeyAvailable) {
        if ([DateTime]::Now -ge $fin) { return }
        Start-Sleep -Milliseconds 15
    }
    $k = [Console]::ReadKey($true)
    if ($k.Key -eq 'Enter') { $script:Tecla = 'ENTER' }
    elseif ($k.Key -eq 'Escape') { $script:Tecla = 'ESC' }
    elseif ($k.Key -eq 'UpArrow') { $script:Tecla = 'UP' }
    elseif ($k.Key -eq 'DownArrow') { $script:Tecla = 'DOWN' }
    elseif ($k.Key -eq 'Backspace') { $script:Tecla = 'BS' }
    elseif (($k.Modifiers -band [ConsoleModifiers]::Control) -and $k.Key -eq 'U') { $script:Tecla = 'CLR' }
    elseif (($k.Modifiers -band [ConsoleModifiers]::Control) -and $k.Key -eq 'C') { $script:Tecla = 'ESC' }
    elseif (-not [char]::IsControl($k.KeyChar)) { $script:Tecla = [string]$k.KeyChar }
}

function Avanzar { $script:Cuadro = ($Cuadro + 1) % 16; Pantalla }
# Animacion mientras se trabaja; tambien sirve de pausa corta
function Tick { Leer; Avanzar }
function Marcar($j, $estado) { $script:Estados[$j] = $estado; Pantalla }

# Descarga por partes para poder dibujar la barra de progreso entre bloque y bloque
function Descargar($url, $archivo) {
    $cli = New-Object System.Net.Http.HttpClient
    $cli.Timeout = [TimeSpan]::FromMinutes(30)
    try {
        $resp = $cli.GetAsync($url, [System.Net.Http.HttpCompletionOption]::ResponseHeadersRead).GetAwaiter().GetResult()
        if (-not $resp.IsSuccessStatusCode) { return $false }
        $largo = $resp.Content.Headers.ContentLength
        $script:Total = if ($largo) { [long]$largo } else { 0 }
        $script:Bajado = 0
        $entrada = $resp.Content.ReadAsStreamAsync().GetAwaiter().GetResult()
        $salida = [IO.File]::Create($archivo)
        try {
            $buf = New-Object byte[] 81920
            $reloj = [Diagnostics.Stopwatch]::StartNew()
            while (($n = $entrada.Read($buf, 0, $buf.Length)) -gt 0) {
                $salida.Write($buf, 0, $n)
                $script:Bajado += $n
                if ($reloj.ElapsedMilliseconds -ge 120) {
                    if ($Total -gt 0) { $script:Prog = [math]::Floor($Bajado * 80 / $Total) }
                    Avanzar; $reloj.Restart()
                }
            }
        } finally { $salida.Dispose(); $entrada.Dispose() }
        return $true
    } catch { return $false }
    finally { $cli.Dispose(); $script:Total = 0 }
}

function Fallar($j) {
    $script:Estados[$j] = 'error'
    $script:ErrorMsg = '✖ No se pudo descargar. ¿Hay internet? ¿Ya hay un release publicado?'
}

function RutaSugerida {
    if ($Sel -eq 1 -and $EsWindows) { $r = Join-Path $env:LOCALAPPDATA 'GGEZ' } else { $r = (Get-Location).Path }
    $script:Ruta = $r
}

# Normaliza la ruta escrita (~, relativa) y revisa que se pueda escribir ahi
function AceptarRuta {
    $r = $Ruta.Trim()
    if (-not $r) { $script:Aviso = 'Escribe una carpeta.'; return $false }
    if ($r.StartsWith('~')) { $r = $HOME + $r.Substring(1) }
    try {
        $r = [IO.Path]::GetFullPath([IO.Path]::Combine((Get-Location).Path, $r))
        [void](New-Item -ItemType Directory -Force -Path $r)
        $prueba = Join-Path $r '.ggez-prueba'
        [IO.File]::WriteAllText($prueba, ''); [IO.File]::Delete($prueba)
    } catch { $script:Aviso = 'No se puede escribir en esa carpeta; prueba con otra.'; return $false }
    $script:Carpeta = $r.TrimEnd('\', '/'); if (-not $Carpeta) { $script:Carpeta = $r }
    $script:Aviso = ''
    return $true
}

# Agrega la carpeta al PATH del usuario para que el comando ggez funcione en terminales nuevas
function AgregarPath {
    $actual = [Environment]::GetEnvironmentVariable('Path', 'User')
    if (-not $actual) { $actual = '' }
    if (($actual -split ';') -contains $Carpeta) { return }
    [Environment]::SetEnvironmentVariable('Path', ($actual.TrimEnd(';') + ';' + $Carpeta).TrimStart(';'), 'User')
    $script:NuevaTerminal = $true
}

function Instalar {
    $script:Paso = 2
    if ($Sel -eq 1 -and $EsWindows) {
        $script:Destino = Join-Path $Carpeta 'GGEZ.exe'
        $script:Pasos = @('Descargando GGEZ.exe', 'Creando el comando ggez y el acceso en el menú Inicio', 'Abriendo GGEZ')
        $script:Estados = @('pendiente', 'pendiente', 'pendiente')
        Marcar 0 'activo'
        $tmp = "$Destino.tmp"
        if (-not (Descargar "$URL/GGEZ.exe" $tmp)) { Remove-Item $tmp -ErrorAction SilentlyContinue; Fallar 0; return }
        Move-Item -Force $tmp $Destino
        $script:Prog = 80; Marcar 0 'ok'

        Marcar 1 'activo'
        AgregarPath
        $acceso = (New-Object -ComObject WScript.Shell).CreateShortcut("$env:APPDATA\Microsoft\Windows\Start Menu\Programs\GGEZ.lnk")
        $acceso.TargetPath = $Destino
        $acceso.Save()
        $script:Prog = 90; Tick; Marcar 1 'ok'

        Marcar 2 'activo'
        Start-Process $Destino
        $script:Prog = 100; Tick; Tick; Marcar 2 'ok'
    } else {
        $nombre = if ($Sel -eq 0) { 'GGEZ' } else { 'GGEZ.exe' }
        $script:Destino = Join-Path $Carpeta $nombre
        $script:Pasos = @("Descargando $nombre")
        $script:Estados = @('pendiente')
        Marcar 0 'activo'
        if (-not (Descargar "$URL/$nombre" $Destino)) { Remove-Item $Destino -ErrorAction SilentlyContinue; Fallar 0; return }
        $script:Prog = 100; Marcar 0 'ok'
    }
    Tick; Tick
    $script:Paso = 3
}

[Console]::TreatControlCAsInput = $true
[Console]::Write("$ESC[?1049h$ESC[?25l")
try {
    Pantalla
    while ($true) {
        Leer
        if ($Paso -eq 0) {
            if ($Tecla -eq 'UP' -or $Tecla -ceq 'k') { $Sel = ($Sel + 2) % 3 }
            elseif ($Tecla -eq 'DOWN' -or $Tecla -ceq 'j') { $Sel = ($Sel + 1) % 3 }
            elseif ($Tecla -match '^[1-3]$') { $Sel = [int]$Tecla - 1 }
            elseif ($Tecla -eq 'ESC' -or $Tecla -ceq 'q') { $Sel = 2; break }
            elseif ($Tecla -eq 'ENTER') { if ($Sel -eq 2) { break }; RutaSugerida; $Paso = 1 }
        } elseif ($Paso -eq 1) {
            if ($Tecla -eq 'ESC') { $Paso = 0; $Aviso = '' }
            elseif ($Tecla -eq 'BS') { if ($Ruta.Length) { $Ruta = $Ruta.Substring(0, $Ruta.Length - 1) } }
            elseif ($Tecla -eq 'CLR') { $Ruta = '' }
            elseif ($Tecla -eq 'ENTER') { if (AceptarRuta) { Instalar } }
            elseif ($Tecla -and $Tecla -cnotin 'UP', 'DOWN') { $Ruta += $Tecla }
        } elseif ($Tecla -eq 'ENTER' -or $Tecla -eq 'ESC') { break }
        if (-not $Tecla) { $Cuadro = ($Cuadro + 1) % 16 }
        Pantalla
    }
} finally {
    [Console]::Write("$ESC[0m$ESC[?25h$ESC[?1049l")
    [Console]::TreatControlCAsInput = $false
}

if ($Sel -eq 2) { Write-Host 'Instalación cancelada.' }
elseif ($ErrorMsg) { Write-Host $ErrorMsg -ForegroundColor Red }
else {
    Write-Host "✔ GGEZ quedó guardado en: $Destino" -ForegroundColor Green
    if ($Sel -eq 1 -and $EsWindows) { Write-Host '  Escribe ggez en una terminal nueva para abrirlo.' }
}
