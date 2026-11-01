"""ver4.0 (the user's request after the second look: the footstep hurt the ears after a while).
Two soft footstep variants, synthesised (no recording): a soft low "poko" (about 300 Hz falling to 180 Hz, still heard on a phone speaker) and a
short low-passed brush of noise, 0.12 s, peak 0.45. The ver3.x footstep_one_step.wav is kept.
Writes unity-project/Assets/Content/Audio/footstep_soft_0.wav and footstep_soft_1.wav."""
import os
import wave
import numpy as np

SR = 44100
OUT = os.path.join(os.path.dirname(__file__), "..", "..", "unity-project", "Assets", "Content", "Audio")


def step(seed, f0, f1, brush):
    rng = np.random.default_rng(seed)
    n = int(SR * 0.12)
    t = np.arange(n) / SR
    attack = np.minimum(1.0, t / 0.004)
    freq = f1 + (f0 - f1) * np.exp(-t / 0.03)
    thump = np.sin(2 * np.pi * np.cumsum(freq) / SR) * attack * np.exp(-t / 0.028)
    noise = rng.standard_normal(n)
    low = np.zeros(n)
    a = np.exp(-2 * np.pi * 1100 / SR)         # one-pole low-pass at about 1.1 kHz, twice
    for _ in range(2):
        acc = 0.0
        for i in range(n):
            acc = (1 - a) * noise[i] + a * acc
            low[i] = acc
        noise = low.copy()
    noise /= np.max(np.abs(noise))
    brush_env = np.minimum(1.0, t / 0.003) * np.exp(-t / 0.02)
    out = thump + brush * noise * brush_env
    out *= np.minimum(1.0, (t[-1] - t) / 0.01)     # no click at the end
    out *= 0.45 / np.max(np.abs(out))
    return (out * 32767).astype(np.int16)


for i, (seed, f0, f1, brush) in enumerate(((7, 330, 190, 0.6), (11, 300, 170, 0.65))):
    path = os.path.join(OUT, f"footstep_soft_{i}.wav")
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(step(seed, f0, f1, brush).tobytes())
    print("wrote", os.path.normpath(path))
