はるかわーるど ver3.0 Phase 2.1 Preview (local technical preview, not a release)
==============================================================================

Start (Unity Editor is not needed):
  1. Open PowerShell in this folder.
  2. python serve_poc.py --port 8892
  3. Open http://127.0.0.1:8892/ in Chrome.
     Phone-style controls on a PC: http://127.0.0.1:8892/?touchControls=1

What is new in Phase 2.1:
  - 3D view: W/S/A/D (and the D-pad) move forward/back/left/right as seen from the camera.
    A held key keeps its direction; S walks back toward the camera without spinning it.
    After a sideways move the camera swings behind the player once you stop.
  - 2D view: dark pixel outlines (1 px by default) around trees, flowers, stones, the house,
    boxes and the player, drawn in the low-resolution image before the integer upscale.
  - A TEMPORARY low-poly ﾊﾙｶﾁｬﾝ (placeholder) replaces the grey mannequin. It is not the
    official model; the official one will replace it later.
  Phase 2 scenery (ground, roads, grass, trees, flowers, stones) is unchanged.

Controls (same as the game):
  WASD / arrow keys : move one cell (hold to keep walking)
  Left click        : examine the clicked object (only when standing in front of it)
  Touch D-pad / A   : shown on touch devices or with ?touchControls=1

POC-only panel (top left, will NOT go into the real game):
  VIEW              : switch 2D pixel view <-> 3D third-person view
  2D pixels/cell    : 16 / 24 / 32 low-resolution pixels per map cell
  2D camera pitch   : 50 / 60 / 90 / 40 degrees
  2D square ground  : keep each map cell square on screen (like the 2D tiles)
  3D keys           : B camera-relative (default) / A world-fixed (W = North)
  3D indoor cam     : closer / closer + keep the camera inside the room
  Time              : real 10-minute clock / force morning / force night
  Grid/blocked      : show the shared Grid and blocked cells (red X)
  Stats             : FPS, frame time, render texture size, draw calls when available
  3D cam follow     : turn when idle (default) / fixed / facing (Phase 1 behaviour)
  2D outline        : 1 px (default) / 2 px / OFF
  Outline color     : #252B39 blue-grey / #3A2A22 brown / #000000 black
  Grass outline     : OFF (default) / soft
  Inner lines       : soft (default) / OFF

The published game (ver2.9, still titled HALKA WORLD) and the ver3.0 Preview are not
changed by this build.
