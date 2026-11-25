"""Author the 9-slice UI frames (Panel, Button, Badge).

The HUD is flat: every frame is a plain white square, so Image.color is the colour on screen
(palette: View/UiKit). The three sheets stay separate because the HUD tells cards apart by sheet
name (UiKit.ApplyTheme) and prefabs reference them. Slice border = 4 texels; Pixel2DSetup sets it on import.
"""
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[1] / 'Assets/Resources/Pixel2D/Sheets'
SIZE = 12

for name in ('Panel', 'Button', 'Badge'):
    Image.new('RGBA', (SIZE, SIZE), (255, 255, 255, 255)).save(ROOT / f'{name}.png')
print('wrote Panel, Button, Badge')
