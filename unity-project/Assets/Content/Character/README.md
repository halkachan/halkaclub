# Character sprite slots

The six original GIFs supplied for this project are kept in `unity-project/SourceGifs/`. Run `unity-project/Tools/import_gifs.py` with Pillow to regenerate their PNG frames without redrawing them. `SourceGifs/manifest.json` records source hashes, dimensions, frame counts, and frame timing.

The ordered PNG frames named `00.png`, `01.png`, and so on are placed in these folders:

- `front_idle/`
- `back_idle/`
- `left_idle/`
- `right_idle/`
- `turn/` (imported and reserved for a later turning feature)
- `front_jump/` (imported and reserved; no jump input in ver1.1)

Run **HALKA > Refresh character sprites** in Unity after regenerating PNGs, then rebuild WebGL. The four idle directions are active in ver1.1, including while stepping; no walk art was added. The original GIFs use 400 ms per idle frame, which is set in `CharacterVisual`.
