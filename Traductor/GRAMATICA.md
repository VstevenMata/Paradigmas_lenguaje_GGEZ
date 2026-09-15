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
<fin-linea> ::= FIN_LINEA { FIN_LINEA }*
```

### 1.5 Palabras reservadas

Las 60 palabras reservadas, agrupadas como en el documento original:

| Grupo | Palabras |
|---|---|
| Estructura | `lobby` `launch` `start` `THE` `GAME` `travel` `from` `server` `region` `quit` |
| Declaración | `stash` `hardcoded` `noloot` `afk` |
| Tipos | `score` `aim` `lore` `win` `pixel` `squad` `stack` `queue` `inventory` |
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

<decl-superior> ::= <decl-server>
                  | <decl-travel>
                  | <decl-region>
                  | <decl-guild>
                  | <decl-questline>
                  | <decl-inventory>
                  | <decl-combo>
                  | <punto-entrada>

<decl-server>   ::= "server" <identificador> <fin-linea>
<decl-travel>   ::= "travel" <identificador> "from" <identificador> <fin-linea>
<decl-region>   ::= "region" <identificador> <bloque>

<punto-entrada> ::= "lobby" "." "launch" "{" "start" "THE" "GAME" "}" <bloque>
```

`start THE GAME` es **texto fijo obligatorio**, no son parámetros. `[D-8]`
Debe existir exactamente un `<punto-entrada>` en el programa que se ejecuta; un
archivo sin él es válido como biblioteca importable con `travel`. `[D-13]`

### 2.1 Bloques

```
<bloque> ::= "glhf" <fin-linea> { <sentencia> }* "gg" <fin-linea>
```

Todo bloque abre con `glhf` y cierra con `gg`, siempre el par completo, aunque
contenga una sola instrucción. La indentación es estética, no significativa.

---

## 3. Tipos

```
<tipo>            ::= <tipo-simple> | <tipo-coleccion> | <identificador>

<tipo-simple>     ::= "score" | "aim" | "lore" | "win" | "pixel"
<tipo-coleccion>  ::= <constructor-col> "[" <tipo> "]"
<constructor-col> ::= "squad" | "stack" | "queue"
```

Los cinco tipos simples son los que pide la práctica de la Semana 2. Los
compuestos son las tres colecciones genéricas más `inventory` (registro) y las
clases (`guild`), que se nombran con `<identificador>`.

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
<decl-guild>     ::= "guild" <identificador> [ "evolves" <identificador> ]
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
`spawn Clase{args}` crea una instancia.

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
                  | "spawn" <identificador> "{" [ <lista-argumentos> ] "}"
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

**Sigue pendiente de decisión del equipo** (no bloquea el analizador léxico, sí el
analizador semántico): los nombres de los métodos de `squad`/`stack`/`queue`
(punto 3.6) y si `questline` entra en la versión 1 (punto 3.4). La gramática de
`questline` ya quedó escrita arriba por si entra.
