using System.Diagnostics;
using System.Numerics;
using Raylib_cs;
using ParadigmasLenguajePropio.UI;

const int anchoInicial = 1500;
const int altoInicial = 900;
const int anchoMinimo = 800;
const int altoMinimo = 450;
const int ladoBoton = 28;
const int margen = 4;
const int espacio = 3;
const int altoDivisor = 2;
const int anchoManijaResize = 6;
const int anchoExploradorMinimo = 100;
const int anchoExploradorMaximo = 400;
const int anchoExploradorInicial = 220;
const int anchoExploradorColapsado = 0;
const float velocidadColapsoExplorador = 1400f;
const float anchoPanelCentralMinimo = 200f;
const int altoTerminal = 160;
const int anchoPanelDerechoInicial = 340;
const int anchoPanelDerechoMinimo = 220;
const int anchoPanelDerechoMaximo = 560;
const int espacioPanelDerecho = 8;
const int anchoBarraLateral = ladoBoton + margen * 2;
const int altoBotonColumnaDerecha = ladoBoton + margen * 2;

Texture2D CargarIcono(string archivo)
{
    var ruta = Path.Combine(AppContext.BaseDirectory, "Assets", "Icons", archivo);
    var textura = Raylib.LoadTexture(ruta);
    Raylib.SetTextureFilter(textura, TextureFilter.Point);
    return textura;
}

void DibujarPantallaCarga(float progreso)
{
    Raylib.BeginDrawing();
    Raylib.ClearBackground(Color.Black);

    const int anchoBarra = 320;
    const int altoBarra = 22;
    int x = (Raylib.GetScreenWidth() - anchoBarra) / 2;
    int y = (Raylib.GetScreenHeight() - altoBarra) / 2;

    Raylib.DrawText("Paradigmas GGEZ", x, y - 44, 22, Color.White);
    Raylib.DrawText("Cargando...", x, y - 20, 14, Color.LightGray);

    var fondo = new Rectangle(x, y, anchoBarra, altoBarra);
    Raylib.DrawRectangleRec(fondo, new Color(40, 40, 40, 255));
    Raylib.DrawRectangleLinesEx(fondo, 1, Color.White);

    const int segmentos = 24;
    const int margenSegmento = 2;
    float anchoSegmento = (anchoBarra - margenSegmento * (segmentos + 1)) / (float)segmentos;
    int segmentosLlenos = (int)(Math.Clamp(progreso, 0f, 1f) * segmentos);
    for (int i = 0; i < segmentosLlenos; i++)
    {
        float sx = x + margenSegmento + i * (anchoSegmento + margenSegmento);
        Raylib.DrawRectangle((int)sx, y + margenSegmento, (int)anchoSegmento, altoBarra - margenSegmento * 2, new Color(0, 0, 170, 255));
    }

    Raylib.EndDrawing();
}


// en Linux usa zenity, para no agregar dependencias de UI nativa al proyecto
string? ElegirCarpeta()
{
    try
    {
        var info = OperatingSystem.IsWindows() ? InfoSelectorCarpetaWindows() : InfoSelectorCarpetaLinux();
        using var proceso = Process.Start(info);
        if (proceso == null) return null;

        string salida = proceso.StandardOutput.ReadToEnd().Trim();
        proceso.WaitForExit();
        return proceso.ExitCode == 0 && salida.Length > 0 ? salida : null;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"No se pudo abrir el selector de carpetas: {ex.Message}");
        return null;
    }
}

ProcessStartInfo InfoSelectorCarpetaLinux() => new()
{
    FileName = "zenity",
    Arguments = "--file-selection --directory --title=\"Elegir una carpeta\"",
    RedirectStandardOutput = true,
    UseShellExecute = false,
};

ProcessStartInfo InfoSelectorCarpetaWindows()
{
    var info = new ProcessStartInfo
    {
        FileName = "powershell",
        RedirectStandardOutput = true,
        UseShellExecute = false,
        CreateNoWindow = true,
    };
    info.ArgumentList.Add("-NoProfile");
    info.ArgumentList.Add("-Command");
    info.ArgumentList.Add(
        "Add-Type -AssemblyName System.Windows.Forms; " +
        "$dialogo = New-Object System.Windows.Forms.FolderBrowserDialog; " +
        "$dialogo.Description = 'Elegir una carpeta'; " +
        "if ($dialogo.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) { Write-Output $dialogo.SelectedPath }");
    return info;
}

// mismo patrón que ElegirCarpeta(), pero para "Guardar como": deja escribir nombre y extensión (ej: nuevo.txt, nuevo.ggez)
string? ElegirRutaGuardarArchivo(string nombreSugerido)
{
    try
    {
        var info = OperatingSystem.IsWindows() ? InfoSelectorGuardarWindows(nombreSugerido) : InfoSelectorGuardarLinux(nombreSugerido);
        using var proceso = Process.Start(info);
        if (proceso == null) return null;

        string salida = proceso.StandardOutput.ReadToEnd().Trim();
        proceso.WaitForExit();
        return proceso.ExitCode == 0 && salida.Length > 0 ? salida : null;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"No se pudo abrir el selector de guardado: {ex.Message}");
        return null;
    }
}

ProcessStartInfo InfoSelectorGuardarLinux(string nombreSugerido)
{
    var info = new ProcessStartInfo
    {
        FileName = "zenity",
        RedirectStandardOutput = true,
        UseShellExecute = false,
    };
    info.ArgumentList.Add("--file-selection");
    info.ArgumentList.Add("--save");
    info.ArgumentList.Add("--confirm-overwrite");
    info.ArgumentList.Add("--title=Guardar como");
    info.ArgumentList.Add($"--filename={nombreSugerido}");
    return info;
}

ProcessStartInfo InfoSelectorGuardarWindows(string nombreSugerido)
{
    var info = new ProcessStartInfo
    {
        FileName = "powershell",
        RedirectStandardOutput = true,
        UseShellExecute = false,
        CreateNoWindow = true,
    };
    string nombreEscapado = nombreSugerido.Replace("'", "''");
    info.ArgumentList.Add("-NoProfile");
    info.ArgumentList.Add("-Command");
    info.ArgumentList.Add(
        "Add-Type -AssemblyName System.Windows.Forms; " +
        "$dialogo = New-Object System.Windows.Forms.SaveFileDialog; " +
        "$dialogo.Title = 'Guardar como'; " +
        $"$dialogo.FileName = '{nombreEscapado}'; " +
        "$dialogo.Filter = 'Todos los archivos (*.*)|*.*'; " +
        "if ($dialogo.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) { Write-Output $dialogo.FileName }");
    return info;
}

// mismo patrón que ElegirCarpeta(): usado para pedir nombres (nuevo archivo/carpeta, renombrar)
string? PedirTexto(string titulo, string mensaje, string valorInicial)
{
    try
    {
        var info = OperatingSystem.IsWindows() ? InfoEntradaTextoWindows(titulo, mensaje, valorInicial) : InfoEntradaTextoLinux(titulo, mensaje, valorInicial);
        using var proceso = Process.Start(info);
        if (proceso == null) return null;

        string salida = proceso.StandardOutput.ReadToEnd().Trim();
        proceso.WaitForExit();
        return proceso.ExitCode == 0 && salida.Length > 0 ? salida : null;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"No se pudo abrir el cuadro de texto: {ex.Message}");
        return null;
    }
}

ProcessStartInfo InfoEntradaTextoLinux(string titulo, string mensaje, string valorInicial)
{
    var info = new ProcessStartInfo
    {
        FileName = "zenity",
        RedirectStandardOutput = true,
        UseShellExecute = false,
    };
    info.ArgumentList.Add("--entry");
    info.ArgumentList.Add($"--title={titulo}");
    info.ArgumentList.Add($"--text={mensaje}");
    info.ArgumentList.Add($"--entry-text={valorInicial}");
    return info;
}

ProcessStartInfo InfoEntradaTextoWindows(string titulo, string mensaje, string valorInicial)
{
    var info = new ProcessStartInfo
    {
        FileName = "powershell",
        RedirectStandardOutput = true,
        UseShellExecute = false,
        CreateNoWindow = true,
    };
    string tituloEscapado = titulo.Replace("'", "''");
    string mensajeEscapado = mensaje.Replace("'", "''");
    string valorEscapado = valorInicial.Replace("'", "''");
    info.ArgumentList.Add("-NoProfile");
    info.ArgumentList.Add("-Command");
    info.ArgumentList.Add(
        "Add-Type -AssemblyName Microsoft.VisualBasic; " +
        $"$r = [Microsoft.VisualBasic.Interaction]::InputBox('{mensajeEscapado}', '{tituloEscapado}', '{valorEscapado}'); " +
        "if ($r -ne '') { Write-Output $r }");
    return info;
}

// como el "Go to File" de VSCode: filtra por nombre de archivo entre todo lo que hay bajo la carpeta abierta
List<(string Nombre, string RutaMostrada, string RutaCompleta)> BuscarArchivosPorNombre(string carpetaRaiz, string consulta)
{
    if (string.IsNullOrEmpty(carpetaRaiz)) return new();

    try
    {
        return Directory.EnumerateFiles(carpetaRaiz, "*", SearchOption.AllDirectories)
            .Where(ruta => Path.GetFileName(ruta).Contains(consulta, StringComparison.OrdinalIgnoreCase))
            .Take(10)
            .Select(ruta => (Path.GetFileName(ruta), Path.GetRelativePath(carpetaRaiz, Path.GetDirectoryName(ruta) ?? carpetaRaiz), ruta))
            .ToList();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"No se pudo recorrer el proyecto: {ex.Message}");
        return new();
    }
}

Raylib.SetConfigFlags(ConfigFlags.ResizableWindow | ConfigFlags.UndecoratedWindow);
Raylib.InitWindow(anchoInicial, altoInicial, "Paradigmas GGEZ");
Raylib.SetWindowMinSize(anchoMinimo, altoMinimo);
Raylib.SetTargetFPS(60);

double inicioCarga = Raylib.GetTime();
DibujarPantallaCarga(0f);

Win95Style.CargarFuente(Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "LiberationSans-Regular.ttf"));

var iconoNuevo = CargarIcono("Icon_Documento.png");
var iconoGuardar = CargarIcono("Icon_NewDocumento.png");
var iconoConstruir = CargarIcono("Icon_Build.png");
var iconoPausa = CargarIcono("Icon_Pausa.png");
var iconoLupa = CargarIcono("Icon_Lupa.png");
var iconoMenu = CargarIcono("Icon_Menu.png");
var iconoDiseno = CargarIcono("Icon_Paint.png");
var iconoCarpetaCerrada = CargarIcono("Icon_CapertaCerrada.png");
var iconoCarpetaAbierta = CargarIcono("Icon_CarpetaAbierta.png");
var iconoTerminal = CargarIcono("Icon_Terminal.png");
var iconoPersonaje = CargarIcono("Icon_Personaje.png");
var iconoPersonaje64px = CargarIcono("Icon_Personaje64px.png");
var iconoPuertaAbierta = CargarIcono("Icon_PuertaAbierta.png");
var iconoPuertaCerrada = CargarIcono("Icon_PuertaCerrada.png");
var iconoDocumentoGgez = CargarIcono("Icon_Documentoggez.png");
var iconoImagenes = CargarIcono("Icon_Imagenes.png");
var iconoDocumentoMd = CargarIcono("Icon_DocumentoMD.png");
var iconoCarpetaNueva = CargarIcono("Icon_CarpetaNueva.png");
var iconoFantasma32 = CargarIcono("Icon_Fantasma32px.png");
var iconoFantasma128 = CargarIcono("Icon_Fantasma128px.png");

// "<" y ">" no tienen glifo en la fuente: se reemplazan por los íconos de puerta
Win95Style.GlifoMenor = iconoPuertaAbierta;
Win95Style.GlifoMayor = iconoPuertaCerrada;

// la carga real es casi instantánea; se fuerza un mínimo para que la pantalla de arranque sea visible
const double duracionMinimaCarga = 3.5;
while (Raylib.GetTime() - inicioCarga < duracionMinimaCarga)
{
    DibujarPantallaCarga((float)((Raylib.GetTime() - inicioCarga) / duracionMinimaCarga));
}

var barraApp = new AppTitleBar { IconoBusqueda = iconoLupa, IconoMenu = iconoMenu, IconoDiseno = iconoDiseno };
var barraMenu = new MenuBar { Bounds = new Rectangle(6, 0, 0, MenuBar.Height) };
barraApp.ZonaExcluidaDelArrastre = barraMenu.AreaOcupada;

// el menú vive dentro de la barra de título (donde antes iba el título de la app), como en VSCode
int yToolbar = AppTitleBar.Height + altoDivisor + margen;

var botonExplorador = new Button(margen, 0, ladoBoton, ladoBoton, "") { Icon = iconoPuertaAbierta, Tooltip = "Mostrar/ocultar explorador" };
var botonNuevo = new Button(margen, 0, ladoBoton, ladoBoton, "") { Icon = iconoNuevo, Tooltip = "Nuevo archivo" };
var botonGuardar = new Button(margen, 0, ladoBoton, ladoBoton, "") { Icon = iconoGuardar, Tooltip = "Guardar" };
var botonConstruir = new Button(margen, 0, ladoBoton, ladoBoton, "") { Icon = iconoConstruir, Tooltip = "Construir" };
var botonPausa = new Button(margen, 0, ladoBoton, ladoBoton, "") { Icon = iconoPausa, Tooltip = "Pausar / Reanudar" };
var botonLupa = new Button(margen, 0, ladoBoton, ladoBoton, "") { Icon = iconoLupa, Tooltip = "Buscar" };
var botonCarpeta = new Button(margen, 0, ladoBoton, ladoBoton, "") { Icon = iconoCarpetaCerrada, Tooltip = "Abrir carpeta" };
var botonCrearCarpeta = new Button(margen, 0, ladoBoton, ladoBoton, "") { Icon = iconoCarpetaNueva, Tooltip = "Crear carpeta" };
var botonTerminal = new Button(margen, 0, ladoBoton, ladoBoton, "") { Icon = iconoTerminal, Tooltip = "Terminal" };

var gruposToolbar = new[]
{
    new[] { botonExplorador },
    new[] { botonNuevo, botonGuardar },
    new[] { botonConstruir, botonPausa },
    new[] { botonLupa, botonCarpeta, botonCrearCarpeta, botonTerminal },
};

int alturaHeader = yToolbar;

var divisoresToolbar = new List<int>();
int yBarraLateral = alturaHeader;
foreach (var grupo in gruposToolbar)
{
    foreach (var boton in grupo)
    {
        boton.Bounds.Y = yBarraLateral;
        yBarraLateral += ladoBoton + espacio;
    }
    yBarraLateral += espacio;
    divisoresToolbar.Add(yBarraLateral);
    yBarraLateral += espacio * 2;
}

int anchoExplorador = anchoExploradorInicial;

// se reposiciona cada cuadro porque depende del ancho actual de la ventana
var botonColumnaDerecha = new Button(0, yToolbar, ladoBoton, ladoBoton, "") { Icon = iconoPuertaAbierta, Tooltip = "Mostrar/ocultar panel derecho" };

var botonesToolbar = gruposToolbar.SelectMany(g => g).Append(botonColumnaDerecha).ToArray();
bool arrastrandoExplorador = false;
int anchoExploradorAlIniciarArrastre = 0;
float mouseXAlIniciarArrastreExplorador = 0f;
bool exploradorOculto = false;
int anchoPanelDerecho = anchoPanelDerechoInicial;
bool arrastrandoPanelDerecho = false;
int anchoPanelDerechoAlIniciarArrastre = 0;
float mouseXAlIniciarArrastrePanelDerecho = 0f;
float anchoExploradorMostrado = anchoExplorador;
var explorador = new ExploradorArchivos
{
    IconoCarpetaAbierta = iconoCarpetaAbierta,
    IconoCarpetaCerrada = iconoCarpetaCerrada,
    IconoDocumento = iconoNuevo,
    IconoDocumentoGgez = iconoDocumentoGgez,
    IconoImagenes = iconoImagenes,
    IconoDocumentoMd = iconoDocumentoMd,
    IconoFantasma = iconoFantasma32,
};
var dialogoModal = new DialogoModal();
var panelCentral = new PanelCentral
{
    IconoDocumento = iconoNuevo,
    IconoDocumentoGgez = iconoDocumentoGgez,
    IconoImagenes = iconoImagenes,
    IconoDocumentoMd = iconoDocumentoMd,
    IconoFantasma = iconoFantasma128,
    Dialogo = dialogoModal,
    ElegirRutaGuardar = ElegirRutaGuardarArchivo,
};

explorador.Dialogo = dialogoModal;
explorador.PedirTexto = PedirTexto;
explorador.ElegirCarpetaBase = ElegirCarpeta;
explorador.OnArchivoRenombrado = panelCentral.ActualizarRutaSiEstaAbierta;

var panelJuego = new PanelJuego(iconoPersonaje64px);
bool columnaDerechaOculta = false;
bool terminalVisible = false;
var barraBusqueda = new BarraBusqueda { Editor = panelCentral };
var cuadroIrALinea = new CuadroIrALinea { Editor = panelCentral };

// se comparte entre el botón de la barra lateral y el menú Archivo > Abrir carpeta
void AbrirCarpetaDialogo()
{
    var rutaElegida = ElegirCarpeta();
    if (rutaElegida != null)
    {
        explorador.AbrirCarpeta(rutaElegida);
        botonCarpeta.Icon = iconoCarpetaAbierta;
        Console.WriteLine($"Carpeta abierta: {rutaElegida}");
    }
}

bool focoEnTerminal = false;
var panelTerminal = new PanelTerminal();

barraMenu.OnOpcionElegida = opcion =>
{
    if (opcion == "Acerca de")
        dialogoModal.Mostrar("Acerca de", "Aqui va la informacion del acerca de", new[] { "Cerrar" }, _ => { });
    else if (opcion == "Sintaxis de GGEZ")
        dialogoModal.Mostrar("Sintaxis", "Aqui va la informacion sobre la sintaxis", new[] { "Cerrar" }, _ => { });
    else if (opcion == "Atajos de teclado")
        dialogoModal.Mostrar("Atajos de teclado", "Aqui va la informacion sobre los atajos de teclado", new[] { "Cerrar" }, _ => { });
    else if (opcion == "Deshacer")
        panelCentral.Deshacer();
    else if (opcion == "Rehacer")
        panelCentral.Rehacer();
    else if (opcion == "Buscar")
        barraBusqueda.Visible = true;
    else if (opcion == "Buscar y reemplazar")
    {
        barraBusqueda.Visible = true;
        barraBusqueda.MostrarReemplazo = true;
    }
    else if (opcion == "Ir a línea")
        cuadroIrALinea.Visible = true;
    else if (opcion == "Explorador")
        exploradorOculto = !exploradorOculto;
    else if (opcion == "Terminal")
        terminalVisible = !terminalVisible;
    else if (opcion == "Panel derecho")
        columnaDerechaOculta = !columnaDerechaOculta;
    else if (opcion == "Nuevo")
        panelCentral.NuevoArchivo();
    else if (opcion == "Abrir carpeta")
        AbrirCarpetaDialogo();
    else if (opcion == "Cerrar carpeta")
        explorador.CerrarCarpeta();
    else if (opcion == "Guardar")
        panelCentral.Guardar();
    else if (opcion == "Guardar como")
        panelCentral.Guardar(forzarElegirRuta: true);
    else if (opcion == "Guardar todo")
        panelCentral.GuardarTodo();
    else if (opcion == "Salir")
        barraApp.Cerrar();
    else if (opcion == "Cortar")
        panelCentral.Cortar();
    else if (opcion == "Copiar")
        panelCentral.Copiar();
    else if (opcion == "Pegar")
        panelCentral.Pegar();
    else if (opcion == "Construir")
    {
        panelCentral.GuardarTodo();
        terminalVisible = true;
        panelTerminal.TabActiva = 1;
    }
    else if (opcion == "Pausar / Reanudar")
        panelJuego.EnPausa = !panelJuego.EnPausa;
    else if (opcion == "Ver salida")
    {
        terminalVisible = true;
        panelTerminal.TabActiva = 1;
    }
    else
        Console.WriteLine($"Menú: {opcion}");
};

explorador.OnAbrirArchivo = nodo =>
{
    panelCentral.AbrirArchivo(nodo.Nombre, nodo.RutaCompleta);
    panelTerminal.RegistrarAperturaArchivo(nodo.RutaCompleta);
};

// "Buscar en el proyecto": lista en vivo (como Ctrl+P en VSCode), Enter o click abre el elegido
barraApp.BuscarArchivos = consulta => BuscarArchivosPorNombre(explorador.CarpetaRaiz, consulta);
barraApp.OnAbrirResultado = ruta =>
{
    panelCentral.AbrirArchivo(Path.GetFileName(ruta), ruta);
    panelTerminal.RegistrarAperturaArchivo(ruta);
};

var barraEstado = new StatusBar();

Button? botonConTooltip = null;
double tiempoInicioHoverTooltip = 0;

while (!Raylib.WindowShouldClose() && !barraApp.ShouldClose)
{
    barraApp.Update();
    dialogoModal.Update();

    bool hayArchivos = panelCentral.HayArchivosAbiertos;
    botonGuardar.Habilitado = hayArchivos;
    botonConstruir.Habilitado = hayArchivos;
    botonPausa.Habilitado = hayArchivos;
    botonLupa.Habilitado = hayArchivos;
    botonExplorador.Icon = exploradorOculto ? iconoPuertaCerrada : iconoPuertaAbierta;
    botonColumnaDerecha.Icon = columnaDerechaOculta ? iconoPuertaCerrada : iconoPuertaAbierta;

    int anchoActual = Raylib.GetScreenWidth();
    int altoActual = Raylib.GetScreenHeight();
    barraEstado.Bounds = new Rectangle(0, altoActual - StatusBar.Height, anchoActual, StatusBar.Height);
    barraEstado.PosicionCursor = panelCentral.HayArchivosAbiertos ? $"Linea {panelCentral.LineaCursor}, Columna {panelCentral.ColumnaCursor}" : "";

    int altoDisponible = altoActual - StatusBar.Height;
    int altoContenido = altoDisponible - alturaHeader - margen * 2 - (terminalVisible ? altoTerminal + margen : 0);

    float objetivoAnchoExplorador = exploradorOculto ? anchoExploradorColapsado : anchoExplorador;
    float pasoColapsoExplorador = velocidadColapsoExplorador * Raylib.GetFrameTime();
    anchoExploradorMostrado = MathF.Abs(objetivoAnchoExplorador - anchoExploradorMostrado) <= pasoColapsoExplorador
        ? objetivoAnchoExplorador
        : anchoExploradorMostrado + MathF.Sign(objetivoAnchoExplorador - anchoExploradorMostrado) * pasoColapsoExplorador;

    botonColumnaDerecha.Bounds.X = anchoActual - margen - ladoBoton;

    explorador.Bounds = new Rectangle(anchoBarraLateral + margen, alturaHeader + margen, anchoExploradorMostrado, altoContenido);

    int xCentral = anchoBarraLateral + margen + (int)anchoExploradorMostrado + anchoManijaResize;
    int xPanelDerecho = anchoActual - margen - anchoPanelDerecho;

    // arrancan más abajo que el explorador para dejarle espacio al botón de alternar el panel derecho
    int yColumnaDerecha = alturaHeader + margen + altoBotonColumnaDerecha;
    int alturaColumnaDerecha = altoContenido - espacioPanelDerecho - altoBotonColumnaDerecha;

    panelJuego.Bounds = new Rectangle(xPanelDerecho, yColumnaDerecha, anchoPanelDerecho, alturaColumnaDerecha);

    // nunca baja del mínimo (para que las pestañas no se dibujen fuera de sus bordes) ni pasa el botón derecho
    float xColumnaDerechaActual = Math.Min(panelJuego.BordeIzquierdoActual, botonColumnaDerecha.Bounds.X - margen);
    float anchoPanelCentral = Math.Max(anchoPanelCentralMinimo, xColumnaDerechaActual - espacioPanelDerecho - xCentral);
    panelCentral.Bounds = new Rectangle(xCentral, alturaHeader + margen, anchoPanelCentral, altoContenido);
    barraBusqueda.Posicionar(panelCentral.Bounds);
    cuadroIrALinea.Posicionar(panelCentral.Bounds);

    if (terminalVisible)
        panelTerminal.Bounds = new Rectangle(anchoBarraLateral + margen, explorador.Bounds.Y + explorador.Bounds.Height + margen, anchoActual - anchoBarraLateral - margen * 2, altoTerminal);
    else
        focoEnTerminal = false; // si la terminal está cerrada, el teclado siempre es del editor

    var manijaResizeExplorador = new Rectangle(explorador.Bounds.X + explorador.Bounds.Width, explorador.Bounds.Y, anchoManijaResize, explorador.Bounds.Height);
    bool mouseSobreManija = !exploradorOculto && Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), manijaResizeExplorador);

    var manijaResizePanelDerecho = new Rectangle(xPanelDerecho - anchoManijaResize, alturaHeader + margen, anchoManijaResize, altoContenido);
    bool mouseSobreManijaPanelDerecho = !columnaDerechaOculta && Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), manijaResizePanelDerecho);

    if (!dialogoModal.Visible && !barraApp.ResultadosVisibles)
    {
        // si el menú ya estaba abierto, este click es para elegirlo/cerrarlo, no para lo de abajo
        bool clickParaElMenu = barraMenu.MenuAbierto && Raylib.IsMouseButtonPressed(MouseButton.Left);
        barraMenu.Update();

        if (!clickParaElMenu)
        {
            if (botonNuevo.IsClicked()) panelCentral.NuevoArchivo();
            if (botonGuardar.IsClicked()) panelCentral.Guardar();
            if (botonConstruir.IsClicked())
            {
                panelCentral.GuardarTodo();
                terminalVisible = true;
                panelTerminal.TabActiva = 1;
            }
            if (botonPausa.IsClicked()) panelJuego.EnPausa = !panelJuego.EnPausa;
            if (botonLupa.IsClicked()) barraBusqueda.Visible = true;
            if (botonCarpeta.IsClicked()) AbrirCarpetaDialogo();
            if (botonCrearCarpeta.IsClicked()) explorador.CrearCarpeta();
            if (botonTerminal.IsClicked())
            {
                terminalVisible = !terminalVisible;
                Console.WriteLine(terminalVisible ? "Abrir terminal" : "Cerrar terminal");
            }
            if (botonExplorador.IsClicked()) exploradorOculto = !exploradorOculto;
            if (botonColumnaDerecha.IsClicked()) columnaDerechaOculta = !columnaDerechaOculta;

            if (!exploradorOculto) explorador.Update();

            panelJuego.Oculto = columnaDerechaOculta;
            panelJuego.Update();

            if (Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                if (Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), panelCentral.Bounds)) focoEnTerminal = false;
                else if (terminalVisible && Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), panelTerminal.Bounds)) focoEnTerminal = true;
            }

            panelCentral.TieneFoco = !focoEnTerminal && !barraBusqueda.TieneFocoEnCampo && !cuadroIrALinea.TieneFocoEnCampo && !barraApp.TieneFocoEnCampo;
            panelCentral.BloquearClickTexto = (barraBusqueda.Visible && Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), barraBusqueda.Bounds))
                || (cuadroIrALinea.Visible && Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), cuadroIrALinea.Bounds));
            panelCentral.Update();
            barraBusqueda.Update();
            cuadroIrALinea.Update();

            if (terminalVisible)
            {
                panelTerminal.TieneFoco = focoEnTerminal;
                panelTerminal.Update();
            }

            if (!arrastrandoExplorador && mouseSobreManija && Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                arrastrandoExplorador = true;
                anchoExploradorAlIniciarArrastre = anchoExplorador;
                mouseXAlIniciarArrastreExplorador = Raylib.GetMousePosition().X;
            }
            if (arrastrandoExplorador && !Raylib.IsMouseButtonDown(MouseButton.Left))
            {
                arrastrandoExplorador = false;
            }
            if (arrastrandoExplorador)
            {
                float deltaX = Raylib.GetMousePosition().X - mouseXAlIniciarArrastreExplorador;
                anchoExplorador = Math.Clamp(anchoExploradorAlIniciarArrastre + (int)deltaX, anchoExploradorMinimo, anchoExploradorMaximo);
            }

            // borde izquierdo de una columna anclada a la derecha: arrastrar a la izquierda la agranda (signo invertido)
            if (!arrastrandoPanelDerecho && mouseSobreManijaPanelDerecho && Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                arrastrandoPanelDerecho = true;
                anchoPanelDerechoAlIniciarArrastre = anchoPanelDerecho;
                mouseXAlIniciarArrastrePanelDerecho = Raylib.GetMousePosition().X;
            }
            if (arrastrandoPanelDerecho && !Raylib.IsMouseButtonDown(MouseButton.Left))
            {
                arrastrandoPanelDerecho = false;
            }
            if (arrastrandoPanelDerecho)
            {
                float deltaXPanelDerecho = Raylib.GetMousePosition().X - mouseXAlIniciarArrastrePanelDerecho;
                anchoPanelDerecho = Math.Clamp(anchoPanelDerechoAlIniciarArrastre - (int)deltaXPanelDerecho, anchoPanelDerechoMinimo, anchoPanelDerechoMaximo);
            }

            Raylib.SetMouseCursor(mouseSobreManija || arrastrandoExplorador || mouseSobreManijaPanelDerecho || arrastrandoPanelDerecho ? MouseCursor.ResizeEw : MouseCursor.Default);

            var mouse = Raylib.GetMousePosition();
            Button? botonHover = Array.Find(botonesToolbar, b => b.Tooltip != null && Raylib.CheckCollisionPointRec(mouse, b.Bounds));
            if (botonHover != botonConTooltip)
            {
                botonConTooltip = botonHover;
                tiempoInicioHoverTooltip = Raylib.GetTime();
            }
        }
    }
    else
    {
        Raylib.SetMouseCursor(MouseCursor.Default);
        botonConTooltip = null;
    }

    Raylib.BeginDrawing();
    Raylib.ClearBackground(Win95Style.Background);

    foreach (var boton in botonesToolbar)
    {
        boton.Draw();
    }
    foreach (var yDivisor in divisoresToolbar)
    {
        Win95Style.DrawDivisorHorizontal(margen, yDivisor, ladoBoton);
    }
    Win95Style.DrawDivisorVertical(anchoBarraLateral, alturaHeader, altoActual - alturaHeader - StatusBar.Height);

    if (exploradorOculto)
        Raylib.DrawRectangleRec(explorador.Bounds, Win95Style.Background);
    else
        explorador.Draw();

    int xDivisorCentro = (int)(explorador.Bounds.X + explorador.Bounds.Width + anchoManijaResize / 2);
    Win95Style.DrawDivisorVertical(xDivisorCentro, (int)explorador.Bounds.Y, (int)explorador.Bounds.Height);

    panelCentral.Draw();

    int xDivisorDerecho = (int)(panelCentral.Bounds.X + panelCentral.Bounds.Width + espacioPanelDerecho / 2);
    Win95Style.DrawDivisorVertical(xDivisorDerecho, (int)panelCentral.Bounds.Y, (int)panelCentral.Bounds.Height);

    panelJuego.Draw();

    if (terminalVisible)
    {
        int yDivisorTerminal = (int)(explorador.Bounds.Y + explorador.Bounds.Height + margen / 2);
        Win95Style.DrawDivisorHorizontal(anchoBarraLateral, yDivisorTerminal, anchoActual - anchoBarraLateral);

        panelTerminal.Draw();
    }

    barraEstado.Draw();
    barraApp.Draw();
    // se dibuja después de la barra de título para quedar sobre su degradado
    barraMenu.Draw();
    Win95Style.DrawDivisorHorizontal(0, AppTitleBar.Height, anchoActual);

    // al final, para quedar encima del explorador/editor/terminal
    barraMenu.DrawMenuAbierto();
    explorador.DrawMenuContextual();

    barraBusqueda.Draw();
    cuadroIrALinea.Draw();
    barraApp.DrawResultadosBusqueda();

    const double retrasoTooltip = 0.5;
    if (botonConTooltip?.Tooltip != null && Raylib.GetTime() - tiempoInicioHoverTooltip > retrasoTooltip)
    {
        // si el botón está en la mitad derecha, el tooltip cuelga hacia la izquierda para no salirse
        bool enMitadDerecha = botonConTooltip.Bounds.X + botonConTooltip.Bounds.Width / 2f > anchoActual / 2f;
        int anchoTooltip = Win95Style.MeasureText(botonConTooltip.Tooltip, 14) + 8;
        int xTooltip = enMitadDerecha
            ? (int)(botonConTooltip.Bounds.X + botonConTooltip.Bounds.Width) - anchoTooltip
            : (int)botonConTooltip.Bounds.X;
        Win95Style.DrawTooltip(botonConTooltip.Tooltip, xTooltip, (int)(botonConTooltip.Bounds.Y + botonConTooltip.Bounds.Height + 4));
    }

    // lo último, para bloquear visualmente el resto de la ventana
    dialogoModal.Draw();

    Raylib.EndDrawing();
}

Raylib.UnloadTexture(iconoNuevo);
Raylib.UnloadTexture(iconoGuardar);
Raylib.UnloadTexture(iconoConstruir);
Raylib.UnloadTexture(iconoPausa);
Raylib.UnloadTexture(iconoLupa);
Raylib.UnloadTexture(iconoMenu);
Raylib.UnloadTexture(iconoDiseno);
Raylib.UnloadTexture(iconoCarpetaCerrada);
Raylib.UnloadTexture(iconoCarpetaAbierta);
Raylib.UnloadTexture(iconoTerminal);
Raylib.UnloadTexture(iconoPersonaje);
Raylib.UnloadTexture(iconoPuertaAbierta);
Raylib.UnloadTexture(iconoPuertaCerrada);
Raylib.UnloadTexture(iconoDocumentoGgez);
Raylib.UnloadTexture(iconoImagenes);
Raylib.UnloadTexture(iconoDocumentoMd);
Raylib.UnloadTexture(iconoCarpetaNueva);
Raylib.UnloadTexture(iconoFantasma32);
Raylib.UnloadTexture(iconoFantasma128);
Raylib.UnloadFont(Win95Style.Fuente);
Raylib.CloseWindow();