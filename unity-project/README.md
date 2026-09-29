# HALKA game ver1.1 Unity project

Open this folder with Unity **6000.3.10f1 (Unity 6.3 LTS)**. The WebGL Build Support module is required to build the website version.

The game uses the Built-in Render Pipeline. It currently renders a small 2D scene, while scenes, cameras, and interactions can be replaced for later 3D areas. Game logic is split into `Core`, `Input`, `Player`, `Interaction`, `World`, `Camera`, and `UI` components. `Core/GameVersion.cs` is the only version source.

The six supplied original GIFs are preserved in `SourceGifs/` and converted without drawing changes to `Assets/Content/Character/`. All four idle directions are used. `turn` and `front_jump` are stored for later versions. See `Assets/Content/Character/README.md` for the import method.

ver1.1 uses one-unit grid cells and 0.18 seconds per step. The scene contains one blocking stone at cell `(1, 1)` using the supplied 64×64 PNG in `Assets/Content/World/stone.png`. The Sprite uses 80 pixels per unit and a PolygonCollider2D fitted to its opaque shape. Keyboard input uses WASD or arrows. Touch devices show a four-way D-pad, while taps outside it still go through the shared Interaction router. `?touchControls=1` on the direct `webgl/` URL enables a desktop mouse preview of the mobile controls for verification.

## Build

1. Open `Assets/Scenes/FirstDay.unity` to inspect the scene.
2. Run **HALKA > Build WebGL for HP** in the Editor. The output is written to `../halkaworld/webgl/`. The former `/harukaijiri/` URL redirects to `/halkaworld/`.
3. Serve the repository root over HTTP for testing; opening an HTML file directly cannot load the WebGL data reliably.

Command line build:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe' -batchmode -nographics -quit -projectPath '<repo>\unity-project' -executeMethod Halka.Game.Editor.ProjectBuilder.BuildWeb -logFile '<repo>\unity-build.log'
```

The HP is hosted on GitHub Pages. Build compression is disabled because this host's per-file `Content-Encoding` headers cannot be configured from this repository. The simple custom WebGL template has no Unity or game logo. `ProjectBuilder` disables the Unity splash settings, development build, and debug symbols.

Do not run **HALKA > Prepare ver1.1 scene** on a customized scene: it recreates `FirstDay.unity`. **Build WebGL for HP** preserves existing scene edits. **HALKA > Validate ver1.1** checks grid steps, the stone, cardinal input, and tap handling. **Refresh character sprites** updates just the directional frame arrays after adding art.
