"""Sheeptator trailer, 1080p60, cut to the score (score.py: 120 BPM, 4/4, a bar = 2 s = 120 frames).

python trailer60.py out.mp4 --audio mix.wav [--preview]   render (--preview: 960x540, fast encode)
python trailer60.py --sfx-json sfx_events.json            only work out the sound effects for score.py --sfx

Footage: the store harness's 60 fps clips (one PNG per 1/60 s of game time), cut on beats. Each clip folder
also holds events.tsv: every game sound and float text per captured frame, which becomes the sound effects.
Captions use the game's own pixel font on the red decree banner.
"""
import json
import os
import re
import subprocess
import sys

from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
import storeart as A  # noqa: E402
import pixfont  # noqa: E402

W, H, FPS = 1920, 1080, 60
BAR = 120          # frames per bar
BEAT = 30
LEAD = 120         # bar 0: the studio logo
SHOTS = r"C:/Users/Omer/AppData/Local/Temp/claude/E--UnityProjects-Sheepgame/ce0af40c-f697-40af-995c-d2032b829b0a/scratchpad/shots"
STUDIO_LOGO = os.path.join(os.path.dirname(HERE), "src", "mchunter_logo.png")


def at(bar, beat=0.0):
    return LEAD + int(round((bar - 1) * BAR + beat * BEAT))


# ---------------------------------------------------------------- the edit
# Each shot runs from `at` until the next shot starts. clip = scene/clip folder, f0 = first source frame,
# or align=(kind, text, k): start so the first matching event lands k frames into the shot.
# cap = caption, cx/cy = caption centre (0..1). sfx = extra sound effects at the cut: [(name, frame offset, vel)].
PLAN = [
    dict(at=0, card="studio", sfx=[("whoosh", 4, 0.35)]),
    dict(at=at(1), clip="t_early/clip_t_shear8", f0=60, cap="WELCOME TO THE SHEEP FACTORY.", cap_from=50),
    dict(at=at(3), card="logo", sfx=[("impact", 0, 1.0)]),
    dict(at=at(4), clip="t_intro/clip_t_leader0", f0=0, cap="THE SUPREME LEADER IS A SHEEP.", cy=0.1),
    dict(at=at(5), clip="t_intro/clip_t_leader1", f0=40, cap="YOU ARE HIS HOOF.", cy=0.1),
    dict(at=at(6), clip="t_early/clip_t_build", f0=8, cap="LAY THE BELTS."),
    dict(at=at(8), clip="t_early/clip_t_doors", f0=24, cap="OPEN THE DOORS."),
    dict(at=at(10), clip="t_early/clip_t_shearpan", f0=100, cap="SHEAR."),
    dict(at=at(11), clip="t_early/clip_t_milk", f0=60, cap="MILK."),
    dict(at=at(12), clip="t_mid/clip_t_sold", align=("float", "SOLD", 20), cap="SELL. EVEN THE SHEEP."),
    dict(at=at(13), clip="t_mid/clip_t_mutton", align=("float", "MUTTON", 60), cap="DON'T ASK ABOUT THE THIRD DOOR."),
    dict(at=at(14), clip="t_mid/clip_t_midpan", f0=60, cap="GROW THE FACTORY."),
    dict(at=at(16), clip="t_mid/clip_t_research", f0=20, cap="38 RESEARCH PROJECTS.", cx=0.71, cy=0.62, sfx=[("click", 2, 0.8)]),
    dict(at=at(17), clip="t_mid/clip_t_wolf", align=("float", "BONK", 60), cap="KEEP THE WOLVES OUT."),
    dict(at=at(18), clip="t_mid/clip_t_event", f0=40, cap="MAKE QUESTIONABLE DECISIONS.", cy=0.91, sfx=[("card", 0, 0.9)]),
    dict(at=at(19), clip="t_mid/clip_t_inspect", f0=40, cap="EVERY SHEEP IS SOMEBODY.", cx=0.36, sfx=[("click", 2, 0.7)]),
    dict(at=at(20), clip="t_gen/clip_t_chaos", f0=120, cap="REWRITE THE SHEEP."),
    dict(at=at(22), clip="t_gen/clip_t_parade", f0=80, cap="13 DNA STRAINS."),
    dict(at=at(23), clip="t_gen/clip_t_genpanel", f0=20, sfx=[("click", 2, 0.7)]),
    dict(at=at(23, 2), clip="t_gen/clip_t_strainbook", f0=20, sfx=[("click", 2, 0.6)]),
    dict(at=at(24), clip="t_late/clip_t_upgrade", f0=34, cap="UPGRADE EVERYTHING."),
    dict(at=at(26), clip="t_late/clip_t_skyline", f0=60, cap="THEN IT GETS OUT OF HAND.", cy=0.12),
    dict(at=at(28), clip="t_mid/clip_t_accident", f0=24, sfx=[("splat", 1, 0.9)]),
    dict(at=at(29, 1), clip="t_late/clip_t_zoom6", f0=0),
    dict(at=at(29, 2), clip="t_late/clip_t_zoom4", f0=0),
    dict(at=at(29, 3), clip="t_late/clip_t_zoom2", f0=0),
    dict(at=at(30), clip="t_late/clip_t_widepan", f0=40, cap="ALL HAIL THE WOOL SINGULARITY."),
    dict(at=at(32, 1), clip="t_late/clip_t_rocket", f0=0, cap="THE MOON MIGHT CONTAIN GRASS.", cap_to=200),
    dict(at=at(35), clip="t_late/clip_t_rocket", f0=420),
    dict(at=at(37), clip="t_early/clip_t_shear8", f0=200),
    dict(at=at(37, 1), clip="t_mid/clip_t_sold", f0=150),
    dict(at=at(37, 2), clip="t_gen/clip_t_chaos", f0=250),
    dict(at=at(37, 3), clip="t_late/clip_t_upgrade", f0=300),
    dict(at=at(38), card="end", sfx=[("impact", 0, 0.7)]),
]
END = at(41) - 1   # 82 s


def ease_out_back(t, s=1.70158):
    t -= 1
    return t * t * ((s + 1) * t + s) + 1


# ---------------------------------------------------------------- events
_events = {}


def clip_events(clip):
    """[(frame, kind, name, a, b)] from the clip's events.tsv"""
    if clip in _events:
        return _events[clip]
    p = os.path.join(SHOTS, clip, "events.tsv")
    out = []
    if os.path.exists(p):
        for line in open(p, encoding="utf-8"):
            f = line.rstrip("\n").split("\t")
            if len(f) >= 3:
                out.append((int(f[0]), f[1], f[2], f[3] if len(f) > 3 else "", f[4] if len(f) > 4 else ""))
    _events[clip] = out
    return out


def resolve_f0(seg):
    if "f0" in seg or "clip" not in seg:
        return seg.get("f0", 0)
    kind, text, k = seg["align"]
    for f, kd, name, a, b in clip_events(seg["clip"]):
        if kd == kind and name.startswith(text) and f >= k:
            return f - k
    print("  no", text, "event in", seg["clip"])
    return 0


for _s in PLAN:
    if "clip" in _s:
        _s["f0"] = resolve_f0(_s)

SHEAR_OUT = ("WOOL", "SILK", "GOLDEN FLEECE", "GLOW WOOL", "COTTON CANDY")
MILK_OUT = ("MILK", "CREAM", "CHOCOLATE MILK")
SKIP_FLOAT = ("LEVEL UP", "REJECTED", "NEW LAMB", "TWINS", "+")
MIN_GAP = {"shear": 0.22, "milk": 0.2, "coin": 0.18, "small_coin": 0.35, "machine": 0.6, "voice": 0.075,
           "baa": 2.4, "door": 0.2, "zap": 0.3, "howl": 2.5, "belt_tick": 0.05, "click": 0.08, "splat": 0.4, "pop": 0.2,
           "levelup": 0.5, "hammer": 0.5}


def map_event(seg, f, kind, name, a, b):
    """a game event -> (sfx, vel, screen x) or None"""
    clip = seg["clip"]
    x = None
    try:
        x = float(a) if kind in ("float", "ui", "harness") else None
        y = float(b) if kind in ("float", "ui", "harness") else None
        if x is not None and not (-80 <= x <= W + 80 and -80 <= y <= H + 80):
            return None   # off screen
    except ValueError:
        pass
    if kind == "ui":
        return ("click", 0.9, x)
    if kind == "harness":
        if name == "belt":
            return ("belt_tick", 0.8, x)
        if name == "upgrade_start":
            return ("hammer", 0.45, x)
        return None
    if kind == "float":
        if name.startswith(SHEAR_OUT):
            return ("shear", 0.8, x)
        if name.startswith(MILK_OUT):
            return ("milk", 0.8, x)
        if name.startswith("SOLD"):
            return ("coin", 1.0, x)
        if re.match(r"^\+\d+c$", name):
            return ("small_coin", 0.6, x)
        if name.startswith("MUTTON"):
            return ("mutton", 1.0, x)
        if name.startswith("BONK"):
            return ("bonk", 1.0, x)
        if name.startswith("DOOR"):
            return ("door", 0.9, x)
        if name.startswith(SKIP_FLOAT):
            return None
        if "t_gen" in clip or "t_late" in clip and "upgrade" not in clip:
            return ("zap", 0.7, x)   # Gene Lab results: CHAOS!, new strains, 'IT'S ALIVE!'...
        return None
    if kind == "sfx":
        vol = float(a or 1)
        if name == "blip":
            return ("voice", 0.9, None) if "t_intro" in clip else None   # the Leader talks only in his dialog shots
        if name == "baa":
            return ("baa", 0.55 + 0.4 * min(1.0, vol), None)
        if name == "rocket":
            return ("rocket", 1.0, None)
        if name == "event":
            return ("card", 0.8, None)
        if name == "fanfare" and "upgrade" in clip:
            return ("levelup", 1.0, None)
        if name == "build":
            if "upgrade" in clip:
                return None
            return ("hammer", 0.8, None) if "build" in clip and f < 120 else ("build_done", 0.9, None)
        if name in ("snip", "squirt") or name == "thud" and vol < 0.3:
            return ("machine", 0.5, None)
        if name == "zap":
            return ("zap", 0.6, None)
        if name == "howl":
            return ("howl", 0.6, None) if "wolf" in clip else None
        if name == "boom" or name == "crunch" and vol >= 0.6:   # accidents and Gene Lab mishaps, not the Slaughterhouse's own crunch
            return ("splat", 0.8, None)
        return None
    return None


def sfx_events():
    """the trailer's sound effects: game events inside the shots used, plus the cuts' own effects"""
    out = []
    last = {}
    for si, seg in enumerate(PLAN):
        a = seg["at"]
        b = PLAN[si + 1]["at"] if si + 1 < len(PLAN) else END + 1
        n = b - a
        for name, off, vel in seg.get("sfx", []):
            out.append(dict(t=(a + off) / FPS, sfx=name, vel=vel, pan=0.0))
        if "clip" not in seg:
            continue
        f0, step = seg["f0"], seg.get("step", 1)
        quick = n <= 40   # the closing montage: only the sounds that read in half a second
        for f, kind, name, ea, eb in clip_events(seg["clip"]):
            k = (f - f0) / step
            if k < 0 or k >= n:
                continue
            m = map_event(seg, f, kind, name, ea, eb)
            if not m:
                continue
            sname, vel, x = m
            if quick and sname not in ("shear", "coin", "zap", "levelup"):
                continue
            t = (a + k) / FPS
            gap = MIN_GAP.get(sname, 0.0)
            if sname in last and t - last[sname] < gap:
                continue
            last[sname] = t
            pan = 0.0 if x is None else max(-1.0, min(1.0, (x / W) * 2 - 1)) * 0.7
            e = dict(t=round(t, 4), sfx=sname, vel=round(vel, 3), pan=round(pan, 3))
            if sname == "baa":
                e["pitch"] = round(0.88 + 0.22 * ((f * 7919) % 100) / 100, 3)
            if sname == "rocket":
                # StartLaunch fires the sound; liftoff is 1 s later; the rocket leaves the frame ~3.6 s after that
                e["args"] = dict(dur=6.6, lift=1.0, leave=4.6)
            out.append(e)
    out.sort(key=lambda e: e["t"])
    return out


# ---------------------------------------------------------------- caption banner
_banner_cache = {}


def banner(text, scale=6):
    """A red decree banner with gold-trimmed edges and the caption in the game font."""
    key = (text, scale)
    if key in _banner_cache:
        return _banner_cache[key]
    t = pixfont.text(text, color=A.CREAM, scale=scale)
    padx, pady = 9 * scale, 4 * scale
    bw, bh = t.width + 2 * padx, t.height + 2 * pady
    k = scale
    art = Image.new("RGBA", (bw // k + 2, bh // k + 2), (0, 0, 0, 0))
    d = ImageDraw.Draw(art)
    aw, ah = art.size
    d.rectangle([0, 0, aw - 1, ah - 1], fill=A.INK)
    d.rectangle([1, 1, aw - 2, ah - 2], fill=A.RED)
    d.line([2, 2, aw - 3, 2], fill=A.RED_L)
    d.line([2, ah - 3, aw - 3, ah - 3], fill=A.RED_D)
    d.rectangle([3, 3, aw - 4, ah - 4], outline=A.GOLD_LO)
    for x in (5, aw - 6):  # rivets
        d.point([(x, 5), (x, ah - 6)], fill=A.GOLD)
    big = A.up(art, k)
    big.alpha_composite(t, ((big.width - t.width) // 2, (big.height - t.height) // 2))
    big = A.shadow(big, k, k, 140)
    _banner_cache[key] = big
    return big


def put_banner(frame, text, i, n, y=0.86, scale=6, x=0.5):
    b = banner(text, scale)
    IN, OUT = 14, 8
    if i < IN:
        k = ease_out_back(i / IN)
    elif i > n - OUT:
        k = max(0.0, (n - i) / OUT)
    else:
        k = 1.0
    if k <= 0.03:
        return frame
    s = b.resize((max(1, int(b.width * k)), max(1, int(b.height * k))), Image.NEAREST)
    frame.alpha_composite(s, (int(x * W - s.width / 2), int(y * H - s.height / 2)))
    return frame


# ---------------------------------------------------------------- cards
_cards = {}


def studio_logo(width):
    key = ("studio", width)
    if key not in _cards:
        lg = Image.open(STUDIO_LOGO).convert("RGBA")
        h = int(lg.height * width / lg.width)
        _cards[key] = lg.resize((width, h), Image.LANCZOS)
    return _cards[key]


def studio_plate(width=420):
    """the studio logo on a dark pixel plate (for the red end card)"""
    key = ("plate", width)
    if key in _cards:
        return _cards[key]
    lg = studio_logo(width)
    k = 4
    pw, ph = lg.width + 2 * 30, lg.height + 2 * 24
    art = Image.new("RGBA", (pw // k, ph // k), (0, 0, 0, 0))
    d = ImageDraw.Draw(art)
    aw, ah = art.size
    d.rectangle([0, 0, aw - 1, ah - 1], fill=A.INK + (235,))
    d.rectangle([1, 1, aw - 2, ah - 2], outline=A.GOLD_LO + (255,))
    big = A.up(art, k)
    big.alpha_composite(lg, ((big.width - lg.width) // 2, (big.height - lg.height) // 2))
    big = A.shadow(big, k, k, 140)
    _cards[key] = big
    return big


def card_base(kind):
    if kind in _cards:
        return _cards[kind]
    base = A.compose(W, H, 15, -18, 0, None, 6, 52.3, (420, 430), band_front=True, leader_bottom=H - 36)
    logo = A.logo_art(6, ol=6)
    sub = pixfont.text("A SHEEP MANAGEMENT TRAGEDY", color=A.GOLD, scale=4)
    _cards[kind] = (base, logo, sub)
    return _cards[kind]


def studio_frame(i, n):
    """bar 0: the McHunter Studios logo on a dark field, fading in and out"""
    if "studio_bg" not in _cards:
        bg = Image.new("RGB", (W, H), (13, 12, 16))
        ImageDraw.Draw(bg).ellipse([W // 2 - 700, H // 2 - 300, W // 2 + 700, H // 2 + 300], fill=(40, 33, 45))
        _cards["studio_bg"] = bg.filter(ImageFilter.GaussianBlur(170)).convert("RGBA")
    f = _cards["studio_bg"].copy()
    t = i / max(1, n - 1)
    k = 0.965 + 0.035 * t                     # a slow, gentle push in
    lg = studio_logo(int(1000 * k))
    fade_in = min(1.0, i / 20)
    fade_out = min(1.0, (n - 1 - i) / 16)
    a = max(0.0, min(fade_in, fade_out))
    a = a * a * (3 - 2 * a)
    if a > 0:
        layer = lg.copy()
        layer.putalpha(layer.getchannel("A").point(lambda v: int(v * a)))
        f.alpha_composite(layer, ((W - lg.width) // 2, (H - lg.height) // 2))
    return f


def card_frame(kind, i, n):
    if kind == "studio":
        return studio_frame(i, n)
    base, logo, sub = card_base(kind)
    f = base.copy()
    lx = W - logo.width - 70
    ly = 90
    if kind == "logo":
        # the logo slams in on the downbeat, a flash, then the subtitle on beat 2
        t = min(1.0, i / 10)
        k = 1.9 - 0.9 * ease_out_back(t, 2.2)
        lg = logo.resize((max(1, int(logo.width * k)), max(1, int(logo.height * k))), Image.NEAREST)
        f.alpha_composite(lg, (lx + (logo.width - lg.width) // 2, ly + (logo.height - lg.height) // 2))
        if i >= BEAT:
            f.alpha_composite(sub, (lx + (logo.width - sub.width) // 2, ly + logo.height + 18))
        if i < 6:
            fl = Image.new("RGBA", (W, H), (255, 244, 220, int(150 * (1 - i / 6))))
            f.alpha_composite(fl)
    else:
        t = min(1.0, i / 12)
        k = ease_out_back(t)
        lg = logo.resize((max(1, int(logo.width * k)), max(1, int(logo.height * k))), Image.NEAREST)
        f.alpha_composite(lg, (lx + (logo.width - lg.width) // 2, ly + (logo.height - lg.height) // 2))
        wish = banner("WISHLIST NOW ON STEAM", scale=7)
        cx = lx + logo.width // 2
        wy = ly + logo.height + 70
        if i >= BEAT:
            k2 = ease_out_back(min(1.0, (i - BEAT) / 14))
            w2 = wish.resize((max(1, int(wish.width * k2)), max(1, int(wish.height * k2))), Image.NEAREST)
            f.alpha_composite(w2, (cx - w2.width // 2, wy + (wish.height - w2.height) // 2))
        sy = wy + wish.height + 30
        if i >= 2 * BEAT:
            f.alpha_composite(sub, (cx - sub.width // 2, sy))
        if i >= 3 * BEAT:
            pl = studio_plate(360)
            a = min(1.0, (i - 3 * BEAT) / 12)
            layer = pl.copy()
            layer.putalpha(layer.getchannel("A").point(lambda v: int(v * a)))
            f.alpha_composite(layer, (cx - pl.width // 2, sy + sub.height + 32))
        # fade to black over the last half second
        if i > n - 30:
            a = int(255 * min(1.0, (i - (n - 30)) / 30))
            f.alpha_composite(Image.new("RGBA", (W, H), (0, 0, 0, a)))
    return f


# ---------------------------------------------------------------- footage
def clip_frame(seg, k):
    d = os.path.join(SHOTS, seg["clip"])
    idx = seg.get("f0", 0) + int(k * seg.get("step", 1))
    p = os.path.join(d, "%04d.png" % idx)
    if not os.path.exists(p):
        # hold the last frame if a clip runs short
        files = sorted(x for x in os.listdir(d) if x.endswith(".png"))
        p = os.path.join(d, files[-1])
    f = Image.open(p).convert("RGBA")
    if f.size != (W, H):
        f = f.resize((W, H), Image.NEAREST)
    return f


def frames():
    for si, seg in enumerate(PLAN):
        a = seg["at"]
        b = PLAN[si + 1]["at"] if si + 1 < len(PLAN) else END + 1
        n = b - a
        for k in range(n):
            if "card" in seg:
                f = card_frame(seg["card"], k, n)
            else:
                f = clip_frame(seg, k)
                if seg.get("cap"):
                    c0 = seg.get("cap_from", 6)
                    c1 = min(n, seg.get("cap_to", n)) - 4
                    if c0 <= k < c1:
                        f = put_banner(f, seg["cap"], k - c0, c1 - c0, seg.get("cy", 0.86), x=seg.get("cx", 0.5))
            yield a + k, f


def main():
    if "--sfx-json" in sys.argv:
        out = sys.argv[sys.argv.index("--sfx-json") + 1]
        ev = sfx_events()
        json.dump(ev, open(out, "w", encoding="utf-8"), indent=0)
        counts = {}
        for e in ev:
            counts[e["sfx"]] = counts.get(e["sfx"], 0) + 1
        print(f"wrote {out}: {len(ev)} sound effects:", ", ".join(f"{k} {v}" for k, v in sorted(counts.items())))
        for s in PLAN:
            if "align" in s:
                print("  aligned", s["clip"], "f0 =", s["f0"])
        return
    out = sys.argv[1]
    audio = sys.argv[sys.argv.index("--audio") + 1] if "--audio" in sys.argv else None
    preview = "--preview" in sys.argv
    ow, oh = (960, 540) if preview else (W, H)
    tmp = out + ".video.mp4"
    enc = ["-c:v", "libx264", "-preset", "veryfast" if preview else "slow", "-crf", "23" if preview else "15",
           "-tune", "animation", "-pix_fmt", "yuv420p", "-profile:v", "high", "-g", "120", "-bf", "2"]
    p = subprocess.Popen(["ffmpeg", "-y", "-loglevel", "error", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{ow}x{oh}",
                          "-r", str(FPS), "-i", "-"] + enc + ["-r", str(FPS), "-movflags", "+faststart", tmp], stdin=subprocess.PIPE)
    n = 0
    for t, f in frames():
        f = f.convert("RGB")
        if preview:
            f = f.resize((ow, oh), Image.BILINEAR)
        p.stdin.write(f.tobytes())
        n += 1
        if n % 600 == 0:
            print(f"  {n} frames ({n / FPS:.0f} s)", flush=True)
    p.stdin.close()
    p.wait()
    if audio:
        subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", tmp, "-i", audio, "-map", "0:v", "-map", "1:a",
                        "-c:v", "copy", "-c:a", "aac", "-b:a", "320k", "-ar", "48000", "-shortest", "-movflags", "+faststart", out], check=True)
        os.remove(tmp)
    else:
        os.replace(tmp, out)
    print(f"wrote {out}: {n} frames, {n / FPS:.2f} s")


if __name__ == "__main__":
    main()
