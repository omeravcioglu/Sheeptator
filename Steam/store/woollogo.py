"""Pixel-exact Python port of the game's woolly wordmark (Assets/Fleeced/Scripts/Art/LogoArt.cs):
fluffy letters on a blood-red extrusion with a few drips.

The glyphs are read from LogoArt.cs itself and the fluff uses a port of .NET's seeded System.Random,
so `wordmark(<the game's text>)` matches the in-game logo pixel for pixel. The port also renders
other layouts (e.g. the name split over two lines) in the same style for the tall capsules.
"""
import os
import re

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
LOGO_CS = os.path.join(HERE, "..", "..", "Assets", "Fleeced", "Scripts", "Art", "LogoArt.cs")


class NetRandom:
    """System.Random(int seed) as implemented by Mono / .NET Framework (Knuth subtractive)."""
    MBIG = 2147483647
    MSEED = 161803398

    def __init__(self, seed):
        sa = [0] * 56
        sub = self.MBIG if seed == -2147483648 else abs(seed)
        mj = self.MSEED - sub
        sa[55] = mj
        mk = 1
        for i in range(1, 55):
            ii = (21 * i) % 55
            sa[ii] = mk
            mk = mj - mk
            if mk < 0:
                mk += self.MBIG
            mj = sa[ii]
        for _ in range(1, 5):
            for i in range(1, 56):
                sa[i] -= sa[1 + (i + 30) % 55]
                if sa[i] < 0:
                    sa[i] += self.MBIG
        self.sa, self.inext, self.inextp = sa, 0, 21

    def _sample_int(self):
        a = self.inext + 1
        if a >= 56:
            a = 1
        b = self.inextp + 1
        if b >= 56:
            b = 1
        r = self.sa[a] - self.sa[b]
        if r == self.MBIG:
            r -= 1
        if r < 0:
            r += self.MBIG
        self.sa[a] = r
        self.inext, self.inextp = a, b
        return r

    def next_double(self):
        return self._sample_int() * (1.0 / self.MBIG)

    def next(self, a, b=None):
        if b is None:
            return int(self.next_double() * a)
        return int(self.next_double() * (b - a)) + a


# Stand-ins for letters LogoArt.cs doesn't define (the game's own glyphs always win).
FALLBACK = {
    'S': [".#####", "######", "##....", ".####.", "....##", "######", "#####."],
    'H': ["##..##", "##..##", "######", "######", "##..##", "##..##", "##..##"],
    'P': ["#####.", "######", "##..##", "######", "#####.", "##....", "##...."],
    'T': ["######", "######", "..##..", "..##..", "..##..", "..##..", "..##.."],
    'A': [".####.", "######", "##..##", "######", "######", "##..##", "##..##"],
    'O': [".####.", "######", "##..##", "##..##", "##..##", "######", ".####."],
    'R': ["#####.", "######", "##..##", "#####.", "####..", "##.##.", "##..##"],
}


def read_font(path=LOGO_CS):
    src = open(path, encoding="utf-8").read()
    font = dict(FALLBACK)
    for ch, rows in re.findall(r"\{\s*'(.)'\s*,\s*new\[\]\s*\{([^}]*)\}\s*\}", src):
        font[ch] = re.findall(r'"([^"]*)"', rows)
    return font


def _hex(h):
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4)) + (255,)


def wordmark(text, sub=3, seed=5, font=None, lines=None):
    """Renders the wordmark. `lines` (list of strings) stacks several lines, centred."""
    font = font or read_font()
    lines = lines or [text]
    rnd = NetRandom(seed)
    M = 4
    gap = 1  # empty glyph rows between stacked lines
    widths = [len(t) - 1 + sum(len(font[c][0]) for c in t) for t in lines]
    gw = max(widths)
    gh = 7 * len(lines) + gap * (len(lines) - 1)
    W, H = gw * sub + 2 * M, gh * sub + 2 * M + 8
    mask = [[False] * H for _ in range(W)]
    for li, t in enumerate(lines):
        ox = M + (gw - widths[li]) * sub // 2
        oy = M + li * (7 + gap) * sub
        for ch in t:
            g = font[ch]
            for j in range(len(g)):
                for i in range(len(g[j])):
                    if g[j][i] == '#':
                        for a in range(sub):
                            for b in range(sub):
                                mask[ox + i * sub + b][oy + j * sub + a] = True
            ox += (len(g[0]) + 1) * sub
    add = []
    for y in range(1, H - 1):
        for x in range(1, W - 1):
            if mask[x][y]:
                continue
            if mask[x][y + 1] and rnd.next_double() < 0.42:
                add.append((x, y))
            elif (mask[x - 1][y] or mask[x + 1][y]) and rnd.next_double() < 0.18:
                add.append((x, y))
    for x, y in add:
        mask[x][y] = True
    red = [[False] * H for _ in range(W)]
    for y in range(H):
        for x in range(W):
            if mask[x][y]:
                for d in range(1, 4):
                    if y + d < H:
                        red[x][y + d] = True
    bottoms = []
    for x in range(M, W - M):
        for y in range(H // 2, H - 1):
            if mask[x][y] and not mask[x][y + 1]:
                bottoms.append(x)
                break
    for i in range(len(bottoms) - 1, 0, -1):
        k = rnd.next(i + 1)
        bottoms[i], bottoms[k] = bottoms[k], bottoms[i]
    for n in range(min(5, len(bottoms))):
        x = bottoms[n]
        maxy = 0
        for y in range(H):
            if mask[x][y]:
                maxy = y
        yb = maxy + 3
        ln = rnd.next(2, 6)
        e = min(H - 2, yb + ln)
        for y in range(yb, e):
            red[x][y] = True
        for dx in (0, 1):
            for dy in (0, 1):
                if x + dx < W and e + dy < H:
                    red[x + dx][e + dy] = True
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    px = img.load()
    ink = _hex("120e0c")
    for y in range(H):
        for x in range(W):
            if mask[x][y] or red[x][y]:
                for dy in (-1, 0, 1):
                    for dx in (-1, 0, 1):
                        if 0 <= x + dx < W and 0 <= y + dy < H:
                            px[x + dx, y + dy] = ink
    blood, blood_lo = _hex("8c2f2f"), _hex("5e1e1e")
    wool, wool_lo, wool_hi = _hex("f4efe6"), _hex("cfc4b2"), _hex("ffffff")
    for y in range(H):
        for x in range(W):
            if red[x][y] and not mask[x][y]:
                px[x, y] = blood_lo if (y + 1 < H and not red[x][y + 1]) else blood
    for y in range(H):
        for x in range(W):
            if not mask[x][y]:
                continue
            below = y + 1 < H and mask[x][y + 1]
            above = y > 0 and mask[x][y - 1]
            col = wool
            if not below or (y + 2 < H and not mask[x][y + 2]):
                col = wool_lo
            elif not above and rnd.next_double() < 0.5:
                col = wool_hi
            px[x, y] = col
    return img


if __name__ == "__main__":
    import sys
    t = sys.argv[1] if len(sys.argv) > 1 else "SHEEPTATOR"
    w = wordmark(t)
    w.resize((w.width * 5, w.height * 5), Image.NEAREST).save("logo_preview.png")
    print(w.size)
