# Gramática formal de GGEZ

Universidad Nacional · EIF400 Paradigmas de Programación · II ciclo 2026
Maikel Segura Quirós · Angel Ureña Naranjo · Víctor Steven Mata Quirós

Basado en `Propuesta de Lenguaje GGEZ.docx`. Los huecos que el documento dejó
abiertos se resolvieron aquí con un valor por defecto; **cada uno está marcado con
`[D-n]` y listado al final** para que el equipo lo confirme o lo cambie.

---

## 0. Notación

Se usa la notación BNF/EBNF de `Problemas de traduccion de lenguajes.pdf`:

| Símbolo | Significado |
|---|---|
| `::=` | "se define como" |
| `\|` | alternativa ("o") |
| `<...>` | símbolo no terminal |
| `"..."` | símbolo terminal (obligatorio) |
| `[...]` | ítem opcional (0 o 1 vez) |
| `{...}*` | ítem que se repite 0 o más veces |

> **Cuidado con la colisión de notación.** GGEZ usa `<`, `>`, `[`, `]`, `{` y `}`
> como delimitadores propios. Para no confundirlos, **todo terminal de GGEZ va
> siempre entre comillas dobles**. Es decir: `<expresion>` es un no terminal,
> `"<"` es el símbolo menor-que del lenguaje.

---

## 1. Elementos léxicos

### 1.1 Conjunto de caracteres e identificadores

```
<letra>         ::= "a" | ... | "z" | "A" | ... | "Z"
<digito>        ::= "0" | "1" | "2" | "3" | "4" | "5" | "6" | "7" | "8" | "9"
<identificador> ::= ( <letra> | "_" ) { <letra> | <digito> | "_" }*
```

Sin tildes ni `ñ`, igual que Pascal. El lenguaje **distingue mayúsculas de
minúsculas** y todas las palabras reservadas van en minúscula. `[D-6]` `[D-7]`

### 1.2 Literales

```
<literal>          ::= <entero> | <real> | <cadena> | <caracter>
                     | "hit" | "miss" | "afk"

<entero>           ::= <digito> { <digito> }*
<real>             ::= <digito> { <digito> }* "." <digito> { <digito> }*
<cadena>           ::= '"' { <car-cadena> }* '"'
<caracter>         ::= "'" <car-cadena> "'"
<car-cadena>       ::= cualquier carácter excepto el delimitador y "\"
                     | "\n" | "\t" | "\\" | "\"" | "\'"
```

El punto decimal obliga a una regla en el analizador léxico: un `.` solo forma
parte de un número si viene **precedido y seguido de dígito**. En cualquier otro
caso es el operador de acceso a miembro (`p1.nombre`). `[D-2]` `[D-5]`

### 1.3 Comentarios

```
/w  esto es un comentario de una línea
/all
    esto es un comentario
    de varias líneas
/end
```

El analizador léxico los descarta; no llegan al analizador sintáctico.

### 1.4 Fin de sentencia

GGEZ **no usa `;`**: el salto de línea cierra la sentencia. El analizador léxico
emite un token `FIN_LINEA` por cada salto, con dos excepciones:

1. Se descartan los saltos de línea **en blanco** (varios seguidos cuentan como uno).
2. Se **ignora** el salto de línea mientras haya un `{` o un `<` sin cerrar, lo que
   permite partir una llamada o un literal de colección en varias líneas. `[D-3]`

```
<fin-linea> ::= FIN_LINEA { FIN_LINEA }* | &"gg" | &FIN_ARCHIVO
```

Un `"gg"` también cierra la sentencia **sin consumirse** (eso es lo que marca el
`&`), y lo mismo el final del archivo. Sin esa regla no se podría escribir un
bloque entero en una línea, y el ejemplo de la Propuesta de Lenguaje lo hace:
`pick 10 | 9 glhf reward "Pro" gg`. `[D-19]`

### 1.5 Palabras reservadas

Las 60 palabras reservadas, agrupadas como en el documento original:

| Grupo | Palabras |
|---|---|
| Estructura | `lobby` `launch` `start` `THE` `GAME` `travel` `from` `server` `region` `quit` |
| Declaración | `stash` `hardcoded` `noloot` `afk` |
| Tipos | `score` `aim` `lore` `win` `pixel` `squad` `inventory` (más `stack` y `queue`, reservadas pero fuera del lenguaje, ver `[D-21]`) |
| Booleanos | `hit` `miss` |
| Flujo | `scan` `fallback` `farm` `survive` `grind` `loadout` `pick` `noob` `ragequit` `respawn` |
| Funciones | `combo` `reward` `drops` |
| Errores | `clutch` `revive` `checkpoint` `wipe` |
| POO | `guild` `questline` `evolves` `spawn` `me` `open` `party` `solo` |
| Bloques | `glhf` `gg` |
| Operadores | `gain` `drain` `crit` `split` `scrap` `matches` `mismatch` `over` `under` `atleast` `atmost` `set` `boost` `sap` `buff` `nerf` `chain` `swap` `dodge` |

`chain`, `swap` y `dodge` (AND, OR, NOT) **no estaban en el documento** y se
agregan aquí porque sin ellas no se puede escribir una condición compuesta. `[D-1]`

### 1.6 Delimitadores

Respetando la tabla de remapeo del documento:

| Rol | En otros lenguajes | En GGEZ |
|---|---|---|
| Bloque | `{ }` | `glhf` … `gg` |
| Agrupación, parámetros, llamadas | `( )` | `{ }` |
| Índices y literales de colección | `[ ]` | `< >` |
| Genéricos | `< >` | `[ ]` |
| Separador de elementos | `,` | `\|` |
| Fin de sentencia | `;` | salto de línea |

> **Por qué esto no genera ambigüedad.** Los operadores relacionales de GGEZ son
> palabras (`over`, `under`, `atleast`, `atmost`), no símbolos. Eso libera `<` y
> `>` por completo, y de paso elimina la ambigüedad clásica de `>>` al cerrar dos
> genéricos anidados que sí sufren C++ y Java.

---

## 2. Estructura del programa

```
<programa> ::= { <fin-linea> } { <decl-superior> }*

<decl-superior> ::= <decl-travel>
                  | <decl-guild>
                  | <decl-questline>
                  | <decl-inventory>
                  | <decl-combo>
                  | <punto-entrada>

<decl-travel>   ::= "travel" <identificador> "from" <identificador> <fin-linea>

<punto-entrada> ::= "lobby" "." "launch" "{" "start" "THE" "GAME" "}" <bloque>
```

`start THE GAME` es **texto fijo obligatorio**, no son parámetros. `[D-8]`
Debe existir exactamente un `<punto-entrada>` en el programa que se ejecuta; un
archivo sin él es válido como biblioteca importable con `travel`. `[D-13]`

### 2.1 Importar otros archivos

`travel X from Y` trae el archivo `X.ggez`. `Y` dice dónde buscarlo, siempre
**relativo a la carpeta del archivo que importa**: `[D-28]`

| Se escribe | Busca |
|---|---|
| `travel utilidades from base` | `utilidades.ggez`, en la misma carpeta |
| `travel utilidades from lib` | `lib/utilidades.ggez` |
| `travel physics from arsenal` | la librería estándar, no un archivo |

Lo importado se usa con el nombre del módulo delante, igual que `chat.send`:

```
travel utilidades from lib

lobby.launch{start THE GAME} glhf
    chat.send{utilidades.promedio{<1 | 2 | 3>}}
    stash utilidades.Jugador j set spawn utilidades.Jugador{"Angel"}
gg
```

Por eso `<tipo>`, `spawn` y `evolves` aceptan un nombre con módulo
(`<nombre-tipo>`, sección 3). Un archivo exporta todos sus `combo`, `guild` e
`inventory`. Para usar un tipo de un módulo hay que importar ese módulo en el
mismo archivo: no alcanza con que lo haya importado otro. `[D-28]`

Reglas:

- Cada archivo se carga **una sola vez**, aunque varios lo importen.
- Un archivo importado **no puede tener** `lobby.launch`: es una biblioteca. `[D-29]`
- Importar en círculo (`a` trae `b` y `b` trae `a`) es error.
- Dos módulos distintos no pueden llamarse igual, ni un módulo llamarse como uno
  de la librería (`chat`, `physics`, ...).
- `server` y `region` estaban en la Propuesta de Lenguaje pero nunca se definió
  para qué sirven: quedan fuera de la versión 1 y dan un error claro, como
  `questline`. `[D-30]`

### 2.2 Bloques

```
<bloque> ::= "glhf" [ <fin-linea> ] { <sentencia> }* "gg"
```

Todo bloque abre con `glhf` y cierra con `gg`, siempre el par completo, aunque
contenga una sola instrucción. La indentación es estética, no significativa.

El salto de línea después de `glhf` es **opcional**: junto con la regla `[D-19]`
de §1.4, eso permite las dos formas.

```
scan nota atleast 90 glhf              pick 10 | 9 glhf reward "Pro" gg
    chat.send{"Pro"}
gg
```

---

## 3. Tipos

```
<tipo>            ::= <tipo-simple> | <tipo-coleccion> | <nombre-tipo>
<nombre-tipo>     ::= <identificador> [ "." <identificador> ]

<tipo-simple>     ::= "score" | "aim" | "lore" | "win" | "pixel"
<tipo-coleccion>  ::= <constructor-col> "[" <tipo> "]"
<constructor-col> ::= "squad"
```

Esto da exactamente lo que pide la práctica de la Semana 2: **cinco tipos simples**
(`score`, `aim`, `lore`, `win`, `pixel`) y **dos compuestos** (`squad`, el vector
genérico, e `inventory`, el registro). Las clases (`guild`) también se nombran con
`<identificador>`. Con un punto, el tipo viene de otro archivo: `utilidades.Jugador`
es el `guild Jugador` de `utilidades.ggez` (sección 2.1).

`squad` es la única colección: ver la decisión `[D-21]`.

---

## 4. Declaraciones

```
<decl-variable>  ::= "stash" <tipo> <identificador> [ "set" <expresion> ] <fin-linea>
<decl-constante> ::= "hardcoded" <tipo> <identificador> "set" <expresion> <fin-linea>
```

El tipo es explícito, nunca se infiere. Una variable de tipo **simple** obliga a
llevar `set` en la declaración; una de tipo **compuesto** puede omitirlo y arranca
vacía. `hardcoded` siempre lleva `set`. `[D-9]` `[D-11]`

---

## 5. Sentencias

```
<sentencia> ::= <decl-variable>
              | <decl-constante>
              | <asignacion>
              | <sent-scan>
              | <sent-farm>
              | <sent-survive>
              | <sent-grind>
              | <sent-loadout>
              | <sent-clutch>
              | <sent-reward>
              | <sent-wipe>
              | <sent-quit>
              | "ragequit" <fin-linea>
              | "respawn" <fin-linea>
              | <expresion> <fin-linea>
```

### 5.1 Asignación

```
<asignacion>    ::= <designador> <op-asignacion> <expresion> <fin-linea>
                  | <designador> <op-incremento> <fin-linea>

<op-asignacion> ::= "set" | "boost" | "sap"
<op-incremento> ::= "buff" | "nerf"

<designador>    ::= ( <identificador> | "me" )
                    { "." <identificador> | "<" <expresion> ">" }*
```

### 5.2 Condicional

```
<sent-scan>   ::= "scan" <expresion> "glhf" <fin-linea> { <sentencia> }* <cierre-scan>

<cierre-scan> ::= "gg" <fin-linea>
                | "gg" "fallback" "glhf" <fin-linea> { <sentencia> }* "gg" <fin-linea>
                | "gg" "fallback" <sent-scan>
```

> **Punto de decisión del analizador sintáctico.** El `else` reutiliza el
> terminador de bloque: se escribe `gg fallback glhf`. Al leer `gg` hace falta
> **un token de anticipación** para saber si el bloque terminó o si viene un
> `fallback`. La gramática sigue siendo LL(1).

### 5.3 Ciclos

```
<sent-farm>    ::= "farm" <asig-simple> "|" <expresion> "|" <paso> <bloque>
<asig-simple>  ::= <designador> "set" <expresion>
<paso>         ::= <designador> <op-incremento>
                 | <designador> <op-asignacion> <expresion>

<sent-survive> ::= "survive" <expresion> <bloque>

<sent-grind>   ::= "grind" "glhf" <fin-linea> { <sentencia> }*
                   "gg" "survive" "{" <expresion> "}" <fin-linea>
```

La condición del `grind` va **al final**, después del `gg`, que es justo lo que lo
distingue de un `survive`: el cuerpo se ejecuta al menos una vez. `[D-10]`

`ragequit` y `respawn` solo son válidos dentro de `farm`, `survive` o `grind`;
usarlos en otro lado es error semántico. `[D-14]`

### 5.4 Selección múltiple

```
<sent-loadout>   ::= "loadout" <expresion> "glhf" <fin-linea>
                     { <rama-pick> }* [ <rama-noob> ] "gg" <fin-linea>

<rama-pick>      ::= "pick" <lista-valores> <bloque>
<lista-valores>  ::= <literal> { "|" <literal> }*
<rama-noob>      ::= "noob" <bloque>
```

Cada `pick` cierra con su propio `gg`, así que **no hay caída entre ramas**
(*fallthrough*) y no hace falta un `ragequit` al final de cada una.

### 5.5 Manejo de errores

```
<sent-clutch> ::= "clutch" "glhf" <fin-linea> { <sentencia> }*
                  "gg" "revive" "{" <identificador> "}" "glhf" <fin-linea>
                      { <sentencia> }*
                  "gg" [ "checkpoint" "glhf" <fin-linea> { <sentencia> }* "gg" ] <fin-linea>

<sent-wipe>   ::= "wipe" "{" <expresion> "}" <fin-linea>
<sent-quit>   ::= "quit" "{" [ <expresion> ] "}" <fin-linea>
```

El objeto que atrapa `revive` tiene dos campos: `.reason` (de tipo `lore`) y
`.line` (de tipo `score`). `wipe` recibe un `lore`. `[D-15]` `[D-16]`

`revive` atrapa tanto lo que lanza un `wipe` como los errores en ejecución
(división entre cero, índice fuera de rango, etc.). Un `wipe` que nadie atrapa
termina el programa como error en ejecución, con su texto como mensaje. El
bloque `checkpoint` corre siempre: haya error o no, y también si el bloque sale
por un `reward`, `ragequit` o `respawn`. La variable de `revive` solo existe
dentro de su bloque y sus campos son de solo lectura. `[D-22]`

---

## 6. Funciones

```
<decl-combo>    ::= [ <modificador> ] "combo" <identificador>
                    "{" [ <lista-parametros> ] "}" "drops" <tipo-retorno> <bloque>

<modificador>   ::= "open" | "party" | "solo"
<lista-parametros> ::= <parametro> { "|" <parametro> }*
<parametro>     ::= <tipo> <identificador>
<tipo-retorno>  ::= <tipo> | "noloot"

<sent-reward>   ::= "reward" [ <expresion> ] <fin-linea>
```

`drops` es **obligatorio siempre**; un combo que no devuelve nada escribe
`drops noloot`. `[D-12]` Si un combo declarado con un `drops` distinto de
`noloot` termina su bloque sin ejecutar `reward`, es error semántico.

---

## 7. Programación orientada a objetos

```
<decl-guild>     ::= "guild" <identificador> [ "evolves" <nombre-tipo> ]
                     "glhf" <fin-linea> { <miembro> }* "gg" <fin-linea>

<miembro>        ::= <decl-campo> | <decl-combo>
<decl-campo>     ::= <modificador> <tipo> <identificador> [ "set" <expresion> ] <fin-linea>

<decl-questline> ::= "questline" <identificador> "glhf" <fin-linea>
                     { <firma-combo> }* "gg" <fin-linea>
<firma-combo>    ::= "combo" <identificador> "{" [ <lista-parametros> ] "}"
                     "drops" <tipo-retorno> <fin-linea>

<decl-inventory> ::= "inventory" <identificador> "glhf" <fin-linea>
                     { <tipo> <identificador> <fin-linea> }* "gg" <fin-linea>
```

El `combo` que se llama igual que su `guild` es el **constructor**, y debe
declararse `drops noloot`. `[D-17]` `me` referencia la instancia actual.
`spawn Clase{args}` crea una instancia: los argumentos van al constructor. Si el
`guild` no tiene constructor, `spawn Clase{}` va sin argumentos.

El modelo sigue a Java:

- **Visibilidad.** `solo` = `private` (solo desde el mismo `guild`), `party` =
  `protected` (el `guild` y sus hijos), `open` = `public` (desde cualquier lado).
  Se revisa en el análisis semántico. Un `combo` miembro sin modificador es
  `open`; un campo siempre lleva modificador. `[D-24]`
- **`me` es opcional**, como `this` en Java. Dentro de un `combo` miembro, un
  nombre a secas se busca primero entre los parámetros y variables locales,
  después entre los campos y métodos del `guild`, y por último entre los `combo`
  globales. Un parámetro o variable local **sí** puede llamarse igual que un
  campo: el nombre a secas es el local y el campo se alcanza con `me.campo`. Es
  la única excepción a la prohibición de tapar nombres. `[D-25]`
- **Valores iniciales.** Un campo sin `set` arranca como en Java: `score` en
  `0`, `aim` en `0.0`, `win` en `miss`, y `lore`, `pixel`, `squad` y los objetos
  en `afk`. `[D-26]`
- **Herencia.** Un solo padre con `evolves`. No existe un equivalente a `super`:
  el constructor del padre no se ejecuta, y el hijo inicializa los campos
  heredados, que para eso deben ser `party` u `open`. Un campo `solo` del padre
  conserva su valor inicial. Un hijo puede reemplazar un método del padre si
  respeta la misma firma, y un objeto hijo puede guardarse en una variable del
  tipo padre. `[D-27]` Además, como en Java:
  - el constructor no se hereda;
  - un hijo no puede declarar un campo o método con el nombre de un campo
    heredado, ni un campo con el nombre de un método heredado;
  - al reemplazar un método no se puede cerrar su visibilidad (un método `open`
    no puede pasar a `party` ni a `solo`);
  - se ejecuta la versión del método que corresponde al objeto real, aunque la
    llamada esté escrita en el padre o la variable sea del tipo padre.

`questline` (interfaces) queda fuera de la versión 1: la regla se deja escrita
para una versión futura.

---

## 8. Expresiones

La cascada de producciones define la precedencia: **la regla de más abajo es la
que más fuerte liga**.

```
<expresion>     ::= <expr-or>
<expr-or>       ::= <expr-and>  { "swap"  <expr-and>  }*
<expr-and>      ::= <expr-rel>  { "chain" <expr-rel>  }*
<expr-rel>      ::= <expr-adit> [ <op-rel> <expr-adit> ]
<expr-adit>     ::= <expr-mult> { ( "gain" | "drain" ) <expr-mult> }*
<expr-mult>     ::= <expr-unaria> { ( "crit" | "split" | "scrap" ) <expr-unaria> }*
<expr-unaria>   ::= [ "dodge" | "drain" ] <expr-postfija>

<expr-postfija> ::= <primario> { <sufijo> }*
<sufijo>        ::= "." <identificador>
                  | "<" <expresion> ">"
                  | "{" [ <lista-argumentos> ] "}"

<primario>      ::= <literal>
                  | <identificador>
                  | "me"
                  | "spawn" <nombre-tipo> "{" [ <lista-argumentos> ] "}"
                  | "{" <expresion> "}"
                  | <literal-coleccion>

<lista-argumentos>  ::= <expresion> { "|" <expresion> }*
<literal-coleccion> ::= "<" [ <expresion> { "|" <expresion> }* ] ">"

<op-rel> ::= "matches" | "mismatch" | "over" | "under" | "atleast" | "atmost"
```

Tabla de precedencia resultante, de mayor a menor: `[D-4]`

| Nivel | Operadores | Asociatividad |
|---|---|---|
| 1 | `.` `{}` `<>` (acceso, llamada, índice) | izquierda |
| 2 | `dodge`, `drain` unario | derecha |
| 3 | `crit` `split` `scrap` | izquierda |
| 4 | `gain` `drain` | izquierda |
| 5 | `matches` `mismatch` `over` `under` `atleast` `atmost` | no asociativa |
| 6 | `chain` (AND) | izquierda, cortocircuito |
| 7 | `swap` (OR) | izquierda, cortocircuito |
| 8 | `set` `boost` `sap` | derecha (solo en sentencia) |

> **Nota sobre `{ }`.** Agrupar y llamar usan el mismo par de símbolos, pero no se
> confunden por su posición: `{...}` **después** de un operando es una llamada
> (`chat.send{"hola"}`), y `{...}` **donde se espera un operando** es una
> agrupación (`{MINIMO drain 60}`).

> **Nota sobre `split` y `scrap`.** Funcionan como en C y Java: si los dos
> operandos son `score`, el resultado es `score` y se descarta la parte decimal
> truncando hacia cero (`7 split 2` → `3`, `drain 7 split 2` → `-3`,
> `drain 7 scrap 3` → `-1`). Si al menos uno es `aim`, el resultado es `aim`
> (`7 split 2.0` → `3.5`). El tipo lo deciden los operandos, no la variable
> donde se guarda: `stash aim m set 10 split 3` guarda `3.0`. `[D-23]`

### 8.1 Ejemplo de árbol de derivación

Para `100 split {MINIMO drain 60}`, la agrupación fuerza que `drain` se evalúe
antes que `split`, igual que el ejemplo `a+b*c` de `Problemas de traducción`:

```
                  <expr-mult>
                 /     |      \
        <expr-unaria> "split" <expr-unaria>
              |                     |
          <primario>          <expr-postfija>
              |                     |
           <entero>             <primario>
              |                 /    |    \
             100              "{" <expr-adit> "}"
                                 /   |    \
                    <expr-mult> "drain" <expr-mult>
                          |                  |
                       MINIMO                60
```

Sin las llaves, `<expr-mult>` habría consumido `100 split MINIMO` primero y el
resultado sería distinto: la gramática **no es ambigua** porque la precedencia
está codificada en la cascada de producciones, no resuelta a posteriori.

---

## 9. Decisiones tomadas en este documento

Todas salen de `GGEZ - pendientes de especificacion.txt`. Están abiertas a cambio:
mover cualquiera es tocar una tabla, no reescribir el traductor.

| # | Decisión | Origen |
|---|---|---|
| D-1 | AND = `chain`, OR = `swap`, NOT = `dodge` | 1.1 — **pendiente de aprobación del equipo** |
| D-2 | Carácter con comilla simple `'a'`, cadena con doble `"abc"` | 1.2 |
| D-3 | El salto de línea se ignora dentro de `{` o `<` sin cerrar | 1.3 |
| D-4 | Tabla de precedencia completa (sección 8) | 3.1 |
| D-5 | Escapes soportados: `\n` `\t` `\\` `\"` `\'` | 1.5 |
| D-6 | Identificadores sin tildes ni `ñ`, admiten `_` | 1.6 |
| D-7 | Lenguaje sensible a mayúsculas; reservadas en minúscula | 1.7 |
| D-8 | `start THE GAME` es texto fijo obligatorio | 1.8 |
| D-9 | Tipos simples obligan `set`; compuestos arrancan vacíos | 3.9 |
| D-10 | `grind` cierra con `gg survive {cond}` | 3.2 |
| D-11 | `hardcoded` siempre lleva `set` | 4.11 |
| D-12 | `drops` obligatorio, `noloot` si no devuelve | 3.8 |
| D-13 | Un archivo sin `lobby.launch` es válido como biblioteca | 4.7 |
| D-14 | `ragequit`/`respawn` solo en ciclos | 3.10 |
| D-15 | El error de `revive` tiene `.reason` (lore) y `.line` (score) | 4.4 |
| D-16 | `wipe` recibe un `lore` | 4.5 |
| D-17 | El combo homónimo del guild es el constructor | 3.7 |
| D-18 | `chat` en minúscula, con llaves: `chat.send{...}` | 2.1, 2.2 |
| D-19 | Un `gg` cierra la sentencia sin consumirse, y el salto tras `glhf` es opcional: así un bloque cabe en una línea | surgió al implementar el `loadout`; el ejemplo del Word ya lo usaba |
| D-20 | Métodos de `squad` (ver abajo) | 3.6 |
| D-21 | **`stack` y `queue` quedan fuera del lenguaje**: la única colección es `squad` | decisión del equipo, 2026-09-26 |
| D-22 | `revive` atrapa `wipe` y errores en ejecución; `checkpoint` corre siempre | surgió al implementar el `clutch`; el ejemplo del Word atrapa una división entre cero |
| D-23 | `score split score` da `score`, truncando como en C y Java | el ejemplo del Word (`loadout puntos split 10`) usa división entera; antes `split` siempre daba `aim` |
| D-24 | Visibilidad como Java: `solo`/`party`/`open` = `private`/`protected`/`public`; método sin modificador es `open` | 3.3. El ejemplo del Word leía un campo `solo` desde afuera: se corrigió con un getter |
| D-25 | `me` opcional; un local puede llamarse igual que un campo |
| D-26 | Campos sin `set` arrancan con el valor por defecto | 
| D-27 | Herencia simple, sin `super`: el hijo inicializa lo heredado |
| D-28 | `travel X from Y`: `Y` es una carpeta relativa al archivo (`base` = la misma); lo importado se usa como `X.algo` | bloque 6, decisión del equipo 2026-09-26 |
| D-29 | Un archivo importado no puede tener `lobby.launch` | decisión del equipo, 2026-09-26 |
| D-30 | `server` y `region` quedan fuera de la versión 1 | nunca se definió para qué sirven |

### D-21 · Solo vectores

La Propuesta de Lenguaje listaba tres colecciones —Vector (`squad`), Pila (`stack`) y
Cola (`queue`)—. El equipo decidió quedarse **solo con el vector**.

Con eso GGEZ tiene exactamente los tipos que pide la práctica de la Semana 2:
**cinco simples** (`score`, `aim`, `lore`, `win`, `pixel`) y **dos compuestos**
(`squad` e `inventory`).

`stack` y `queue` siguen siendo palabras reservadas, para que quien las escriba reciba
un mensaje claro en vez de que se interpreten como el nombre de un tipo inventado:

```
GGEZ no tiene 'stack': la unica coleccion es squad. Use squad[T] en su lugar.
```

> **Pendiente:** la Propuesta de Lenguaje (el Word) todavía lista Pila y Cola como
> tipos. Hay que quitarlas de ahí para que el documento entregado y el traductor digan
> lo mismo.

### D-20 · Los métodos de `squad`

De la Propuesta solo venía fijado `.size`. Los otros dos son propuestas:

| Cuántos | Agregar al final | Sacar por índice |
|---|---|---|
| `.size` | `.recruit{x}` | `.kick{i}` |

`.recruit{x}` exige que `x` sea del tipo interno del vector; `.kick{i}` exige un `score`.
Para leer o cambiar una posición se usa el indexado directo: `v<0>` y `v<0> set 99`.

**Sigue pendiente de decisión del equipo:** si `questline` (las interfaces) entra en
la versión 1 (punto 3.4 del txt de pendientes). La gramática de `questline` ya quedó
escrita arriba por si entra.
