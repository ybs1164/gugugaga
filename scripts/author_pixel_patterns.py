"""Author every in-game sprite as a code-native pattern, plus Pixel2D/SpriteCatalog.csv.

Style: chunky 16-px-per-tile art drawn at Scale 2 (one art pixel = 2 detail pixels), warm saturated palette,
team colour on helmets/hoods/roofs/sails. Patterns carry NO outline: the composer strips nothing from them and
draws the one shared stroke (PixelSpriteComposer.Outline/Stroke = one art pixel) around every finished silhouette,
plus the same line wherever a front layer covers a back one — that is what keeps every border identical.

Palette entries written as `c=@RRGGBB` are team pixels: the composer multiplies them by the team colour.
Placements are given as the art-pixel position of the trimmed art's bottom-left corner on the 16x20 art canvas
(tile = rows 0-15, headroom above); a 1-pixel margin is kept free for the stroke. Rules: docs/spec/csv/sprites.md
"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1] / 'Assets/Resources/Pixel2D'
PATTERNS = ROOT / 'Patterns'

PALETTE = {
    'k': '3A2228',                                  # interior dark (eyes) - never on a silhouette edge
    's': 'F2C391', 'z': 'CC8661',                   # skin
    'w': 'D0935A', 'b': 'A0623C', 'n': '6E4030',    # wood
    'm': 'EEF1F5', 'i': 'B4BCC8', 'j': '7E8798',    # metal
    'g': 'F8D86A', 'h': 'D69A3A',                   # gold
    'l': '9BD86E', 'f': '5DAA4E', 'd': '3D7E46',    # leaf
    'r': 'E0574A', 'q': 'A9383E',                   # red cloth
    'y': 'EBC163', 'u': 'BF8A3A',                   # straw
    'p': 'A066B4', 'v': '74468A',                   # violet
    'e': 'CFC8BE', 'c': '9A918A', 'x': '6A625E',    # stone
    'a': '5DBBDB', 'A': '3C8FC4', 'o': 'B4E6F0',    # water
    'W': 'F4F4F0', 'G': 'D2D2CC',                   # white cloth
    'L': 'F0DDB6', 'Q': 'CBB088',                   # plaster
    'F': 'F59A3A', 'Y': 'FFE07A',                   # fire
    'B': '4F7FCF', 'D': '3A5EA8',                   # blue (icons)
    'P': 'F08CB0',                                  # pink (icons)
    '1': '@FFFFFF', '2': '@C2C2C2', '3': '@8C8C8C', # team ramp
}

def trim(rows):
    width = max(len(r) for r in rows)
    rows = [r.ljust(width, '.') for r in rows]
    ys = [y for y, r in enumerate(rows) if r.strip('.')]
    xs = [x for x in range(width) if any(r[x] != '.' for r in rows)]
    return [r[xs[0]:xs[-1] + 1] for r in rows[ys[0]:ys[-1] + 1]]

ART = {}

def art(name, *rows):
    ART[name] = trim(list(rows))

# ---------------------------------------------------------------- units (facing right)
art('BodySoldier',
    "....1111....", "...111111...", "..11111112..", "..12222222..", "..zsssssss..",
    "..zsksssks..", "..zsksssks..", "...zsssss...", "...b1111b...", "..sb1111bs..",
    "..s2nnnn2s..", "...222222...", "...nn..nn...", "...nn..nn...")
art('BodyHeavy',
    "....11......", "...1111.....", "...immmmi...", "..immmmmmi..", "..jiiiiiij..",
    "..zsksssks..", "..zsksssks..", "...zsssss...", "...jimmij...", "..sjmiimjs..",
    "..s1nnnn1s..", "...111111...", "...jj..jj...", "...jj..jj...")
art('BodyHood',
    "....1111....", "...111111...", "..11111111..", "..112sssss..", "..12skssks..",
    "..12skssks..", "..112ssss...", "...1112z....", "..sffffffs..", "..sfnnnnfs..",
    "...ffffff...", "...ff..ff...", "...nn..nn...", "...nn..nn...")
art('BodySpy',
    "....1111....", "...111111...", "..11111111..", "..1123zzzz..", "..123zkzkz..",
    "..112zzzzz..", "..1112zzz1..", "..11111111..", ".1111111112.", ".1111111122.",
    ".2111111222.", "..22222222..", "...nn..nn...", "...nn..nn...")
art('BodyRogue',
    "....nnnn....", "...nnnnnnn..", "..nn111111..", ".11n111111..", ".1.nsssssss.",
    "...nskssksz.", "...nskssksz.", "....zsssss..", "...b2222b...", "..sb1111bs..",
    "..sbnnnnbs..", "...bbbbbb...", "...nn..nn...", "...nn..nn...")
art('BodyRobe',
    ".....11.....", "....111.....", "...11111....", "..1111111...", ".222222222..",
    "..zsssssss..", "..zskssks...", "..zWWWWWWW..", "..1WWWWWW1..", "..s11WW11s..",
    "...111111...", "..11111111..", "..12222221..", "...nn..nn...")
art('BodyGiant',
    ".....nnnn.....", "....nnnnnn....", "...nnssssss...", "...nsssssss...", "...nsksssks...",
    "...nsksssks...", "...zsssssss...", "....zszzzs....", "..sssssssssss.", ".sssssssssssss",
    ".ssszssssszsss", ".sss.zssssz.ss", ".zss.111111.zs", ".zz..122221.zz", ".....111111...",
    "....sss..sss..", "....sss..sss..", "....zzz..zzz..")
art('RiderSoldier',
    "...1111...", "..111111..", ".11111112.", ".12222222.", ".zsksssks.",
    ".zsksssks.", "..zsssss..", "..b1111b..", ".sb1111bs.")
art('RiderHeavy',
    "...11.....", "..1111....", "..immmmi..", ".immmmmmi.", ".jiiiiiij.",
    ".zsksssks.", ".zsksssks.", "..zsssss..", "..jimmij..", ".sjmiimjs.")
art('Horse',
    "..........nn..", ".........nwww.", "........nwwkww", "........nwwwbb", "nnwwwwwwwwwb..",
    "nwwwwwwwwwwb..", "n.bwwwwwwwbb..", "..bb.bb.bb.bb.", "..nn.nn.nn.nn.")
art('Catapult',
    "........ee", ".......ecx", "......bcc.", ".....bb...", "....bb....", "...bb.....",
    ".b.bb...b.", ".bwwwwwwb.", "wbbbbbbbbw", "nn111111nn", "xnbbbbbbnx")
art('Sun', "..g..g", "gggg..", "gYYgg.", "gYYYgg", ".gYYg.", "g.gg.g")
art('Bolt', "..Y", ".YY", "YYY", ".YY", ".Y.", "Y..")
art('Sword', ".m..", ".mi.", ".mi.", ".mi.", ".mi.", "hggh", ".bn.", ".nn.")
art('Club', ".ww.", "wwbw", "wwbb", "wbbb", ".bb.", ".bn.", ".bn.", ".nn.")
art('Spear', ".m.", "mmi", "mii", ".j.", ".b.", ".b.", ".b.", ".b.", ".b.", ".b.", ".b.", ".n.", ".n.", ".n.")
art('Lance', ".m..", ".m..", "mmi.", ".j..", ".b11", ".b12", ".b2.", ".b..", ".b..", ".b..", ".b..", ".b..", ".n..", ".n..")
art('Shield', ".jjjj.", "j1111j", "j1gg2j", "j1gg2j", "j1122j", "j1223j", ".j22j.", "..jj..")
art('Bow', "ww..", ".bw.", "..bw", "..bw", "..bw", "..bw", "..bw", "..bw", ".bw.", "ww..")
art('Staff', ".p.", "ppp", "pWp", "ppp", ".b.", ".b.", ".b.", ".b.", ".b.", ".b.", ".b.", ".b.", ".n.")
art('Dagger', ".m.", ".m.", ".mi", "hgh", ".n.", ".n.")

# ---------------------------------------------------------------- boats
art('Hull', "ww..........ww", "wwwwwwwwwwwwww", "b111111111111b", ".bbbbbbbbbbbb.", "..bbbbbbbbbb..", "...nnnnnnnn...")
art('DarkHull', "nn..........nn", "nnnnnnnnnnnnnn", "x111111111111x", ".xxxxxxxxxxxx.", "..xxxxxxxxxx..", "...xxxxxxxx...")
art('MetalHull', "m............m", "mmmmmmmmmmmmmm", "i111111111111i", "iijiijiijiijii", ".iiiiiiiiiiii.", "..jjjjjjjjjj..", "...jjjjjjjj...")
art('SmallHull', "w........w", "wwwwwwwwww", "b11111111b", ".bbbbbbbb.", "..nnnnnn..")
art('Raft', "wwwywwwwwwywww", "bbbubbbbbbubbb", "wwwywwwwwwywww", ".nnnnnnnnnnnn.")
art('Sail', "n....", "nWW..", "nWWW.", "nWWWG", "n1112", "nWWWG", "nWWWG", "n1112", "nWWG.", "nWG..", "n....", "n....", "n....")
art('DarkSail', "n....", "nxx..", "nxxx.", "nxxxx", "nWxWx", "nxWxx", "nWxWx", "nxxxx", "nxxx.", "nxx..", "n....", "n....", "n....")
art('Ram', "mm..", "miim", "iijj", "jj..")
art('Cannon', "jiiimm", "jjiiii", ".nnn..")
art('Mortar', ".jj.", "jiij", "jiij", "xjjx")

# ---------------------------------------------------------------- terrain & resources
art('Tree',
    "..llll..", ".lllllf.", "lllllfff", "llllffff", "lflfffdf",
    "fffffdfd", ".ffdfdd.", "..dddd..", "...bn...", "...bn...")
art('Pine', "...l...", "..llf..", "..lff..", ".llffd.", ".lfffd.", "llfffdd", "lffffdd", "...n...", "...n...")
art('Mountain',
    ".....WW.......", "....WWWG......", "....WGWWG.....", "...cWccGc.....", "...ccccccc..e.",
    "..ecccccxc.eec", "..eccccxxxeecc", ".eccccccxxeccx", ".ecccccccxccxx", "eccccccccxcxxx",
    "eccccccccxxxxx")
art('Boulders', "..eee.....", ".eeecc..ee", "eeecccxeec", "eccccxxecx", "cccccxxccx", ".xxxxx.xx.")
art('Bush', "...llff...", ".llflrfff.", "llrlffffdf", "lfffrfdfrd", "ffrfffffdd", ".fdfrdfdd.", "..dddddd..")
art('Wheat', "..yy.yy.", ".yuyyuyy", ".yyuyyu.", "..yuuy..", "...rr...", "..yuuy..", ".yu..uy.", "yu....uy")
art('Deer', "......n.n.", ".......n..", "......wwww", "......wkww", "wwwwwwwww.", "bwwwwwwww.", ".wwwwwwb..", ".bb...bb..")
art('Ore', "...eee....", "..eeecc...", ".eegcccx..", "eecccgccx.", "eccmccccxx", "ccccccxxxx", ".xxxxxxxx.")
art('Mushrooms', ".rrrr.....", "rWrrWr....", "rrrrrr.rr.", "..LL..rWrr", "..LL...LL.", "..LL...LL.")
art('Fish', "..iiii...", ".iimiii.i", "iikiiiiii", ".ijjjji.i", "..jjjj...")
art('Starfish', "...yy...", "...yy...", "yyyyyyyy", ".yyFFyy.", "..yyyy..", ".yy..yy.", "yy....yy")
art('Lighthouse', "..gg..", ".gYYg.", ".jjjj.", ".WWWW.", ".rrrr.", ".rrrr.", ".WWWW.", ".WWWW.", ".rrrr.", ".rrrr.", "WWWWWW", "cccccc")
art('Ruin', "ee........", "ec........", "ec........", "ec....ee..", "ec....ec..", "ec....ec..", "ecc.c.ecc.", "cccccccccc")

# ---------------------------------------------------------------- buildings
art('Hut', "...yy...", "..yyyy..", ".yyyyuy.", "yyyyyuuy", "uuuuuuuu", ".wwnnww.", ".wwnnww.", ".bbnnbb.")
art('House', "...11...", "..1111..", ".111112.", "11111122", "33333333", ".LLLLLQ.", ".LaLnnQ.", ".LLLnnQ.", ".bbbnnb.")
art('Tower', "..11..", ".1111.", "111122", "333333", ".LLLQ.", ".LaLQ.", ".LLLQ.", ".LLLQ.", ".LaLQ.", ".LLLQ.", ".LnnQ.", ".bnnb.")
art('Crown', "g...g...g", "gg.ggg.gg", "ggggggggg", "gBgrgrgBg", "hhhhhhhhh")
art('Wall', "ee.ee.ee.ee.ee", "eeeeeeeeeeeeee", "ecceccecceccec", "cccccccccccccc")
art('Flag', "n1111", "n1112", "n1122", "n1112", "n....", "n....", "n....", "n....")
art('Pennant', "nWWW", "nWgW", "nWW.", "n...", "n...", "n...")
art('Anvil', "jjjjjj.", "jmmiijj", "..jij..", ".jjjjj.", "xxxxxxx")
art('Cabin', "...bbb...", "..bwwwb..", ".bwwwwwb.", "bwwwwwwwb", "nnnnnnnnn", ".wwwnwww.", ".bbbnbbb.", ".wwwnwww.", ".bbbbbbb.")
art('Logs', "wwwwwy", "bbbbbu", "wwwwwy", "bbbbbu")
art('SawBlade', "..mm..", ".miij.", "mijjij", "mijjij", ".iiij.", "..jj..")
art('Crops',
    "y.y.y.y.y.y.y.", "uyuyuyuyuyuyuy", "bnbnbnbnbnbnbn", ".y.y.y.y.y.y.y", "uyuyuyuyuyuyuy",
    "nbnbnbnbnbnbnb", "y.y.y.y.y.y.y.", "uyuyuyuyuyuyuy", "bnbnbnbnbnbnbn")
art('MineEntrance', ".bbbb.", "bnkknb", "bkkkkb", "bkkkkb", "bkkkkb")
art('MillTower', "..bb..", ".bwwb.", ".LLLQ.", ".LLLQ.", ".LaLQ.", "LLLLQQ", "LLLLQQ", "LLnnQQ", "bbnnbb")
art('MillSails',
    ".....WW.....", ".....WG.....", ".....WG.....", ".....WG.....", ".....nn.....", "WWWWnnnnWWWW",
    "GGGGnnnnGGGG", ".....nn.....", ".....WG.....", ".....WG.....", ".....WG.....", ".....WG.....")
art('StoneHouse', "...xxx...", "..xcccx..", ".xcccccx.", "xcccccccx", "xxxxxxxxx", ".eeeeeee.", ".eFFeeee.", ".eFFennc.", ".ccccnnc.")
art('Chimney', ".Y.", "FYF", "xFx", "xcx", "xcx", "xcx", "xcx")
art('Stall', "rWrWrWrWrW", "rWrWrWrWrW", "qGqGqGqGqG", ".n......n.", ".n......n.", ".n.gg.ll.n", "wwwwwwwwww", "bbbbbbbbbb", ".n......n.")
art('Dock', "wwwwwwwwwwwwww", "bbbbbbbbbbbbbb", ".n...n...n...n", ".n...n...n...n")
art('Road', ".ecceecee.", "eccecceece", "ceeccecece", "ecceceecce", "ceecceecce", ".eceeccec.")
art('Bridge', "wwwwwwwwwwwwww", "n...n....n...n", "wbwbwbwbwbwbwb", "bwbwbwbwbwbwbw", "wbwbwbwbwbwbwb", "nnnnnnnnnnnnnn", ".n..........n.", ".n..........n.")
art('Temple', "....gg....", "..gghhgg..", "gggggggggg", "hhhhhhhhhh", ".WxWxxWxW.", ".WxWxxWxW.", ".WxWxxWxW.", ".WxWxxWxW.", "eeeeeeeeee", "cccccccccc")
art('Peak', "..W..", ".WGc.", ".ccc.", "ecccx", "ccxxx")
art('Sapling', "..l..", ".llf.", "llffd", ".lfd.", "lffdd", "..n..")
art('Altar', ".WWWWWW.", "eeeeeeee", ".cccccc.", ".ce..ec.", ".cccccc.")
art('Dove', "WW...", "WWWWG", ".WWG.", "..G..")
art('Pyramid', ".....hh.....", "....gghh....", "....gghh....", "...ggghhh...", "..gggghhhh..", "..gggnnhhh..", ".ggggnnhhhh.", "gggggggghhhh")
art('Obelisk', "..gg..", ".gggh.", ".eeec.", ".WaWc.", ".eeec.", ".eeec.", ".eeec.", ".eeec.", ".eeec.", ".eeec.", ".eeec.", "eeeecc", "cccccx")
art('Gate', "eeeeeeeeeeee", "cccccccccccc", "eer......ree", "ecr......rce", "ecq......qce", "ec........ce", "ec........ce", "ec........ce", "ec........ce", "cc........cc")
art('Tent', ".....n......", "....rWr.....", "...rWrWr....", "..rWrWrWr...", ".rWrWrWrWr..", "rWrWrWrWrWr.", ".bbbnnbbbb..", ".bbbnnbbbb..")
art('Coins', "..gg..", ".gghg.", "gghggh", "hhhhhh")
art('WizardTower', "...p..", "..pp..", ".pppp.", "pppppv", "hhhhhh", ".LLLQ.", ".LaLQ.", ".LLLQ.", ".LLLQ.", ".LaLQ.", ".LLLQ.", ".LLLQ.", "LLnnQQ", "bbnnbb")
art('Missing', "..rrrr..", ".rrWWrr.", ".rr..Wr.", "....Wrr.", "...Wrr..", "...rr...", "........", "...Wr...", "...rr...")

# ---------------------------------------------------------------- icons (9x9, Scale 2 like the world)
ICONS = {
    'attack': ["......mmi", ".....mmi.", "....mmi..", "...mmi...", "h.mmi....", ".hhi.....", ".nhh.....", "nb..h....", "n........"],
    'defense': ["iiiiiiiii", "iBBBBBBBi", "iBmBBBBBi", "iBBBBBBBi", "iBBBBBBDi", ".iBBBBDi.", ".iBBBDDi.", "..iBDDi..", "...iii..."],
    'guard': ["hhhhhhhhh", "hgggggggh", "hgWggggGh", "hggggggGh", "hgggggghh", ".hgggghh.", ".hggghhh.", "..hghhh..", "...hhh..."],
    'shields': ["iiiiiiiii", "irrrWrrri", "irrrWrrri", "iWWWWWWWi", "irrrWrrqi", ".irrWrqi.", ".irrWqqi.", "..irWqi..", "...iii..."],
    'range': ["..rrrrr..", ".rWWWWWr.", "rWWrrrWWr", "rWrrWrrWr", "rWrWWWrWr", "rWrrWrrWr", "rWWrrrWWr", ".rWWWWWr.", "..rrrrr.."],
    'selfdestruct': [".....YF..", "....n....", "...jjj...", ".jjjjjjj.", "jjmijjjjx", "jjiijjjjx", "jjjjjjjxx", ".jjjjjxx.", "..xxxxx.."],
    'counter': ["..W......", ".WW......", "WWWWWWWW.", ".WW......", "..W...W..", "......WW.", ".WWWWWWWW", "......WW.", "......W.."],
    'charge': ["FF..FF...", ".FF..FF..", "..FF..FF.", "...FF..FF", "..FF..FF.", ".FF..FF..", "FF..FF..."],
    'retreat': ["...FF..FF", "..FF..FF.", ".FF..FF..", "FF..FF...", ".FF..FF..", "..FF..FF.", "...FF..FF"],
    'ambush': ["..lllf...", ".llllff..", "llflfffd.", "lWkffWkfd", "lfffffffd", ".ffffffd.", "..ddddd.."],
    'infiltrate': ["...ppp...", "..ppppp..", ".ppvvvpp.", ".pvzzzvp.", ".pzkzkzp.", ".ppzzzpp.", "ppppppppp", "pppppppvv", ".vvvvvvv."],
    'scout': ["..WWWWW..", ".WWaaaWW.", "WWaAkAaWW", "WWaAAAaWW", ".WWaaaWW.", "..WWWWW.."],
    'herd': [".n.....n.", "..n...n..", "...www...", "..wwwww..", "..wkwkw..", "..wwwww..", "...wzw...", "...www..."],
    'convert': ["....Y....", "...pYp...", "..ppppp..", ".ppWpppp.", ".pWppppv.", ".ppppppv.", "..pppvv..", "...vvv..."],
    'combo': ["m.......m", "im.....mi", ".im...mi.", "..im.mi..", "...imi...", "..mi.im..", "hmi...imh", ".h.....h.", "n.......n"],
    'splash': ["....a....", "...aa....", "..aoaa...", "..aaaa...", "a..aa..a.", ".a.....a.", "aaa...aaa", ".AAAAAAA."],
    'stiff': [".eeeeeee.", "eeeeeeecx", "eecccccxx", "eecccccxx", "eecccccxx", "eecccccxx", "ecxxxxxxx", ".xxxxxxx."],
    'freeze': ["....o....", ".o..o..o.", "..o.o.o..", "...ooo...", "ooooWoooo", "...ooo...", "..o.o.o..", ".o..o..o.", "....o...."],
    'victory': ["ggggggggg", "gWgggggh.", ".gggggghh", "..gggghh.", "...gggh..", "....gh...", "....gh...", "...hhhh..", "..hhhhhh."],
    'defeat': ["..WWWWW..", ".WWWWWWW.", "WWWWWWWWW", "WkkWWWkkW", "WkkWWWkkW", "WWWWkWWWW", ".WWWWWWW.", "..WGWGW..", "..WGWGW.."],
    'deselect': ["rr.....rr", "rrr...rrr", ".rrr.rrr.", "..rrrrr..", "...rrr...", "..rrrrr..", ".rrr.rrr.", "rrr...rrr", "rr.....rr"],
    'turn': ["bbbbbbbbb", ".WWWWWWW.", "..WyyyW..", "...WyW...", "....y....", "...WyW...", "..WyyyW..", ".WyyyyyW.", "bbbbbbbbb"],
    'mountaineering': ["....W....", "...WWG...", "...WcG...", "..ccccx..", "..cccxx.e", ".ecccxxee", "eccccxxcc", "ccccxxxcx"],
    'meditation': ["....P....", "...PPP...", ".P.PWP.P.", "PPPPPPPPP", ".PPPPPPP.", "..fffff.."],
    'mining': [".iiiiii..", "ii....ii.", "i..b...i.", "...b.....", "...b.....", "...b.....", "...n.....", "...n....."],
    'philosophy': [".LLLLLLL.", "bLQQQQQLb", "bLLLLLLLb", "bLQQQQQLb", "bLLLLLLLb", "bLQQQQQLb", "bLLLLLLLb", ".bbbbbbb."],
    'smithing': ["...jjj...", "...jmj...", "....b....", "....b....", "jjjjjjj..", "jmmiijjj.", "..jjij...", ".jjjjjj..", "xxxxxxxx."],
    'gathering': ["..r.r.r..", ".rrrrrrr.", "wwwwwwwww", "wbwbwbwbw", "bwbwbwbwb", ".wbwbwbw.", "..bbbbb.."],
    'farming': ["y.y.y.y.y", "yuyyuyyuy", ".yuyyuyy.", "..yuuuy..", "...fff...", "...fff...", "..f.f.f..", ".f..f..f."],
    'diplomacy': ["nWWWWWW..", "nWWWWWWW.", "nWWGWWWW.", "nWWWWWG..", "nWWWW....", "n........", "n........", "n........"],
    'construction': ["eeceecee.", "ccccccccc", "eceeceece", "ccccccccc", "eeceeceec", "ccccccccc", "ceeceecee", "ccccccccc"],
    'riding': ["....nn...", "...nwww..", "..nwwkww.", "..nwwwwbb", ".nwwwwb..", ".nwwwwb..", "nwwwwwb..", "wwwwwbb.."],
    'freespirit': ["......WW.", ".....WWG.", "....WWG..", "...WWGG..", "..WWGG...", ".WWGG....", ".WGG.....", "b........"],
    'roads': ["...ce....", "...ce....", "..ccee...", "..ccee...", ".cccyee..", ".ccc.eee.", "ccccyeeee", "cccc.eeee"],
    'chivalry': ["....r....", "...rr....", "..iiiii..", ".immmmmi.", ".iiiiiii.", ".jkkkkkj.", ".iiiiiii.", ".iiijiii.", "..jjjjj.."],
    'trade': ["...nn....", "..uyyu...", ".yyyyyy..", "yyygyyyu.", "yygggyyu.", "yyygyyyu.", ".yyyyyu..", "..uuuu..."],
    'hunting': ["....m....", "...mmi...", "...mii...", "....j....", "....b....", "....b....", "....b....", "...bnb...", "....n...."],
    'forestry': ["...ll....", "..llff...", ".llfffd..", ".lffffd..", "..ffdd...", "...bn....", "...bn....", ".ccbnc..."],
    'archery': ["ww.......", ".bw...m..", "..bw.m...", "..bwm....", "..bmw....", "..mbw....", ".m.bw....", "m.bw.....", "ww......."],
    'mathematics': ["...jjj...", ".jjiiijj.", ".jiijiij.", "jijjmjjij", "jiijjjiij", ".jiijiij.", ".jjiiijj.", "...jjj..."],
    'spiritualism': ["....p....", "...pp....", "...ppv...", "..ppWpv..", "..pWWpv..", "..pppvv..", "...vvv...", "..eeeee..", ".ccccccc."],
    'fishing': ["..iiii...", ".iimiii.i", "iikiiiiii", ".ijjjji.i", "..jjjj..."],
    'ramming': ["....mm...", "...mmmi..", "bbbmmmmii", "bbbmmmmii", "...mmmi..", "....mm..."],
    'sailing': ["....n....", "....nWW..", "....nWWG.", "....nWWWG", "....n1112", "....n....", "wwwwwwwww", ".bbbbbbb.", "..nnnnn.."],
    'aquatics': ["..aa.....", ".aooa..aa", "aa..aaoo.", "........a", "..aa.....", ".aooa..aa", "aa..aaoo.", "........a"],
    'navigation': ["...www...", ".wwWWWww.", ".wWWrWWw.", "wWWWrWWWw", "wWWWiWWWw", "wWWWiWWWw", ".wWWWWWw.", ".wwWWWww.", "...www..."],
    'tech_hub': ["....g....", "....g....", "...ggg...", "gggggggg.", ".gggggg..", "..gghgg..", "..gg.gg..", ".gh...hg.", ".h.....h."],
    'hp': [".rr...rr.", "rrrr.rrrr", "rWrrrrrrr", "rrrrrrrrq", ".rrrrrrq.", "..rrrrq..", "...rrq...", "....q...."],
    'heal': ["...lll...", "...lll...", "...lll...", "lllllllll", "lllllllll", "lllllllll", "...lll...", "...lll...", "...lll..."],
    'move': ["....W....", "...WWW...", "..WWWWW..", ".WWWWWWW.", "WWWWWWWWW", "...WWW...", "...WWW...", "...WWW...", "...GGG..."],
}
for name, rows in ICONS.items():
    art('Icon_' + name, *rows)

# ---------------------------------------------------------------- 16x16 ground tiles (seamless, no stroke)
def ground(name, base, marks):
    grid = [[base] * 16 for _ in range(16)]
    for char, cells in marks:
        for x, y in cells: grid[y % 16][x % 16] = char
    ART[name] = [''.join(r) for r in grid]

def tufts(spots):
    """Grass tuft: two dark blades and one light tip per spot."""
    dark, light = [], []
    for x, y in spots:
        dark += [(x, y + 1), (x + 2, y + 1), (x + 1, y + 1)]
        light += [(x + 1, y)]
    return dark, light

SPOTS = [(2, 2), (9, 1), (13, 6), (5, 8), (11, 11), (1, 13), (7, 14)]
PALETTE.update({'H': '8CC765', 'T': '74B154', 'U': 'AEDC84',      # grass
                'S': 'ECD49A', 'R': 'D7B877', 'V': 'F7E8BE',      # sand
                'N': 'F1F6F9', 'M': 'D5E2EC',                     # snow
                'Z': 'FFFFFF', 'X': 'E2E2E2', 'K': 'F2F2F2',      # tribe field (tinted)
                'C': 'D9E1EA', 'E': 'F3F6FA', 'I': 'B9C6D4',      # cloud
                'O': '3D86C6', 'J': '5BA6DE'})                    # ocean
d, lt = tufts(SPOTS)
ground('Grass', 'H', [('T', d), ('U', lt)])
ground('Field', 'Z', [('X', d), ('K', lt)])
ground('Sand', 'S', [('R', [(3, 3), (4, 3), (12, 5), (7, 9), (8, 9), (14, 12), (2, 12), (10, 14)]),
                     ('V', [(3, 2), (12, 4), (7, 8), (14, 11), (2, 11)])])
ground('Snow', 'N', [('M', [(4, 3), (5, 3), (12, 7), (13, 7), (3, 11), (9, 13), (10, 13)])])
waves = lambda pts: [(x + dx, y) for x, y in pts for dx in range(3)]
ground('Water', 'a', [('o', waves([(2, 3), (10, 7), (4, 12)])), ('A', waves([(12, 13), (7, 1)]))])
ground('Ocean', 'O', [('J', waves([(3, 4), (11, 9), (5, 13)]))])
ground('Fog', 'C', [('E', [(x, y) for x in range(2, 8) for y in range(2, 5)] + [(x, y) for x in range(9, 15) for y in range(9, 12)]),
                    ('I', [(x, 5) for x in range(2, 8)] + [(x, 12) for x in range(9, 15)])])

# ---------------------------------------------------------------- write patterns
def save(name, rows):
    used = sorted(set(''.join(rows)) - {'.'})
    lines = [f'{c}={PALETTE[c]}' for c in used] + rows
    (PATTERNS / f'{name}.txt').write_text('\n'.join(lines) + '\n', encoding='utf-8')

for name, rows in ART.items():
    save(name, rows)
PALETTE['s'] = 'FFFFFF'
save('Solid', ['s' * 16] * 16)

# ---------------------------------------------------------------- catalog
HEADER = 'VisualId,Sheet,Column,Row,Width,Height,X,Y,Scale,FlipX,Layer,Color,MinLevel,MaxLevel,Note'
rows = [HEADER]

def row(vid, sheet, w, h, x, y, scale=2, flip=0, layer=0, color='FFFFFF', lo=0, hi='', note='Original pattern'):
    rows.append(f'{vid},{sheet},0,0,{w},{h},{x},{y},{scale},{flip},{layer},{color},{lo},{hi},{note}')

def put(vid, name, ax, ay, layer=0, color='FFFFFF', lo=0, hi='', flip=0):
    """Art-pixel anchor (bottom-left of the trimmed art) on the 16x20 canvas -> detail-pixel centre offset."""
    a = ART[name]
    w, h = len(a[0]), len(a)
    assert 1 <= ax and ax + w <= 15 and 1 <= ay and ay + h <= 19, f'{vid}/{name} leaves the stroke margin'
    row(vid, 'Pattern/' + name, w, h, 2 * ax + w - 16, 2 * ay + h - 16, layer=layer, color=color, lo=lo, hi=hi, flip=flip)

FIELDS = {'XinXi': 'B7C29A', 'Imperius': '9CCB6E', 'Bardur': '7E9A5E', 'Oumaji': 'E6D28E', 'Kickoo': '8FDCA0',
          'Hoodrick': 'C9A65C', 'Luxidoor': 'BFA3D6', 'Vengir': 'A39890', 'Zebasi': 'CDBB70', 'AiMo': 'D8E4E8',
          'Quetzali': '74B86A', 'Yadakk': 'D6AE7E'}
for vid, name, tint in [('Grass', 'Grass', 'FFFFFF'), ('Forest', 'Grass', 'E4EEDC'), ('Mountain', 'Grass', 'FFFFFF'),
                        ('Sand', 'Sand', 'FFFFFF'), ('Rock', 'Sand', 'E8E2DA'), ('Snow', 'Snow', 'FFFFFF')]:
    row('Ground.' + vid, 'Pattern/' + name, 16, 16, 0, 0, color=tint)
for tribe, tint in FIELDS.items():
    row('Ground.Field_' + tribe, 'Pattern/Field', 16, 16, 0, 0, color=tint, note='Tribe field (TribeTerrain.csv first Land tile)')
row('Ground.Water', 'Pattern/Water', 16, 16, 0, 0)
row('Ground.Ocean', 'Pattern/Ocean', 16, 16, 0, 0)

put('Terrain.Forest', 'Pine', 1, 6)
put('Terrain.Forest', 'Pine', 8, 7)
put('Terrain.Forest', 'Tree', 4, 1, layer=1)
put('Terrain.Mountain', 'Mountain', 1, 1)
put('Terrain.Rock', 'Boulders', 3, 2)
row('Terrain.Cloud', 'Pattern/Fog', 16, 16, 0, 0)
row('UI.Solid', 'Pattern/Solid', 16, 16, 0, 0, scale=1)
put('UI.Missing', 'Missing', 4, 3)
row('UI.Shadow', 'Pattern/Solid', 16, 16, 0, 0, scale=1, color='293344')

put('Structure.Village', 'Hut', 1, 5)
put('Structure.Village', 'Hut', 7, 2, layer=1)
put('Structure.Capital', 'Hut', 1, 5)
put('Structure.Capital', 'Hut', 7, 2, layer=1)
put('Structure.Capital', 'Crown', 2, 14, layer=2)
put('Structure.Ruin', 'Ruin', 3, 2)
put('Structure.Resource_Fruit', 'Bush', 3, 3)
put('Structure.Resource_Crop', 'Wheat', 4, 3)
put('Structure.Resource_Animal', 'Deer', 3, 3)
put('Structure.Resource_Metal', 'Ore', 3, 3)
put('Structure.Resource_Ore', 'Ore', 3, 3)
put('Structure.Resource_Food', 'Mushrooms', 3, 3)
put('Structure.Resource_Fish', 'Fish', 4, 5)
put('Structure.Starfish', 'Starfish', 4, 4)
put('Structure.Lighthouse', 'Lighthouse', 5, 2)

put('Building.LumberHut', 'Cabin', 2, 5)
put('Building.LumberHut', 'Logs', 8, 2, layer=1)
put('Building.Sawmill', 'Cabin', 2, 5)
put('Building.Sawmill', 'SawBlade', 8, 2, layer=1)
put('Building.Farm', 'Crops', 1, 2)
put('Building.Mine', 'Mountain', 1, 2)
put('Building.Mine', 'MineEntrance', 5, 2, layer=1)
put('Building.Windmill', 'MillTower', 5, 2)
put('Building.Windmill', 'MillSails', 2, 6, layer=1)
put('Building.Forge', 'Chimney', 9, 8)
put('Building.Forge', 'StoneHouse', 2, 2, layer=1)
put('Building.Market', 'Stall', 3, 3)
put('Building.Port', 'SmallHull', 3, 6)
put('Building.Port', 'Dock', 1, 2, layer=1)
put('Building.Road', 'Road', 3, 5)
put('Building.Bridge', 'Bridge', 1, 4)
for temple, mark in [('Temple', None), ('GreatTemple', 'Coins'), ('ForestTemple', 'Sapling'),
                     ('MountainTemple', 'Peak'), ('WaterTemple', 'Fish')]:
    vid = 'Building.' + temple
    put(vid, 'Temple', 2, 3)
    if mark: put(vid, mark, 15 - len(ART[mark][0]) - 1, 2, layer=1)
    put(vid, 'Crown', 3, 14, layer=2, lo=3)
put('Building.AltarOfPeace', 'Altar', 4, 3)
put('Building.AltarOfPeace', 'Dove', 6, 9, layer=1)
put('Building.EmperorsTomb', 'Pyramid', 2, 3)
put('Building.EyeOfGod', 'Obelisk', 5, 2)
put('Building.GateOfPower', 'Gate', 2, 3)
put('Building.GrandBazaar', 'Tent', 1, 7)
put('Building.GrandBazaar', 'Stall', 4, 2, layer=1)
put('Building.ParkOfFortune', 'Tree', 2, 5)
put('Building.ParkOfFortune', 'Coins', 8, 2, layer=1)
put('Building.TowerOfWisdom', 'WizardTower', 5, 2)
put('Building.Embassy', 'House', 3, 2)
put('Building.Embassy', 'Flag', 10, 8, layer=1)

put('City.Houses', 'House', 4, 3, hi=1)
put('City.Houses', 'House', 1, 6, lo=2, hi=3)
put('City.Houses', 'House', 7, 3, layer=1, lo=2, hi=3)
put('City.Houses', 'House', 1, 6, lo=4)
put('City.Houses', 'Tower', 8, 6, lo=4)
put('City.Houses', 'House', 5, 2, layer=1, lo=4)
put('City.Capital', 'Crown', 4, 14, layer=3)
put('City.Wall', 'Wall', 1, 1, layer=4)
put('City.Workshop', 'Anvil', 1, 1, layer=5)
put('City.Park', 'Sapling', 10, 1, layer=5)
put('City.Embassy', 'Pennant', 1, 11, layer=5)
put('City.Flag', 'Flag', 10, 10, layer=6)

BODY = 3   # standalone body anchor column; boats shift riders by UnitView.EmbarkedRider*
def unit(uid, body, weapon=None, wx=10, wy=4, back=None, front=None):
    vid = 'Unit.' + uid
    if back: put(vid, back[0], back[1], back[2])
    put(vid, body, BODY, 1, layer=1)
    if weapon: put(vid, weapon, wx, wy, layer=2)
    if front: put(vid, front[0], front[1], front[2], layer=3)
unit('infantry', 'BodySoldier', 'Club')
unit('shield', 'BodySoldier', back=('Spear', 10, 2), front=('Shield', 7, 3))
unit('gladiator', 'BodyHeavy', 'Sword', wy=5)
unit('archer', 'BodyHood', 'Bow', wx=9, wy=3)
unit('cleric', 'BodyRobe', 'Staff', wx=10, wy=2)
unit('spy', 'BodySpy')
unit('dagger', 'BodyRogue', 'Dagger', wx=10, wy=4)
put('Unit.giant', 'BodyGiant', 1, 1)
for uid, rider, weapon, wy in [('cavalry', 'RiderSoldier', 'Sword', 11), ('knight', 'RiderHeavy', 'Lance', 5)]:
    put('Unit.' + uid, 'Horse', 1, 1)
    put('Unit.' + uid, rider, 3, 8, layer=1)
    put('Unit.' + uid, weapon, 11, wy, layer=2)
put('Unit.catapult', 'Catapult', 3, 1)
put('Unit.SunAvatar', 'Sun', 9, 13)                       # mythic units: tinted giants
put('Unit.SunAvatar', 'BodyGiant', 1, 1, layer=1, color='FFE7A0')
put('Unit.StormTitan', 'BodyGiant', 1, 1, color='A9BCE0')
put('Unit.StormTitan', 'Bolt', 1, 11, layer=1)

def boat(bid, hull, extra=None, sail=None, hull_color='FFFFFF'):
    vid = 'Boat.' + bid
    if sail: put(vid, sail, 2, 5, layer=-1)
    put(vid, hull, 1 + (14 - len(ART[hull][0])) // 2, 1, color=hull_color)
    if extra: put(vid, extra[0], extra[1], extra[2], layer=1)
boat('raft', 'Raft')
boat('scout', 'Hull', sail='Sail')
boat('rammer', 'Hull', ('Ram', 11, 2))
boat('bomber', 'Hull', ('Mortar', 2, 6))
boat('dinghy', 'SmallHull')
boat('pirate', 'DarkHull', sail='DarkSail')
boat('juggernaut', 'MetalHull', ('Cannon', 2, 7))

for name in ICONS:
    a = ART['Icon_' + name]
    row('Icon.' + name, 'Pattern/Icon_' + name, len(a[0]), len(a), 0, 0)

(ROOT / 'SpriteCatalog.csv').write_text('\n'.join(rows) + '\n', encoding='utf-8')
print(f'wrote {len(ART) + 1} patterns, {len(rows) - 1} catalog rows')
