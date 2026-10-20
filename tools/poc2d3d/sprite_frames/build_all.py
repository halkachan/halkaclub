"""Build every 2D ﾊﾙｶﾁｬﾝ frame from the four base pictures in bases/ (1 front, 2 back, 3 right, 4 left)
by the motion of the old GIFs in templates/, write the GIFs, and optionally install them.

  python build_all.py             frames in out/, GIFs in gifs/ (nothing in the game changes)
  python build_all.py --install   also: GIFs to unity-project/SourceGifs/, bench_sit.png to
                                  Assets/Content/Character/, then unity-project/Tools/import_gifs.py
Then open Unity (or run any batch check) so the PNGs are imported, and rebuild WebGL.
"""
import os
import shutil
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
UNITY = os.path.abspath(os.path.join(HERE, "..", "..", "..", "unity-project"))

import gen_basic   # noqa: E402
import gen_side    # noqa: E402
import gen_jump    # noqa: E402
import gen_bench   # noqa: E402
import make_gifs   # noqa: E402


def main():
    gen_basic.main()
    gen_side.main()
    gen_jump.main()
    gen_bench.main()
    gifs = os.path.join(HERE, "gifs")
    make_gifs.main(gifs)
    if "--install" in sys.argv:
        for name in make_gifs.NAMES:
            shutil.copyfile(os.path.join(gifs, name + ".gif"), os.path.join(UNITY, "SourceGifs", name + ".gif"))
        shutil.copyfile(os.path.join(HERE, "out", "bench_sit.png"),
                        os.path.join(UNITY, "Assets", "Content", "Character", "bench_sit.png"))
        subprocess.run([sys.executable, os.path.join(UNITY, "Tools", "import_gifs.py")], check=True)
        print("installed: SourceGifs, Character frames, bench_sit.png (update tools/poc2d3d/player_sprite_sha256.txt"
              " and run tools/poc2d3d/player_model_manifest.py so the checks know the new set)")


if __name__ == "__main__":
    main()
