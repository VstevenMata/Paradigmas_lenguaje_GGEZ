namespace ParadigmasLenguajePropio.Controls;

// Sprites del mundo como cuadriculas: cada letra es un rol de color de la paleta del tema, '.' es transparente
// K contorno, P principal, S piel, D detalle, W brillo, T decoracion, U puerta, L logro (TemaJuego* en Themes/Paletas)
// Generado y validado (tamanos y letras) con un script; si se edita a mano, respetar el tamano de cada uno
public static class SpritesMundo
{
    // Personaje 16x16 por tema: [quieto, paso]
    public static readonly IReadOnlyDictionary<string, string[][]> Heroe = new Dictionary<string, string[][]>
    {
        ["Win95"] = [["................", ".....KKKKKK.....", "....KPPPPPPKK...", "...KPPPWWPPPPK..", "...KKKKKKKKKKKK.", "...KSSSSSSSSK...", "...KSKSSSSKSK...", "...KSSSSSSSSK...", "...KKKKKKKKKK...", "....KSSSSSSK....", "..KKPPDPPDPPKK..", ".KSKPPDDDDPPKSK.", "..KKDWDDDDWDKK..", "...KDDDDDDDDK...", "...KKKK..KKKK...", "..KKKKK..KKKKK.."],
            ["................", ".....KKKKKK.....", "....KPPPPPPKK...", "...KPPPWWPPPPK..", "...KKKKKKKKKKKK.", "...KSSSSSSSSK...", "...KSKSSSSKSK...", "...KSSSSSSSSK...", "...KKKKKKKKKK...", "....KSSSSSSK....", "..KKPPDPPDPPKK..", ".KSKPPDDDDPPKSK.", "..KKDWDDDDWDKK..", "...KDDDDDDDDK...", "....KKKKKKKK....", "....KKK..KKK...."]],
        ["Zelda"] = [["......KKKK......", ".....KPPPPK.....", "....KPPPPPPKK...", "...KPPPPPPPPPK..", "...KDDDDDDDDKPK.", "...KSSSSSSSSK.K.", "...KSWKSSKWSK...", "...KSSSSSSSSK...", "....KSSKKSSK....", "..KKKPPPPPPKKK..", ".KSKPPPDDPPPKSK.", ".KSKPPPPPPPPKSK.", "..KKDDDDDDDDKK..", "....KPPPPPPK....", "....KDDK.KDDK...", "....KKK...KKK..."],
            ["......KKKK......", ".....KPPPPK.....", "....KPPPPPPKK...", "...KPPPPPPPPPK..", "...KDDDDDDDDKPK.", "...KSSSSSSSSK.K.", "...KSWKSSKWSK...", "...KSSSSSSSSK...", "....KSSKKSSK....", "..KKKPPPPPPKKK..", ".KSKPPPDDPPPKSK.", ".KSKPPPPPPPPKSK.", "..KKDDDDDDDDKK..", "....KPPPPPPK....", ".....KDDKDDK....", ".....KKK.KKK...."]],
        ["MegaMan"] = [[".....KKKKKK.....", "....KPPPPPPK....", "...KPPDDDDPPK...", "...KPPPPPPPPK...", "...KPSSSSSSPK...", "...KSWWSSWWSK...", "...KSWKSSWKSK...", "...KSSSSSSSSK...", "....KSSKKSSK....", ".KKKKPPPPPPKKKK.", "KDDKPPPDDPPPKDDK", "KDDKPPPPPPPPKDDK", ".KKKKPPPPPPKKKK.", "...KPPPKKPPPK...", "..KPPPK..KPPPK..", "..KKKKK..KKKKK.."],
            [".....KKKKKK.....", "....KPPPPPPK....", "...KPPDDDDPPK...", "...KPPPPPPPPK...", "...KPSSSSSSPK...", "...KSWWSSWWSK...", "...KSWKSSWKSK...", "...KSSSSSSSSK...", "....KSSKKSSK....", ".KKKKPPPPPPKKKK.", "KDDKPPPDDPPPKDDK", "KDDKPPPPPPPPKDDK", ".KKKKPPPPPPKKKK.", "....KPPPPPPK....", "...KPPPKKPPPK...", "...KKKKKKKKKK..."]],
        ["Sonic"] = [[".KKKK...........", "..KPPKKK........", "...KPPPPKKK.....", "KKKPPPPPPPPKK...", ".KPPPPPPPSWWKK..", "..KPPPPPPSWKWK..", "KKPPPPPPPSSSSK..", ".KPPPPPPPSSSKK..", "..KPPPPPPPKKK...", "...KPPPPPPK.....", "...KSSPPPSSK....", "....KPPPPPK.....", "...KPPK.KPPK....", "..KDDDK.KDDDK...", ".KDDDDK.KDDDDK..", ".KKKKK...KKKKK.."],
            [".KKKK...........", "..KPPKKK........", "...KPPPPKKK.....", "KKKPPPPPPPPKK...", ".KPPPPPPPSWWKK..", "..KPPPPPPSWKWK..", "KKPPPPPPPSSSSK..", ".KPPPPPPPSSSKK..", "..KPPPPPPPKKK...", "...KPPPPPPK.....", "...KSSPPPSSK....", "....KPPPPPK.....", "....KPPKPPK.....", "....KDDKDDK.....", "...KDDDKDDDK....", "...KKKKKKKKK...."]],
    };

    // Decoracion del terreno 8x8 por tema
    public static readonly IReadOnlyDictionary<string, string[]> Decoracion = new Dictionary<string, string[]>
    {
        ["Win95"] = ["........", ".KKK....", ".KTTKKK.", ".KTTTTTK", ".KTTTTTK", ".KTTTTTK", ".KKKKKKK", "........"],
        ["Zelda"] = ["..KKKK..", ".KTTTTK.", "KTTTTTTK", "KTTTTTTK", ".KTTTTK.", "..KDDK..", "...DD...", "..KDDK.."],
        ["MegaMan"] = [".KKKKKK.", ".KTTTTK.", ".KTDDTK.", ".KTTTTK.", ".KTDDTK.", ".KTTTTK.", ".KTTTTK.", ".KKKKKK."],
        ["Sonic"] = ["........", "..KKKK..", ".KTTTTK.", ".KT..TK.", ".KT..TK.", ".KTTTTK.", "..KKKK..", "........"],
    };

    // 12x16
    public static readonly string[] PuertaCerrada = ["...KKKKKK...", "..KUUUUUUK..", ".KUUUUUUUUK.", ".KUUUKKUUUK.", "KUUUUKKUUUUK", "KUUUUKKUUUUK", "KUUUUKKUUUUK", "KUUUUKKUUUUK", "KUUUUKKUDDUK", "KUUUUKKUDDUK", "KUUUUKKUUUUK", "KUUUUKKUUUUK", "KUUUUKKUUUUK", "KUUUUKKUUUUK", "KUUUUKKUUUUK", "KKKKKKKKKKKK"];
    // 12x16
    public static readonly string[] PuertaAbierta = ["...UUUUUU...", "..UKKKKKKU..", ".UKKKKKKKKU.", ".UKKKKKKKKU.", "UKKKKKKKKKKU", "UKKKKKKKKKKU", "UKKKKKKKKKKU", "UKKKKKKKKKKU", "UKKKKKKKKKKU", "UKKKKKKKKKKU", "UKKKKKKKKKKU", "UKKKKKKKKKKU", "UKKKKKKKKKKU", "UKKKKKKKKKKU", "UKKKKKKKKKKU", "UUUUUUUUUUUU"];
    // 8x12
    public static readonly string[] Bandera = [".K......", ".KWWWW..", ".KWWWWW.", ".KWWWW..", ".KW.....", ".K......", ".K......", ".K......", ".K......", ".K......", "KKK.....", "........"];
    // 8x12
    public static readonly string[] BanderaLograda = [".K......", ".KLLLL..", ".KLLLLL.", ".KLLLL..", ".KL.....", ".K......", ".K......", ".K......", ".K......", ".K......", "KKK.....", "........"];
    // 16x8
    public static readonly string[] Nube = ["................", ".....WWWW.......", "...WWWWWWWW.....", "..WWWWWWWWWWW...", ".WWWWWWWWWWWWWW.", "WWWWWWWWWWWWWWWW", ".WWWWWWWWWWWWWW.", "................"];
    // 8x8
    public static readonly string[] MiniPuerta = ["..KKKK..", ".KUUUUK.", "KUUKUUUK", "KUUKUUUK", "KUUKUDUK", "KUUKUUUK", "KUUKUUUK", "KKKKKKKK"];
    // 8x8
    public static readonly string[] MiniBandera = [".K......", ".KLLLL..", ".KLLLLL.", ".KLLLL..", ".K......", ".K......", "KKK.....", "........"];
    // 8x8
    public static readonly string[] Fantasma = ["..KKKK..", ".KWWWWK.", "KWKWWKWK", "KWWWWWWK", "KWWWWWWK", "KWWWWWWK", "KWKWWKWK", ".K.KK.K."];
    // 16x16
    public static readonly string[] Jefe = [".....KKKKKK.....", "...KKWWWWWWKK...", "..KWWWWWWWWWWK..", ".KWWWWWWWWWWWWK.", ".KWWKKWWWWKKWWK.", "KWWWKDKWWKDKWWWK", "KWWWKKKWWKKKWWWK", "KWWWWWWWWWWWWWWK", "KWWWWKKKKKKWWWWK", "KWWWWKDDDDKWWWWK", "KWWWWWKKKKWWWWWK", "KWWWWWWWWWWWWWWK", "KWWWWWWWWWWWWWWK", "KWWKWWWKKWWWKWWK", "KWK.KWK..KWK.KWK", ".K...K....K...K."];
}
