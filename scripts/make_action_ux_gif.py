"""ActionUxCapture가 찍은 프레임(Logs/action_ux/frames)을 GIF 하나로 묶는다. 탭한 자리에 퍼지는 원을 그려 넣는다.

사용: python scripts/make_action_ux_gif.py [출력 경로]  (기본 docs/images/ux/action_ux.gif)
"""
import csv
import glob
import os
import sys

from PIL import Image, ImageDraw

SRC = "Logs/action_ux"
FPS = 15  # ActionUxCapture.Fps
SCALE = 0.75
RING_FRAMES = 6


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else "docs/images/ux/action_ux.gif"
    paths = sorted(glob.glob(os.path.join(SRC, "frames", "*.png")))
    if not paths:
        sys.exit("no frames in " + SRC)
    taps = []
    with open(os.path.join(SRC, "taps.csv"), newline="") as f:
        for row in csv.DictReader(f):
            taps.append((int(row["Frame"]), int(row["X"]), int(row["Y"])))
    frames = []
    for i, path in enumerate(paths):
        img = Image.open(path).convert("RGB")
        draw = ImageDraw.Draw(img)
        for frame, x, y in taps:
            age = i - frame
            if 0 <= age < RING_FRAMES:
                r = 10 + age * 4
                draw.ellipse((x - r, y - r, x + r, y + r), outline=(255, 255, 255), width=3)
                draw.ellipse((x - 4, y - 4, x + 4, y + 4), fill=(255, 255, 255))
        if SCALE != 1:
            img = img.resize((int(img.width * SCALE), int(img.height * SCALE)), Image.LANCZOS)
        frames.append(img.quantize(colors=128, method=Image.MEDIANCUT))
    os.makedirs(os.path.dirname(out), exist_ok=True)
    frames[0].save(out, save_all=True, append_images=frames[1:], duration=int(1000 / FPS), loop=0, optimize=True)
    print(f"{len(frames)} frames -> {out} ({os.path.getsize(out) // 1024} KB)")


if __name__ == "__main__":
    main()
