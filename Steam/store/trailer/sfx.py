"""Trailer sound effects for Sheeptator, synthesized (no samples): warm, band-limited, nothing shrill.

Every function returns a mono numpy array at score.SR (rocket() returns stereo, shape (2, n)).
Pitched effects take chord tones (midi numbers) so they sit in the music's key.
python sfx.py  ->  writes every effect to sfx_preview/ and prints a spectral summary.
"""
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import score as S  # noqa: E402

SR = S.SR
TAU = 2 * np.pi
rng = np.random.default_rng(31337)


def tt(L):
    return np.arange(L) / SR


def noise(L):
    return rng.normal(0, 1, L)


def fade(x, ms=6):
    k = min(len(x), int(ms * 1e-3 * SR))
    if k > 1:
        x[-k:] *= np.linspace(1, 0, k)
    return x


def at(dst, t0, sig, g=1.0):
    """mix sig into dst starting at t0 seconds (grows dst if needed)"""
    i = int(t0 * SR)
    need = i + len(sig)
    if need > len(dst):
        dst = np.concatenate([dst, np.zeros(need - len(dst))])
    dst[i:i + len(sig)] += sig * g
    return dst


def modal(freqs, amps, taus, dur, attack=0.0006):
    L = int(dur * SR)
    t = tt(L)
    out = np.zeros(L)
    for f, a, tau in zip(freqs, amps, taus):
        if f < 7500:
            out += a * np.exp(-t / tau) * np.sin(TAU * f * t + rng.uniform(0, TAU))
    out *= 1 - np.exp(-t / attack)
    return fade(out)


def burst(dur, lo, hi, tau, order=2):
    L = int(dur * SR)
    return fade(S.fft_filter(noise(L), S.bp_gain(lo, hi, order)) * np.exp(-tt(L) / tau))


def res(f, F, bw):
    return 1.0 / (1.0 + ((f - F) / bw) ** 2)


def norm(x, peak=0.9):
    m = np.abs(x).max()
    return x * (peak / m) if m > 0 else x


def soft_lp(x, fc=6000):
    return S.fft_filter(x, S.lp_gain(fc, 2))


# ------------------------------------------------------------------ small UI and mechanical sounds
def click(vel=1.0):
    """a soft mouse click"""
    L = int(0.035 * SR)
    t = tt(L)
    n = S.fft_filter(noise(L), S.bp_gain(1200, 3600, 2)) * np.exp(-t / 0.0016)
    body = 0.6 * np.sin(TAU * 1250 * t) * np.exp(-t / 0.0035) + 0.5 * np.sin(TAU * 380 * t) * np.exp(-t / 0.007)
    return fade(norm(n * 0.45 + body) * 0.55 * vel)


def knock(f=380, vel=1.0):
    return modal([f, 2.43 * f, 4.1 * f], [1, 0.38, 0.12], [0.032, 0.016, 0.008], 0.12) * vel


def door(vel=1.0):
    """a wooden gate: clack-clack"""
    out = np.zeros(int(0.25 * SR))
    out = at(out, 0.0, knock(360, 1.0) + np.pad(burst(0.01, 900, 3000, 0.002) * 0.3, (0, int(0.11 * SR))))
    out = at(out, 0.075, knock(300, 0.75))
    return norm(out) * 0.6 * vel


def belt_tick(vel=1.0):
    k = rng.uniform(0.92, 1.08)
    m = modal([250 * k, 660 * k, 1480 * k], [1, 0.45, 0.16], [0.028, 0.014, 0.007], 0.08)
    m[:int(0.006 * SR)] += burst(0.006, 800, 3000, 0.0015) * 0.35
    return norm(m) * 0.45 * vel


def hammer(vel=1.0):
    """construction: three knocks and a puff of dust"""
    out = np.zeros(int(0.5 * SR))
    for i, (t0, v) in enumerate(((0.0, 1.0), (0.11, 0.8), (0.22, 0.65))):
        k = 1 + 0.04 * i
        h = modal([150 * k, 610 * k, 1650 * k], [1, 0.5, 0.18], [0.06, 0.03, 0.012], 0.15)
        h[:int(0.004 * SR)] += burst(0.004, 1000, 3500, 0.001) * 0.4
        out = at(out, t0, h * v)
    dust = S.fft_filter(noise(int(0.25 * SR)), S.lp_gain(900, 2))
    td = tt(len(dust))
    dust *= (1 - np.exp(-td / 0.03)) * np.exp(-td / 0.08)
    out = at(out, 0.24, fade(dust) * 0.25)
    return norm(out) * 0.7 * vel


def pop(vel=1.0):
    L = int(0.08 * SR)
    t = tt(L)
    f = 280 + 650 * (1 - np.exp(-t / 0.012))
    x = np.sin(TAU * np.cumsum(f) / SR) * np.exp(-t / 0.022) * (1 - np.exp(-t / 0.0008))
    return fade(norm(x) * 0.6 * vel)


def marimba(m, vel=1.0, dur=1.0):
    f = S.hz(m)
    k = (440 / f) ** 0.3
    x = modal([f, 3.93 * f, 9.2 * f], [1, 0.22, 0.05], [0.45 * k, 0.1 * k, 0.035 * k], dur)
    x[:int(0.004 * SR)] += burst(0.004, 400, 2500, 0.001) * 0.15
    return x * vel


def bell(m, vel=1.0, dur=1.2):
    f = S.hz(m)
    x = modal([f, 2.0 * f, 2.76 * f, 4.07 * f, 5.4 * f], [1, 0.45, 0.32, 0.12, 0.06], [0.55, 0.3, 0.22, 0.1, 0.06], dur)
    return soft_lp(x, 6500) * vel


def chord_tones(chord, lo=72, hi=86):
    """the chord's pitch classes laid out between midi lo..hi"""
    pcs = sorted({m % 12 for m in chord})
    return [m for m in range(lo, hi + 1) if m % 12 in pcs]


# ------------------------------------------------------------------ the factory
def build_done(chord, vel=1.0):
    """construction finished: a pop and a soft chord ping"""
    tones = chord_tones(chord, 67, 79)
    out = pop(0.8)
    out = at(out, 0.02, marimba(tones[0], 0.5, 0.8))
    out = at(out, 0.06, marimba(tones[min(2, len(tones) - 1)], 0.45, 0.8))
    return norm(out) * 0.55 * vel


def levelup(chord, vel=1.0):
    """an upgrade completes: a quick rising chord on marimba, a shimmer, a whoosh up"""
    tones = chord_tones(chord, 67, 86)
    out = np.zeros(int(1.0 * SR))
    for i, m in enumerate(tones[:4]):
        out = at(out, i * 0.045, marimba(m, 0.7 + 0.1 * i, 0.9))
    out = at(out, 0.16, bell(tones[min(4, len(tones) - 1)], 0.25, 0.9))
    L = int(0.3 * SR)
    sw = S.fft_filter(noise(L), S.bp_gain(500, 2500, 2))
    tw = tt(L)
    sw *= (tw / 0.3) ** 2 * np.exp(-np.maximum(tw - 0.25, 0) / 0.02)
    out = at(out, 0.0, fade(sw) * 0.12)
    return norm(out) * 0.6 * vel


def coin(chord, vel=1.0):
    """SOLD: a soft cash-register ka-ching in the key of the music"""
    tones = chord_tones(chord, 72, 84)
    out = np.zeros(int(0.9 * SR))
    chk = modal([900, 2050], [1, 0.4], [0.012, 0.006], 0.04)
    chk[:int(0.008 * SR)] += burst(0.008, 1500, 4500, 0.002) * 0.5
    out = at(out, 0.0, chk * 0.5)
    out = at(out, 0.03, bell(tones[0], 0.55, 0.8))
    out = at(out, 0.1, bell(tones[min(2, len(tones) - 1)], 0.75, 0.8))
    return norm(out) * 0.5 * vel


def small_coin(chord, vel=1.0):
    tones = chord_tones(chord, 72, 84)
    return norm(bell(tones[min(1, len(tones) - 1)], 1.0, 0.5)) * 0.25 * vel


def machine(vel=1.0):
    """a sheep enters a machine: a clunk and a short motor whirr"""
    out = modal([110, 290], [1, 0.4], [0.05, 0.025], 0.1)
    L = int(0.22 * SR)
    t = tt(L)
    ph = TAU * np.cumsum(85 + 25 * np.exp(-t / 0.08)) / SR
    wh = sum(np.sin(n * ph) / n for n in range(1, 12)) * (1 - np.exp(-t / 0.02)) * np.exp(-t / 0.09)
    wh = S.fft_filter(wh, S.lp_gain(900, 2)) * (0.7 + 0.3 * np.sin(TAU * 31 * t))
    out = at(out, 0.02, fade(wh) * 0.35)
    return norm(out) * 0.4 * vel


def shear(vel=1.0):
    """WOOL: a snip-snip and a soft puff of fleece"""
    out = np.zeros(int(0.25 * SR))
    out = at(out, 0.0, burst(0.03, 1400, 3800, 0.005, 3) * 0.4)
    out = at(out, 0.055, burst(0.03, 1300, 3500, 0.005, 3) * 0.32)
    L = int(0.18 * SR)
    t = tt(L)
    puff = S.fft_filter(noise(L), S.bp_gain(250, 1100, 2)) * (1 - np.exp(-t / 0.012)) * np.exp(-t / 0.05)
    out = at(out, 0.07, fade(puff) * 0.8)
    return norm(out) * 0.42 * vel


def milk(vel=1.0):
    """MILK: a gentle bloop"""
    def bloop(f0, f1, dur, tau):
        L = int(dur * SR)
        t = tt(L)
        f = f1 + (f0 - f1) * np.exp(-t / 0.03)
        ph = TAU * np.cumsum(f) / SR
        return fade((np.sin(ph) + 0.18 * np.sin(2 * ph)) * (1 - np.exp(-t / 0.003)) * np.exp(-t / tau))
    out = bloop(720, 330, 0.16, 0.05)
    out = at(out, 0.045, bloop(900, 480, 0.1, 0.03) * 0.35)
    return norm(out) * 0.45 * vel


def mutton(vel=1.0):
    """MUTTON: a dull comic thunk"""
    L = int(0.3 * SR)
    t = tt(L)
    f = 52 + 75 * np.exp(-t / 0.03)
    x = np.sin(TAU * np.cumsum(f) / SR) * np.exp(-t / 0.11) * (1 - np.exp(-t / 0.001))
    x = at(x, 0.0, S.fft_filter(noise(int(0.06 * SR)), S.lp_gain(450, 2)) * np.exp(-tt(int(0.06 * SR)) / 0.015) * 0.6)
    x = at(x, 0.0, burst(0.05, 300, 1300, 0.012) * 0.2)
    return fade(norm(x) * 0.7 * vel)


def bonk(vel=1.0):
    """the guard post bonks a wolf: a wooden knock and a low cartoon boing"""
    out = modal([560, 1340, 2150], [1, 0.42, 0.12], [0.05, 0.022, 0.01], 0.15)
    L = int(0.5 * SR)
    t = tt(L)
    f = 150 * (1 + 0.16 * np.sin(TAU * 15 * t) * np.exp(-t / 0.22)) * (1 + 0.5 * np.exp(-t / 0.035))
    ph = TAU * np.cumsum(f) / SR
    boing = np.tanh(1.6 * (np.sin(ph) + 0.3 * np.sin(2 * ph))) * np.exp(-t / 0.16) * (1 - np.exp(-t / 0.004))
    boing = S.fft_filter(boing, S.lp_gain(2200, 2))
    out = at(out, 0.01, fade(boing) * 0.75)
    return norm(out) * 0.7 * vel


def howl(vel=1.0):
    """a low, far-off cartoon 'awoo' as the wolf runs"""
    L = int(1.0 * SR)
    t = tt(L)
    f = 260 + 120 * np.sin(np.pi * np.clip(t / 0.9, 0, 1)) ** 0.7 - 40 * t
    ph = TAU * np.cumsum(f) / SR
    out = np.zeros(L)
    for n in range(1, 14):
        fn = n * np.mean(f)
        g = n ** -1.1 * (res(fn, 420, 120) + 0.4 * res(fn, 900, 200))
        out += g * np.sin(n * ph)
    env = np.sin(np.pi * np.clip(t / 0.95, 0, 1)) ** 1.5
    return fade(norm(out * env) * 0.3 * vel)


def card(vel=1.0):
    """an event card slides in: a paper whoosh and a soft landing thump"""
    L = int(0.32 * SR)
    t = tt(L)
    out = np.zeros(L)
    for lo, hi, peak, g in ((300, 900, 0.08, 0.8), (900, 2300, 0.14, 0.55), (2300, 5000, 0.2, 0.25)):
        b = S.fft_filter(noise(L), S.bp_gain(lo, hi, 2))
        out += b * g * np.exp(-((t - peak) / 0.06) ** 2)
    th = np.sin(TAU * 85 * tt(int(0.12 * SR))) * np.exp(-tt(int(0.12 * SR)) / 0.04)
    out = at(out, 0.2, th * 0.9)
    return fade(norm(out) * 0.5 * vel)


def splat(vel=1.0):
    """an accident: a cartoon squash (the music does the mourning)"""
    out = burst(0.12, 250, 1500, 0.035) * 0.8
    L = int(0.25 * SR)
    t = tt(L)
    out = at(out, 0.0, np.sin(TAU * np.cumsum(70 + 40 * np.exp(-t / 0.02)) / SR) * np.exp(-t / 0.09))
    return fade(norm(out) * 0.6 * vel)


def zap(chord, vel=1.0):
    """CHAOS in the Gene Lab: a wobbly zap and a sparkle in key"""
    L = int(0.45 * SR)
    t = tt(L)
    idx = 5 * np.exp(-t / 0.12)
    mod = np.sin(TAU * 27 * t)
    ph = TAU * 300 * t + idx * mod
    wob = (np.sin(ph) + 0.3 * np.sin(2 * ph)) * (1 - np.exp(-t / 0.005)) * np.exp(-t / 0.13)
    wob = S.fft_filter(wob, S.lp_gain(2500, 2))
    out = fade(wob) * 0.6
    tones = chord_tones(chord, 74, 88)
    picks = rng.choice(len(tones), 3, replace=len(tones) < 3)
    for i, k in enumerate(picks):
        out = at(out, 0.05 + 0.04 * i, bell(tones[k], 0.18, 0.4))
    return norm(out) * 0.5 * vel


# ------------------------------------------------------------------ voices
def baa(vel=1.0, pitch=1.0, dur=0.6):
    """a sheep: a low, natural bleat with the 'a-a-a' tremolo, formant-shaped (no buzz, no squeak)"""
    L = int((dur + 0.12) * SR)
    t = tt(L)
    f0 = 200 * pitch
    rate = rng.uniform(7.5, 9.5)
    on = np.clip((t - 0.07) / 0.1, 0, 1)
    wob = 1 + 0.028 * np.sin(TAU * rate * t) * on
    contour = (1 - 0.12 * np.exp(-t / 0.04)) * (1 - 0.09 * np.clip((t - dur * 0.55) / (dur * 0.45), 0, 1))
    f = f0 * wob * contour
    ph = TAU * np.cumsum(f) / SR
    F1 = 320 + 400 * (1 - np.exp(-t / 0.035))
    F2 = 950 + 260 * (1 - np.exp(-t / 0.05))
    out = np.zeros(L)
    for n in range(1, 26):
        fn = n * f0
        if fn > 5000:
            break
        g = n ** -0.9 * (1.0 * res(fn, F1, 120) + 0.65 * res(fn, F2, 150) + 0.2 * res(fn, 2450, 260) + 0.06 * res(fn, 3350, 320))
        out += g * np.sin(n * ph + rng.uniform(0, TAU))
    am = 1 - 0.5 * (0.5 + 0.5 * np.sin(TAU * rate * t + np.pi)) * on
    env = (1 - np.exp(-t / 0.018)) * np.clip((dur + 0.1 - t) / 0.14, 0, 1)
    breath = S.fft_filter(noise(L), S.bp_gain(500, 2600, 2)) * 0.03
    x = (out * am + breath) * env
    return fade(soft_lp(norm(x), 5000) * 0.55 * vel)


VOWELS = ((700, 1100), (560, 900), (460, 820), (650, 1250), (520, 1000))


def voice(vel=1.0):
    """one gruff syllable of the Supreme Leader's gibberish"""
    dur = rng.uniform(0.07, 0.1)
    L = int((dur + 0.04) * SR)
    t = tt(L)
    f0 = rng.uniform(108, 132)
    F1, F2 = VOWELS[rng.integers(len(VOWELS))]
    f = f0 * (1 + 0.04 * np.exp(-t / 0.02)) * (1 - 0.05 * t / dur)
    ph = TAU * np.cumsum(f) / SR
    out = np.zeros(L)
    for n in range(1, 34):
        fn = n * f0
        if fn > 4200:
            break
        g = n ** -0.8 * (res(fn, F1, 110) + 0.7 * res(fn, F2, 140) + 0.12 * res(fn, 2500, 300))
        out += g * np.sin(n * ph)
    env = (1 - np.exp(-t / 0.006)) * np.clip((dur + 0.03 - t) / 0.035, 0, 1)
    return fade(norm(out * env) * 0.42 * vel)


# ------------------------------------------------------------------ big moments
def rocket(dur=6.6, lift=1.0, leave=4.6):
    """the launch: ignition rumble on the pad, a roar at liftoff (t=lift), fading as it leaves the frame"""
    L = int(dur * SR)
    t = tt(L)
    env = np.where(t < lift, 0.25 + 0.35 * (t / lift) ** 1.5, 1.0)
    env = np.where(t > lift, 1.0 - 0.0 * t, env)
    env *= np.where(t > leave, np.exp(-(t - leave) / 0.8), 1.0)
    env *= 1 - np.exp(-t / 0.08)
    out = np.zeros((2, L))
    for ch in range(2):
        w = noise(L)
        brown = np.cumsum(w)
        brown = S.fft_filter(brown, S.hp_gain(30, 2))
        brown /= np.abs(brown).max()
        roar = S.fft_filter(brown, S.lp_gain(1100, 2)) * 1.4
        hiss = S.fft_filter(noise(L), S.bp_gain(500, 2600, 2)) * 0.05
        flick = 1 + 0.15 * S.fft_filter(noise(L), S.lp_gain(12, 2)) / 0.028
        cr = np.zeros(L)
        for k in rng.integers(int(lift * SR), L, int(dur * 25)):
            seg = burst(0.006, 900, 3200, 0.0015)
            cr[k:k + len(seg)] += seg[: L - k] * rng.uniform(0.1, 0.3)
        out[ch] = (roar + hiss + cr) * env * np.clip(flick, 0.5, 1.6)
    sub = np.sin(TAU * np.cumsum(38 + 14 * np.exp(-t / 0.5)) / SR) * env * 0.5
    thump = np.zeros(L)
    i0 = int(lift * SR)
    tl = tt(L - i0)
    thump[i0:] = np.sin(TAU * np.cumsum(45 + 40 * np.exp(-tl / 0.05)) / SR) * np.exp(-tl / 0.35)
    out += sub + thump * 0.8
    out /= np.abs(out).max()
    return out * 0.75


def impact(vel=1.0):
    """weight under the logo hit: a sub drop and a thump"""
    L = int(1.2 * SR)
    t = tt(L)
    sub = np.sin(TAU * np.cumsum(36 + 30 * np.exp(-t / 0.12)) / SR) * np.exp(-t / 0.45) * (1 - np.exp(-t / 0.002))
    th = S.fft_filter(noise(int(0.1 * SR)), S.lp_gain(260, 2)) * np.exp(-tt(int(0.1 * SR)) / 0.025)
    out = at(sub, 0.0, th * 1.2)
    out = at(out, 0.0, burst(0.03, 900, 3500, 0.006) * 0.12)
    return fade(norm(out) * 0.9 * vel)


def whoosh(dur=0.5, vel=1.0):
    """a pass-by whoosh for big cuts"""
    L = int(dur * SR)
    t = tt(L)
    out = np.zeros(L)
    for lo, hi, peak, g in ((200, 700, 0.5, 1.0), (700, 1800, 0.6, 0.6), (1800, 4200, 0.68, 0.25)):
        b = S.fft_filter(noise(L), S.bp_gain(lo, hi, 2))
        out += b * g * np.exp(-((t / dur - peak) / 0.2) ** 2)
    return fade(norm(out) * 0.45 * vel)


# ------------------------------------------------------------------ preview
def _preview():
    out = os.path.join(HERE, "sfx_preview")
    os.makedirs(out, exist_ok=True)
    dm, f, a7 = [62, 65, 69], [65, 69, 72], [57, 61, 64, 67]
    items = {
        "click": click(), "door": door(), "belt_tick": belt_tick(), "hammer": hammer(), "pop": pop(),
        "build_done": build_done(dm), "levelup": levelup(f), "coin": coin(dm), "small_coin": small_coin(dm),
        "machine": machine(), "shear": shear(), "milk": milk(), "mutton": mutton(), "bonk": bonk(), "howl": howl(),
        "card": card(), "splat": splat(), "zap": zap(a7), "baa": baa(), "baa_low": baa(pitch=0.85), "voice": voice(),
        "impact": impact(), "whoosh": whoosh(),
    }
    print(f"{'sfx':11s} {'dur':>5s} {'peak':>6s} {'centroid':>8s} {'>4k%':>6s} {'>8k%':>6s}")
    for k, x in items.items():
        X = np.abs(np.fft.rfft(x)) ** 2
        fr = np.fft.rfftfreq(len(x), 1 / SR)
        c = (fr * X).sum() / X.sum()
        print(f"{k:11s} {len(x) / SR:5.2f} {20 * np.log10(np.abs(x).max() + 1e-9):6.1f} {c:8.0f} {X[fr > 4000].sum() / X.sum() * 100:6.2f} {X[fr > 8000].sum() / X.sum() * 100:6.3f}")
        S.write_wav(os.path.join(out, k + ".wav"), np.stack([x, x]) * 0.8)
    r = rocket()
    S.write_wav(os.path.join(out, "rocket.wav"), r * 0.8)


if __name__ == "__main__":
    _preview()
