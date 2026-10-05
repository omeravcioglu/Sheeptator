"""Assembles the Sheeptator Steam trailer from harness frame sequences.

Usage: python trailer.py plan.json out.mp4 [music.wav]
Plan segments:
  {"type": "card", "kind": "intro" | "end", "dur": 3.0}
  {"type": "frames", "dir": ".../clip_x", "start": 0, "end": 120, "caption": "...", "cap_from": 10, "cap_to": 110}
Frames are the harness's 1920x1080 PNGs (0000.png, 0001.png ...), captured at 30 fps.
Captions use the game's own pixel font on a red decree banner.
"""
import json
import math
import os
import subprocess
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
import storeart as A  # noqa: E402
import pixfont  # noqa: E402

W, H, FPS = 1920, 1080, 30
XFADE = 8
FFMPEG = "ffmpeg"


def ease_out_back(t, s=1.70158):
    t -= 1
    return t * t * ((s + 1) * t + s) + 1


# ---------------------------------------------------------------- caption banner
_banner_cache = {}


def banner(text, scale=6):
    """A red decree banner with gold-trimmed edges and the caption in the game font."""
    if text in _banner_cache:
        return _banner_cache[text]
    t = pixfont.text(text, color=A.CREAM, scale=scale)
    padx, pady = 9 * scale, 4 * scale
    bw, bh = t.width + 2 * padx, t.height + 2 * pady
    k = scale
    art = Image.new("RGBA", (bw // k + 2, bh // k + 2), (0, 0, 0, 0))
    from PIL import ImageDraw
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
    _banner_cache[text] = big
    return big


def put_banner(frame, text, i, n, y=0.84):
    b = banner(text)
    if i < 8:
        k = ease_out_back(i / 8)
    elif i > n - 6:
        k = max(0.0, (n - i) / 6)
    else:
        k = 1.0
    if k <= 0.03:
        return frame
    s = b.resize((max(1, int(b.width * k)), max(1, int(b.height * k))), Image.NEAREST)
    frame = frame.convert("RGBA")
    frame.alpha_composite(s, ((W - s.width) // 2, int(y * H - s.height / 2)))
    return frame.convert("RGB")


# ---------------------------------------------------------------- cards
def poster(rot):
    bg = A.sunburst(W, H, W // 2, int(H * 0.62), 6, rays=32, rot=rot)
    bg.alpha_composite(A.radial_dark(W, H, W // 2, int(H * 0.62), 0.2, (W * 0.7, H * 1.0), strength=0.9))
    return bg


def intro_frames(dur):
    n = int(dur * FPS)
    L = A.leader_art(12)
    logo = A.logo_art(6, ol=5)
    for i in range(n):
        f = poster(i * 0.004)
        # the Leader rises into view, then the logo slams in
        t = min(1.0, i / 20)
        y = int(H - L.height * (0.25 + 0.75 * ease_out_back(t)))
        f.alpha_composite(L, ((W - L.width) // 2, y + 40))
        if i >= 22:
            t2 = min(1.0, (i - 22) / 10)
            k = 2.2 - 1.2 * ease_out_back(t2)
            lg = logo.resize((max(1, int(logo.width * k)), max(1, int(logo.height * k))), Image.NEAREST)
            f.alpha_composite(lg, ((W - lg.width) // 2, 70 + (logo.height - lg.height) // 2))
        yield f.convert("RGB")


def end_frames(dur):
    n = int(dur * FPS)
    L = A.leader_art(10)
    logo = A.logo_art(6, ol=5)
    wish = banner("WISHLIST NOW ON STEAM", scale=7)
    for i in range(n):
        f = poster(0.1 + i * 0.004)
        f.alpha_composite(L, ((W - L.width) // 2, H - L.height + 30))
        t = min(1.0, i / 14)
        k = ease_out_back(t)
        lg = logo.resize((max(1, int(logo.width * k)), max(1, int(logo.height * k))), Image.NEAREST)
        f.alpha_composite(lg, ((W - lg.width) // 2, 80 + (logo.height - lg.height) // 2))
        if i >= 12:
            k2 = ease_out_back(min(1.0, (i - 12) / 10))
            w2 = wish.resize((max(1, int(wish.width * k2)), max(1, int(wish.height * k2))), Image.NEAREST)
            f.alpha_composite(w2, ((W - w2.width) // 2, int(H * 0.47 - w2.height / 2)))
        yield f.convert("RGB")


# ---------------------------------------------------------------- footage
def frame_seq(seg):
    d = seg["dir"]
    n = seg["end"] - seg["start"] + 1
    for k, idx in enumerate(range(seg["start"], seg["end"] + 1)):
        f = Image.open(os.path.join(d, "%04d.png" % idx)).convert("RGB")
        if f.size != (W, H):
            f = f.resize((W, H), Image.NEAREST)
        if seg.get("caption"):
            c0, c1 = seg.get("cap_from", 0), seg.get("cap_to", n)
            if c0 <= k < c1:
                f = put_banner(f, seg["caption"], k - c0, c1 - c0, seg.get("y", 0.84))
        yield f


def segment(seg):
    if seg["type"] == "card":
        return intro_frames(seg["dur"]) if seg["kind"] == "intro" else end_frames(seg["dur"])
    return frame_seq(seg)


def main():
    plan = json.load(open(sys.argv[1], encoding="utf-8"))
    out = sys.argv[2]
    music = sys.argv[3] if len(sys.argv) > 3 else None
    tmp = out + ".video.mp4"
    p = subprocess.Popen([FFMPEG, "-y", "-loglevel", "error", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{W}x{H}",
                          "-r", str(FPS), "-i", "-", "-c:v", "libx264", "-preset", "slow", "-crf", "18",
                          "-pix_fmt", "yuv420p", "-movflags", "+faststart", tmp], stdin=subprocess.PIPE)
    total = 0
    tail = []
    for si, seg in enumerate(plan):
        head = []
        for f in segment(seg):
            if tail and len(head) < XFADE:
                head.append(f)
                if len(head) == XFADE:
                    for k, (a, b) in enumerate(zip(tail, head)):
                        p.stdin.write(Image.blend(a, b, (k + 1) / (XFADE + 1)).tobytes())
                        total += 1
                    tail = []
                continue
            tail.append(f)
            if len(tail) > XFADE:
                p.stdin.write(tail.pop(0).tobytes())
                total += 1
        print(f"segment {si} ({seg['type']}) done, {total} frames", flush=True)
    for f in tail:
        p.stdin.write(f.tobytes())
        total += 1
    p.stdin.close()
    p.wait()
    secs = total / FPS
    if music:
        subprocess.run([FFMPEG, "-y", "-loglevel", "error", "-i", tmp, "-i", music, "-map", "0:v", "-map", "1:a",
                        "-c:v", "copy", "-c:a", "aac", "-b:a", "192k",
                        "-af", f"loudnorm=I=-15:TP=-1.5:LRA=11,afade=t=out:st={max(0, secs - 2.5):.2f}:d=2.5",
                        "-shortest", "-movflags", "+faststart", out], check=True)
        os.remove(tmp)
    else:
        os.replace(tmp, out)
    print(f"wrote {out}: {total} frames, {secs:.1f} s")


if __name__ == "__main__":
    main()
