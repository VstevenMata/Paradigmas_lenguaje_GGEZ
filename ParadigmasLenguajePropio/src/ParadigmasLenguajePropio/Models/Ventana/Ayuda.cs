namespace ParadigmasLenguajePropio.Models;

public enum TipoFila { Atajo, Palabra, Ejemplo }

// Atajo: teclas + accion. Palabra: termino GGEZ + significado. Ejemplo: titulo + explicacion + codigo GGEZ y su equivalente en Java
// Java es la columna derecha; con DerechaEsGgez muestra otro archivo GGEZ en vez del equivalente
public record FilaAyuda(TipoFila Tipo, string Izquierda, string Derecha, string? Codigo = null, string? Java = null,
    string EtiquetaCodigo = "GGEZ", string EtiquetaJava = "EQUIVALE EN JAVA", bool DerechaEsGgez = false)
{
    public bool DerechaEsJava => TieneJava && !DerechaEsGgez;
    public bool DerechaMuestraGgez => TieneJava && DerechaEsGgez;
    public IReadOnlyList<string> Teclas => Izquierda.Split(" / ");
    public bool EsAtajo => Tipo == TipoFila.Atajo;
    public bool EsPalabra => Tipo == TipoFila.Palabra;
    public bool EsEjemplo => Tipo == TipoFila.Ejemplo;
    public bool TieneJava => Java != null;
}

public record SeccionAyuda(string Titulo, IReadOnlyList<FilaAyuda> Filas, string? Intro = null)
{
    public bool TieneIntro => Intro != null;
}

public static class Ayuda
{
    static FilaAyuda A(string teclas, string accion) => new(TipoFila.Atajo, teclas, accion);
    static FilaAyuda P(string termino, string significado) => new(TipoFila.Palabra, termino, significado);
    static FilaAyuda E(string titulo, string explicacion, string ggez, string? java = null) => new(TipoFila.Ejemplo, titulo, explicacion, ggez, java);

    public static readonly IReadOnlyList<SeccionAyuda> Atajos =
    [
        new("Archivo",
        [
            A("Ctrl+N", "Nuevo archivo"),
            A("Ctrl+O", "Abrir carpeta"),
            A("Ctrl+S", "Guardar"),
            A("Ctrl+Shift+S", "Guardar como"),
            A("Ctrl+Alt+S", "Guardar todo"),
        ]),
        new("Edición",
        [
            A("Ctrl+Z", "Deshacer"),
            A("Ctrl+Y / Ctrl+Shift+Z", "Rehacer"),
            A("Ctrl+X / Ctrl+C / Ctrl+V", "Cortar, copiar y pegar"),
            A("Ctrl+A", "Seleccionar todo"),
            A("Alt+Arriba / Alt+Abajo", "Mover la línea arriba o abajo"),
            A("Enter", "Después de glhf, cierra el bloque con gg (Bracket Colorizer)"),
            A("Enter / Tab", "Acepta la sugerencia de palabra GGEZ (Esc la cierra)"),
        ]),
        new("Buscar y navegar",
        [
            A("Ctrl+F", "Buscar en el archivo"),
            A("Ctrl+H", "Buscar y reemplazar"),
            A("Ctrl+G", "Ir a una línea"),
        ]),
        new("Vista",
        [
            A("Ctrl+B", "Mostrar u ocultar el explorador"),
            A("Ctrl+J", "Mostrar u ocultar la terminal"),
            A("Ctrl++ / Ctrl+- / Ctrl+0", "Acercar, alejar y restablecer zoom (también Ctrl+rueda)"),
        ]),
        new("Ejecutar y ayuda",
        [
            A("F5", "Construir y ejecutar el .ggez abierto"),
            A("F1", "Mostrar estos atajos"),
        ]),
        new("Terminal",
        [
            A("Ctrl+C", "Interrumpir el proceso"),
            A("Ctrl+Shift+C", "Copiar toda la salida"),
            A("Ctrl+V", "Pegar"),
            A("Ctrl+L", "Limpiar la pantalla"),
            A("Ctrl+D / Ctrl+Z", "Enviar fin de entrada"),
            A("Ctrl+U / Ctrl+W", "Borrar hasta el inicio / la palabra anterior"),
            A("Ctrl+A / Ctrl+E", "Ir al inicio / fin de la línea"),
            A("Tab", "Autocompletar"),
            A("Arriba / Abajo", "Recorrer el historial de comandos"),
            A("RePág / AvPág", "Desplazar la salida"),
            A("Ctrl+Inicio / Ctrl+Fin", "Ir al principio / final de la salida"),
        ]),
    ];

    public static readonly IReadOnlyList<SeccionAyuda> Sintaxis =
    [
        new("1. Diccionario: cómo leer GGEZ",
        [
            P("glhf ... gg", "Abren y cierran un bloque. Son las llaves { } de Java."),
            P("{ }", "Son los paréntesis ( ) de Java: agrupan cálculos y encierran argumentos."),
            P("|", "Separa argumentos y elementos. Es la coma de Java: sumar{2 | 3} = sumar(2, 3)."),
            P("< >", "Son los corchetes [ ] de Java: lista<0> = lista[0]."),
            P("set", "Asigna un valor. Es el = de Java."),
            P("salto de línea", "Termina la sentencia. No se usa punto y coma."),
            P("/w", "Comentario de una línea, como // en Java. /all ... /end es un bloque."),
        ], "Casi todo en GGEZ es una palabra de videojuegos en lugar de un símbolo. Con esta tabla se puede leer cualquier ejemplo."),

        new("2. Tipos de datos",
        [
            P("score", "Número entero (int). Ej: 42"),
            P("aim", "Número decimal (double). Ej: 3.5"),
            P("lore", "Texto (String). Ej: \"Hola\""),
            P("pixel", "Un solo carácter (char). Ej: 'a'"),
            P("win", "Verdadero o falso (boolean): hit = true, miss = false"),
            P("afk", "Sin valor (null)"),
            P("squad[score]", "Lista de enteros (ArrayList<Integer>)"),
        ]),

        new("3. Operadores",
        [
            P("gain  drain", "Suma y resta: + y -. gain también une textos: \"Hola \" gain nombre"),
            P("crit  split  scrap", "Multiplicación, división y resto: * / %. 7 split 2 da 3; 7 split 2.0 da 3.5"),
            P("matches  mismatch", "Igual y distinto: == y !="),
            P("over  under", "Mayor y menor: > y <"),
            P("atleast  atmost", "Mayor o igual y menor o igual: >= y <="),
            P("chain  swap  dodge", "Y, o, no: && || !"),
            P("boost  sap", "Sumar o restar a la variable: += y -="),
            P("buff  nerf", "Sumar o restar uno: ++ y --"),
        ]),

        new("4. Primer programa",
        [
            E("Hola mundo", "Todo programa arranca en lobby.launch. Lo de adentro del bloque es lo que se ejecuta.",
                "lobby.launch{start THE GAME} glhf\n    chat.send{\"Hola mundo\"}\ngg",
                "public static void main(String[] args) {\n    System.out.println(\"Hola mundo\");\n}"),
            E("Variables", "stash declara una variable; siempre se escribe el tipo. hardcoded declara una constante.",
                "stash score vidas set 3\nstash lore nombre set \"Ana\"\nhardcoded score MAX set 10\nvidas boost 2      /w vidas vale 5",
                "int vidas = 3;\nString nombre = \"Ana\";\nfinal int MAX = 10;\nvidas += 2;"),
            E("Imprimir", "chat.send muestra un valor en la terminal. chat.rage lo muestra como error.",
                "chat.send{\"Hola \" gain nombre}   /w Hola Ana\nchat.rage{\"Algo falló\"}",
                "System.out.println(\"Hola \" + nombre);\nSystem.err.println(\"Algo falló\");"),
        ]),

        new("5. Decisiones",
        [
            E("If / else", "scan es el if y fallback es el else. El fallback va pegado al gg que cierra el scan.",
                "scan vidas over 0 glhf\n    chat.send{\"Sigues vivo\"}\ngg fallback glhf\n    chat.send{\"Game over\"}\ngg",
                "if (vidas > 0) {\n    System.out.println(\"Sigues vivo\");\n} else {\n    System.out.println(\"Game over\");\n}"),
            E("Else if", "Para encadenar condiciones se escribe fallback scan.",
                "scan nota atleast 90 glhf\n    chat.send{\"A\"}\ngg fallback scan nota atleast 70 glhf\n    chat.send{\"B\"}\ngg fallback glhf\n    chat.send{\"C\"}\ngg",
                "if (nota >= 90) {\n    System.out.println(\"A\");\n} else if (nota >= 70) {\n    System.out.println(\"B\");\n} else {\n    System.out.println(\"C\");\n}"),
            E("Switch", "loadout es el switch, pick es cada case y noob es el default. No hace falta break: cada pick termina en su gg.",
                "loadout opcion glhf\n    pick 1 glhf\n        chat.send{\"Jugar\"}\n    gg\n    pick 2 | 3 glhf\n        chat.send{\"Opciones\"}\n    gg\n    noob glhf\n        chat.send{\"Salir\"}\n    gg\ngg",
                "switch (opcion) {\n    case 1:\n        System.out.println(\"Jugar\"); break;\n    case 2: case 3:\n        System.out.println(\"Opciones\"); break;\n    default:\n        System.out.println(\"Salir\");\n}"),
        ]),

        new("6. Ciclos",
        [
            E("For", "farm tiene tres partes separadas por |: inicio | condición | paso. La variable se declara antes.",
                "stash score i set 0\nfarm i set 0 | i under 3 | i buff glhf\n    chat.send{i}      /w 0, 1, 2\ngg",
                "for (int i = 0; i < 3; i++) {\n    System.out.println(i);\n}"),
            E("While", "survive repite mientras la condición sea verdadera.",
                "survive vidas over 0 glhf\n    vidas nerf\ngg",
                "while (vidas > 0) {\n    vidas--;\n}"),
            E("Do-while", "grind corre el bloque al menos una vez y la condición va al final, entre llaves.",
                "grind glhf\n    vidas nerf\ngg survive {vidas over 0}",
                "do {\n    vidas--;\n} while (vidas > 0);"),
            E("Break / continue", "ragequit sale del ciclo; respawn salta a la siguiente vuelta.",
                "farm i set 0 | i under 5 | i buff glhf\n    scan i matches 1 glhf\n        respawn\n    gg\n    scan i matches 3 glhf\n        ragequit\n    gg\n    chat.send{i}      /w 0, 2\ngg",
                "for (i = 0; i < 5; i++) {\n    if (i == 1) continue;\n    if (i == 3) break;\n    System.out.println(i);\n}"),
        ]),

        new("7. Funciones",
        [
            E("Función con resultado", "combo declara una función; drops dice qué tipo devuelve y reward es el return. Se escriben fuera de lobby.launch.",
                "combo sumar{score a | score b} drops score glhf\n    reward a gain b\ngg\n\nchat.send{sumar{2 | 3}}      /w 5",
                "static int sumar(int a, int b) {\n    return a + b;\n}\n\nSystem.out.println(sumar(2, 3));"),
            E("Función sin resultado", "drops noloot significa que no devuelve nada (void).",
                "combo saludar{lore nombre} drops noloot glhf\n    chat.send{\"Hola \" gain nombre}\ngg\n\nsaludar{\"Luis\"}      /w Hola Luis",
                "static void saludar(String nombre) {\n    System.out.println(\"Hola \" + nombre);\n}\n\nsaludar(\"Luis\");"),
        ]),

        new("8. Listas",
        [
            E("Crear y usar una lista", "squad[tipo] es una lista. Los elementos van entre < > separados por |, y el índice empieza en 0.",
                "stash squad[score] notas set <90 | 75 | 60>\nnotas.recruit{100}     /w agrega al final\nchat.send{notas<0>}    /w 90\nchat.send{notas.size}  /w 4\nnotas.kick{1}          /w quita el de la posición 1",
                "var notas = new ArrayList<>(List.of(90, 75, 60));\nnotas.add(100);\nSystem.out.println(notas.get(0));\nSystem.out.println(notas.size());\nnotas.remove(1);"),
        ]),

        new("9. Errores",
        [
            E("Try / catch / finally", "clutch intenta, revive atrapa el error en e y checkpoint corre siempre. wipe lanza un error; e.reason es su mensaje.",
                "clutch glhf\n    wipe{\"algo salió mal\"}\ngg revive {e} glhf\n    chat.rage{e.reason}\ngg checkpoint glhf\n    chat.send{\"siempre corre\"}\ngg",
                "try {\n    throw new Exception(\"algo salió mal\");\n} catch (Exception e) {\n    System.err.println(e.getMessage());\n} finally {\n    System.out.println(\"siempre corre\");\n}"),
        ]),

        new("10. Objetos",
        [
            E("Registro", "inventory agrupa datos sin métodos. spawn crea uno nuevo, pasando los valores en orden.",
                "inventory Punto glhf\n    score x\n    score y\ngg\n\nstash Punto p set spawn Punto{3 | 4}\nchat.send{p.x}      /w 3",
                "record Punto(int x, int y) {}\n\nPunto p = new Punto(3, 4);\nSystem.out.println(p.x());"),
            E("Clase", "guild es una clase. El combo con el mismo nombre del guild es el constructor. solo = private, party = protected, open = public.",
                "guild Perro glhf\n    solo lore nombre\n    combo Perro{lore n} drops noloot glhf\n        nombre set n\n    gg\n    combo ladrar{} drops noloot glhf\n        chat.send{nombre gain \" dice guau\"}\n    gg\ngg\n\nstash Perro toby set spawn Perro{\"Toby\"}\ntoby.ladrar{}      /w Toby dice guau",
                "class Perro {\n    private String nombre;\n    Perro(String n) {\n        nombre = n;\n    }\n    void ladrar() {\n        System.out.println(nombre + \" dice guau\");\n    }\n}\n\nPerro toby = new Perro(\"Toby\");\ntoby.ladrar();"),
            E("Herencia", "evolves es extends. El hijo puede reemplazar un método del padre con la misma firma. me es this (opcional).",
                "guild Animal glhf\n    party lore nombre\n    combo hablar{} drops lore glhf\n        reward \"...\"\n    gg\ngg\n\nguild Gato evolves Animal glhf\n    combo Gato{lore n} drops noloot glhf\n        nombre set n\n    gg\n    combo hablar{} drops lore glhf\n        reward nombre gain \" dice miau\"\n    gg\ngg",
                "class Animal {\n    protected String nombre;\n    String hablar() { return \"...\"; }\n}\n\nclass Gato extends Animal {\n    Gato(String n) { nombre = n; }\n    String hablar() {\n        return nombre + \" dice miau\";\n    }\n}"),
        ]),

        new("11. Usar otro archivo",
        [
            E("Importar", "travel trae otro archivo .ggez. from base = la misma carpeta; from lib = la carpeta lib. Lo importado se usa con el nombre del archivo delante.",
                "travel estadisticas from lib\n\nchat.send{estadisticas.promedio{notas}}",
                "import lib.Estadisticas;\n\nSystem.out.println(Estadisticas.promedio(notas));"),
            new(TipoFila.Ejemplo, "Ejemplo con dos archivos",
                "La biblioteca no lleva lobby.launch. Adentro de ella sus tipos y funciones se usan a secas; desde afuera van con el nombre del archivo delante, también los tipos.",
                "travel geometria from lib\n\nlobby.launch{start THE GAME} glhf\n    stash geometria.Rectangulo r set spawn geometria.Rectangulo{3 | 4}\n    chat.send{geometria.area{r}}      /w 12\ngg",
                "/w Sin lobby.launch: es una biblioteca\ninventory Rectangulo glhf\n    score ancho\n    score alto\ngg\n\ncombo area{Rectangulo r} drops score glhf\n    reward r.ancho crit r.alto\ngg",
                "principal.ggez", "lib/geometria.ggez", DerechaEsGgez: true),
            P("Ruta", "Es relativa al archivo que importa, no a la carpeta abierta en el IDE."),
            P("Una sola vez", "Cada archivo se carga una vez aunque varios lo importen."),
            P("En círculo", "Si a importa b y b importa a, da error: importacion en circulo: a.ggez -> b.ggez -> a.ggez"),
            P("Errores", "Un error dentro del archivo importado se reporta con el nombre y la línea de ese archivo."),
        ]),
    ];
}
