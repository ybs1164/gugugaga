"""Author the 9-slice UI frames (Panel, Button, Badge) in the world sprites' outline style.

Each frame is greyscale except the outline, so Image.color still tints it. The outline is the composer's
shared stroke colour and width (PixelSpriteComposer.Outline / Stroke): 2 texels of #3F2631.
Slice border = 4 texels (outline 2 + bevel 1 + margin 1); Pixel2DSetup sets it on import.
"""
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[1] / 'Assets/Resources/Pixel2D/Sheets'
SIZE = 12
OUTLINE = (63, 38, 49, 255)
CLEAR = (0, 0, 0, 0)

def frame(fill, light, shade, corner):
    """corner: how many texels to cut at each corner (1 = square-ish, 2 = rounded badge)."""
    im = Image.new('RGBA', (SIZE, SIZE), CLEAR)
    for y in range(SIZE):
        for x in range(SIZE):
            dx, dy = min(x, SIZE - 1 - x), min(y, SIZE - 1 - y)
            if dx + dy < corner: continue                    # cut corner
            if dx < 2 or dy < 2 or dx + dy < corner + 2:     # 2-texel outline, following the cut
                im.putpixel((x, y), OUTLINE); continue
            if dy == 2 and y < SIZE // 2 or dx == 2 and x < SIZE // 2: c = light   # top/left bevel
            elif dy == 2 or dx == 2: c = shade                                       # bottom/right bevel
            else: c = fill
            im.putpixel((x, y), c + (255,))
    return im

frame((0x64, 0x76, 0x85), (0x8a, 0x9c, 0xae), (0x52, 0x60, 0x6d), 1).save(ROOT / 'Panel.png')
frame((0x94, 0xaf, 0xc6), (0xc8, 0xd8, 0xe6), (0x64, 0x76, 0x85), 1).save(ROOT / 'Button.png')
frame((0xff, 0xff, 0xff), (0xff, 0xff, 0xff), (0xc8, 0xd0, 0xd8), 2).save(ROOT / 'Badge.png')
print('wrote Panel, Button, Badge')
