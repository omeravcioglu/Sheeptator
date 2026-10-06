"""Sheeptator trailer score: a sneaky-then-pompous orchestral march, synthesized from scratch.

python score.py [out.wav] [--stems DIR]

120 BPM, 4/4, 40 bars = 80 s, 48 kHz stereo.
  bars 1-2   intro: tiptoe pizzicato, a timpani/snare roll
  bar  3     logo hit
  bars 4-11  A:  D minor sneaky march, bassoon tune over pizzicato and a ticking woodblock
  bars 12-19 A': clarinet + bassoon + glockenspiel, string pad, light snare
  bars 20-27 B:  F major brass march, tuba oom-pah, snare and cymbals
  bar  28    the accident: a thud and a sad trombone
  bar  29    three stabs (zoom-out cuts) and a roll
  bars 30-37 C:  the tune in D major, full orchestra
  bars 38-40 final hit and a little tuba "bwomp"

Every instrument is additive or physically inspired synthesis with band-limited partials (nothing above
~7 kHz is generated on purpose), a convolution reverb and gentle bus compression. No square waves.
"""
import os
import sys
import wave

import numpy as np

SR = 48000
BPM = 120.0
BEAT = 60.0 / BPM
BARS = 40
LEAD = 2.0                         # bar 0: the studio logo, before the march
SONG = LEAD + BARS * 4 * BEAT
N = int((SONG + 0.5) * SR)
rng = np.random.default_rng(20261005)
TAU = 2 * np.pi
F32 = np.float32


def ov(*xs):
    """overlay signals of different lengths"""
    out = np.zeros(max(len(x) for x in xs))
    for x in xs:
        out[: len(x)] += x
    return out


def hz(m):
    return 440.0 * 2.0 ** ((m - 69) / 12.0)


_NOTE = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}


def pm(name):
    """'C#4' / 'Bb3' / 'D5' -> midi"""
    n = _NOTE[name[0]]
    i = 1
    while i < len(name) and name[i] in "#b":
        n += 1 if name[i] == "#" else -1
        i += 1
    return n + 12 * (int(name[i:]) + 1)


def bar(b, beat=0.0):
    """seconds at bar b (1-based), beat offset (0-based beats); bar 1 starts after the studio logo"""
    return LEAD + ((b - 1) * 4 + beat) * BEAT


# ------------------------------------------------------------------ helpers
def fft_filter(x, gain_fn):
    """zero-phase filter by an arbitrary magnitude response gain_fn(freqs)"""
    n = len(x)
    nf = 1 << int(np.ceil(np.log2(max(2, n))))
    X = np.fft.rfft(x, nf)
    f = np.fft.rfftfreq(nf, 1 / SR)
    X *= gain_fn(f)
    return np.fft.irfft(X, nf)[:n].astype(F32)


def lp_gain(fc, order=2):
    return lambda f: 1.0 / np.sqrt(1.0 + (f / fc) ** (2 * order))


def hp_gain(fc, order=2):
    return lambda f: 1.0 / np.sqrt(1.0 + (fc / np.maximum(f, 1e-3)) ** (2 * order))


def bp_gain(lo, hi, order=2):
    a, b = hp_gain(lo, order), lp_gain(hi, order)
    return lambda f: a(f) * b(f)


def env_adsr(L, a, d, s, r, hold):
    """attack a, decay d to level s, hold until `hold` seconds, then release r. Returns length L."""
    t = np.arange(L, dtype=np.float64) / SR
    e = np.empty(L)
    a = max(a, 1e-4)
    att = t < a
    e[att] = (t[att] / a) ** 1.5
    dec = ~att
    e[dec] = s + (1 - s) * np.exp(-(t[dec] - a) / max(d, 1e-4))
    rel = t > hold
    if rel.any():
        lvl = s + (1 - s) * np.exp(-max(hold - a, 0) / max(d, 1e-4)) if hold > a else (hold / a) ** 1.5
        e[rel] = lvl * np.exp(-(t[rel] - hold) / max(r, 1e-4))
    # never end on a step
    fade = min(L, int(0.004 * SR))
    e[-fade:] *= np.linspace(1, 0, fade)
    return e


def phase_track(f0, L, vib=None, scoop=None, glide=None, drift=0.0):
    """instantaneous phase (radians) of the fundamental with vibrato / scoop / glide"""
    t = np.arange(L, dtype=np.float64) / SR
    f = np.full(L, f0, dtype=np.float64)
    if vib:
        rate, depth, delay = vib
        ramp = np.clip((t - delay) / 0.25, 0, 1)
        f *= 1 + depth * ramp * np.sin(TAU * rate * t + rng.uniform(0, TAU))
    if scoop:
        cents, tc = scoop
        f *= 2 ** (cents / 1200 * np.exp(-t / tc))
    if glide:
        cents, t0, t1 = glide
        g = np.clip((t - t0) / max(t1 - t0, 1e-3), 0, 1)
        f *= 2 ** (cents / 1200 * g * g)
    if drift:
        # slow random pitch wander, like a real player
        k = max(2, int(L / SR * 3) + 2)
        pts = rng.normal(0, drift, k)
        f *= 1 + np.interp(np.linspace(0, k - 1, L), np.arange(k), pts)
    return TAU * np.cumsum(f) / SR, f


def additive(phase, f0, amp_fn, L, maxf=7000.0, max_n=80):
    """sum of harmonics n*phase; amp_fn(n, fn) returns a scalar or array of length L"""
    out = np.zeros(L)
    for n in range(1, max_n + 1):
        fn = n * f0
        if fn > maxf:
            break
        a = amp_fn(n, fn)
        if np.isscalar(a):
            if a < 1e-4:
                continue
        elif a.max() < 1e-4:
            continue
        out += a * np.sin(n * phase + rng.uniform(0, TAU))
    return out


# ------------------------------------------------------------------ instruments (return mono float arrays)
def pizz(m, vel, length=1.0):
    f0 = hz(m)
    T0 = 0.42 * (110.0 / f0) ** 0.35 * length
    L = int((T0 * 5 + 0.05) * SR)
    t = np.arange(L) / SR
    out = np.zeros(L)
    B = 0.00008  # a hint of string stiffness
    bright = 0.55 + 0.45 * vel
    for n in range(1, 60):
        fn = n * f0 * np.sqrt(1 + B * n * n)
        if fn > 6000:
            break
        a = n ** -1.15 * (abs(np.sin(np.pi * n * 0.17)) + 0.05) * np.exp(-fn / (3600 * bright))
        tau = T0 / (1 + 0.55 * (n - 1) ** 1.1)
        out += a * np.exp(-t / tau) * np.sin(TAU * fn * t + rng.uniform(0, TAU))
    out *= 1 - np.exp(-t / 0.0015)
    # finger snap: a few ms of soft noise
    k = int(0.012 * SR)
    snap = fft_filter(rng.normal(0, 1, k), bp_gain(300, 2500)) * np.exp(-np.arange(k) / (0.003 * SR))
    out[:k] += 0.05 * snap
    fade = int(0.01 * SR)
    out[-fade:] *= np.linspace(1, 0, fade)
    return out * vel


def bassoon(m, dur, vel, stacc=False):
    f0 = hz(m)
    hold = dur
    L = int((hold + 0.25) * SR)
    ph, _ = phase_track(f0, L, vib=(5.0, 0.0028, 0.28) if dur > 0.45 else None, scoop=(-12, 0.02), drift=0.0006)
    bright = 2200 + 1800 * vel

    def amp(n, fn):
        formant = 1 + 2.6 * np.exp(-((fn - 470) / 170) ** 2) + 1.3 * np.exp(-((fn - 1180) / 260) ** 2)
        return n ** -0.75 * formant * np.exp(-fn / bright)

    out = additive(ph, f0, amp, L, maxf=5500)
    env = env_adsr(L, 0.028 if stacc else 0.045, 0.09, 0.62 if stacc else 0.82, 0.07 if stacc else 0.11, hold)
    # breath
    br = fft_filter(rng.normal(0, 1, L), bp_gain(700, 2400)) * 0.012
    return (out * 0.16 + br) * env * vel


def clarinet(m, dur, vel, stacc=False):
    f0 = hz(m)
    L = int((dur + 0.25) * SR)
    ph, _ = phase_track(f0, L, scoop=(-8, 0.015), drift=0.0005)
    bright = 1500 + 1500 * vel

    def amp(n, fn):
        odd = n % 2 == 1
        a = n ** -0.8 if odd else 0.22 * n ** -0.9 * (1 + fn / 1800)
        return a / (1 + (fn / bright) ** 2.4)

    out = additive(ph, f0, amp, L, maxf=6000)
    env = env_adsr(L, 0.03 if stacc else 0.05, 0.1, 0.7 if stacc else 0.85, 0.07 if stacc else 0.12, dur)
    br = fft_filter(rng.normal(0, 1, L), bp_gain(1200, 3500)) * 0.006
    return (out * 0.2 + br) * env * vel


BRASS = {
    # attack, peak brightness, sustain brightness, release, scoop cents
    "trumpet": (0.022, 2700, 2000, 0.09, -28, -0.05),
    "horn": (0.045, 1000, 760, 0.14, -18, -0.3),
    "trombone": (0.03, 1400, 1050, 0.11, -22, -0.15),
    "tuba": (0.04, 520, 380, 0.12, -15, -0.3),
}


def brass(m, dur, vel, kind="trumpet", stacc=False, wah=None, glide=None):
    f0 = hz(m)
    a, bpk, bsus, rel, sc, tilt = BRASS[kind]
    L = int((dur + rel * 4 + 0.05) * SR)
    t = np.arange(L) / SR
    vib = (5.2, 0.0032, 0.35) if dur > 0.7 and not stacc else None
    if wah:
        vib = (wah, 0.006, 0.15)
    ph, _ = phase_track(f0, L, vib=vib, scoop=(sc, 0.03), glide=glide, drift=0.0005)
    k = 0.45 + 0.55 * vel
    B = (bsus + (bpk - bsus) * np.exp(-t / 0.14)) * k
    B *= 1 - 0.72 * np.exp(-t / 0.02)  # the lip buzz opens up
    if wah:
        # plunger: the tone opens and closes
        B *= 0.55 + 0.45 * (0.5 + 0.5 * np.sin(TAU * wah * t - np.pi / 2))
    rel_mask = t > dur
    B[rel_mask] *= np.exp(-(t[rel_mask] - dur) / 0.08)

    def amp(n, fn):
        # brass: strong low harmonics up to a brightness corner, then a steep roll-off (no fizz on top)
        return n ** tilt / (1 + (fn / np.maximum(B, 60)) ** 3.0)

    out = additive(ph, f0, amp, L, maxf=6500) * 0.55
    env = env_adsr(L, a, 0.12, 0.72 if stacc else 0.86, rel, dur)
    return out * env * vel * 0.22


def strings(m, dur, vel, att=0.14, rel=0.28, voices=4):
    """a small section: detuned voices with their own vibrato"""
    f0 = hz(m)
    L = int((dur + rel * 4 + 0.05) * SR)
    out = np.zeros(L)
    bright = 1800 + 1600 * vel
    for v in range(voices):
        det = 2 ** (rng.normal(0, 7) / 1200)
        ph, _ = phase_track(f0 * det, L, vib=(rng.uniform(4.8, 5.9), 0.0035, rng.uniform(0.05, 0.2)), drift=0.0008)

        def amp(n, fn):
            body = 1 + 0.6 * np.exp(-((fn - 450) / 220) ** 2) + 0.4 * np.exp(-((fn - 2400) / 600) ** 2)
            return n ** -0.9 * body / (1 + (fn / bright) ** 2.2)

        out += additive(ph, f0 * det, amp, L, maxf=5500)
    out /= voices ** 0.7
    env = env_adsr(L, att, 0.3, 0.9, rel, dur)
    return out * env * vel * 0.17


def glock(m, vel):
    f0 = hz(m)
    L = int(2.2 * SR)
    t = np.arange(L) / SR
    parts = ((1.0, 1.0, 1.5), (2.756, 0.28, 0.45), (5.404, 0.09, 0.16), (8.933, 0.035, 0.07))
    out = np.zeros(L)
    for r, a, tau in parts:
        fr = f0 * r
        if fr > 7500:
            continue
        out += a * np.exp(-t / tau) * np.sin(TAU * fr * t)
    out *= 1 - np.exp(-t / 0.0008)
    fade = int(0.02 * SR)
    out[-fade:] *= np.linspace(1, 0, fade)
    return out * vel


def timpani(m, vel):
    f0 = hz(m)
    L = int(3.2 * SR)
    t = np.arange(L) / SR
    modes = ((1.0, 1.0, 1.7), (1.504, 0.55, 1.05), (1.742, 0.35, 0.75), (2.0, 0.3, 0.6), (2.245, 0.18, 0.45),
             (2.494, 0.13, 0.36), (2.8, 0.08, 0.3), (2.98, 0.06, 0.25))
    pitch = 1 + 0.018 * np.exp(-t / 0.06) * vel  # the head relaxes after the hit
    out = np.zeros(L)
    for r, a, tau in modes:
        ph = TAU * np.cumsum(f0 * r * pitch) / SR
        out += a * (0.4 + 0.6 * vel) ** (r - 1) * np.exp(-t / tau) * np.sin(ph + rng.uniform(0, TAU))
    k = int(0.05 * SR)
    thump = fft_filter(rng.normal(0, 1, k), lp_gain(500, 2)) * np.exp(-np.arange(k) / (0.008 * SR))
    out[:k] += 0.25 * thump
    out *= 1 - np.exp(-t / 0.001)
    fade = int(0.05 * SR)
    out[-fade:] *= np.linspace(1, 0, fade)
    return out * vel


def bass_drum(vel):
    L = int(1.4 * SR)
    t = np.arange(L) / SR
    f = 48 + 30 * np.exp(-t / 0.04)
    out = np.sin(TAU * np.cumsum(f) / SR) * np.exp(-t / 0.42)
    out += 0.35 * np.sin(TAU * 83 * t) * np.exp(-t / 0.18)
    k = int(0.06 * SR)
    out[:k] += 0.3 * fft_filter(rng.normal(0, 1, k), lp_gain(250, 2)) * np.exp(-np.arange(k) / (0.01 * SR))
    out *= 1 - np.exp(-t / 0.002)
    fade = int(0.05 * SR)
    out[-fade:] *= np.linspace(1, 0, fade)
    return out * vel


def snare(vel, brush=False):
    L = int(0.45 * SR)
    t = np.arange(L) / SR
    if brush:
        n = fft_filter(rng.normal(0, 1, L), bp_gain(900, 5000, 2))
        e = (1 - np.exp(-t / 0.012)) * np.exp(-t / 0.11)
        return n * e * vel * 0.5
    body = 0.6 * np.sin(TAU * 182 * t) * np.exp(-t / 0.05) + 0.35 * np.sin(TAU * 331 * t) * np.exp(-t / 0.035)
    wires = fft_filter(rng.normal(0, 1, L), bp_gain(700, 4800, 3)) * np.exp(-t / (0.07 + 0.05 * vel))
    out = body * (0.6 + 0.4 * vel) + wires * (0.55 + 0.45 * vel)
    out *= 1 - np.exp(-t / 0.0008)
    fade = int(0.02 * SR)
    out[-fade:] *= np.linspace(1, 0, fade)
    return out * vel


_CYM = None


def cymbal(vel, length=3.2, swell=False):
    """a warm crash: many inharmonic partials, highs kept modest"""
    global _CYM
    L = int(length * SR)
    t = np.arange(L) / SR
    if _CYM is None:
        k = 140
        _CYM = (np.exp(rng.uniform(np.log(320), np.log(8500), k)), rng.uniform(0, TAU, k), rng.uniform(0.6, 1.0, k))
    freqs, phs, amps = _CYM
    out = np.zeros(L)
    for f, p, a in zip(freqs, phs, amps):
        tau = 2.6 * (500 / f) ** 0.45
        out += a * (f / 500) ** -0.55 * np.exp(-t / tau) * np.sin(TAU * f * t + p)
    noise = fft_filter(rng.normal(0, 1, L), bp_gain(1500, 7000, 2)) * np.exp(-t / 0.7) * 1.2
    out = out / 14 + noise * 0.08
    if swell:
        e = (t / length) ** 2.2
        fade = int(0.01 * SR)
        e[-fade:] *= np.linspace(1, 0, fade)
        out = fft_filter(rng.normal(0, 1, L), bp_gain(800, 6500, 2)) * 0.12 * e
    else:
        out *= 1 - np.exp(-t / 0.0015)
        fade = int(0.05 * SR)
        out[-fade:] *= np.linspace(1, 0, fade)
    return out * vel


def woodblock(vel, pitch=1.0):
    L = int(0.12 * SR)
    t = np.arange(L) / SR
    out = np.sin(TAU * 610 * pitch * t) * np.exp(-t / 0.028) + 0.45 * np.sin(TAU * 1490 * pitch * t) * np.exp(-t / 0.012)
    out *= 1 - np.exp(-t / 0.0005)
    fade = int(0.01 * SR)
    out[-fade:] *= np.linspace(1, 0, fade)
    return out * vel


# ------------------------------------------------------------------ mixer
class Bus:
    def __init__(self, name, gain_db, pan=0.0, verb=0.25, width=0.0):
        self.name, self.g, self.pan, self.verb, self.width = name, 10 ** (gain_db / 20), pan, verb, width
        self.buf = np.zeros((2, N), dtype=F32)

    def add(self, t, sig, pan=None, gain=1.0, jitter=0.004):
        if t < 0:
            return
        t += rng.normal(0, jitter) if jitter else 0
        i = int(max(0, t) * SR)
        if i >= N:
            return
        sig = sig[: N - i]
        p = self.pan if pan is None else pan
        if self.width:
            p = float(np.clip(p + rng.uniform(-self.width, self.width), -1, 1))
        a = (p + 1) * np.pi / 4
        self.buf[0, i:i + len(sig)] += (sig * np.cos(a) * gain * self.g).astype(F32)
        self.buf[1, i:i + len(sig)] += (sig * np.sin(a) * gain * self.g).astype(F32)


BUS = {}


def bus(name, *a, **k):
    if name not in BUS:
        BUS[name] = Bus(name, *a, **k)
    return BUS[name]


# ------------------------------------------------------------------ notation
def seq(text, start, unit=0.5):
    """'D4 r F4 A4*2' -> [(time_s, beats, midi, accent)] starting at `start` seconds; unit in beats"""
    out = []
    b = 0.0
    for tok in text.split():
        mult = 1.0
        if "*" in tok:
            tok, mm = tok.split("*")
            mult = float(mm)
        acc = tok.startswith(">")
        tok = tok.lstrip(">")
        if tok != "r":
            out.append((start + b * BEAT, unit * mult, pm(tok), acc))
        b += unit * mult
    return out


def play(events, inst, busname, vel=0.7, art=0.85, octave=0, pan=None, gain=1.0, **kw):
    bs = BUS[busname]
    for t, beats, m, acc in events:
        v = min(1.0, vel * (1.18 if acc else 1.0) * rng.uniform(0.93, 1.05))
        dur = beats * BEAT * art
        sig = inst(m + 12 * octave, dur, v, **kw)
        bs.add(t, sig, pan=pan, gain=gain)


CHORDS = {
    # bass root, fifth, chord tones (octave 3-4), pad voicing
    "Dm": ("D2", "A2", ["F3", "A3", "D4"], ["D3", "A3", "D4", "F4"]),
    "A7": ("A2", "E2", ["G3", "C#4", "E4"], ["A2", "G3", "C#4", "E4"]),
    "Gm": ("G2", "D2", ["G3", "Bb3", "D4"], ["G2", "G3", "Bb3", "D4"]),
    "F": ("F2", "C2", ["A3", "C4", "F4"], ["F2", "C4", "F4", "A4"]),
    "C7": ("C2", "G2", ["G3", "Bb3", "E4"], ["C3", "G3", "Bb3", "E4"]),
    "Bb": ("Bb1", "F2", ["Bb3", "D4", "F4"], ["Bb2", "F3", "D4", "F4"]),
    "D": ("D2", "A2", ["F#3", "A3", "D4"], ["D3", "A3", "D4", "F#4"]),
    "G": ("G2", "D2", ["G3", "B3", "D4"], ["G2", "G3", "B3", "D4"]),
}

A_MINOR = [
    "D4 r F4 A4 G#4 A4*2 r",
    "D5 r C5 A4 Bb4 A4 F4 E4",
    "E4 r G4 Bb4 A4 G4 F4 E4",
    "D4 r A3 r D4*2 r*2",
    "G4 r Bb4 D5 C#5 D5*2 r",
    "F5 r E5 D5 A4 r F4 r",
    "E5 D5 C#5 Bb4 A4 G4 F4 E4",
    "D4 r A3 r D4*2 r*2",
]
A_CHORDS = ["Dm", "Dm", "A7", "Dm", "Gm", "Dm", "A7", "Dm"]

C_MAJOR = [
    ">D4 r F#4 A4 G#4 A4*2 r",
    ">D5 r C#5 A4 B4 A4 F#4 E4",
    ">E4 r G4 B4 A4 G4 F#4 E4",
    ">D4 r A3 r D4*2 r*2",
    ">G4 r B4 D5 C#5 D5*2 r",
    ">F#5 r E5 D5 A4 r F#4 r",
    ">E5 D5 C#5 B4 A4 G4 F#4 E4",
    ">D5*2 r A4 >D5*2 r*2",
]
C_CHORDS = ["D", "D", "A7", "D", "G", "D", "A7", "D"]

B_TUNE = [  # quarter = 1 beat, unit 0.25 beat tokens (x4 = quarter)
    ">C5*6 A4*2 F4*4 A4*4",
    "G4*6 Bb4*2 C5*4 E4*4",
    ">A4*6 C5*2 F5*4 E5*2 D5*2",
    "D5*8 Bb4*4 D5*4",
    ">Bb4*6 G4*2 D5*4 Bb4*4",
    "C5*6 E5*2 G5*4 E5*2 C5*2",
    ">F5*6 C5*2 A4*4 C5*4",
    "E5*4 C#5*4 A4*4 E4*2 G4*2",
]
B_CHORDS = ["F", "C7", "F", "Bb", "Gm", "C7", "F", "A7"]


def chord_at(t):
    """the harmony under the music at time t (seconds): midi chord tones, for pitched sound effects"""
    if t < LEAD:
        return [pm("D4"), pm("F#4"), pm("A4")]
    b = int((t - LEAD) / (4 * BEAT)) + 1
    if b <= 3:
        name = "Dm"
    elif b <= 11:
        name = A_CHORDS[b - 4]
    elif b <= 19:
        name = A_CHORDS[b - 12]
    elif b <= 27:
        name = B_CHORDS[b - 20]
    elif b == 28:
        name = "Dm"
    elif b == 29:
        name = "A7"
    elif b <= 37:
        name = C_CHORDS[b - 30]
    else:
        name = "D"
    root, fifth, tri, pad = CHORDS[name]
    return [pm(x) for x in tri]


def diatonic_below(m, scale_pcs, steps=2):
    """the note `steps` scale degrees below m (for horn harmony)"""
    x = m
    k = 0
    while k < steps:
        x -= 1
        if x % 12 in scale_pcs:
            k += 1
    return x


F_MAJOR = {5, 7, 9, 10, 0, 2, 4}


# ------------------------------------------------------------------ the arrangement
def compose():
    bus("pizz_bass", -3.8, pan=0.15, verb=0.22)
    bus("pizz_chord", -7, pan=-0.25, verb=0.3, width=0.15)
    bus("bassoon", 3.2, pan=0.2, verb=0.24)
    bus("clarinet", 6.0, pan=-0.2, verb=0.28)
    bus("trumpet", 4.4, pan=-0.12, verb=0.3, width=0.12)
    bus("horn", 3.2, pan=0.3, verb=0.42, width=0.1)
    bus("trombone", -0.6, pan=0.25, verb=0.32)
    bus("tuba", 6.0, pan=0.05, verb=0.16)
    bus("strings", -5.6, pan=0.0, verb=0.45, width=0.5)
    bus("glock", -22.8, pan=0.35, verb=0.4)
    bus("timp", -15.0, pan=-0.1, verb=0.35)
    bus("bd", -10.4, pan=0.0, verb=0.22)
    bus("snare", -4.8, pan=0.1, verb=0.25)
    bus("cym", -7.5, pan=-0.3, verb=0.3)
    bus("wood", -8.4, pan=0.4, verb=0.2)

    def pizz_bar(b, ch, vel=0.7, chords=True, walk=None):
        root, fifth, tri, _ = CHORDS[ch]
        BUS["pizz_bass"].add(bar(b, 0), ov(pizz(pm(root), vel), 0.6 * pizz(pm(root) + 12, vel * 0.8)))
        BUS["pizz_bass"].add(bar(b, 2), ov(pizz(pm(fifth), vel * 0.85), 0.5 * pizz(pm(fifth) + 12, vel * 0.7)))
        if chords:
            for beat in (1, 3):
                for k, nt in enumerate(tri):
                    BUS["pizz_chord"].add(bar(b, beat) + 0.006 * k, pizz(pm(nt), vel * 0.75, length=0.8))

    def pad(b, ch, vel=0.5, beats=4, att=0.18):
        for nt in CHORDS[ch][3]:
            BUS["strings"].add(bar(b, 0), strings(pm(nt), beats * BEAT * 0.98, vel, att=att))

    def oompah(b, ch, vel=0.75, pah=True):
        root, fifth, tri, _ = CHORDS[ch]
        BUS["tuba"].add(bar(b, 0), brass(pm(root), 0.42, vel, "tuba", stacc=True))
        BUS["tuba"].add(bar(b, 2), brass(pm(fifth), 0.42, vel * 0.9, "tuba", stacc=True))
        if pah:
            for beat in (1, 3):
                for nt in tri:
                    BUS["horn"].add(bar(b, beat), brass(pm(nt), 0.28, vel * 0.7, "horn", stacc=True))

    def snare_bar(b, pattern, vel=1.0, brush=False):
        for off, v in pattern:
            BUS["snare"].add(bar(b, off), snare(v * vel, brush=brush), jitter=0.002)

    def roll(t0, t1, v0, v1, inst="snare", m=None):
        n = int((t1 - t0) / (BEAT / 6))
        for k in range(n):
            u = k / max(1, n - 1)
            v = v0 + (v1 - v0) * u ** 1.3
            t = t0 + k * BEAT / 6
            if inst == "snare":
                BUS["snare"].add(t, snare(v * rng.uniform(0.85, 1.0)), jitter=0.002)
            else:
                BUS["timp"].add(t, timpani(m, v * rng.uniform(0.85, 1.0)), jitter=0.002)

    def crash(t, v=0.8):
        BUS["cym"].add(t, cymbal(v), jitter=0)

    def hit(t, ch, v=1.0, beats=1.0, octave_up=True):
        root, fifth, tri, padv = CHORDS[ch]
        for nt in tri:
            BUS["trombone"].add(t, brass(pm(nt) - 12, beats * BEAT, v * 0.8, "trombone"))
            BUS["horn"].add(t, brass(pm(nt), beats * BEAT, v * 0.8, "horn"))
        if octave_up:
            for nt in tri[1:]:
                BUS["trumpet"].add(t, brass(pm(nt) + 12, beats * BEAT, v * 0.75, "trumpet"))
        BUS["tuba"].add(t, brass(pm(root), beats * BEAT, v, "tuba"))
        for nt in padv:
            BUS["strings"].add(t, strings(pm(nt), beats * BEAT, v * 0.8, att=0.02, rel=0.35))
        BUS["timp"].add(t, timpani(pm(root) + 12 if pm(root) < 40 else pm(root), v))
        BUS["bd"].add(t, bass_drum(v * 0.9))

    MARCH = [(0, 0.75), (1, 0.55), (1.5, 0.28), (1.75, 0.33), (2, 0.7), (3, 0.55), (3.5, 0.32), (3.75, 0.4)]
    DRIVE = [(0, 0.5), (0.5, 0.28), (1, 0.75), (1.5, 0.28), (2, 0.5), (2.5, 0.28), (3, 0.75), (3.5, 0.33), (3.75, 0.28)]
    BACK = [(1, 0.45), (3, 0.45), (3.75, 0.14)]

    # ---- bar 0: the studio logo. a harp-like arpeggio in D, a glockenspiel ding, a soft string swell
    t0 = 0.3
    for i, nt in enumerate(["D3", "A3", "D4", "F#4", "A4", "D5"]):
        BUS["pizz_chord"].add(t0 + i * 0.075, pizz(pm(nt), 0.55, length=2.4), jitter=0)
    BUS["glock"].add(t0 + 0.5, glock(pm("D5"), 0.6), jitter=0)
    BUS["glock"].add(t0 + 0.5, glock(pm("A5"), 0.3), jitter=0)
    for nt in ["D3", "A3", "D4", "F#4"]:
        BUS["strings"].add(0.1, strings(pm(nt), 1.35, 0.32, att=0.45, rel=0.45), jitter=0)

    # ---- intro, bars 1-2: tiptoe
    walk = ["D3 r A2 r D3 r A2 r", "D3 r A2 r C#3 r E3 r"]
    for k, w in enumerate(walk):
        for t, beats, m, acc in seq(w, bar(1 + k)):
            BUS["pizz_bass"].add(t, ov(pizz(m, 0.62), 0.45 * pizz(m + 12, 0.5)))
        for beat in (1, 3):
            BUS["wood"].add(bar(1 + k, beat), woodblock(0.55))
    BUS["bassoon"].add(bar(1, 3.5), bassoon(pm("A2"), 0.18, 0.5, stacc=True))
    BUS["bassoon"].add(bar(2, 3.0), bassoon(pm("A2"), 0.18, 0.55, stacc=True))
    roll(bar(2, 2), bar(3, 0) - 0.02, 0.12, 0.75)
    roll(bar(2, 2.5), bar(3, 0) - 0.02, 0.2, 0.8, inst="timp", m=pm("A2"))
    BUS["cym"].add(bar(2, 1.0), cymbal(0.7, length=3 * BEAT, swell=True), jitter=0)

    # ---- bar 3: the logo hit
    hit(bar(3, 0), "Dm", 1.0, beats=1.6)
    crash(bar(3, 0), 1.0)
    BUS["timp"].add(bar(3, 2), timpani(pm("A2"), 0.6))
    BUS["timp"].add(bar(3, 3), timpani(pm("D2") + 12, 0.45))
    BUS["bassoon"].add(bar(3, 3.0), bassoon(pm("A2"), 0.2, 0.6, stacc=True))
    BUS["bassoon"].add(bar(3, 3.5), bassoon(pm("C#3"), 0.2, 0.65, stacc=True))

    # ---- A, bars 4-11: the bassoon sneaks
    for k in range(8):
        b = 4 + k
        play(seq(A_MINOR[k], bar(b)), bassoon, "bassoon", vel=0.72, art=0.55, octave=-1, stacc=True)
        pizz_bar(b, A_CHORDS[k], 0.66)
        for beat in (1, 3):
            BUS["wood"].add(bar(b, beat), woodblock(0.4 if beat == 1 else 0.48))
        if k in (3, 7):
            BUS["timp"].add(bar(b, 0), timpani(pm("D2") + 12, 0.4))
    # pickup into A'
    BUS["clarinet"].add(bar(11, 3.0), clarinet(pm("A3"), 0.2, 0.6, stacc=True))
    BUS["clarinet"].add(bar(11, 3.5), clarinet(pm("C#4"), 0.2, 0.65, stacc=True))

    # ---- A', bars 12-19: clarinet, glock, a pad, light snare
    crash(bar(12, 0), 0.5)
    for k in range(8):
        b = 12 + k
        tune = A_MINOR[k]
        play(seq(tune, bar(b)), clarinet, "clarinet", vel=0.78, art=0.62, stacc=True)
        play(seq(tune, bar(b)), bassoon, "bassoon", vel=0.5, art=0.5, octave=-1, stacc=True)
        if k in (0, 1, 4, 5):
            play(seq(tune, bar(b)), lambda m, d, v: glock(m, v), "glock", vel=0.75, octave=1)
        pizz_bar(b, A_CHORDS[k], 0.7)
        pad(b, A_CHORDS[k], 0.42)
        snare_bar(b, BACK, 0.9)
        BUS["bd"].add(bar(b, 0), bass_drum(0.42))
        BUS["bd"].add(bar(b, 2), bass_drum(0.3))
    # fanfare pickup into B
    for k, nt in enumerate(["C4", "F4", "A4"]):
        BUS["trumpet"].add(bar(19, 3) + k * BEAT / 3, brass(pm(nt), BEAT / 3 * 0.8, 0.75, "trumpet", stacc=True))
    roll(bar(19, 3), bar(20, 0) - 0.02, 0.25, 0.7)

    # ---- B, bars 20-27: F major brass march
    for k in range(8):
        b = 20 + k
        ev = seq(B_TUNE[k], bar(b), unit=0.25)
        play(ev, lambda m, d, v: brass(m, d, v, "trumpet"), "trumpet", vel=0.82, art=0.86)
        play([(t, bt, m - 12, a) for t, bt, m, a in ev], clarinet, "clarinet", vel=0.45, art=0.86, gain=0.4)
        harm = [(t, bt, diatonic_below(m, F_MAJOR if B_CHORDS[k] != "A7" else {9, 1, 4, 7, 2, 5, 10}), a) for t, bt, m, a in ev]
        play(harm, lambda m, d, v: brass(m, d, v, "horn"), "horn", vel=0.6, art=0.86)
        oompah(b, B_CHORDS[k], 0.8)
        pizz_bar(b, B_CHORDS[k], 0.55, chords=False)
        pad(b, B_CHORDS[k], 0.38)
        snare_bar(b, MARCH, 0.95)
        BUS["bd"].add(bar(b, 0), bass_drum(0.62))
        BUS["bd"].add(bar(b, 2), bass_drum(0.48))
        BUS["timp"].add(bar(b, 0), timpani(pm(CHORDS[B_CHORDS[k]][0]) + (12 if pm(CHORDS[B_CHORDS[k]][0]) < 40 else 0), 0.42))
    crash(bar(20, 0), 0.85)
    crash(bar(24, 0), 0.7)

    # ---- bar 28: the accident. a thud, then a sad trombone
    BUS["timp"].add(bar(28, 0), timpani(pm("D2") + 12, 0.95))
    BUS["bd"].add(bar(28, 0), bass_drum(1.0))
    BUS["tuba"].add(bar(28, 0), brass(pm("D2"), 0.35, 0.9, "tuba", stacc=True))
    BUS["pizz_bass"].add(bar(28, 0), ov(pizz(pm("D2"), 0.9), pizz(pm("D3"), 0.7)))
    sad = [("G3", 0.85, 0.42), ("F#3", 1.35, 0.42), ("F3", 1.85, 0.42), ("E3", 2.35, 1.7)]
    for i, (nt, beat, d) in enumerate(sad):
        last = i == len(sad) - 1
        BUS["trombone"].add(bar(28, beat), brass(pm(nt), d * BEAT if not last else 1.25, 0.72, "trombone",
                                                 wah=5.5 if last else 2.2, glide=(-70, 0.6, 1.25) if last else None) * 1.6)

    # ---- bar 29: three stabs for the zoom-out cuts, a roll, a swell
    for k, beat in enumerate((1, 2, 3)):
        hit(bar(29, beat), "A7", 0.6 + 0.15 * k, beats=0.45)
    roll(bar(29, 1), bar(30, 0) - 0.02, 0.2, 0.85)
    BUS["cym"].add(bar(29, 1.4), cymbal(0.85, length=2.6 * BEAT, swell=True), jitter=0)

    # ---- C, bars 30-37: the tune in D major, everyone
    crash(bar(30, 0), 1.0)
    # the rocket: a timpani roll under the countdown, liftoff on the downbeat of bar 33
    roll(bar(32, 1.5), bar(33, 0) - 0.02, 0.2, 0.85, inst="timp", m=pm("A2"))
    crash(bar(33, 0), 1.0)
    BUS["bd"].add(bar(33, 0), bass_drum(0.95))
    crash(bar(35, 0), 0.7)
    for k in range(8):
        b = 30 + k
        tune = C_MAJOR[k]
        ev = seq(tune, bar(b))
        play(ev, lambda m, d, v: brass(m, d, v, "trumpet"), "trumpet", vel=0.85, art=0.8)
        play(ev, lambda m, d, v: strings(m, d, v, att=0.03, rel=0.18, voices=4), "strings", vel=0.7, art=0.85)
        play(ev, clarinet, "clarinet", vel=0.5, art=0.75, gain=0.5, stacc=True)
        play(ev, lambda m, d, v: brass(m, d, v, "horn"), "horn", vel=0.55, art=0.8, octave=-1)
        if k in (0, 1, 4, 5):
            play(ev, lambda m, d, v: glock(m, v), "glock", vel=0.7, octave=1)
        oompah(b, C_CHORDS[k], 0.85)
        pizz_bar(b, C_CHORDS[k], 0.6, chords=False)
        pad(b, C_CHORDS[k], 0.35)
        snare_bar(b, DRIVE, 1.0)
        BUS["bd"].add(bar(b, 0), bass_drum(0.7))
        BUS["bd"].add(bar(b, 2), bass_drum(0.55))
        r = pm(CHORDS[C_CHORDS[k]][0])
        BUS["timp"].add(bar(b, 0), timpani(r + 12 if r < 40 else r, 0.5))
    roll(bar(37, 2), bar(38, 0) - 0.02, 0.3, 0.9)

    # ---- outro: the final hit, then the bwomp
    hit(bar(38, 0), "D", 1.0, beats=3.0)
    crash(bar(38, 0), 1.0)
    for nt in ["D3", "A3", "D4", "F#4", "A4", "D5"]:
        BUS["strings"].add(bar(38, 0), strings(pm(nt), 4.5 * BEAT, 0.65, att=0.02, rel=0.9))
    roll(bar(38, 0.5), bar(39, 2), 0.4, 0.1, inst="timp", m=pm("D2") + 12)
    BUS["pizz_chord"].add(bar(39, 3.0), pizz(pm("A3"), 0.55))
    BUS["pizz_chord"].add(bar(39, 3.5), pizz(pm("C#4"), 0.6))
    BUS["tuba"].add(bar(40, 0), brass(pm("D2"), 0.32, 0.95, "tuba", stacc=True))
    BUS["bassoon"].add(bar(40, 0), bassoon(pm("D3"), 0.25, 0.8, stacc=True))
    BUS["pizz_bass"].add(bar(40, 0), ov(pizz(pm("D2"), 0.85), pizz(pm("D3"), 0.6)))
    BUS["wood"].add(bar(40, 0), woodblock(0.6, pitch=0.8))


# ------------------------------------------------------------------ reverb and mastering
def make_ir(length=2.4, rt60=1.75):
    L = int(length * SR)
    t = np.arange(L) / SR
    ir = np.zeros((2, L))
    for ch in range(2):
        noise = rng.normal(0, 1, L)
        tail = np.zeros(L)
        # frequency-dependent decay: highs die sooner
        bands = [(20, 250, 1.15), (250, 1000, 1.0), (1000, 3000, 0.75), (3000, 7000, 0.5), (7000, 20000, 0.3)]
        for lo, hi, k in bands:
            nb = fft_filter(noise, bp_gain(lo, hi, 3))
            tail += nb * np.exp(-6.91 * t / (rt60 * k))
        pre = int(0.018 * SR)
        tail[:pre] = 0
        tail[pre:pre + int(0.03 * SR)] *= np.linspace(0, 1, int(0.03 * SR))
        # a few early reflections
        for d, g in ((0.011, 0.5), (0.017, 0.35), (0.023, 0.3), (0.031, 0.25), (0.043, 0.2)):
            dd = d * rng.uniform(0.9, 1.1)
            ir[ch, int(dd * SR)] += g * (1 if rng.random() > 0.3 else -1)
        ir[ch] += tail * 0.5
    ir /= np.sqrt((ir ** 2).sum(axis=1, keepdims=True))
    return ir


def convolve(x, h):
    n = len(x) + len(h) - 1
    nf = 1 << int(np.ceil(np.log2(n)))
    return np.fft.irfft(np.fft.rfft(x, nf) * np.fft.rfft(h, nf), nf)[: len(x)]


def smooth(x, a_ms, r_ms, rate):
    """attack/release one-pole follower over an envelope sampled at `rate`"""
    a = np.exp(-1 / (a_ms * 1e-3 * rate))
    r = np.exp(-1 / (r_ms * 1e-3 * rate))
    y = np.empty_like(x)
    s = x[0]
    for i, v in enumerate(x):
        c = a if v > s else r
        s = c * s + (1 - c) * v
        y[i] = s
    return y


def compress(st, thresh_db=-17.0, ratio=2.0, a_ms=12, r_ms=160, rate=1000):
    hop = SR // rate
    m = st.shape[1] // hop
    pw = (st[:, : m * hop] ** 2).mean(axis=0).reshape(m, hop).mean(axis=1)
    lvl = 10 * np.log10(pw + 1e-12)
    lvl = smooth(lvl, a_ms, r_ms, rate)
    over = np.maximum(lvl - thresh_db, 0)
    gr = -over * (1 - 1 / ratio)
    g = 10 ** (gr / 20)
    g = np.interp(np.arange(st.shape[1]) / hop, np.arange(m), g)
    return st * g, gr.min()


def limit(st, ceil_db=-1.0, look_ms=4, r_ms=80):
    ceil = 10 ** (ceil_db / 20)
    pk = np.abs(st).max(axis=0)
    look = int(look_ms * 1e-3 * SR)
    # moving max over the lookahead window (block-wise for speed)
    from numpy.lib.stride_tricks import sliding_window_view
    pad = np.concatenate([pk, np.zeros(look)])
    mx = sliding_window_view(pad, look + 1).max(axis=1)[: len(pk)]
    need = np.minimum(1.0, ceil / np.maximum(mx, 1e-9))
    rate = 2000
    hop = SR // rate
    m = len(need) // hop
    nd = need[: m * hop].reshape(m, hop).min(axis=1)
    sm = -smooth(-nd, 0.5, r_ms, rate)  # instant attack (lookahead), slow release
    sm = np.minimum(sm, nd)
    g = np.interp(np.arange(len(need)) / hop, np.arange(m), sm)
    return st * g


def write_wav(path, st):
    x = np.clip(st, -1, 1)
    # TPDF dither to 24-bit
    d = (rng.random(x.shape) - rng.random(x.shape)) / (2 ** 23)
    q = np.round((x + d) * (2 ** 23 - 1)).astype(np.int32)
    inter = np.empty(q.shape[1] * 2, dtype=np.int32)
    inter[0::2], inter[1::2] = q[0], q[1]
    b = inter.astype("<i4").tobytes()
    b3 = bytearray()
    raw = np.frombuffer(b, dtype=np.uint8).reshape(-1, 4)[:, :3]
    b3 = raw.tobytes()
    with wave.open(path, "wb") as w:
        w.setnchannels(2)
        w.setsampwidth(3)
        w.setframerate(SR)
        w.writeframes(b3)


SECTIONS = [("intro", 1, 2), ("logo", 3, 3), ("A", 4, 11), ("A'", 12, 19), ("B", 20, 27), ("break", 28, 29), ("C", 30, 37), ("outro", 38, 40)]


def render_sfx(path):
    """mix the trailer's sound effects (from trailer60.py --sfx-json) onto their own bus"""
    import json
    import sfx as X
    ev = json.load(open(path, encoding="utf-8"))
    bus("sfx", -2.5, pan=0.0, verb=0.14)
    pitched = {"coin", "small_coin", "levelup", "build_done", "zap"}
    counts = {}
    for e in ev:
        name, t, vel, pan = e["sfx"], e["t"], e.get("vel", 1.0), e.get("pan", 0.0)
        fn = getattr(X, name)
        if name == "rocket":
            sig = fn(**e.get("args", {}))
            i = int(t * SR)
            n = min(sig.shape[1], N - i)
            BUS["sfx"].buf[:, i:i + n] += (sig[:, :n] * vel * BUS["sfx"].g).astype(F32)
        else:
            if name in pitched:
                sig = fn(chord_at(t), vel)
            elif name == "baa":
                sig = fn(vel, pitch=e.get("pitch", 1.0))
            else:
                sig = fn(vel)
            BUS["sfx"].add(t, sig, pan=pan, jitter=0)
        counts[name] = counts.get(name, 0) + 1
    print("  sfx:", ", ".join(f"{k} {v}" for k, v in sorted(counts.items())))


def report():
    """RMS (dBFS) of every bus inside each section, so the mix can be balanced by numbers"""
    names = list(BUS)
    print("            " + " ".join(f"{n[:7]:>7s}" for n in names))
    for sec, b0, b1 in SECTIONS:
        i0, i1 = int(bar(b0) * SR), int(bar(b1 + 1) * SR)
        row = []
        for n in names:
            x = BUS[n].buf[:, i0:i1]
            r = np.sqrt((x.astype(np.float64) ** 2).mean())
            row.append(f"{20*np.log10(r+1e-12):7.1f}" if r > 1e-5 else "      .")
        print(f"  {sec:9s} " + " ".join(row))


def main():
    out = sys.argv[1] if len(sys.argv) > 1 and not sys.argv[1].startswith("--") else os.path.join(os.path.dirname(os.path.abspath(__file__)), "score.wav")
    stems = sys.argv[sys.argv.index("--stems") + 1] if "--stems" in sys.argv else None
    compose()
    if "--sfx" in sys.argv:
        render_sfx(sys.argv[sys.argv.index("--sfx") + 1])
    dry = np.zeros((2, N))
    send = np.zeros((2, N))
    for b in BUS.values():
        dry += b.buf
        send += b.buf * b.verb
    if stems:
        report()
    if "--report-only" in sys.argv:
        return
    ir = make_ir()
    send = fft_filter(send[0], hp_gain(180, 1)), fft_filter(send[1], hp_gain(180, 1))
    wet = np.stack([convolve(send[0], ir[0]), convolve(send[1], ir[1])])
    mix = dry + wet * 0.55
    # tidy the ends of the spectrum: rumble out, a gentle shelf over the top
    shelf = lambda f: hp_gain(32, 2)(f) * (1 / np.sqrt(1 + (f / 11000) ** 4))
    mix = np.stack([fft_filter(mix[0], shelf), fft_filter(mix[1], shelf)])
    pk = np.abs(mix).max()
    mix *= 10 ** (-6 / 20) / pk
    mix, gr = compress(mix)
    mix *= 10 ** (-1.5 / 20) / np.abs(mix).max() * 1.5
    mix = limit(mix, -1.2)
    # fade the very end
    f0 = int(SONG * SR)
    fl = N - f0
    mix[:, f0:] *= np.linspace(1, 0, fl) ** 2
    write_wav(out, mix.astype(np.float64))
    print(f"wrote {out}: {N / SR:.1f} s, compressor max GR {gr:.1f} dB, peak {20*np.log10(np.abs(mix).max()):.2f} dBFS")


if __name__ == "__main__":
    main()
