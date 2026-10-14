#!/usr/bin/env python3
"""Synthesises AVAPirates's sound effects and tunes into ../src/AVAPirates/Sounds as 16-bit mono WAV.

Nothing is sampled from any other game: every effect is built here from square, triangle and
noise voices, and the tunes are new compositions. Run it from anywhere:
    python3 tools/make_sounds.py
"""
import math
import os
import random
import struct
import wave

RATE = 22050
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src", "AVAPirates", "Sounds")
RNG = random.Random(1985)


def write(name, samples, volume=0.7):
    peak = max(1e-9, max(abs(s) for s in samples))
    with wave.open(os.path.join(OUT, name), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(s / peak * volume * 32767)) for s in samples))


def voice(kind, phase):
    p = phase % 1.0
    if kind == "square":
        return 1.0 if p < 0.5 else -1.0
    if kind == "pulse":
        return 1.0 if p < 0.25 else -1.0
    if kind == "tri":
        return 4 * p - 1 if p < 0.5 else 3 - 4 * p
    if kind == "saw":
        return 2 * p - 1
    if kind == "sine":
        return math.sin(2 * math.pi * p)
    return RNG.uniform(-1, 1)


def tone(seconds, f0, f1=None, kind="square", attack=0.004, release=1.0, vibrato=0.0, vib_rate=8.0, gain=1.0):
    """A note gliding from f0 to f1 (exponentially), with a short attack and a shaped decay."""
    n = int(RATE * seconds)
    f1 = f1 or f0
    out, phase, held = [], 0.0, 0.0
    for i in range(n):
        t = i / RATE
        f = f0 * (f1 / f0) ** (i / max(1, n - 1))
        if vibrato:
            f *= 1 + vibrato * math.sin(2 * math.pi * vib_rate * t)
        phase += f / RATE
        if kind == "noise":
            # Noise re-sampled at the voice's rate: a lower 'pitch' sounds rougher.
            if int(phase) != int(phase - f / RATE):
                held = RNG.uniform(-1, 1)
            s = held
        else:
            s = voice(kind, phase)
        env = min(1.0, t / attack) * (1 - i / n) ** release
        out.append(s * env * gain)
    return out


def silence(seconds):
    return [0.0] * int(RATE * seconds)


def cat(*parts):
    out = []
    for p in parts:
        out.extend(p)
    return out


def mix(*parts):
    n = max(len(p) for p in parts)
    out = [0.0] * n
    for p in parts:
        for i, v in enumerate(p):
            out[i] += v
    return out


NOTES = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}


def hz(note):
    n = NOTES[note[0]]
    rest = note[1:]
    while rest and rest[0] in "#b":
        n += 1 if rest[0] == "#" else -1
        rest = rest[1:]
    return 440.0 * 2 ** ((12 * (int(rest) + 1) + n - 69) / 12)


def tune(bpm, lead, bass, kind="pulse"):
    """Two voices: a lead and a bass line, as (note, beats) lists; '-' is a rest."""
    beat = 60.0 / bpm

    def line(notes, k, gain, release):
        out = []
        for note, beats in notes:
            d = beats * beat
            out.extend(silence(d) if note == "-" else tone(d, hz(note), kind=k, release=release, gain=gain))
        return out

    return mix(line(lead, kind, 0.55, 0.6), line(bass, "tri", 0.6, 0.3))


def main():
    os.makedirs(OUT, exist_ok=True)
    # The guns, the sea and the timbers.
    write("Cannon.wav", mix(tone(0.7, 140, 28, "noise", release=0.5), tone(0.55, 80, 30, "square", release=0.8), tone(0.06, 2400, 500, "noise", release=1.0, gain=0.6)), 0.65)
    write("Splash.wav", mix(tone(0.4, 1500, 300, "noise", release=0.7), tone(0.25, 260, 120, "sine", release=0.9, gain=0.3)), 0.3)
    write("Crack.wav", mix(tone(0.18, 900, 150, "noise", release=0.6), tone(0.15, 220, 70, "saw", release=0.9)), 0.5)
    write("Sink.wav", cat(mix(tone(1.2, 200, 40, "noise", release=0.4), tone(1.2, 120, 30, "saw", release=0.5, vibrato=0.15, vib_rate=4)), tone(0.6, 400, 100, "noise", release=0.9, gain=0.5)), 0.55)
    write("Board.wav", cat(*[mix(tone(0.07, 700, 300, "noise", release=0.8), tone(0.06, 320, 200, "square", release=1.0, gain=0.4)) for _ in range(3)], tone(0.3, 300, 120, "noise", release=0.6)), 0.5)
    write("Struck.wav", cat(*[tone(0.15, hz(n), kind="tri", release=0.4) for n in ["G4", "D4", "G3"]]), 0.45)
    write("Storm.wav", mix(tone(1.8, 400, 120, "noise", release=0.3), tone(1.8, 60, 40, "saw", release=0.4, vibrato=0.3, vib_rate=2)), 0.5)
    write("Turn.wav", tone(0.12, 300, 200, "noise", release=1.2, gain=0.6), 0.15)
    write("Bump.wav", mix(tone(0.2, 120, 50, "square", release=1.0), tone(0.25, 300, 80, "noise", release=1.0)), 0.4)
    # Swordplay.
    write("Clang.wav", mix(tone(0.35, 2400, 2350, "square", release=1.5, gain=0.5), tone(0.35, 3100, 3000, "sine", release=1.2), tone(0.05, 4000, 1000, "noise", release=1.0, gain=0.6)), 0.45)
    write("Swish.wav", tone(0.16, 2500, 700, "noise", release=0.9, gain=0.7), 0.25)
    write("Stab.wav", mix(tone(0.12, 600, 150, "noise", release=0.9), tone(0.18, 180, 90, "saw", release=0.8)), 0.45)
    write("Yield.wav", cat(tone(0.2, hz("E4"), kind="tri", release=0.4), tone(0.4, hz("C4"), kind="tri", release=0.6)), 0.4)
    write("Overboard.wav", cat(tone(0.35, 900, 200, "sine", release=0.4), mix(tone(0.5, 1500, 200, "noise", release=0.6), tone(0.3, 200, 90, "sine", release=0.8, gain=0.4))), 0.5)
    # The port, the purse and the menus.
    write("Bell.wav", mix(*[cat(silence(0.5 * i), tone(1.4, hz("A4"), kind="sine", release=0.7), ) for i in range(2)], *[cat(silence(0.5 * i), tone(1.4, hz("E5"), kind="sine", release=0.9, gain=0.4)) for i in range(2)]), 0.4)
    write("Coin.wav", cat(tone(0.05, hz("B5"), kind="square", release=0.5), tone(0.18, hz("E6"), kind="square", release=0.7)), 0.3)
    write("Dig.wav", cat(*[cat(tone(0.08, 300, 120, "noise", release=1.0), silence(0.12)) for _ in range(3)], tone(0.2, 150, 90, "square", release=0.8)), 0.45)
    write("Menu.wav", tone(0.03, 1200, kind="tri", release=0.8), 0.2)
    write("Pick.wav", cat(tone(0.03, 900, kind="tri", release=0.6), tone(0.05, 1350, kind="tri", release=0.8)), 0.25)
    write("Fanfare.wav", tune(160,
        [("C5", 0.5), ("C5", 0.25), ("C5", 0.25), ("G5", 1), ("E5", 0.5), ("G5", 0.5), ("C6", 2)],
        [("C3", 1), ("G2", 1), ("C3", 1), ("G2", 0.5), ("C3", 1.5)]), 0.5)
    write("Victory.wav", tune(150,
        [("D5", 0.5), ("F#5", 0.5), ("A5", 0.5), ("D6", 1), ("A5", 0.5), ("D6", 2.5)],
        [("D3", 1.5), ("A2", 1), ("D3", 3)]), 0.55)
    write("Defeat.wav", tune(70,
        [("D5", 1), ("C5", 1), ("Bb4", 1), ("A4", 1), ("G4", 1), ("F#4", 1), ("G4", 3)],
        [("G2", 2), ("Eb2", 2), ("D2", 2), ("G2", 3)], kind="tri"), 0.55)

    # The title: a rolling hornpipe in D, ship-shape and bold (new). Loops.
    write("TitleTune.wav", tune(132,
        [("A4", 0.5), ("D5", 0.5), ("F#5", 0.5), ("A5", 0.5), ("G5", 0.5), ("F#5", 0.5), ("E5", 0.5), ("D5", 0.5),
         ("C#5", 0.5), ("E5", 0.5), ("A5", 1), ("A4", 1), ("-", 1),
         ("B4", 0.5), ("D5", 0.5), ("G5", 0.5), ("B5", 0.5), ("A5", 0.5), ("G5", 0.5), ("F#5", 0.5), ("E5", 0.5),
         ("D5", 0.5), ("F#5", 0.5), ("E5", 0.5), ("C#5", 0.5), ("D5", 2),
         ("F#5", 0.5), ("A5", 0.5), ("D6", 1), ("C#6", 0.5), ("B5", 0.5), ("A5", 1),
         ("G5", 0.5), ("B5", 0.5), ("E5", 1), ("F#5", 0.5), ("G5", 0.5), ("A5", 1),
         ("B5", 0.5), ("A5", 0.5), ("G5", 0.5), ("F#5", 0.5), ("E5", 0.5), ("C#5", 0.5), ("A4", 1),
         ("D5", 0.5), ("F#5", 0.5), ("E5", 0.5), ("C#5", 0.5), ("D5", 2)],
        [(n, 1) for n in ["D3", "A2", "D3", "A2", "A2", "E3", "A2", "A2", "G2", "D3", "G2", "D3", "A2", "A2", "D3", "D3",
                          "D3", "A2", "D3", "A2", "G2", "D3", "A2", "E3", "G2", "D3", "A2", "A2", "D3", "A2", "D3", "D3"]], kind="pulse"), 0.45)
    # At sea: a broad, swaying air in G, three to the bar (new). Loops.
    write("SeaMusic.wav", tune(108,
        [("D5", 2), ("B4", 1), ("G4", 2), ("A4", 1), ("B4", 1.5), ("C5", 0.5), ("D5", 1), ("E5", 3),
         ("D5", 2), ("G5", 1), ("F#5", 2), ("E5", 1), ("D5", 1.5), ("C5", 0.5), ("A4", 1), ("B4", 3),
         ("D5", 2), ("B4", 1), ("G4", 2), ("A4", 1), ("B4", 1.5), ("D5", 0.5), ("G5", 1), ("E5", 3),
         ("C5", 2), ("E5", 1), ("D5", 1.5), ("C5", 0.5), ("A4", 1), ("G4", 3)],
        [(n, 3) for n in ["G2", "E2", "C3", "C3", "G2", "D3", "D3", "G2", "G2", "E2", "C3", "C3", "A2", "D3", "G2", "G2"]], kind="tri"), 0.32)
    # In port: a jaunty jig in A minor for the taverns (new). Loops.
    write("PortMusic.wav", tune(150,
        [("A4", 0.5), ("C5", 0.5), ("E5", 0.5), ("A5", 1), ("G5", 0.5), ("E5", 1), ("D5", 0.5), ("C5", 0.5), ("B4", 0.5), ("G4", 0.5),
         ("A4", 0.5), ("C5", 0.5), ("E5", 0.5), ("D5", 0.5), ("B4", 0.5), ("G4", 0.5), ("A4", 1.5), ("-", 1.5),
         ("E5", 0.5), ("G5", 0.5), ("E5", 0.5), ("D5", 1), ("B4", 0.5), ("C5", 0.5), ("D5", 0.5), ("E5", 0.5), ("D5", 0.5), ("B4", 0.5), ("G4", 0.5),
         ("A4", 0.5), ("B4", 0.5), ("C5", 0.5), ("B4", 0.5), ("G#4", 0.5), ("E4", 0.5), ("A4", 1.5), ("-", 1.5)],
        [(n, 1.5) for n in ["A2", "A2", "G2", "G2", "A2", "E2", "A2", "A2", "C3", "C3", "G2", "G2", "A2", "E2", "A2", "A2"]], kind="pulse"), 0.34)
    # Battle: guns and drums, a driving figure in E minor (new). Loops.
    lead = [("E5", 0.5), ("E5", 0.25), ("E5", 0.25), ("B4", 0.5), ("E5", 0.5), ("G5", 0.5), ("F#5", 0.5), ("E5", 0.5), ("D5", 0.5),
            ("C5", 0.5), ("C5", 0.25), ("C5", 0.25), ("G4", 0.5), ("C5", 0.5), ("E5", 0.5), ("D5", 0.5), ("B4", 1),
            ("E5", 0.5), ("E5", 0.25), ("E5", 0.25), ("B4", 0.5), ("E5", 0.5), ("A5", 0.5), ("G5", 0.5), ("F#5", 0.5), ("G5", 0.5),
            ("A5", 0.5), ("G5", 0.5), ("F#5", 0.5), ("D#5", 0.5), ("E5", 2)]
    bass = [(n, 0.5) for n in ["E2", "E3"] * 4 + ["C2", "C3"] * 4 + ["E2", "E3"] * 4 + ["B1", "B2"] * 2 + ["E2", "E3"] * 2]
    write("BattleMusic.wav", tune(156, lead, bass), 0.4)
    # Swordplay: quick, nervous fencing music in D minor (new). Loops.
    lead = [("D5", 0.25), ("E5", 0.25), ("F5", 0.25), ("E5", 0.25), ("D5", 0.5), ("A4", 0.5), ("Bb4", 0.25), ("C5", 0.25), ("D5", 0.25), ("C5", 0.25), ("Bb4", 0.5), ("G4", 0.5),
            ("A4", 0.25), ("Bb4", 0.25), ("C5", 0.25), ("D5", 0.25), ("E5", 0.5), ("C#5", 0.5), ("D5", 1), ("-", 1),
            ("F5", 0.25), ("G5", 0.25), ("A5", 0.25), ("G5", 0.25), ("F5", 0.5), ("D5", 0.5), ("E5", 0.25), ("F5", 0.25), ("G5", 0.25), ("F5", 0.25), ("E5", 0.5), ("C#5", 0.5),
            ("D5", 0.25), ("E5", 0.25), ("F5", 0.25), ("E5", 0.25), ("D5", 0.5), ("C#5", 0.5), ("D5", 2)]
    bass = [(n, 0.5) for n in ["D2", "A2"] * 4 + ["Bb1", "F2", "A1", "E2"] * 2 + ["D2", "A2"] * 4 + ["G1", "D2", "A1", "E2", "D2", "A2", "D2", "A2"]]
    write("DuelMusic.wav", tune(144, lead, bass), 0.38)
    # Retirement: the title's hornpipe, slowed to an old sailor's walk (new). Loops.
    write("Ending.wav", tune(76,
        [("A4", 1), ("D5", 1), ("F#5", 1), ("A5", 1.5), ("G5", 0.5), ("F#5", 1), ("E5", 1), ("D5", 2),
         ("B4", 1), ("D5", 1), ("G5", 1), ("B5", 1.5), ("A5", 0.5), ("G5", 1), ("F#5", 1), ("E5", 2),
         ("F#5", 1), ("A5", 1), ("D6", 2), ("C#6", 1), ("B5", 1), ("A5", 2),
         ("G5", 1), ("E5", 1), ("C#5", 1), ("E5", 1), ("D5", 4)],
        [(n, 2) for n in ["D3", "D3", "A2", "D3", "G2", "G2", "A2", "A2", "D3", "D3", "G2", "A2", "E2", "A2", "D3", "D3"]], kind="tri"), 0.5)


if __name__ == "__main__":
    main()
