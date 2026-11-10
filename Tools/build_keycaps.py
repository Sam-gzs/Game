import os
import re
import uuid
from PIL import Image

ART = "Assets/Art"
OUT = "Assets/Resources/Keys"
TEMPLATE = "Assets/Resources/Blocks/ground.png.meta"
TARGET_H = 96          # every key cap is normalised to this pixel height
PPU = TARGET_H         # so the sprite is 1 world unit tall

NAMES = {
    "A键.png": "key_a",
    "W键.png": "key_w",
    "D键.png": "key_d",
    "S键.png": "key_s",
    "E键.png": "key_e",
    "Q键.png": "key_q",
    "上键.png": "key_up",
    "右键.png": "key_right",
    "左键.png": "key_left",
    "左键鼠标.png": "mouse_left",
    "右键鼠标.png": "mouse_right",
}


def make_meta(path, w, h, ppu):
    with open(TEMPLATE, encoding="utf-8") as f:
        t = f.read()
    t = re.sub(r"guid: [0-9a-f]{32}", "guid: %s" % uuid.uuid4().hex, t, count=1)
    t = re.sub(r"spritePixelsToUnits: \d+", "spritePixelsToUnits: %d" % ppu, t)
    t = re.sub(r"spritePivot: \{x: [\d.]+, y: [\d.]+\}", "spritePivot: {x: 0.5, y: 0.5}", t)
    t = re.sub(r"        width: \d+\r?\n        height: \d+",
               "        width: %d\n        height: %d" % (w, h), t)
    with open(path + ".meta", "w", encoding="utf-8", newline="\n") as f:
        f.write(t)


def main():
    os.makedirs(OUT, exist_ok=True)
    done = 0
    for src, name in NAMES.items():
        p = os.path.join(ART, src)
        if not os.path.exists(p):
            print("skip missing", src)
            continue
        im = Image.open(p).convert("RGBA")
        bbox = im.getbbox()
        if bbox is None:
            print("skip empty", src)
            continue
        im = im.crop(bbox)
        w, h = im.size
        k = TARGET_H / float(h)
        nw, nh = max(1, int(round(w * k))), TARGET_H
        im = im.resize((nw, nh), Image.LANCZOS)
        dst = os.path.join(OUT, name + ".png")
        im.save(dst)
        make_meta(dst, nw, nh, PPU)
        print("%-14s <- %-14s bbox=%s -> %dx%d" % (name, src, bbox, nw, nh))
        done += 1
    print("\n%d key caps written to %s" % (done, OUT))


main()
