# Instala GGEZ en Windows: irm https://raw.githubusercontent.com/VstevenMata/Paradigmas_lenguaje_GGEZ/main/instalar.ps1 | iex
$ErrorActionPreference = 'Stop'
$carpeta = "$env:LOCALAPPDATA\GGEZ"
$exe = "$carpeta\GGEZ.exe"
New-Item -ItemType Directory -Force $carpeta | Out-Null
Invoke-WebRequest "https://github.com/VstevenMata/Paradigmas_lenguaje_GGEZ/releases/latest/download/GGEZ.exe" -OutFile $exe

# Acceso directo en el menu Inicio
$acceso = (New-Object -ComObject WScript.Shell).CreateShortcut("$env:APPDATA\Microsoft\Windows\Start Menu\Programs\GGEZ.lnk")
$acceso.TargetPath = $exe
$acceso.Save()

Start-Process $exe
Write-Host "GGEZ instalado en $exe"
