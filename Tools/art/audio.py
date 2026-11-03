#!/usr/bin/env python3
"""Game audio from the Higgsfield takes in art-src/audio (free; needs ffmpeg).

  python3 Tools/art/audio.py

Sound effects are trimmed to the useful hit, faded and peak-normalised, mono; the ambience beds and the
music are cut to a seamless loop (the tail crossfaded into the head). Everything is written as OGG Vorbis to
Assets/Resources/Audio/<name>.ogg, which AudioManager loads in place of its synthesized sounds.

Sources (Higgsfield, 2026-11-03): Seed Audio 1.0 for the effects, Mirelo text-to-audio for the ambience,
Sonilo for the music. Prompts are in the commit that added this script.
"""
import os
import subprocess
import sys

import numpy as np

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC = os.path.join(ROOT, "art-src", "audio")
OUT = os.path.join(ROOT, "Assets", "Resources", "Audio")
SR = 44100

# name: (source file, start s, length s, peak, fade-out s). start=None finds the first hit.
SFX = {
    "click":   ("click.wav",   None, 0.12, 0.55, 0.05),
    "coin":    ("coin.wav",    0.78, 0.70, 0.80, 0.30),
    "build":   ("build.wav",   None, 1.45, 0.85, 0.20),
    "error":   ("error.wav",   None, 0.40, 0.55, 0.12),
    "levelup": ("levelup.wav", None, 2.90, 0.85, 0.45),
    "deliver": ("deliver.wav", None, 1.00, 0.80, 0.30),
    "bell":    ("bell.wav",    None, 5.00, 0.75, 1.50),
    "whistle": ("whistle.wav", 0.75, 2.60, 0.85, 0.50),
    "chuff":   ("chuff.wav",   None, 0.60, 0.85, 0.25),
}
# name: (source, start s, end s, crossfade s, peak, channels)
LOOPS = {
    "amb_day":   ("amb_day.mp3",   0.2, 10.0, 2.0, 0.55, 1),
    "amb_night": ("amb_night.mp3", 0.2, 10.0, 2.0, 0.55, 1),
    "music":     ("music.m4a",     4.0, 84.0, 3.0, 0.70, 2),
}


def load(name, channels):
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", os.path.join(SRC, name), "-ac", str(channels), "-ar", str(SR), "-f", "f32le", "-"],
                         check=True, capture_output=True).stdout
    a = np.frombuffer(raw, dtype=np.float32).astype(np.float64)
    return a.reshape(-1, channels)


def save(name, a):
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, name + ".ogg")
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "f32le", "-ar", str(SR), "-ac", str(a.shape[1]), "-i", "-",
                    "-c:a", "libvorbis", "-q:a", "5", path], input=a.astype(np.float32).tobytes(), check=True)
    return path


def normalise(a, peak):
    m = np.abs(a).max()
    return a * (peak / m) if m > 0 else a


def sfx(name, src, start, length, peak, fade):
    a = load(src, 1)
    if start is None:
        env = np.abs(a[:, 0])
        hit = np.argmax(env > env.max() * 0.12)
        s = max(0, hit - int(0.008 * SR))
    else:
        s = int(start * SR)
    a = a[s:s + int(length * SR)].copy()
    n = len(a)
    a[:int(0.004 * SR)] *= np.linspace(0, 1, int(0.004 * SR))[:, None]  # no click at the cut
    f = min(n, int(fade * SR))
    a[n - f:] *= np.linspace(1, 0, f)[:, None] ** 2
    return normalise(a, peak)


def loop(src, start, end, xfade, peak, channels):
    a = load(src, channels)
    a = a[int(start * SR):int(end * SR)].copy()
    x = int(xfade * SR)
    head, tail = a[:x].copy(), a[-x:].copy()
    t = np.linspace(0, 1, x)[:, None]
    body = a[:-x].copy()
    body[:x] = head * np.sqrt(t) + tail * np.sqrt(1 - t)  # equal-power crossfade: the end flows into the start
    return normalise(body, peak)


def main():
    if not os.path.isdir(SRC):
        sys.exit("missing " + SRC)
    for name, spec in SFX.items():
        print(name, save(name, sfx(name, *spec)))
    for name, spec in LOOPS.items():
        print(name, save(name, loop(*spec)))


if __name__ == "__main__":
    main()
