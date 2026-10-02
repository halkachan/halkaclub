# HALKA WORLD ver1.8 Unity project

Open this folder with Unity **6000.3.10f1 (Unity 6.3 LTS)**. The WebGL Build Support module is required to build the website version.

The game uses the Built-in Render Pipeline. It currently renders a small 2D scene, while scenes, cameras, and interactions can be replaced for later 3D areas. Game logic is split into `Core`, `Input`, `Player`, `Interaction`, `World`, `Camera`, and `UI` components. `Core/GameVersion.cs` is the only version source.

The ten supplied original GIFs are preserved in `SourceGifs/` and converted without drawing changes to `Assets/Content/Character/`. Four idle and four walk directions are used. `turn` and `front_jump` are stored for later versions. See `Assets/Content/Character/README.md` for the import method. The current design specification is `Docs/HALKA_WORLD_企画書仕様書_ver1.1.md` (document ver1.1, game ver1.8).

ver1.8 treats one logical cell as a 32×32-pixel map tile (0.5 Unity unit at 64 PPU) and uses 0.18 seconds per step. Bounds are 21×13 cells. A subtle one-pixel world-space overlay marks cell boundaries. `GridWorld2D.CellToWorld(cell)` returns the tile center. Every 32×32 world sprite uses a centered pivot and the tile center without a visual offset. The player's root is the foot tile center; its 64×64 artwork is fixed at +0.25 unit Y. Stone `(1, 1)`, flower `(-3, 1)`, and the tree root `(5, 1)` use the shared obstacle and examine components. The tree artwork is the unscaled 96×128 sprite centered 0.75 unit above its root, spanning 3×4 cells. Only its bottom-middle root cell blocks walking; the separate artwork collider receives canopy clicks. `RootedWorldObjectDepth2D` changes only tree sorting order according to the player's foot row. The tree shows `き。` only when the player is stopped in an adjacent cell and faces its root. Blocked input still changes facing. Keyboard input uses WASD or arrows. Touch devices show a four-way D-pad and an A button; the A button checks the cell ahead through the shared router. `?touchControls=1` on the direct `webgl/` URL enables a desktop mouse preview.

## Build

1. Open `Assets/Scenes/FirstDay.unity` to inspect the scene.
2. Run **HALKA > Build WebGL for HP** in the Editor. The output is written to `../halkaworld/webgl/`. The former `/harukaijiri/` URL redirects to `/halkaworld/`.
3. Serve the repository root over HTTP for testing; opening an HTML file directly cannot load the WebGL data reliably.

Command line build:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe' -batchmode -nographics -quit -projectPath '<repo>\unity-project' -executeMethod Halka.Game.Editor.ProjectBuilder.BuildWeb -logFile '<repo>\unity-build.log'
```

The HP is hosted on GitHub Pages. Build compression is disabled because this host's per-file `Content-Encoding` headers cannot be configured from this repository. The simple custom WebGL template has no Unity or game logo. `ProjectBuilder` disables the Unity splash settings, development build, and debug symbols. Managed code stripping is disabled for this build because stripping the SpriteMask engine class caused WebGL startup to fail; this increases the WebAssembly download size.

The original `grass.png`, `flower.png`, and `tree.png` are preserved byte for byte from the supplied assets. `SourceGifs/grass_rustle.gif` is also preserved byte for byte; `Tools/import_gifs.py` converts its five frames and records 90/90/90/90/420 ms timings. `ProjectBuilder` fills every walkable cell in the 21×13 field with `GrassDecoration.prefab` (270 instances; `GridObstacle` excludes stone, flower, and tree root without name checks). Grass renders behind Player and has no collider or interaction. `GrassField2D.HasGrass(cell)` exposes occupancy and animates only grass entered by the player. One Player-only `SpriteMask` hides the bottom ten canvas pixels when source or destination contains grass. Player, grass, stone, flower, and tree artwork positions do not change for occlusion or depth.

Do not run **HALKA > Prepare ver1.8 scene** on a customized scene: it recreates `FirstDay.unity`. **Build WebGL for HP** preserves existing scene edits. **HALKA > Validate ver1.8** checks grid steps, fixed art anchors, the root-only tree obstacle and click collider, tree depth, all three examine objects, grass placement and animation, message timing, audio wiring, and mobile action wiring. **Refresh character sprites** updates the directional frame arrays after adding art.

Each successful StepStarted plays one step from `Assets/Content/Audio/footstep_one_step.wav` through PlayerFootstepAudio. The unchanged user MP3 is saved as `footstep.mp3`; it contains six separate bursts, so `Tools/extract_footstep.py` extracts the first burst by sample range without EQ, pitch, normalization, or speed changes. The message box uses a white background, black text, a thin border, and k8x12L.
