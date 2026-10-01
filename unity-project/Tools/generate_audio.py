"""Generate the original, deterministic HALKA WORLD step sound."""

from pathlib import Path
import math
import struct
import wave

SAMPLE_RATE = 22050
DURATION_SECONDS = 0.052
OUTPUT = Path(__file__).resolve().parents[1] / "Assets/Content/Audio/footstep.wav"


def main() -> None:
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    count = round(SAMPLE_RATE * DURATION_SECONDS)
    seed = 0x48414C4B
    samples = []
    for index in range(count):
        seed = (1664525 * seed + 1013904223) & 0xFFFFFFFF
        noise = ((seed >> 16) / 32767.5) - 1.0
        t = index / SAMPLE_RATE
        envelope = (1.0 - index / count) ** 2
        tone = math.sin(2.0 * math.pi * (190.0 * t + 500.0 * t * t))
        value = (0.65 * noise + 0.35 * tone) * envelope * 0.28
        samples.append(struct.pack("<h", round(max(-1.0, min(1.0, value)) * 32767)))
    with wave.open(str(OUTPUT), "wb") as wav:
        wav.setnchannels(1)
        wav.setsampwidth(2)
        wav.setframerate(SAMPLE_RATE)
        wav.writeframes(b"".join(samples))


if __name__ == "__main__":
    main()
