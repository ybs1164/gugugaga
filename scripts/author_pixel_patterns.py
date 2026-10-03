"""Author code-native 16x16 patterns; no third-party bitmap is modified."""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1] / 'Assets/Resources/Pixel2D/Patterns'
PALETTE = dict(o='3D3546', b='80533E', w='C58E55', s='F3D9A4',
               m='8895A0', h='D5E4E5', g='8DBE64', d='527E48',
               a='58A9BD', v='337895', r='CF6B58', y='F0C66A')

def save(name, rows, outline=True):
    assert len(rows) == 16 and all(len(row) == 16 for row in rows), name
    grid = [list(row) for row in rows]
    if outline:
        # One source-pixel inner silhouette stroke, independent of object size.
        for y in range(16):
            for x in range(16):
                if rows[y][x] != '.' and any(
                    not (0 <= nx < 16 and 0 <= ny < 16) or rows[ny][nx] == '.'
                    for nx, ny in ((x-1,y),(x+1,y),(x,y-1),(x,y+1))):
                    grid[y][x] = 'o'
    chars = set(''.join(''.join(row) for row in grid)) - {'.'}
    text = '\n'.join(f'{c}={PALETTE[c]}' for c in PALETTE if c in chars)
    (ROOT / (name + '.txt')).write_text(text + '\n' + '\n'.join(''.join(r) for r in grid) + '\n', encoding='utf-8')

SHAPES = {
    'Mountain': [
        '................','.......mm.......','......mhhm......','.....mhhhmm.....',
        '....mhhhhmmm....','...mmhmmhmmmm...','...mmmmmmmmmm...','..mmmmmmmmmmmm..',
        '..mmmmhmmmmmmm..','..mmmhmmmmmmmm..','..mmmmmmmmmmmm..','..mmmmmmmmmmmm..',
        '..mmmmmmmmmmmm..','...mmmmmmmmmm...','................','................'],
    'Horse': [
        '................','....bb..........','...bbbb.........','..bwwbbb........',
        '..bwbbbb........','...bbbbbb.......','....bbbbbbb.....','....bbwwbbbb....',
        '...bbbwwbbbbb...','...bbbbbbbbbb...','...bbbbbbbbbb...','...bbbbbbbbbb...',
        '....bbb..bbb....','....bbb..bbb....','................','................'],
    'Catapult': [
        '................','................','.........mmmm...','........mmhmm...',
        '.......bbbbbb...','......bbbb......','.....bbbb.......','....bbbbbb......',
        '...bbbb.bbb.....','...bbb..bbb.....','..bwwwwwwwbb....','..bbbbbbbbbb....',
        '...mmm..mmm.....','...mmm..mmm.....','................','................'],
    'Boat': [
        '................','................','.......ww.......','......wwww......',
        '.....wwwwww.....','....wwwwwwww....','...wwwwwwwwww...','..wwwwwwwwwwww..',
        '..wwwwwwwwwwww..','..wwbbbbbbwwww..','..wwwwwwwwwwww..','...wwwwwwwwww...',
        '....bbbbbbbb....','.....bbbbbb.....','................','................'],
    'Raft': [
        '................','................','................','...bbbbbbbbbb...',
        '..bwwbwwbwwbwb..','..bwwbwwbwwbwb..','..bwwbwwbwwbwb..','..bbbbbbbbbbbb..',
        '..bwwbwwbwwbwb..','..bwwbwwbwwbwb..','..bbbbbbbbbbbb..','..bwwbwwbwwbwb..',
        '...bbbbbbbbbb...','................','................','................'],
    'Sail': [
        '................','......bb........','......bbss......','......bbsss.....',
        '......bbssss....','......bbsssss...','......bbssssss..','......bbssssss..',
        '......bbssssss..','......bbsssss...','......bbsss.....','......bb........',
        '......bb........','......bb........','................','................'],
    'Fish': [
        '................','................','................','................',
        '.....aaaaa......','....ahhaaaa.....','...ahhaaaaaa.aa.','..aaaaaoaaaaaaa.',
        '..aaaaaaaaaaaaa.','...aaaaaaaaa.aa.','....aaaaaaa.....','.....aaaaa......',
        '................','................','................','................'],
    'Starfish': [
        '................','................','.......yy.......','......yyyy......',
        '......yyyy......','..yy..yyyy..yy..','..yyyyyyyyyyyy..','...yyyyyyyyyy...',
        '....yyyyyyyy....','.....yyyyyy.....','....yyyyyyyy....','...yyyy..yyyy...',
        '...yyy....yyy...','................','................','................'],
    'Lighthouse': [
        '................','.......yy.......','......yssy......','.....yyyyyy.....',
        '.....hhhhhh.....','.....hhhhhh.....','.....rrrrrr.....','.....rrrrrr.....',
        '.....hhhhhh.....','.....hhhhhh.....','.....rrrrrr.....','.....rrrrrr.....',
        '....hhhhhhhh....','...mmmmmmmmmm...','................','................'],
    'Windmill': [
        '................','......mmmm......','......mhhm......','......mhhm......',
        '..mmmmmmmmmmmm..','..mhhsmyymshhm..','..mmmmmmmmmmmm..','.....bwmmwb.....',
        '.....bwmmwb.....','.....bwmmwb.....','.....bwwwwb.....','.....bboobb.....',
        '.....bboobb.....','....bbbbbbbb....','................','................'],
    'Arrow': [
        '................','................','.......ss.......','......ssss......',
        '.....ssssss.....','....ssssssss....','...ssssssssss...','..ssssssssssss..',
        '..ssssssssssss..','......ssss......','......ssss......','......ssss......',
        '......ssss......','......ssss......','................','................'],
    'Cross': [
        '................','................','......ssss......','......ssss......',
        '......ssss......','......ssss......','..ssssssssssss..','..ssssssssssss..',
        '..ssssssssssss..','..ssssssssssss..','......ssss......','......ssss......',
        '......ssss......','......ssss......','................','................'],
    'Heart': [
        '................','................','...sss....sss...','..sssss..sssss..',
        '..ssssssssssss..','..ssssssssssss..','..ssssssssssss..','...ssssssssss...',
        '....ssssssss....','.....ssssss.....','......ssss......','.......ss.......',
        '................','................','................','................'],
    'Crown': [
        '................','................','................','................',
        '................','....y..yy..y....','....yy.yy.yy....','....yyyyyyyy....',
        '....yryyyyry....','....yyyyyyyy....','................','................',
        '................','................','................','................'],
    'Missing': [
        '................','.rrrrrrrrrrrrrr.','.rrrrrrrrrrrrrr.','.rrssssssssrrr..',
        '.rrrrrrrrssrrrr.','.rrrrrrrssrrrrr.','.rrrrrrssrrrrrr.','.rrrrrssrrrrrrr.',
        '.rrrrrssrrrrrrr.','.rrrrrrrrrrrrrr.','.rrrrrssrrrrrrr.','.rrrrrssrrrrrrr.',
        '.rrrrrrrrrrrrrr.','.rrrrrrrrrrrrrr.','.rrrrrrrrrrrrrr.','................'],
}
for name, rows in SHAPES.items():
    save(name, rows, False)  # the composer adds the one shared stroke
for name, base, wave in [('Water','a','h'), ('Ocean','v','a')]:
    rows = [[base]*16 for _ in range(16)]
    for x,y in [(3,3),(10,8),(2,13)]:
        for dx in range(3): rows[y][x+dx] = wave
    save(name, [''.join(row) for row in rows], False)
save('Fog', ['m'*16 for _ in range(16)], False)
PALETTE['s'] = 'FFFFFF'
save('Solid', ['s'*16 for _ in range(16)], False)
