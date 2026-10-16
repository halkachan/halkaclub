はるかわーるど ver3.0 Phase 3 Preview (local technical preview, not a release)
============================================================================

Start (Unity Editor is not needed):
  1. Open PowerShell in this folder.
  2. python serve_poc.py --port 8893
  3. Open http://127.0.0.1:8893/ in Chrome.
     Phone-style controls on a PC: http://127.0.0.1:8893/?touchControls=1

What is new in Phase 3:
  - The house, the bench, the signs and the well are Blender low-poly models (2D and 3D).
  - Walk up into the house's entrance alcove to go inside, as before. The door sits at the
    back of the alcove, so ﾊﾙｶﾁｬﾝ steps up to it instead of walking into it.
  - The room is built from Blender pieces: floor, walls, the doorway and all furniture
    (bed, desk, cushion, recording mic, easel, shelf).
  - 3D indoors: the wall sides facing the camera are cut down to a low base, so the room,
    the furniture and the player stay in sight from every direction.
  - 3D outdoors: walking behind the house hides only the parts in the way (the roof alone,
    or the walls with the door and the roof); the ground shadow stays.
  - 3D clicks: a click that lands on ﾊﾙｶﾁｬﾝ but on no object examines what she faces (the
    same as the A button); a standing wall stops clicks on things behind it.
  Phase 2 scenery, the Phase 2.1 controls, outlines, see-through trees and the 2D sprite
  of ﾊﾙｶﾁｬﾝ are unchanged. The 3D ﾊﾙｶﾁｬﾝ is still the TEMPORARY blocky model.

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
  2D see-through trees : ON (default) / OFF
  3D indoor walls   : cut camera side (default) / rays only (Phase 1/2 behaviour)

The published game (ver2.9, still titled HALKA WORLD) and the ver3.0 Preview are not
changed by this build. Phase 1, 2 and 2.1 previews stay in their own folders.
