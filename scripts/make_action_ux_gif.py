"""ActionUxCapture가 찍은 프레임(Logs/action_ux/frames)을 GIF 하나로 묶는다.

사용: python scripts/make_action_ux_gif.py [출력 경로]  (기본 docs/images/ux/action_ux.gif)
"""
import glob
import os
import sys

from PIL import Image

SRC = "Logs/action_ux"
FPS = 15  # ActionUxCapture.Fps
SCALE = 0.75


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else "docs/images/ux/action_ux.gif"
    paths = sorted(glob.glob(os.path.join(SRC, "frames", "*.png")))
    if not paths:
        sys.exit("no frames in " + SRC)
    frames = []
    for path in paths:
        img = Image.open(path).convert("RGB")
        if SCALE != 1:
            img = img.resize((int(img.width * SCALE), int(img.height * SCALE)), Image.LANCZOS)
        frames.append(img.quantize(colors=128, method=Image.MEDIANCUT))
    os.makedirs(os.path.dirname(out), exist_ok=True)
    frames[0].save(out, save_all=True, append_images=frames[1:], duration=int(1000 / FPS), loop=0, optimize=True)
    print(f"{len(frames)} frames -> {out} ({os.path.getsize(out) // 1024} KB)")


if __name__ == "__main__":
    main()
