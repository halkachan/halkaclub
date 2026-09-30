# Character sprite slots

The ten original GIFs supplied for this project are kept in `unity-project/SourceGifs/`. Run `unity-project/Tools/import_gifs.py` with Pillow to regenerate their PNG frames without redrawing them. `SourceGifs/manifest.json` records source hashes, dimensions, frame counts, and frame timing.

The ordered PNG frames named `00.png`, `01.png`, and so on are placed in these folders:

- `front_idle/`
- `back_idle/`
- `left_idle/`
- `right_idle/`
- `walk_front/`
- `walk_back/`
- `walk_left/`
- `walk_right/`
- `turn/` (imported and reserved for a later turning feature)
- `front_jump/` (imported and reserved; no jump input in ver1.3)

Run **HALKA > Refresh character sprites** in Unity after regenerating PNGs, then rebuild WebGL. The four idle directions use 400 ms per frame and the four walk directions use 150 ms per frame, matching the original GIF timing. All frames are 64×64 with fixed pivots, Point filtering, no mipmaps and uncompressed import at 64 PPU. `CharacterVisual` keeps the active walk clip between successive held steps.
