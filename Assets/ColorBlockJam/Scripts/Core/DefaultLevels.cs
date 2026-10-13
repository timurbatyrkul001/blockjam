using System.Collections.Generic;
using static ColorBlockJam.BlockColor;
using static ColorBlockJam.Side;

namespace ColorBlockJam
{
    // İlk 20 level (GDD Bölüm 5). 1-6 elle tasarlandı; 7-20 üretilip LevelSolver ile seçildi
    // (her biri en az bir "önce başka bloğu kenara al" hamlesi gerektiriyor).
    // Bunlar sadece başlangıç verisi: editor ilk açılışta her birini
    // Assets/ColorBlockJam/Levels altında ayrı bir LevelAsset dosyasına çevirir; sonrasında
    // level'lar Unity içinden, kod yazmadan düzenlenir.
    public static class DefaultLevels
    {
        public static List<LevelDef> All() => new()
        {
            // ---- 1-3: Temel eşleşme (tek renk, çok boş alan) ----
            LevelDef.Create(
                    ".....",
                    ".....",
                    "..a..",
                    "..a..",
                    ".....",
                    ".....")
                .Time(60).Coins(10)
                .Block('a', Red)
                .Gate(Top, 2, 1, Red),

            LevelDef.Create(
                    ".....",
                    ".aa..",
                    ".....",
                    ".....",
                    "...b.",
                    "...b.")
                .Time(60).Coins(10)
                .Block('a', Red).Block('b', Red)
                .Gate(Left, 4, 1, Red)
                .Gate(Bottom, 3, 1, Red),

            LevelDef.Create(
                    "......",
                    ".aa...",
                    "......",
                    "...bb.",
                    "......",
                    ".c....",
                    ".c....")
                .Time(60).Coins(10)
                .Block('a', Red).Block('b', Red).Block('c', Red)
                .Gate(Right, 2, 4, Red),

            // ---- 4-6: Birden fazla renk (renk okuma) ----
            LevelDef.Create(
                    "......",
                    ".aa...",
                    "......",
                    "....b.",
                    "....b.",
                    "......")
                .Time(60).Coins(10)
                .Block('a', Red).Block('b', Blue)
                .Gate(Top, 1, 2, Red)
                .Gate(Bottom, 4, 1, Blue),

            LevelDef.Create(
                    "......",
                    ".aa..c",
                    ".....c",
                    ".b....",
                    ".b....",
                    "...dd.")
                .Time(60).Coins(10)
                .Block('a', Red).Block('b', Blue).Block('c', Green).Block('d', Yellow)
                .Gate(Left, 4, 1, Red)
                .Gate(Bottom, 1, 1, Blue)
                .Gate(Right, 3, 2, Green)
                .Gate(Top, 3, 2, Yellow),

            LevelDef.Create(
                    "......",
                    "aa..bb",
                    "......",
                    "..cc..",
                    "......",
                    "d....e",
                    "d....e")
                .Time(75).Coins(15)
                .Block('a', Red).Block('b', Blue).Block('c', Green).Block('d', Yellow).Block('e', Purple)
                .Gate(Left, 5, 1, Blue)
                .Gate(Right, 5, 1, Red)
                .Gate(Bottom, 2, 2, Green)
                .Gate(Top, 0, 1, Yellow)
                .Gate(Top, 5, 1, Purple),

            // ---- 7-10: Yol kapatma (önce bir bloğu kenara alıp alan aç) ----
            // en az 4 hamle, 1 tanesi kenara alma
            LevelDef.Create(
                    "aa...b",
                    "aa...b",
                    "......",
                    "......",
                    "......",
                    "cc....",
                    "cc....")
                .Time(75).Coins(15)
                .Block('a', Red).Block('b', Blue).Block('c', Green)
                .Gate(Top, 4, 2, Red)
                .Gate(Left, 4, 2, Blue)
                .Gate(Bottom, 3, 2, Green),

            // en az 5 hamle, 1 tanesi kenara alma
            LevelDef.Create(
                    "......",
                    "...cc.",
                    "b.....",
                    "b.....",
                    "b.aaad",
                    ".....d",
                    ".....d")
                .Time(75).Coins(15)
                .Block('a', Red).Block('b', Blue).Block('c', Green).Block('d', Yellow)
                .Gate(Left, 3, 1, Red)
                .Gate(Right, 0, 3, Blue)
                .Gate(Left, 0, 2, Green)
                .Gate(Left, 4, 3, Yellow),

            // en az 5 hamle, 1 tanesi kenara alma
            LevelDef.Create(
                    "......",
                    ".c..aa",
                    ".c....",
                    ".c.b..",
                    "...b.d",
                    "...b.d",
                    "......")
                .Time(75).Coins(15)
                .Block('a', Red).Block('b', Blue).Block('c', Green).Block('d', Yellow)
                .Gate(Left, 0, 1, Red)
                .Gate(Right, 3, 3, Blue)
                .Gate(Top, 5, 1, Green)
                .Gate(Left, 3, 3, Yellow),

            // en az 5 hamle, 1 tanesi kenara alma
            LevelDef.Create(
                    "...aaa",
                    "d.....",
                    "d.bb..",
                    "d.....",
                    ".....c",
                    ".....c",
                    "......")
                .Time(75).Coins(15)
                .Block('a', Red).Block('b', Blue).Block('c', Green).Block('d', Yellow)
                .Gate(Bottom, 0, 3, Red)
                .Gate(Top, 3, 2, Blue)
                .Gate(Left, 0, 3, Green)
                .Gate(Right, 1, 3, Yellow),

            // ---- 11-15: Farklı boyutlar (alan yönetimi) ----
            // en az 6 hamle, 2 tanesi kenara alma
            LevelDef.Create(
                    ".......",
                    "..ccc..",
                    "..ccc..",
                    ".......",
                    ".......",
                    "...d...",
                    ".bbbbaa",
                    ".....aa")
                .Time(90).Coins(20)
                .Block('a', Red).Block('b', Blue).Block('c', Green).Block('d', Yellow)
                .Gate(Bottom, 3, 2, Red)
                .Gate(Right, 6, 1, Blue)
                .Gate(Left, 0, 3, Green)
                .Gate(Bottom, 5, 2, Yellow),

            // en az 7 hamle, 2 tanesi kenara alma
            LevelDef.Create(
                    "...b...",
                    ".c.b...",
                    ".c.bdd.",
                    "...bdd.",
                    "eeee...",
                    ".......",
                    ".aaa...",
                    ".aaa...")
                .Time(90).Coins(20)
                .Block('a', Red).Block('b', Blue).Block('c', Green).Block('d', Yellow).Block('e', Purple)
                .Gate(Right, 5, 3, Red)
                .Gate(Left, 1, 4, Blue)
                .Gate(Bottom, 4, 1, Green)
                .Gate(Left, 5, 2, Yellow)
                .Gate(Top, 0, 4, Purple),

            // en az 8 hamle, 2 tanesi kenara alma
            LevelDef.Create(
                    ".......",
                    ".cccc.e",
                    "......e",
                    "......e",
                    "f..b..a",
                    "f.....a",
                    "f.dddda",
                    "f.....a")
                .Time(90).Coins(20)
                .Block('a', Red).Block('b', Blue).Block('c', Green).Block('d', Yellow).Block('e', Purple).Block('f', Orange)
                .Gate(Top, 1, 2, Red)
                .Gate(Right, 4, 1, Blue)
                .Gate(Right, 1, 2, Green)
                .Gate(Bottom, 0, 4, Yellow)
                .Gate(Top, 4, 1, Purple)
                .Gate(Top, 3, 1, Orange),

            // en az 8 hamle, 2 tanesi kenara alma
            LevelDef.Create(
                    ".......",
                    "ff..ecc",
                    "ff...cc",
                    ".ddd.cc",
                    "abbb...",
                    "abbb...",
                    "a......",
                    ".......")
                .Time(90).Coins(20)
                .Block('a', Red).Block('b', Blue).Block('c', Green).Block('d', Yellow).Block('e', Purple).Block('f', Orange)
                .Gate(Right, 4, 3, Red)
                .Gate(Left, 5, 3, Blue)
                .Gate(Bottom, 0, 2, Green)
                .Gate(Top, 0, 4, Yellow)
                .Gate(Right, 3, 1, Purple)
                .Gate(Bottom, 3, 2, Orange),

            // en az 9 hamle, 3 tanesi kenara alma
            LevelDef.Create(
                    ".eeeeff",
                    "..a..ff",
                    "..a.cff",
                    "..a.c..",
                    "..a.cb.",
                    "..ddcb.",
                    "..dd.b.",
                    ".......")
                .Time(90).Coins(20)
                .Block('a', Red).Block('b', Blue).Block('c', Green).Block('d', Yellow).Block('e', Purple).Block('f', Orange)
                .Gate(Left, 1, 4, Red)
                .Gate(Top, 5, 2, Blue)
                .Gate(Bottom, 5, 1, Green)
                .Gate(Top, 0, 3, Yellow)
                .Gate(Bottom, 1, 4, Purple)
                .Gate(Right, 1, 3, Orange),

            // ---- 16-20: Yoğun board (sıralama + gerçek puzzle) ----
            // en az 10 hamle, 3 tanesi kenara alma
            LevelDef.Create(
                    "......d",
                    ".bbee.d",
                    ".ccee.d",
                    ".ccee..",
                    ".cc....",
                    ".....gg",
                    "fff.agg",
                    "....agg")
                .Time(120).Coins(25)
                .Block('a', Red).Block('b', Blue).Block('c', Green).Block('d', Yellow).Block('e', Purple).Block('f', Orange).Block('g', Pink)
                .Gate(Right, 3, 2, Red)
                .Gate(Bottom, 2, 3, Blue)
                .Gate(Top, 5, 2, Green)
                .Gate(Right, 0, 3, Yellow)
                .Gate(Bottom, 0, 2, Purple)
                .Gate(Left, 5, 1, Orange)
                .Gate(Top, 2, 2, Pink),

            // en az 11 hamle, 3 tanesi kenara alma
            LevelDef.Create(
                    ".bbaa..",
                    ".bbaa..",
                    "dbbaa..",
                    "dgg..hh",
                    ".ggc.hh",
                    "fffc.hh",
                    "eee....",
                    "eee....")
                .Time(120).Coins(25)
                .Block('a', Red).Block('b', Blue).Block('c', Green).Block('d', Yellow).Block('e', Purple).Block('f', Orange).Block('g', Pink).Block('h', Cyan)
                .Gate(Right, 0, 3, Red)
                .Gate(Bottom, 0, 2, Blue)
                .Gate(Right, 3, 3, Green)
                .Gate(Left, 0, 3, Yellow)
                .Gate(Top, 4, 3, Purple)
                .Gate(Left, 4, 2, Orange)
                .Gate(Bottom, 2, 2, Pink)
                .Gate(Top, 0, 2, Cyan),

            // en az 12 hamle, 3 tanesi kenara alma
            LevelDef.Create(
                    "eee....",
                    "...bbb.",
                    ".ffbbb.",
                    ".ff.hh.",
                    ".aaa..i",
                    "gaaa.ci",
                    "gdd..c.",
                    ".....c.")
                .Time(120).Coins(25)
                .Block('a', Red).Block('b', Blue).Block('c', Green).Block('d', Yellow).Block('e', Purple).Block('f', Orange).Block('g', Pink).Block('h', Cyan).Block('i', Red)
                .Gate(Top, 2, 3, Red)
                .Gate(Bottom, 0, 3, Blue)
                .Gate(Right, 5, 3, Green)
                .Gate(Right, 2, 1, Yellow)
                .Gate(Left, 3, 1, Purple)
                .Gate(Bottom, 3, 2, Orange)
                .Gate(Top, 0, 1, Pink)
                .Gate(Left, 4, 2, Cyan),

            // en az 12 hamle, 3 tanesi kenara alma
            LevelDef.Create(
                    "ffcceee",
                    "ffcceee",
                    "ffcc...",
                    "....gg.",
                    "aaa.ggb",
                    "aaaiihb",
                    "...iihb",
                    ".ddd.h.")
                .Time(120).Coins(25)
                .Block('a', Red).Block('b', Blue).Block('c', Green).Block('d', Yellow).Block('e', Purple).Block('f', Orange).Block('g', Pink).Block('h', Cyan).Block('i', Red)
                .Gate(Left, 4, 3, Red)
                .Gate(Top, 6, 1, Blue)
                .Gate(Right, 3, 3, Green)
                .Gate(Left, 2, 1, Yellow)
                .Gate(Bottom, 1, 3, Purple)
                .Gate(Top, 0, 2, Orange)
                .Gate(Top, 4, 2, Pink)
                .Gate(Bottom, 5, 2, Cyan),

            // en az 13 hamle, 4 tanesi kenara alma
            LevelDef.Create(
                    ".aa.ccc",
                    ".aa.ccc",
                    ".aaeg..",
                    "ddhegii",
                    "ddhegff",
                    "ddh..ff",
                    "bbb..ff",
                    "bbb....")
                .Time(120).Coins(25)
                .Block('a', Red).Block('b', Blue).Block('c', Green).Block('d', Yellow).Block('e', Purple).Block('f', Orange).Block('g', Pink).Block('h', Cyan).Block('i', Red)
                .Gate(Right, 2, 4, Red)
                .Gate(Top, 3, 4, Blue)
                .Gate(Left, 2, 3, Green)
                .Gate(Bottom, 3, 3, Yellow)
                .Gate(Top, 0, 1, Purple)
                .Gate(Bottom, 1, 2, Orange)
                .Gate(Left, 5, 3, Pink)
                .Gate(Bottom, 0, 1, Cyan),
        };
    }
}
