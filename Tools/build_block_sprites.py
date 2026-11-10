#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
Turn the raw art in Assets/Art into game-ready sprites in Assets/Resources/Blocks.

Rules
  * crop each block to its own bounding box (the source PNGs are exported on
    differently sized canvases, some with a white "page" margin)
  * knock the white page margin out to transparent (flood fill from the border,
    so white highlights *inside* a block survive)
  * resample to an exact 64 px per grid cell (PPU 64), point filtered
  * write a TextureImporter .meta next to it so Unity never re-slides it

Run:  python Tools/build_block_sprites.py
"""
import os, re, random
from PIL import Image, ImageSequence
from collections import deque

ART = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "Art")
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "Resources", "Blocks")

CELL = 64            # pixels per grid cell  ->  spritePixelsToUnits = 64
BAR_W = 320          # charge bar width in pixels


# ---------------------------------------------------------------- helpers
def load(name):
    return Image.open(os.path.join(ART, name)).convert("RGBA")


def key_white(im, thresh=244):
    """Flood fill the white page margin (from the border inwards) to alpha 0."""
    w, h = im.size
    px = im.load()
    seen = bytearray(w * h)
    q = deque()

    def white(x, y):
        r, g, b, a = px[x, y]
        return a > 0 and r >= thresh and g >= thresh and b >= thresh

    for x in range(w):
        for y in (0, h - 1):
            if not seen[y * w + x] and white(x, y):
                seen[y * w + x] = 1
                q.append((x, y))
    for y in range(h):
        for x in (0, w - 1):
            if not seen[y * w + x] and white(x, y):
                seen[y * w + x] = 1
                q.append((x, y))

    while q:
        x, y = q.popleft()
        px[x, y] = (0, 0, 0, 0)
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nx, ny = x + dx, y + dy
            if 0 <= nx < w and 0 <= ny < h and not seen[ny * w + nx] and white(nx, ny):
                seen[ny * w + nx] = 1
                q.append((nx, ny))

    # one feathering pass so the hand-drawn anti-aliased halo does not survive as a white edge
    edge = [(x, y) for y in range(h) for x in range(w)
            if px[x, y][3] > 0 and min(px[x, y][:3]) >= 232]
    for x, y in edge:
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nx, ny = x + dx, y + dy
            if 0 <= nx < w and 0 <= ny < h and px[nx, ny][3] == 0:
                px[x, y] = (0, 0, 0, 0)
                break
    return im


def resize(im, size):
    return im.resize(size, Image.BOX)


META_TEMPLATE = None


def meta(guid, w, h, pivot=(0.5, 0.5), ppu=CELL):
    t = META_TEMPLATE
    t = re.sub(r"guid: [0-9a-f]{32}", "guid: %s" % guid, t, count=1)
    t = t.replace("spritePixelsToUnits: 8", "spritePixelsToUnits: %d" % ppu)
    t = t.replace("spritePivot: {x: 0.5, y: 0.5}",
                  "spritePivot: {x: %g, y: %g}" % pivot)
    t = re.sub(r"width: 8\n(\s+)height: 8", "width: %d\n\\1height: %d" % (w, h), t)
    t = t.replace("pivot: {x: 0, y: 0}", "pivot: {x: %g, y: %g}" % pivot)
    t = t.replace("WhiteSquare_0", "sprite_0")
    t = t.replace("nameFileIdTable:\n      sprite_0: 2868101353237766674",
                  "nameFileIdTable: {}")
    return t


def save(im, name, pivot=(0.5, 0.5), ppu=CELL):
    path = os.path.join(OUT, name + ".png")
    im.save(path)
    # Keep the GUID of an already imported sprite: scene/prefab references must
    # survive a rebuild of this folder.
    guid = None
    meta_path = path + ".meta"
    if os.path.exists(meta_path):
        with open(meta_path, "r", encoding="utf-8") as f:
            m = re.search(r"guid: ([0-9a-f]{32})", f.read())
        if m:
            guid = m.group(1)
    if guid is None:
        guid = "%032x" % random.getrandbits(128)
    with open(meta_path, "w", encoding="utf-8", newline="\n") as f:
        f.write(meta(guid, im.width, im.height, pivot, ppu))
    print("  %-18s %dx%d" % (name, im.width, im.height))


def frames(prefix, names, pivot=(0.5, 0.5)):
    """All frames of one animation share a single crop box so they line up."""
    ims = []
    for n in names:
        im = load(n)
        im = key_white(im) if n.startswith("按钮") or n.startswith("黏液") else im
        ims.append(im)
    box = ims[0].getbbox()
    for im in ims[1:]:
        b = im.getbbox()
        box = (min(box[0], b[0]), min(box[1], b[1]), max(box[2], b[2]), max(box[3], b[3]))
    for i, im in enumerate(ims):
        save(resize(im.crop(box), (CELL, CELL)), "%s_%d" % (prefix, i + 1), pivot)


def gif_frames(filename, prefix, cell_sized=True):
    """Export the real GIF frames. The source GIFs run at 200 ms per frame."""
    im = Image.open(os.path.join(ART, filename))
    frames = [key_white(f.convert("RGBA")) for f in ImageSequence.Iterator(im)]
    box = list(frames[0].getbbox())
    for f in frames[1:]:
        b = f.getbbox()
        box = [min(box[0], b[0]), min(box[1], b[1]), max(box[2], b[2]), max(box[3], b[3])]
    bw, bh = box[2] - box[0], box[3] - box[1]
    for i, f in enumerate(frames):
        if cell_sized:
            out = resize(f.crop(tuple(box)), (CELL, CELL))
        else:
            h = max(1, round(BAR_W * bh / float(bw)))
            out = resize(f.crop(tuple(box)), (BAR_W, h))
        save(out, "%s_%d" % (prefix, i))


def main():
    global META_TEMPLATE
    with open(os.path.join(ART, "WhiteSquare.png.meta"), encoding="utf-8") as f:
        META_TEMPLATE = f.read()

    os.makedirs(OUT, exist_ok=True)
    print("writing sprites to", os.path.normpath(OUT))

    # ---- breakable crates: 4 damage frames each -------------------------
    frames("glass", ["玻璃箱1.PNG", "玻璃箱2.PNG", "玻璃箱3.PNG", "玻璃箱4.PNG"])
    frames("wood", ["木箱1.PNG", "木箱2.PNG", "木箱3.PNG", "木箱4.PNG"])
    frames("iron", ["铁箱1.PNG", "铁箱2.PNG", "铁箱3.PNG", "铁箱4.PNG"])

    # ---- single-cell blocks --------------------------------------------
    for src, dst in [("基础地块.PNG", "ground"),
                     ("黏液块.PNG", "sticky"),
                     ("光滑块.PNG", "slider"),
                     ("铁块.PNG", "metal")]:
        im = load(src)
        if src.startswith("黏液"):
            im = key_white(im)
        save(resize(im.crop(im.getbbox()), (CELL, CELL)), dst)

    # ---- refractor wedge: four rotations of the SAME blue-grey triangle ----
    # The raw art is the wedge in the lower-right corner ("/" slope).  Rotating
    # rather than mirroring keeps the light-blue bevel on the reflecting edge.
    #   mirror_lu  wedge lower-right, "/"  ball from left   -> up    (左-上)
    #   mirror_ld  wedge upper-right, "\"  ball from left   -> down  (左-下)
    #   mirror_ru  wedge lower-left,  "\"  ball from right  -> up    (右-上)
    #   mirror_rd  wedge upper-left,  "/"  ball from right  -> down  (右-下)
    tri = load("折射机关.PNG")
    tri = resize(tri.crop(tri.getbbox()), (CELL, CELL))
    save(tri, "mirror_lu")                                #   0 deg
    save(tri.transpose(Image.ROTATE_90), "mirror_ld")    #  90 deg CCW
    save(tri.transpose(Image.ROTATE_270), "mirror_ru")   #  90 deg CW
    save(tri.transpose(Image.ROTATE_180), "mirror_rd")   # 180 deg

    # Legacy names still resolve, so older scenes / saved prefabs keep working.
    save(tri, "mirror_up")
    save(tri.transpose(Image.ROTATE_270), "mirror_down")

    # ---- switch button --------------------------------------------------
    for src, dst in [("按钮关闭.PNG", "btn_off"), ("按钮打开.PNG", "btn_on")]:
        im = load(src)
        im = key_white(im)
        save(resize(im.crop(im.getbbox()), (CELL, CELL)), dst)

    # ---- iron gate: 1 cell wide, 2 cells tall, pivot sits on the lower cell
    gate = load("半边铁门.PNG")
    save(resize(gate.crop(gate.getbbox()), (CELL, CELL * 2)), "gate", pivot=(0.5, 0.25))

    # ---- charge bar: 4 states, one shared crop box ----------------------
    ims = []
    for n in ["蓄力条1.PNG", "蓄力条2.PNG", "蓄力条3.PNG", "蓄力条4.PNG"]:
        ims.append(key_white(load(n)))
    box = list(ims[0].getbbox())
    for im in ims[1:]:
        b = im.getbbox()
        box = [min(box[0], b[0]), min(box[1], b[1]), max(box[2], b[2]), max(box[3], b[3])]
    bw, bh = box[2] - box[0], box[3] - box[1]
    for i, im in enumerate(ims):
        h = max(1, round(BAR_W * bh / float(bw)))
        save(resize(im.crop(tuple(box)), (BAR_W, h)), "ui_charge_%d" % i)

    # ---- animated frames, straight from the GIFs (200 ms / frame) -------
    gif_frames("玻璃箱动态.GIF", "anim_glass")
    gif_frames("木箱动态.GIF", "anim_wood")
    gif_frames("铁箱动态.GIF", "anim_iron")
    gif_frames("蓄力条动态.GIF", "anim_charge", cell_sized=False)

    # ---- backdrop -------------------------------------------------------
    bg = load("背景.PNG")
    save(bg, "bg_room", ppu=CELL)


if __name__ == "__main__":
    main()
