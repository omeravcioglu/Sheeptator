"""The game's 5x7 pixel font (Assets/Fleeced/Scripts/Art/PixelFont.cs), parsed from the C# source,
for captions and banners in the same lettering as the in-game UI."""
import os
import re

from PIL import Image, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
FONT_CS = os.path.join(HERE, "..", "..", "Assets", "Fleeced", "Scripts", "Art", "PixelFont.cs")
CELL_H = 9


def _load():
    src = open(FONT_CS, encoding="utf-8").read()
    block = src[src.index("Defs ="):src.index("};", src.index("Defs ="))]
    glyphs = {}
    for m in re.finditer(r'"((?:[^"\\]|\\.)*)"', block):
        d = m.group(1).replace('\\"', '"').replace("\\\\", "\\")
        if not d:
            continue
        ch, rows = d[0], d[1:].split("|")
        glyphs[ch] = rows
    return glyphs


GLYPHS = _load()


def text(s, color=(244, 239, 230), scale=1, outline=(18, 14, 12), shadow=True):
    """Renders a line of text (art px, then integer upscale). Returns RGBA."""
    w = sum(len(GLYPHS.get(c, GLYPHS["?"])[0]) + 1 for c in s) + 2
    img = Image.new("RGBA", (w + 2, CELL_H + 4), (0, 0, 0, 0))
    px = img.load()
    x = 2
    for c in s:
        rows = GLYPHS.get(c, GLYPHS["?"])
        gw = len(rows[0])
        for j, row in enumerate(rows[:CELL_H]):
            for i, v in enumerate(row[:gw]):
                if v == "#":
                    px[x + i, 2 + j] = color + (255,)
        x += gw + 1
    if outline:
        a = img.split()[3].filter(ImageFilter.MaxFilter(3))
        base = Image.new("RGBA", img.size, outline + (255,))
        base.putalpha(a)
        if shadow:
            sh = Image.new("RGBA", img.size, (0, 0, 0, 0))
            sh.alpha_composite(base, (1, 1))
            sh.alpha_composite(base)
            base = sh
        base.alpha_composite(img)
        img = base
    bb = img.getbbox()
    img = img.crop(bb) if bb else img
    return img.resize((img.width * scale, img.height * scale), Image.NEAREST)


if __name__ == "__main__":
    text("Build the sheep factory.", scale=6).save("caption_preview.png")
    print(len(GLYPHS), "glyphs")
