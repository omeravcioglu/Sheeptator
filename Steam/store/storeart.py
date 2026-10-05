"""Builds the Steam capsules, library art and icons for Sheeptator.

Inputs (src/): raw art exported from the game by the store harness (see README in this folder):
  logo.png              the in-game SHEEPTATOR wordmark at 1 art px = 1 px (transparent)
  leader_stern.png ...  48x48 Supreme Leader portraits (transparent)
  scene_*.png           camera renders without HUD at 8 screen px per art px
Run:  python storeart.py      (writes the finished PNGs next to this file)
"""
import math
import os
import random
import sys

from PIL import Image, ImageDraw, ImageFilter, ImageEnhance

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "src")
OUT = HERE


def H(h):
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


INK = H("120e0c")
RED_DD = H("3e1414")
RED_D = H("5e1e1e")
RED = H("8c2f2f")
RED_L = H("a83a32")
GOLD = H("ffdc5a")
GOLD_LO = H("c9a227")
CREAM = H("f4efe6")
GRASS_D = H("1e2a18")


def load(name):
    return Image.open(os.path.join(SRC, name)).convert("RGBA")


def up(img, k):
    """Integer nearest-neighbour upscale (keeps pixel art crisp)."""
    return img.resize((img.width * k, img.height * k), Image.NEAREST)


def down_exact(img, k):
    """Exact integer downscale of a render made at k x (pixels are k x k blocks)."""
    return img.resize((img.width // k, img.height // k), Image.NEAREST)


def logo_img(lines=None):
    """The game's wordmark (pixel-exact port of LogoArt.cs); `lines` stacks it for tall formats."""
    sys.path.insert(0, HERE)
    import woollogo
    return woollogo.wordmark("SHEEPTATOR", lines=lines)


def trim(img):
    bb = img.getbbox()
    return img.crop(bb) if bb else img


def sunburst(w, h, cx, cy, block, rays=28, a=RED, b=RED_L, rot=0.0):
    """Propaganda rays from (cx, cy), drawn at 1/block resolution then upscaled for pixel edges."""
    sw, sh = max(1, math.ceil(w / block)), max(1, math.ceil(h / block))
    img = Image.new("RGB", (sw, sh), a)
    px = img.load()
    scx, scy = cx / block, cy / block
    for y in range(sh):
        for x in range(sw):
            ang = math.atan2(y - scy, x - scx) + rot
            k = int(math.floor((ang + math.pi) / (2 * math.pi) * rays))
            if k % 2 == 0:
                px[x, y] = b
    big = img.resize((sw * block, sh * block), Image.NEAREST).crop((0, 0, w, h))
    return big.convert("RGBA")


def radial_dark(w, h, cx, cy, inner, outer, color=RED_DD, strength=0.85):
    """Darkens towards the edges (vignette)."""
    m = Image.new("L", (w, h), 0)
    px = m.load()
    for y in range(h):
        for x in range(w):
            d = math.hypot((x - cx) / outer[0], (y - cy) / outer[1])
            t = min(1.0, max(0.0, (d - inner) / (1 - inner)))
            px[x, y] = int(255 * strength * t * t)
    over = Image.new("RGBA", (w, h), color + (255,))
    over.putalpha(m)
    return over


def outline(img, k, color=INK):
    """Adds a k-px ink outline around the opaque area (for pasting art onto busy backgrounds)."""
    a = img.split()[3]
    grown = a.filter(ImageFilter.MaxFilter(2 * k + 1))
    base = Image.new("RGBA", img.size, color + (255,))
    base.putalpha(grown)
    base.alpha_composite(img)
    return base


def pad(img, p):
    out = Image.new("RGBA", (img.width + 2 * p, img.height + 2 * p), (0, 0, 0, 0))
    out.alpha_composite(img, (p, p))
    return out


def shadow(img, dx, dy, alpha=150):
    a = img.split()[3].point(lambda v: alpha if v > 0 else 0)
    sh = Image.new("RGBA", img.size, INK + (0,))
    sh.putalpha(a)
    out = Image.new("RGBA", (img.width + abs(dx), img.height + abs(dy)), (0, 0, 0, 0))
    out.alpha_composite(sh, (max(dx, 0), max(dy, 0)))
    out.alpha_composite(img, (max(-dx, 0), max(-dy, 0)))
    return out


def scene_strip(scene, k_from, k_to, box):
    """Crops box (in the source render's pixels) and rescales from k_from to k_to px per art px."""
    c = scene.crop(box)
    art = down_exact(c, k_from)
    return up(art, k_to) if k_to > 1 else art


def fade_top(img, height):
    """Fades the top `height` px of an image to transparent."""
    a = img.split()[3]
    m = Image.new("L", img.size, 255)
    px = m.load()
    for y in range(min(height, img.height)):
        v = int(255 * (y / height) ** 1.4)
        for x in range(img.width):
            px[x, y] = v
    from PIL import ImageChops
    img = img.copy()
    img.putalpha(ImageChops.multiply(a, m))
    return img


def save(img, name):
    path = os.path.join(OUT, name)
    img.convert("RGB").save(path) if not name.endswith("_transparent.png") else img.save(path)
    print(name, img.size)


# ------------------------------------------------------------------ compositions
# Source renders (src/), made by the store harness without HUD. Each entry: file, screen px per art px,
# world centre of the render. Crops are given in world tiles so they work at any render scale.
RENDERS = {
    "diorama8": ("diorama_8x.png", 8, (48.0, 25.0)),
    "diorama6": ("diorama_6x.png", 6, (48.0, 25.0)),
    "late8": ("late_8x.png", 8, (38.0, 19.0)),
    "factory4": ("factory_4x.png", 4, (50.0, 32.0)),
}
# the sheep line in the diorama: pen door x=36, queue 37..44, Shearing Machine 45-46, Milking Machine 54-55
LINE_Y0, LINE_Y1 = 23.85, 27.15     # belt bottom .. top of the Shearing Machine's scissors


def world_crop(key, x0, x1, y0, y1, k):
    """Crops world rect [x0,x1]x[y0,y1] from a render and returns it at k screen px per art px."""
    name, s, (cx, cy) = RENDERS[key]
    img = load(name)
    W, Hh = img.size
    sx = lambda wx: int(round(W / 2 + (wx - cx) * 16 * s))
    sy = lambda wy: int(round(Hh / 2 - (wy - cy) * 16 * s))
    box = (sx(x0), sy(y1), sx(x1), sy(y0))
    # snap to whole art pixels so the downscale is exact
    box = tuple(v - (v % s) for v in box)
    c = Image.new("RGBA", (box[2] - box[0], box[3] - box[1]), GRASS_D + (255,))
    c.alpha_composite(img.crop(box), (0, 0))
    art = c.resize((c.width // s, c.height // s), Image.NEAREST)
    return up(art, k)


def line_band(w, k, x_end, fade=0):
    """The sheep line ending at world x `x_end` on the right, w px wide at k px per art px."""
    tiles = w / (16 * k)
    b = world_crop("diorama8" if k in (1, 2, 4, 8) else "diorama6", x_end - tiles, x_end, LINE_Y0, LINE_Y1, k)
    b = b.crop((0, 0, w, b.height))
    return fade_top(b, fade) if fade else b


def poster_bg(w, h, cx, cy, block, strength=0.9, rays=28):
    bg = sunburst(w, h, cx, cy, block, rays=rays)
    bg.alpha_composite(radial_dark(w, h, cx, cy, 0.25, (w * 0.75, h * 0.95), strength=strength))
    return bg


def ground_strip(w, h, k):
    """Grass under the band so the bottom edge isn't the belt's shadow."""
    g = world_crop("diorama8" if k in (1, 2, 4, 8) else "diorama6", 37, 37 + w / (16 * k) + 1, 21.0, 21.0 + h / (16 * k) + 0.2, k)
    return g.crop((0, 0, w, h))


def leader_art(k, name="leader_stern.png", ol=3):
    return shadow(outline(up(load(name), k), ol), 2 * ol + 2, 2 * ol + 2)


def logo_art(k, ol=3, lines=None):
    return shadow(outline(up(trim(logo_img(lines)), k), ol), 2 * ol, 2 * ol)


SKYLINE = ["house_5", "lab_5", "textile_5", "workshop_5", "storage_5", "atelier_4", "plant_5",
           "house_4", "farm_5", "lab_4", "textile_4", "slaughter_5", "shed_5", "house_5", "workshop_4"]


def skyline(w, k, haze=0.5, seed=3):
    """A distant row of the game's Level 4-5 buildings, hazed towards the poster red."""
    rnd = random.Random(seed)
    parts = [load("b_" + n + ".png") for n in SKYLINE]
    hmax = max(p.height for p in parts)
    row = Image.new("RGBA", (w // k + 64, hmax), (0, 0, 0, 0))
    x = -rnd.randint(0, 12)
    i = 0
    while x < row.width:
        p = parts[i % len(parts)]
        row.alpha_composite(p, (x, hmax - p.height))
        x += p.width - rnd.randint(2, 8)
        i += 1
    # poster silhouette: solid dark red, with only the warm lit windows kept (no neon sign text)
    px = row.load()
    sil = (70, 22, 22)
    for yy in range(row.height):
        for xx in range(row.width):
            r, g, bl, a = px[xx, yy]
            if a == 0:
                continue
            warm = r > 200 and g > 150 and bl < 120 and abs(r - g) < 90
            px[xx, yy] = ((214, 160, 70, 255) if warm else sil + (255,))
    return up(row.crop((0, 0, w // k + 1, hmax)), k)


def grass_patch(w, h, k):
    """Plain game grass (from under the diorama line), tiled to w x h."""
    tile = world_crop("diorama8" if k in (1, 2, 4, 8) else "diorama6", 40.0, 52.0, 20.6, 23.7, k)
    out = Image.new("RGBA", (w, h))
    for y in range(0, h, tile.height):
        for x in range(0, w, tile.width):
            out.paste(tile, (x, y))
    return out


def foreground_only(band, k, x0=None, cut_x=36.4):
    """The band with the grass above the belt made transparent (sheep, machines and the belt stay).
    Works on art pixels: green pixels go, then the grass's isolated dirt speckles."""
    art = band.resize((band.width // k, band.height // k), Image.NEAREST)
    px = art.load()
    W, Hh = art.size
    belt_top = Hh - int(round((24.95 - LINE_Y0) * 16))
    # nothing left of the pen door (the pen's fence and grass) comes to the front
    cut = int(round((cut_x - x0) * 16)) if x0 is not None else 0
    for y in range(Hh):
        for x in range(min(cut, W)):
            px[x, y] = (0, 0, 0, 0)
    for y in range(belt_top):
        for x in range(W):
            r, g, b, a = px[x, y]
            if g > r + 12 and g > b + 12:
                px[x, y] = (0, 0, 0, 0)
    for _ in range(2):
        clear = []
        for y in range(belt_top):
            for x in range(W):
                if px[x, y][3] == 0:
                    continue
                n = 0
                for dy in (-1, 0, 1):
                    for dx in (-1, 0, 1):
                        if (dx or dy) and (not (0 <= x + dx < W and 0 <= y + dy < Hh) or px[x + dx, y + dy][3] == 0):
                            n += 1
                if n >= 6:
                    clear.append((x, y))
        for x, y in clear:
            px[x, y] = (0, 0, 0, 0)
    return up(art, k)


def compose(w, h, leader_k, leader_x, logo_k, logo_xy, band_k, band_end, sun, lines=None,
            leader="leader_stern.png", band_front=False, leader_bottom=None, sky_k=2, sky_haze=0.55):
    img = poster_bg(w, h, sun[0], sun[1], 4)
    band = line_band(w, band_k, band_end)
    by = h - band.height
    sky = skyline(w, sky_k, sky_haze)
    img.alpha_composite(sky, (0, by + int(band.height * 0.22) - sky.height))
    banded = fade_top(band, int(band.height * 0.22))
    L = leader_art(leader_k, leader)
    ly = (h - L.height + 3 * leader_k) if leader_bottom is None else leader_bottom - L.height
    if band_front:
        # grass behind the Leader, but the sheep, the machines and the belt in front of him
        x0 = band_end - w / (16 * band_k)
        cut = int(round((36.4 - x0) * 16 * band_k))
        if cut > 0:
            patch = grass_patch(cut, band.height, band_k)
            bb = band.copy()
            bb.paste(patch, (0, 0))
            banded = fade_top(bb, int(band.height * 0.22))
        img.alpha_composite(banded, (0, by))
        img.alpha_composite(L, (leader_x, ly))
        img.alpha_composite(foreground_only(band, band_k, band_end - w / (16 * band_k)), (0, by))
    else:
        img.alpha_composite(banded, (0, by))
        img.alpha_composite(L, (leader_x, ly))
    if not logo_k:
        return img
    lg = logo_art(logo_k, ol=max(3, logo_k), lines=lines)
    x, y = logo_xy
    if x == "c":
        x = (w - lg.width) // 2
    elif x == "r":
        x = w - lg.width - 14
    img.alpha_composite(lg, (x, y))
    return img


def header(w=920, h=430):
    return compose(w, h, 7, -26, 3, ("r", 30), 3, 51.5, (150, 150), band_front=True, leader_bottom=h - 18)


def small(w=462, h=174):
    img = poster_bg(w, h, w // 2, h // 2, 2, strength=0.75)
    lg = logo_art(2, ol=2)
    img.alpha_composite(lg, ((w - lg.width) // 2, (h - lg.height) // 2 - 4))
    return img


def main_capsule(w=1232, h=706):
    return compose(w, h, 10, -10, 4, ("r", 34), 4, 52.3, (240, 260), band_front=True, leader_bottom=h - 26)


def vertical(w=748, h=896):
    return compose(w, h, 11, (w - 48 * 11) // 2 - 6, 4, ("c", 24), 4, 48.6, (w // 2, 470), lines=["SHEEP", "TATOR"],
                   band_front=True, leader_bottom=h - 70, sky_k=2)


def library_capsule(w=600, h=900):
    return compose(w, h, 10, (w - 480) // 2 - 6, 4, ("c", 30), 3, 48.7, (w // 2, 470), lines=["SHEEP", "TATOR"],
                   band_front=True, leader_bottom=h - 64, sky_k=2)


def library_hero(w=3840, h=1240):
    """No text or logo (Steam rule): the poster with the Leader on the right behind his line;
    the left stays calm for the library logo (bottom-left)."""
    return compose(w, h, 16, 3010, None, None, 8, 62.9, (3010 + 384, 420), band_front=True,
                   leader_bottom=h - 70, sky_k=4)


def library_logo(w=1280):
    lg = trim(logo_img())
    k = 5
    big = shadow(outline(up(lg, k), 5), 8, 8, 170)
    out = Image.new("RGBA", (w, big.height + 8), (0, 0, 0, 0))
    out.alpha_composite(big, ((w - big.width) // 2, 4))
    return out


def page_background(w=1438, h=810):
    c = world_crop("factory4", 50 - w / 128, 50 + w / 128, 32 - h / 128, 32 + h / 128, 4)
    return c.crop((0, 0, w, h))


def app_icon(size=184, leader="leader_stern.png"):
    """Community/app icon: the Leader's head on the red sunburst."""
    L = up(load(leader), 4)                       # 192 x 192
    img = sunburst(L.width, L.height, L.width // 2, int(L.height * 0.45), 4, rays=20)
    img.alpha_composite(radial_dark(L.width, L.height, L.width // 2, L.height // 2, 0.35, (L.width * 0.7, L.height * 0.7), strength=0.8))
    img.alpha_composite(outline(L, 2))
    o = (L.width - size) // 2
    return img.crop((o, 0, o + size, size))


def shortcut_icon(size=256, leader="leader_stern.png"):
    """Transparent desktop icon: the Leader's head (portrait cropped above the shoulders)."""
    L = load(leader).crop((0, 0, 48, 40))
    k = 5
    big = outline(up(L, k), 4)
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    img.alpha_composite(big, ((size - big.width) // 2, (size - big.height) // 2))
    return img



JOBS = {
    "header": (header, "header_capsule_920x430.png"),
    "library_header": (header, "library_header_920x430.png"),
    "small": (small, "small_capsule_462x174.png"),
    "main": (main_capsule, "main_capsule_1232x706.png"),
    "vertical": (vertical, "vertical_capsule_748x896.png"),
    "library_capsule": (library_capsule, "library_capsule_600x900.png"),
    "library_hero": (library_hero, "library_hero_3840x1240.png"),
    "library_logo": (library_logo, "library_logo_transparent.png"),
    "page_background": (page_background, "page_background_1438x810.png"),
}

if __name__ == "__main__":
    which = sys.argv[1:] or list(JOBS) + ["icons"]
    for name in which:
        if name in JOBS:
            fn, out = JOBS[name]
            save(fn(), out)
        if name == "icons":
            ic = app_icon()
            ic.convert("RGB").save(os.path.join(OUT, "community_icon_184x184.jpg"), quality=95)
            print("community_icon_184x184.jpg", ic.size)
            sc = shortcut_icon()
            sc.save(os.path.join(OUT, "shortcut_icon_256.png"))
            sc.save(os.path.join(OUT, "client_icon.ico"), sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
            print("shortcut_icon_256.png / client_icon.ico", sc.size)
