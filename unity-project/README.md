# HALKA WORLD ver1.5 Unity project

Open this folder with Unity **6000.3.10f1 (Unity 6.3 LTS)**. The WebGL Build Support module is required to build the website version.

The game uses the Built-in Render Pipeline. It currently renders a small 2D scene, while scenes, cameras, and interactions can be replaced for later 3D areas. Game logic is split into `Core`, `Input`, `Player`, `Interaction`, `World`, `Camera`, and `UI` components. `Core/GameVersion.cs` is the only version source.

The ten supplied original GIFs are preserved in `SourceGifs/` and converted without drawing changes to `Assets/Content/Character/`. Four idle and four walk directions are used. `turn` and `front_jump` are stored for later versions. See `Assets/Content/Character/README.md` for the import method. The current design specification is `Docs/HALKA_WORLD_企画書仕様書_ver1.1.md` (document ver1.1, game ver1.5).

ver1.5 treats one logical cell as a 32×32-pixel map tile (0.5 Unity unit at 64 PPU) and uses 0.18 seconds per step. Bounds are 21×13 cells, preserving approximately the previous physical field size. A subtle one-pixel world-space overlay marks cell boundaries. `GridWorld2D.CellToWorld(cell)` returns the center of that tile. Every 32×32 world sprite uses a centered pivot and is placed at the tile center, without a visual offset. The player's root is the foot tile center; its 64×64 artwork uses a centered pivot at a fixed +0.25-unit Y position, covering the foot tile and the tile directly above. No obstacle-dependent artwork movement runs. The scene contains one blocking stone at cell `(1, 1)` using the supplied 32×32 PNG in `Assets/Content/World/stone.png`, imported at 64 pixels per unit without scaling. Its BoxCollider2D matches the opaque 28×20-pixel area. The shared `ExamineInteractable` displays `いし。` only when stopped in a cardinally adjacent cell and facing the stone. Blocked movement input still changes facing. Keyboard input uses WASD or arrows. Touch devices show a four-way D-pad, while taps outside it still go through the shared Interaction router. `?touchControls=1` on the direct `webgl/` URL enables a desktop mouse preview of the mobile controls for verification.

## Build

1. Open `Assets/Scenes/FirstDay.unity` to inspect the scene.
2. Run **HALKA > Build WebGL for HP** in the Editor. The output is written to `../halkaworld/webgl/`. The former `/harukaijiri/` URL redirects to `/halkaworld/`.
3. Serve the repository root over HTTP for testing; opening an HTML file directly cannot load the WebGL data reliably.

Command line build:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe' -batchmode -nographics -quit -projectPath '<repo>\unity-project' -executeMethod Halka.Game.Editor.ProjectBuilder.BuildWeb -logFile '<repo>\unity-build.log'
```

The HP is hosted on GitHub Pages. Build compression is disabled because this host's per-file `Content-Encoding` headers cannot be configured from this repository. The simple custom WebGL template has no Unity or game logo. `ProjectBuilder` disables the Unity splash settings, development build, and debug symbols.

The original `grass.png` is preserved byte for byte from the supplied 32×32 asset. `SourceGifs/grass_rustle.gif` is also preserved byte for byte; `Tools/import_gifs.py` converts its five 32×32 frames to `Assets/Content/World/grass_rustle/` and records 90/90/90/90/420 ms timings in the manifest. The same script creates `grass_front.png` and five `grass_rustle_front/` frames by retaining only the bottom 10 pixels of each original 32×32 image. All grass sprites use the center pivot. `ProjectBuilder` fills every walkable cell in the 21×13 field with `GrassDecoration.prefab` (272 instances, omitting only the blocking stone cell). Each prefab root and its renderer child are at the exact cell center, order 0 behind the player (10). A single world-space `Grass Front Overlay` uses the same center and order 20. `GrassField2D` listens for `PlayerMover.StepStarted` to hide the old Front and start the target Back animation; `StepCompleted` shows the target Front only after arrival, matching the Back's current frame. The previous cell's Back animation may continue after departure. Grass and its overlay have no collider, obstacle, or interaction component, and no player or grass visual offset changes dynamically.

Do not run **HALKA > Prepare ver1.5 scene** on a customized scene: it recreates `FirstDay.unity`. **Build WebGL for HP** preserves existing scene edits. **HALKA > Validate ver1.5** checks grid steps, fixed art anchor, sprites, stone interaction, full-field grass placement, passability and one-shot animation, facing and examine range, message timing, cardinal input, and tap handling. **Refresh character sprites** updates the directional frame arrays after adding art.
