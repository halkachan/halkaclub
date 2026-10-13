はるかわーるど ver3.0 Phase 2 Preview (local technical preview, not a release)
==============================================================================

Start (Unity Editor is not needed):
  1. Open PowerShell in this folder.
  2. python serve_poc.py --port 8891
  3. Open http://127.0.0.1:8891/ in Chrome.
     Phone-style controls on a PC: http://127.0.0.1:8891/?touchControls=1

What is new in Phase 2:
  Blender low-poly ground, dirt roads, grass tufts, trees, flowers and stones,
  placed from the official Map JSON (first_field). The player and the house are
  still the Phase 1 placeholders; signs, bench, well and furniture are boxes.

Controls (same as the game):
  WASD / arrow keys : move one cell (hold to keep walking)
  Left click        : examine the clicked object (only when standing in front of it)
  Touch D-pad / A   : shown on touch devices or with ?touchControls=1

POC-only panel (top left, will NOT go into the real game):
  VIEW              : switch 2D pixel view <-> 3D third-person view
  2D pixels/cell    : 16 / 24 / 32 low-resolution pixels per map cell
  2D camera pitch   : 50 / 60 / 90 / 40 degrees
  2D square ground  : keep each map cell square on screen (like the 2D tiles)
  3D keys           : A world-fixed (W = North) / B camera-relative (W = forward)
  3D indoor cam     : closer / closer + keep the camera inside the room
  Time              : real 10-minute clock / force morning / force night
  Grid/blocked      : show the shared Grid and blocked cells (red X)
  Stats             : FPS, frame time, render texture size, draw calls when available

The published game (ver2.9, still titled HALKA WORLD) and the ver3.0 Preview are not
changed by this build.
