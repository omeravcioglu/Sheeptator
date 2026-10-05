"""A pompous chiptune march for the Sheeptator trailer, in the spirit of the game's own synth
(square waves, saw "baa"s, noise drums). Writes march.wav (44.1 kHz, 16-bit, mono).

Run:  python march.py [out.wav]
Structure (112 BPM, 4/4): drum roll (2 bars), theme A (8), theme B (8), theme A loud (8), baa chord.
"""
import math
import random
import sys
import wave

import numpy as np

RATE = 44100
BPM = 112
BEAT = 60.0 / BPM
BAR = 4 * BEAT
rng = np.random.default_rng(7)

NOTE = {n: i for i, n in enumerate(["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"])}
NOTE.update({"Db": 1, "Eb": 3, "Gb": 6, "Ab": 8, "Bb": 10})


def freq(name):
    """'C5', 'Bb4' ..."""
    p, o = name[:-1], int(name[-1])
    midi = 12 * (o + 1) + NOTE[p]
    return 440.0 * 2 ** ((midi - 69) / 12)


def t_axis(dur):
    return np.arange(int(dur * RATE)) / RATE


def env_adsr(n, a=0.005, d=0.05, s=0.7, r=0.04):
    e = np.ones(n) * s
    na, nd, nr = int(a * RATE), int(d * RATE), int(r * RATE)
    na = max(1, min(na, n))
    e[:na] = np.linspace(0, 1, na)
    nd = max(0, min(nd, n - na))
    e[na:na + nd] = np.linspace(1, s, nd) if nd else e[na:na + nd]
    nr = max(1, min(nr, n))
    e[-nr:] *= np.linspace(1, 0, nr)
    return e


def square(f, dur, duty=0.25, vib=0.0):
    t = t_axis(dur)
    ph = np.cumsum(np.full(len(t), f) * (1 + vib * np.sin(2 * np.pi * 5.5 * t) * np.clip(t * 4 - 0.4, 0, 1))) / RATE
    return np.where((ph % 1.0) < duty, 1.0, -1.0)


def triangle(f, dur):
    t = t_axis(dur)
    p = (t * f) % 1.0
    return 4 * np.abs(p - 0.5) - 1


def noise(dur):
    return rng.uniform(-1, 1, int(dur * RATE))


def baa(f0, dur=0.7, seed=0):
    """Port of Sfx.Baa: saw + pulse + 3rd harmonic with a bleating vibrato."""
    t = t_axis(dur)
    vib = np.sin(t * 2 * np.pi * (11 + seed)) * np.where(t > 0.12, 0.06, 0.01)
    f = f0 * (1 + vib) * np.where(t < 0.08, 1.15 - t * 1.5, 1.0) * (1 - t * 0.15)
    phase = np.cumsum(f) / RATE
    p = phase % 1.0
    s = (2 * p - 1) * 0.5 + np.where(p < 0.3, 1.0, -0.4) * 0.4 + np.sin(phase * 2 * np.pi * 3) * 0.25
    envl = np.minimum(1, t * 25) * np.clip((dur - t) * 5, 0, 1) * (0.75 + 0.25 * np.sin(t * 2 * np.pi * 7))
    return s * envl


class Track:
    def __init__(self, seconds):
        self.buf = np.zeros(int(seconds * RATE) + RATE)

    def add(self, at, sig, gain):
        i = int(at * RATE)
        self.buf[i:i + len(sig)] += sig * gain


# melody: (note or None, length in eighths)
THEME_A = [
    [("G4", 2), ("C5", 2), ("C5", 1), ("D5", 1), ("E5", 2)],
    [("E5", 2), ("D5", 1), ("C5", 1), ("D5", 4)],
    [("G4", 2), ("D5", 2), ("D5", 1), ("E5", 1), ("F5", 2)],
    [("F5", 2), ("E5", 1), ("D5", 1), ("E5", 4)],
    [("E5", 2), ("G5", 2), ("G5", 1), ("F5", 1), ("E5", 2)],
    [("D5", 2), ("F5", 2), ("F5", 1), ("E5", 1), ("D5", 2)],
    [("C5", 2), ("E5", 1), ("D5", 1), ("C5", 2), ("B4", 2)],
    [("C5", 4), ("G4", 2), (None, 2)],
]
THEME_B = [
    [("A4", 2), ("A4", 1), ("A4", 1), ("C5", 2), ("F5", 2)],
    [("E5", 2), ("D5", 2), ("C5", 4)],
    [("Bb4", 2), ("Bb4", 1), ("Bb4", 1), ("D5", 2), ("G5", 2)],
    [("F5", 2), ("E5", 2), ("D5", 4)],
    [("C5", 2), ("C5", 1), ("C5", 1), ("E5", 2), ("G5", 2)],
    [("A5", 2), ("G5", 2), ("F5", 2), ("E5", 2)],
    [("D5", 2), ("E5", 1), ("F5", 1), ("G5", 2), ("G4", 2)],
    [("C5", 2), ("G4", 2), ("C5", 4)],
]
# chord per bar: (root, chord tones) for the oom-pah
CH = {
    "C": ("C3", ["C4", "E4", "G4"]), "G": ("G2", ["B3", "D4", "G4"]), "G7": ("G2", ["B3", "F4", "G4"]),
    "Dm": ("D3", ["D4", "F4", "A4"]), "F": ("F2", ["F4", "A4", "C5"]), "Bb": ("Bb2", ["Bb3", "D4", "F4"]),
    "Am": ("A2", ["A3", "C4", "E4"]),
}
CHORDS_A = ["C", "G", "G7", "C", "C", "Dm", "G7", "C"]
CHORDS_B = ["F", "C", "Bb", "F", "C", "F", "G7", "C"]


def render(out):
    intro_bars = 2
    # theme A, theme B, then the closing half of theme A (ends on the tonic) into the baa chord
    bars = [(THEME_A, CHORDS_A, 0.8), (THEME_B, CHORDS_B, 0.9), (THEME_A[4:], CHORDS_A[4:], 1.0)]
    total = intro_bars * BAR + sum(len(b[0]) for b in bars) * BAR + 3.5
    lead, bass, chords, drums, sfx = (Track(total) for _ in range(5))
    # intro: snare roll crescendo + kick hits
    t0 = 0.0
    n = int(intro_bars * BAR / (BEAT / 4))
    for i in range(n):
        g = 0.15 + 0.6 * i / n
        drums.add(t0 + i * BEAT / 4, noise(0.06) * env_adsr(int(0.06 * RATE), 0.001, 0.02, 0.3, 0.03), g)
    for b in range(intro_bars):
        drums.add(b * BAR, kick(), 0.9)
    t = intro_bars * BAR
    for theme, chs, vol in bars:
        for bi, bar in enumerate(theme):
            bt = t + bi * BAR
            # lead
            pos = 0
            for note, ln in bar:
                dur = ln * BEAT / 2
                if note:
                    f = freq(note)
                    s = square(f, dur * 0.92, duty=0.25, vib=0.006) * env_adsr(int(dur * 0.92 * RATE), 0.004, 0.06, 0.75, 0.03)
                    s2 = square(f * 2, dur * 0.92, duty=0.5) * env_adsr(int(dur * 0.92 * RATE), 0.004, 0.04, 0.3, 0.03)
                    lead.add(bt + pos * BEAT / 2, s + 0.25 * s2, 0.22 * vol)
                pos += ln
            # oom-pah
            root, tones = CH[chs[bi]]
            fifth = freq(root) * 1.5
            for beat in range(4):
                at = bt + beat * BEAT
                if beat in (0, 2):
                    f = freq(root) if beat == 0 else fifth
                    bass.add(at, triangle(f, BEAT * 0.85) * env_adsr(int(BEAT * 0.85 * RATE), 0.003, 0.1, 0.6, 0.05), 0.5)
                    drums.add(at, kick(), 0.7 * vol)
                else:
                    stab = sum(square(freq(x), BEAT * 0.35, duty=0.125) for x in tones) / 3
                    chords.add(at, stab * env_adsr(int(BEAT * 0.35 * RATE), 0.002, 0.05, 0.4, 0.04), 0.13 * vol)
                    drums.add(at, snare(), 0.32 * vol)
            # roll into the next phrase
            if bi in (3, 7):
                for k in range(4):
                    drums.add(bt + 3 * BEAT + k * BEAT / 4, snare(0.05), 0.22 + 0.06 * k)
        # a baa answers the end of each theme
        nb = len(theme)
        if nb == 8:
            sfx.add(t + 7 * BAR + 2.0 * BEAT, baa(freq("G4"), 0.6, 1), 0.45)
        t += nb * BAR
    # finale: a C-major chord of baas and a big hit
    for i, (note, seed) in enumerate([("C4", 0), ("E4", 2), ("G4", 3), ("C5", 4)]):
        sfx.add(t + i * 0.03, baa(freq(note), 1.6, seed), 0.3)
    drums.add(t, kick(), 1.0)
    drums.add(t, noise(1.2) * np.exp(-t_axis(1.2) * 3.5), 0.35)
    bass.add(t, triangle(freq("C2"), 1.6) * env_adsr(int(1.6 * RATE), 0.003, 0.2, 0.7, 0.4), 0.6)
    mix = lead.buf + bass.buf + chords.buf + drums.buf + sfx.buf
    mix = simple_reverb(mix)
    # soft limiter: lift the body of the mix, round off the kick and finale transients
    mix /= max(1e-9, np.percentile(np.abs(mix), 99.5))
    mix = np.tanh(mix * 1.1)
    mix /= max(1e-9, np.max(np.abs(mix))) / 0.89
    pcm = (mix * 32767).astype(np.int16)
    with wave.open(out, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(pcm.tobytes())
    print(out, f"{len(pcm) / RATE:.1f}s")
    return len(pcm) / RATE


def kick():
    t = t_axis(0.18)
    return np.sin(2 * np.pi * np.cumsum(120 - t * 400) / RATE) * np.exp(-t * 22)


def snare(dur=0.12):
    t = t_axis(dur)
    return (noise(dur) * 0.8 + np.sin(2 * np.pi * 190 * t) * 0.3) * np.exp(-t * 30)


def simple_reverb(x):
    out = x.copy()
    for delay, g in ((0.043, 0.22), (0.071, 0.16), (0.113, 0.11), (0.167, 0.07)):
        d = int(delay * RATE)
        out[d:] += x[:-d] * g
    return out


if __name__ == "__main__":
    render(sys.argv[1] if len(sys.argv) > 1 else "march.wav")
