using System.Collections.Generic;
using UnityEngine;

namespace Fleeced
{
    public static class Icons
    {
        public static Color32 Color(char c)
        {
            switch (c)
            {
                case 'k': return Pal.H("1c1416");
                case 'w': return Pal.H("f2eee4");
                case 'l': return Pal.H("c8ccd0");
                case 'G': return Pal.H("8a9098");
                case 'D': return Pal.H("4a4e56");
                case 'y': return Pal.H("f0c840");
                case 'Y': return Pal.H("b08820");
                case 'o': return Pal.H("e07a28");
                case 'g': return Pal.H("5aa040");
                case 'h': return Pal.H("2e6a2a");
                case 'r': return Pal.H("c83a30");
                case 'R': return Pal.H("7a1e1a");
                case 'p': return Pal.H("e888a8");
                case 'B': return Pal.H("3a64a8");
                case 'c': return Pal.H("8ad8e8");
                case 'P': return Pal.H("8a4ab0");
                case 'b': return Pal.H("8a5a34");
                case 'd': return Pal.H("5a3a24");
                case 's': return Pal.H("e8b090");
                default: return Pal.H("ff00ff");
            }
        }

        public static readonly Dictionary<string, string> Art = new Dictionary<string, string>
        {
            { "coin", "..kkkk..|.kyyyyk.|kyyYYyyk|kyYwyyYk|kyYyyyYk|kyyYYyyk|.kyyyyk.|..kkkk.." },
            { "wool", "........|..kkk...|.kwwwkk.|kwwwwwwk|kwwlwwwk|kwlwwwlk|.kkkkkk.|........" },
            { "cleanwool", "......c.|..kkk.ccc|.kwwwkkc.|kwwwwwwk|kwwwwwwk|kwwwwwwk|.kkkkkk.|........" },
            { "yarn", "..kkkk..|.kPPPPk.|kPpPPpPk|kPPpPPPk|kpPPpPPk|kPPPPpPk|.kPPPPkk|..kkkk.k" },
            { "fabric", "........|.kkkkkk.|kBBBBBBk|kBcBBcBk|kBBBBBBk|kBcBBcBk|kkkkkkkk|........" },
            { "clothing", ".kk..kk.|krrkkrrk|krrrrrrk|kkrwwrkk|.krrrrk.|.krwwrk.|.krrrrk.|.kkkkkk." },
            { "luxury", "........|y..y..y.|yy.yy.yy|yyyyyyyy|yrryyBBy|yyyyyyyy|YYYYYYYY|........" },
            { "food", "......gg|.....ggh|....kok.|...kook.|..kook..|.kook...|kok.....|kk......" },
            { "pop", ".kwkwk..|kwwwwwk.|kwkkkwk.|kkwkwkk.|.kkkkk..|.kkrkk..|..kkk...|........" },
            { "happy", "..kkkk..|.kyyyyk.|kykyykyk|kyyyyyyk|kykyykyk|kyykkyyk|.kyyyyk.|..kkkk.." },
            { "sad", "..kkkk..|.kyyyyk.|kykyykyk|kyyyyyyk|kyykkyyk|kykyykyk|.kyyyyk.|..kkkk.." },
            { "research", "..kkk...|..kGk...|..kGk...|.kGGGk..|kGgggGk.|kgggggk.|kgggggk.|.kkkkk.." },
            { "power", "....kk..|...kyk..|..kyk...|.kyyyk..|..kyk...|.kyk....|kyk.....|kk......" },
            { "skull", ".kkkkk..|kwwwwwk.|kwkwkwk.|kwwwwwk.|.kwkwk..|.kkkkk..|........|........" },
            { "shears", "G......G|lG....Gl|.lG..Gl.|..lGGl..|...kk...|..r..r..|.r....r.|.rr..rr." },
            { "heart", ".rr.rr..|rrrrrrr.|rrwrrrr.|rrrrrrr.|.rrrrr..|..rrr...|...r....|........" },
            { "gear", "...GG...|.G.GG.G.|..GGGG..|GGGkkGGG|GGGkkGGG|..GGGG..|.G.GG.G.|...GG..." },
            { "shield", "kkkkkkk.|kBBwBBk.|kBBwBBk.|kwwwwwk.|kBBwBBk.|.kBwBk..|..kwk...|...k...." },
            { "chart", "........|......g.|.....gg.|..g.gg..|.ggg....|gg......|kkkkkkkk|........" },
            { "crate", "kkkkkkkk|kbbbbbbk|kbkbbkbk|kbbkkbbk|kbbkkbbk|kbkbbkbk|kbbbbbbk|kkkkkkkk" },
            { "house", "...kk...|..kddk..|.kddddk.|kddddddk|.kwwbwk.|.kwwbwk.|.kwwbwk.|.kkkkkk." },
            { "sheep", "........|.wwww...|wwwwwkk.|wwwwwkwk|.wwwwkk.|.k.k.k..|........|........" },
            { "flower", "..p.p...|.ppypp..|..ppp...|...g....|..gg....|...g....|...g....|........" },
            { "rocket", "...w....|..wrw...|..www...|..wBw...|..www...|.rwwwr..|.r.o.r..|...o...." },
            { "moon", "..kkk...|.kyyyk..|kyyykk..|kyyk....|kyyk....|kyyykk..|.kyyyk..|..kkk..." },
            { "wolf", "k.....k.|kk...kk.|kGGGGGk.|GrGGGrG.|GGGGGGG.|.GwGwG..|..GGG...|........" },
            { "clock", "..kkkk..|.kwwwwk.|kwwkwwwk|kwwkwwwk|kwwwkkwk|kwwwwwwk|.kwwwwk.|..kkkk.." },
            { "hammer", "kkkkk...|kGGGGk..|kkkGkk..|...bk...|...bk...|...bk...|...bk...|...kk..." },
            { "vat", ".kkkkk..|kcgggck.|kcgwgck.|kcgggck.|kcgggck.|kkkkkkk.|kGGGGGk.|kkkkkkk." },
            { "warning", "...kk...|..kyyk..|..kykk..|.kyykyk.|.kyykyk.|kyyyyyyk|kyyykyyk|kkkkkkkk" },
            { "tomb", "..kkk...|.kGGGk..|kGGkGGk.|kGkkkGk.|kGGkGGk.|kGGGGGk.|kGGGGGk.|kkkkkkk." },
            { "spa", "..w.w...|.w.w....|..w.w...|.kkkkkk.|kbdbdbbk|kdbbdbdk|.kkkkkk.|........" },
            { "statue", "..lll...|.lllll..|.llllkl.|..llll..|..l..l..|.kkkkk..|.kGGGk..|kkkkkkk." },
            { "milk", "..kkkk..|..kllk..|..kwwk..|.kwwwwk.|kwwwwwwk|kwBBBBwk|kwwwwwwk|.kkkkkk." },
            { "cheese", "........|......kk|....kkyk|..kkyyyk|kkyyYyyk|kyyyyyYk|kYyyYyyk|kkkkkkkk" },
            { "mutton", "....kkk.|...kpppk|..kpprpk|..kprrrk|.kwkrrk.|kwwkkk..|.kwk....|..k....." },
            { "silk", "........|..kkk..w|.kpppkk.|kppwpppk|kpppppwk|kpwppppk|.kkkkkk.|........" },
            { "goldfleece", "........|..kkk..y|.kyyykky|kyywyyyk|kyyyyywk|kywyyYyk|.kkkkkk.|........" },
            { "glowwool", "g.......|..kkk..g|.kgggkk.|kggwgggk|kgggggwk|kgwgghgk|.kkkkkk.|g......." },
            { "cream", "..kkkk..|.kwwwwk.|.kkkkkk.|kwyyyywk|kwyyyywk|kwwyywwk|kwwwwwwk|.kkkkkk." },
            { "dna", "kk...kk.|kBk.kgk.|.kBkgk..|..kgk...|.kgkBk..|kgk.kBk.|kk...kk.|........" },
            { "chocomilk", "..kkkk..|..kllk..|..kwwk..|.kwwwwk.|kwwwwwwk|kwddddwk|kwwwwwwk|.kkkkkk." },
            { "cottoncandy", "..kkkk..|.kppppk.|kppwpppk|kpppppPk|.kppppk.|..kkbk..|....b...|....b..." },
        };

        /// UI icon set (9x9 art pixels, UI palette). Building signs keep using Art above.
        public static Color32 UiColor(char c)
        {
            switch (c)
            {
                case 'K': return Pal.H("120e0c");
                case 'k': return Pal.H("2b211b");
                case 'W': return Pal.H("f4efe6");
                case 'w': return Pal.H("cfc4b2");
                case 'Q': case 'E': return Pal.H("ffffff");
                case 'I': return Pal.H("eef4ff");
                case 'i': return Pal.H("b9c6dc");
                case 'G': return Pal.H("ffdc5a");
                case 'g': return Pal.H("c9a227");
                case 'Y': return Pal.H("fff3b0");
                case 'R': return Pal.H("ff6454");
                case 'r': return Pal.H("8c2f2f");
                case 'N': return Pal.H("80ea6a");
                case 'n': return Pal.H("3f8a3a");
                case 'B': return Pal.H("6b4f35");
                case 'b': return Pal.H("4a3624");
                case 'H': return Pal.H("8a6a44");
                case 'S': return Pal.H("8f8a84");
                case 's': return Pal.H("5d5853");
                case 'C': return Pal.H("f0e8d8");
                case 'D': return Pal.H("a09888");
                case 'O': return Pal.H("f08a3a");
                case 'o': return Pal.H("b85a1f");
                case 'U': return Pal.H("6ab0ff");
                case 'u': return Pal.H("3a6ea8");
                case 'P': return Pal.H("e8b4a8");
                case 'p': return Pal.H("c98f84");
                case 'M': return Pal.H("8a7a6e");
                case 'A': return Pal.H("9a9aa6");
                case 'a': return Pal.H("6b6b78");
                case 'L': return Pal.H("d8d8e0");
                case 'Z': return Pal.H("5a4d44");
                case 'F': return Pal.H("2b2522");
                case 'V': return Pal.H("b07cff");
                case 'v': return Pal.H("7a4ab8");
                default: return Pal.H("ff00ff");
            }
        }

        public static readonly Dictionary<string, string> Ui = new Dictionary<string, string>
        {
            { "coin", "..KKKKK..|.KYYGGGK.|KYGGGGGgK|KYGgggGgK|KGGgGGGgK|KGGgggGgK|KGGGGGggK|.KgggggK.|..KKKKK.." },
            { "dna", "KK.....KK|KUKSSSKNK|.KUK.KNK.|..KUKNK..|...KNK...|..KNKUK..|.KNK.KUK.|KNKSSSKUK|KK.....KK" },
            { "silk", "..KK.KK.Q|.KPPKPPK.|KPQPPPPPK|KPPPPPPpK|KPPPQPPpK|KPPPPPppK|.KpPPppK.|..KKKKK.." },
            { "goldfleece", "..KK.KK.Q|.KYYKYYK.|KYQGGGGGK|KGGGGGGgK|KGGGYGGgK|KGGGGGggK|.KgGGggK.|..KKKKK.." },
            { "glowwool", "Y.KK.KK.Y|.KNNKNNK.|KNYNNNNNK|KNNNNNNnK|KNNNYNNnK|KNNNNNnnK|.KnNNnnK.|Y.KKKKK.Y" },
            { "cream", "..KKKKK..|.KWQWWWK.|KWWWWWWWK|KKKKKKKKK|KCYYYYYCK|KCYGYGYCK|KCYYYYYCK|KCCCCCCCK|.KKKKKKK." },
            { "chocomilk", "..KKKKK..|..KCCCK..|..KWWWK..|.KWWWWWK.|KWWQWWWWK|KWbbbbbWK|KWbBbBbWK|KWWWWWWwK|.KKKKKKK." },
            { "cottoncandy", "..KKKKK..|.KPPPPPK.|KPPQPPPPK|KPPPPPpPK|.KPpPPPK.|..KKHKK..|....H....|....H....|....H...." },
            { "wool", "..KK.KK..|.KWWKWWK.|KWQWWWWWK|KWWWWWWwK|KWWWWWWwK|KWWWWWwwK|.KwWWwwK.|..KKKKK.." },
            { "cleanwool", "..KK.K.Y.|.KIIKIYGY|KIQIIIIY.|KIIIIIIiK|KIIIIIIiK|KIIIIIiiK|.KiIIiiK.|..KKKKK.." },
            { "yarn", "..KKKKK..|.KVVvVVK.|KVvVVVvVK|KVVvVvVVK|KvVVVVVvK|KVVvVvVVK|.KVVVvVK.|..KKKKKKK|.......K." },
            { "fabric", ".KKKKKKK.|KUUUUUUUK|KUuUUuUUK|KUUUUUUUK|KKKKKKKKK|KUUUUUUUK|KUuUUuUUK|KUUUUUUUK|.KKKKKKK." },
            { "clothing", ".KK...KK.|KRRKKKRRK|KRRRRRRRK|KKRRRRRKK|.KRWRWRK.|.KRRRRRK.|.KRWRWRK.|.KRRRRRK.|.KKKKKKK." },
            { "luxury", "K...K...K|KK.KGK.KK|KGKGGGKGK|KGGGGGGGK|KGRGUGRGK|KGGGGGGGK|KKKKKKKKK" },
            { "food", ".....N.N.|.....NnN.|....OoN..|...OOOo..|...OOo...|..OOOo...|..OOo....|.OOo.....|.Oo......" },
            { "flock", "..WWWWW..|.WWQWWWW.|MMWMMMWMM|.MMEMEMM.|..MMMMM..|..MMMMM..|...MPM..." },
            { "happy", "..KKKKK..|.KNNNNNK.|KNNKNKNNK|KNNKNKNNK|KNNNNNNNK|KNKNNNKNK|KNNKKKNNK|.KNNNNNK.|..KKKKK.." },
            { "meh", "..KKKKK..|.KGGGGGK.|KGGKGKGGK|KGGKGKGGK|KGGGGGGGK|KGGGGGGGK|KGGKKKGGK|.KGGGGGK.|..KKKKK.." },
            { "sad", "..KKKKK..|.KRRRRRK.|KRRKRKRRK|KRRKRKRRK|KRRRRRRRK|KRRKKKRRK|KRKRRRKRK|.KRRRRRK.|..KKKKK.." },
            { "research", "...KKK...|...KCK...|...KCK...|..KCCCK..|.KCCCCCK.|KCUUUUUCK|KUUuUUUUK|KUUUUuUUK|.KKKKKKK." },
            { "power", "....KKKK.|...KGGK..|..KGGK...|.KGGGGGK.|..KKGGK..|...KGK...|..KGK....|..KK....." },
            { "skull", "..KKKKK..|.KCCCCCK.|KCCCCCCCK|KCKKCKKCK|KCKKCKKCK|KCCCKCCCK|.KCCCCCK.|..KCKCK..|..KKKKK.." },
            { "hammer", ".KKKKKK..|KSSSSSSK.|KssssssK.|.KKKBKK..|...KBK...|...KBK...|...KHK...|...KbK...|...KKK..." },
            { "shears", "KK.....KK|KSK...KSK|.KSK.KSK.|..KSKSK..|...KGK...|..KRKRK..|.KRK.KRK.|KRRK.KRRK|.KK...KK." },
            { "bag", "...KKK...|..KbBbK..|...KbK...|..KBBBK..|.KBHBBBK.|KBHBGGBBK|KBBGBBBBK|KBBBGGBbK|.KKKKKKK." },
            { "map", ".KKKKKKK.|KCDCCRCRK|KCCDCCRCK|KCCCDRCRK|KCCCCDCCK|KCCCDCCCK|KCCDCCCCK|KCCCCCCCK|.KKKKKKK." },
            { "speaker", "....K....|...KK..D.|KKKCK...D|KCCCK.D.D|KCCCK.D.D|KKKCK...D|...KK..D.|....K...." },
            { "mute", "....K....|...KK....|KKKCKR..R|KCCCK.RR.|KCCCK.RR.|KKKCKR..R|...KK....|....K...." },
            { "pause", ".CC.CC.|.CC.CC.|.CC.CC.|.CC.CC.|.CC.CC.|.CC.CC.|.CC.CC." },
            { "play", ".C.....|.CC....|.CCC...|.CCCC..|.CCC...|.CC....|.C....." },
            { "ff", "C...C...|CC..CC..|CCC.CCC.|CCCCCCCC|CCC.CCC.|CC..CC..|C...C..." },
            { "fff", "C...C...C...|CC..CC..CC..|CCC.CCC.CCC.|CCCCCCCCCCCC|CCC.CCC.CCC.|CC..CC..CC..|C...C...C..." },
            { "sprout", ".NN...NN.|NNNN.NNNN|.NNnNnNN.|...nNn...|....n....|....n....|..bbbbb..|.bbbbbbb." },
            { "sun", "....G....|.G.....G.|...GGG...|..GYGGG..|G.GGGGg.G|..GGGgg..|...ggg...|.G.....G.|....G...." },
            { "leaf", "......KK.|....KKOK.|...KOOOK.|..KOOoOK.|.KOOoOOK.|.KOoOOK..|.KoOKK...|KoKK.....|K........" },
            { "snow", "....L....|.L..L..L.|..L.L.L..|...LLL...|LLLLQLLLL|...LLL...|..L.L.L..|.L..L..L.|....L...." },
            { "lock", "..KKKKK..|.KSSSSSK.|.KSK.KSK.|.KSK.KSK.|KKKKKKKKK|KGGGGGGGK|KGGGKGGGK|KgggKgggK|KKKKKKKKK" },
            { "caret", "CCCCCCC|.CCCCC.|..CCC..|...C..." },
            { "close", "CC...CC|CCC.CCC|.CCCCC.|..CCC..|.CCCCC.|CCC.CCC|CC...CC" },
            { "heart", ".KK...KK.|KRRK.KRRK|KRQRRRRRK|KRRRRRRRK|.KRRRRRK.|..KRRRK..|...KRK...|....K...." },
            { "eye", "..KKKKK..|.KCCCCCK.|KCCKKKCCK|KCCKUKCCK|KCCKKKCCK|.KCCCCCK.|..KKKKK.." },
            { "tomb", "..KKKKK..|.KSSSSSK.|KSSSsSSSK|KSSsssSSK|KSSSsSSSK|KSSSsSSSK|KSSSSSSSK|KsSSSSSsK|KKKKKKKKK" },
            { "check", ".......KK|......KNK|KK...KNK.|KNK.KNK..|.KNKNK...|..KNK....|...K....." },
            { "star", "....K....|...KGK...|KKKKGKKKK|KGGGGGGGK|.KGGYGGK.|..KGGGK..|.KGGKGGK.|.KGK.KGK.|.KK...KK." },
            { "exclaim", "KKKKKKK|KGGKGGK|KGGKGGK|KGGKGGK|KGGKGGK|KGGGGGK|KGGKGGK|KKKKKKK" },
            { "arrow", "KKKKKKK|KGGGGGK|.KGGGK.|..KGK..|...K..." },
            { "milk", "..KKKKK..|..KCCCK..|..KWWWK..|.KWWWWWK.|KWWQWWWWK|KWUUUUUWK|KWUuUUUWK|KWWWWWWwK|.KKKKKKK." },
            { "cheese", ".......KK|.....KKGK|...KKGGgK|.KKGGGGGK|KGGGgGGgK|KGgGGGGGK|KGGGGgGGK|KgGGGGGgK|KKKKKKKKK" },
            { "crown", "K.K.K.K.K|KGKGKGKGK|KGGGGGGGK|KGRGUGRGK|KGGGGGGGK|KgggggggK|KKKKKKKKK" },
            { "mutton", "....KKKK.|...KPPPRK|..KPPPRRK|..KPPRRrK|.KWKPRrK.|KWWKKKK..|KWWK.....|.KK......|........." },
            { "pen", "K..K..K..K|KHKKHKKHKK|KHHHHHHHHK|KHKKRRKKHK|KHHHRRHHHK|KHKKRRKKHK|KHK.RR.KHK|KKK.KK.KKK" },
            { "wolf", "KK..........KK|KaK........KaK|KAaK......KaAK|KAAaKKKKKKaAAK|KAAAAAAAAAAAAK|KAAAAAAAAAAAAK|KAARRAAAARRAAK|KAAAAAAAAAAAAK|.KAAALLLLAAAK.|..KALLLLLLAK..|..KALLKKLLAK..|...KLQLLQLK...|....KKKKKK...." },
        };

        static Icons()
        {
            Ui["pop"] = Ui["flock"];
            Ui["skull_off"] = Ui["skull"].Replace('C', 'Z');
            Ui["exclaim_red"] = Ui["exclaim"].Replace('G', 'R');
            Ui["pause_dim"] = Ui["pause"].Replace('C', 'D');
            foreach (var k in new[] { "pause", "play", "ff", "fff", "close", "caret" }) Ui[k + "_ink"] = Ui[k].Replace('C', 'K');
        }

        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        public static bool Has(string name) { return Ui.ContainsKey(name) || Art.ContainsKey(name); }

        public static Texture2D Tex(string name)
        {
            Texture2D t;
            if (cache.TryGetValue(name, out t)) return t;
            string art;
            bool ui = Ui.TryGetValue(name, out art);
            if (!ui && !Art.TryGetValue(name, out art)) { ui = true; art = Ui["skull"]; }
            var rows = art.Split('|');
            int w = 0; foreach (var r in rows) w = Mathf.Max(w, r.Length);
            var c = new PixelCanvas(w, rows.Length);
            if (ui)
            {
                for (int j = 0; j < rows.Length; j++)
                    for (int i = 0; i < rows[j].Length; i++)
                        if (rows[j][i] != '.') c.Set(i, j, UiColor(rows[j][i]));
            }
            else c.Art(0, 0, art);
            t = c.ToTexture();
            cache[name] = t;
            return t;
        }

        public static string ForBuilding(BKind k)
        {
            switch (k)
            {
                case BKind.House: return "house";
                case BKind.Farm: return "food";
                case BKind.Shed: return "shears";
                case BKind.Market: return "coin";
                case BKind.Workshop: return "yarn";
                case BKind.Barn: return "heart";
                case BKind.Lab: return "research";
                case BKind.Storage: return "crate";
                case BKind.Textile: return "fabric";
                case BKind.Wheel: return "power";
                case BKind.Atelier: return "luxury";
                case BKind.GuardPost: return "shield";
                case BKind.Trading: return "chart";
                case BKind.Bank: return "coin";
                case BKind.Plant: return "skull";
                case BKind.Spa: return "spa";
                case BKind.Statue: return "statue";
                case BKind.Graveyard: return "tomb";
                case BKind.Rocket: return "rocket";
                case BKind.CloneVat: return "vat";
                case BKind.Flowers: return "flower";
                case BKind.Sign: return "warning";
                case BKind.Funnel: return "sheep";
                case BKind.Palace: return "luxury";
                case BKind.Dairy: return "milk";
                case BKind.Creamery: return "cheese";
                case BKind.Trough: return "food";
                case BKind.GeneLab: return "dna";
                case BKind.Pen: return "pen";
                case BKind.FeedLot: return "food";
                case BKind.Slaughter: return "mutton";
            }
            return "crate";
        }
    }
}
