"""ver4.0 (the user's requests after the second look): the footstep hurt the ears after a while,
then the soft low "poko" was too heavy ("もっとかわいい感じ"). The user picked B6 of the previews: a
tiny, very short rising "pu" (600 Hz rising towards 900 Hz, 0.05 s, peak 0.4), synthesised, no
recording. Two variants (the second 8 % lower) played in turn by PlayerFootstepAudio.
The ver3.x footstep_one_step.wav is kept.
Writes unity-project/Assets/Content/Audio/footstep_soft_0.wav and footstep_soft_1.wav."""
import os
import wave
import numpy as np

SR = 44100
OUT = os.path.join(os.path.dirname(__file__), "..", "..", "unity-project", "Assets", "Content", "Audio")


def step(k):
    n = int(SR * 0.05)
    t = np.arange(n) / SR
    freq = 600 * k + 300 * k * (1 - np.exp(-t / 0.01))
    out = np.sin(2 * np.pi * np.cumsum(freq) / SR) * np.minimum(1.0, t / 0.002) * np.exp(-t / 0.014)
    out *= np.minimum(1.0, (n - 1 - np.arange(n)) / (SR * 0.008))   # no click at the end
    out *= 0.4 / np.max(np.abs(out))
    return (out * 32767).astype(np.int16)


for i, k in enumerate((1.0, 0.92)):
    path = os.path.join(OUT, f"footstep_soft_{i}.wav")
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(step(k).tobytes())
    print("wrote", os.path.normpath(path))
