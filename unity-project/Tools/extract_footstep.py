"""Extract one audible step from the user-supplied multi-step MP3.

Requires ffmpeg on PATH. The original MP3 remains unchanged in Content/Audio.
"""

from pathlib import Path
import subprocess


SAMPLE_RATE = 44100
START_SAMPLE = 3087  # 0.070 s, before the first audible burst.
END_SAMPLE = 7497    # 0.170 s, after the first audible burst.
ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Assets/Content/Audio/footstep.mp3"
OUTPUT = ROOT / "Assets/Content/Audio/footstep_one_step.wav"


def main() -> None:
    subprocess.run(
        [
            "ffmpeg", "-hide_banner", "-loglevel", "error", "-y",
            "-i", str(SOURCE),
            "-af", f"atrim=start_sample={START_SAMPLE}:end_sample={END_SAMPLE},asetpts=PTS-STARTPTS",
            "-c:a", "pcm_s16le", "-ar", str(SAMPLE_RATE), "-ac", "1",
            str(OUTPUT),
        ],
        check=True,
    )


if __name__ == "__main__":
    main()
