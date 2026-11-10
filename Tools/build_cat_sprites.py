#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
Turn the five cat GIFs in Assets/Art/Cat into game-ready sprites.

Rules
  * every clip shares ONE crop box and ONE 64x64 canvas (PPU 64, point filter,
    centred pivot). Swapping animation states therefore never shifts the cat.
  * the source GIFs are already transparent; no white-page keying needed.
  * each GIF frame is exported as its own sprite, named <prefix>_<index>.
  * the grey-white cat only has a "stuck on slime" clip in the source art, so
    grey-white motion / idle sprites are derived from the orange frames by
    desaturating the fur. These are PLACEHOLDERS - drop in real art later under
    the same names and nothing else has to change.

Run:  python Tools/build_cat_sprites.py
"""
import os
import sys
from PIL import Image, ImageSequence

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import build_block_sprites as base

ROOT = os.path.join(HERE, "..")
ART = os.path.join(ROOT, "Assets", "Art", "Cat")
OUT = os.path.join(ROOT, "Assets", "Resources", "Cat")
CELL = 64
PPU = 64

base.META_TEMPLATE = open(
    os.path.join(ROOT, "Assets", "Art", "WhiteSquare.png.meta"),
    encoding="utf-8").read()
base.OUT = OUT
os.makedirs(OUT, exist_ok=True)

# file, prefix, frame count (checked against the source)
CLIPS = [
    ("橘猫_翻滚.GIF",      "cat_o_roll",  11),  # ball: rolling / flying
    ("橘猫_行走.GIF",      "cat_o_walk",   5),  # cat entity: walking
    ("橘猫_黏液附着.GIF",  "cat_o_stuck",  3),  # orange cat stuck on slime (blink)
    ("橘猫_蜷缩.GIF",      "cat_o_curl",   6),  # standing -> curl into a ball
    ("灰白猫_黏液附着.GIF", "cat_g_stuck", 3),  # grey cat stuck on slime (blink)
]


def read_frames(filename):
    im = Image.open(os.path.join(ART, filename))
    return [f.convert("RGBA") for f in ImageSequence.Iterator(im)]


def grey_fur(im):
    """Desaturate only the orange/brown fur; keep outline, belly, nose and eyes."""
    im = im.copy()
    px = im.load()
    w, h = im.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a < 16:
                continue
            # orange fur: red dominant, green above blue, not the pink nose / blue eyes
            if r > b + 28 and g > b + 18 and r > 95 and b < 200:
                v = int(0.30 * r + 0.55 * g + 0.15 * b)
                v = max(72, min(210, int(v * 0.92 + 12)))
                px[x, y] = (v, int(v * 0.97), int(v * 0.93), a)
    return im


def main():
    clips = {}
    for filename, prefix, expected in CLIPS:
        frames = read_frames(filename)
        assert len(frames) == expected, (filename, len(frames), expected)
        clips[filename] = frames

    # one crop box for every clip in every animation
    box = None
    for frames in clips.values():
        for f in frames:
            b = f.getbbox()
            if b is None:
                continue
            box = b if box is None else (
                min(box[0], b[0]), min(box[1], b[1]),
                max(box[2], b[2]), max(box[3], b[3]))
    print("shared crop box:", box, "-> %dx%d canvas" % (CELL, CELL))
    print("writing sprites to", os.path.normpath(OUT))

    def emit(frames, prefix):
        for i, f in enumerate(frames):
            base.save(base.resize(f.crop(box), (CELL, CELL)),
                      "%s_%d" % (prefix, i), pivot=(0.5, 0.5), ppu=PPU)

    for filename, prefix, _ in CLIPS:
        emit(clips[filename], prefix)

    # ---- placeholders for the missing grey-cat motion / idle art ---------
    orange_roll = clips["橘猫_翻滚.GIF"]
    orange_walk = clips["橘猫_行走.GIF"]
    emit([grey_fur(f) for f in orange_roll], "cat_g_roll")
    emit([grey_fur(f) for f in orange_walk], "cat_g_walk")
    base.save(base.resize(grey_fur(orange_roll[10]).crop(box), (CELL, CELL)),
              "cat_g_idle", pivot=(0.5, 0.5), ppu=PPU)
    print("  cat_g_idle     64x64  (grey placeholder, front-facing blink pose)")


if __name__ == "__main__":
    main()
