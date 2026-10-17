はるかわーるど ver3.0 Phase 4 Preview (local technical preview, not a release)
============================================================================

Start (Unity Editor is not needed):
  1. Open PowerShell in this folder.
  2. python serve_poc.py --port 8894
  3. Open http://127.0.0.1:8894/ in Chrome.
     Phone-style controls on a PC: http://127.0.0.1:8894/?touchControls=1
     POC panel (developer options):  http://127.0.0.1:8894/?debug=1
     Both:                           http://127.0.0.1:8894/?debug=1&touchControls=1

What is new in Phase 4 (one shared world: 2D and 3D show the same game state):
  - Menu (top left): AUTO ON/OFF, Life Log, and "ひょうじ 2D/3D" to switch the view.
  - AUTO: after 60 seconds without input ﾊﾙｶﾁｬﾝ strolls by herself, outdoors and indoors
    (she never enters or leaves the house by herself). Any key, click or touch stops it.
  - Life Log: steps and play time count the same way in 2D and 3D.
  - The crow: one crow for both views. 2D draws the original crow sprites; 3D draws a
    Blender low-poly crow. It walks, flies to a tree, perches, comes back down. Stand next
    to it, facing it, and press A (or click it): 「カァ。」, its voice, and it flies off.
  - Bench and cushion: press A (or click) in front of them to sit; press A again (or click
    ﾊﾙｶﾁｬﾝ or the seat) to stand up. As in the game, she does not walk while seated.
    2D uses the game's sitting sprite, 3D a sitting pose on the seat.
  - 3D, leaving the house: the camera first shows the house front with ﾊﾙｶﾁｬﾝ. The next
    move returns to the usual camera (a key still held from walking out has to be released
    once).
  - 3D: a tree between the camera and ﾊﾙｶﾁｬﾝ turns 40% see-through (0.25 s), like 2D.
  - 3D: the camera stands a little farther back than in Phase 3 (outdoor 3.4, indoor 3.6).
  - 3D crow: in flight its legs are drawn up under the body; for a landing they reach forward.
  - Phones are played in landscape. Held upright, the page shows
    「スマホを横向きにしてください」 and the game pauses (no walking, no A, no AUTO, the play
    time does not count; the world clock keeps running). Turning the phone back continues
    where it was, without reloading. On phones the game stays inside the safe area.
  The 3D ﾊﾙｶﾁｬﾝ is still the TEMPORARY blocky model; 2D uses her original sprites.

Controls (same as the game):
  WASD / arrow keys : move one cell (hold to keep walking; in 3D relative to the camera)
  Left click        : examine the clicked object or the crow (only when standing in front)
  Menu button       : AUTO / Life Log / ひょうじ (2D / 3D)
  Touch D-pad / A   : shown on touch devices or with ?touchControls=1

POC-only panel (?debug=1, below the Menu button; will NOT go into the real game):
  VIEW              : switch 2D pixel view <-> 3D third-person view
  POC options       : show / hide the rows below
  2D pixels/cell    : 16 / 24 / 32 low-resolution pixels per map cell
  2D camera pitch   : 50 / 60 / 90 / 40 degrees
  2D square ground  : keep each map cell square on screen (like the 2D tiles)
  3D keys           : B camera-relative (default) / A world-fixed (W = North)
  3D indoor cam     : closer / closer + keep the camera inside the room
  Time              : real 10-minute clock / force morning / force night
  Grid/blocked      : show the shared Grid and blocked cells (red X)
  Stats             : FPS, frame time, render texture size, draw calls when available,
                      AUTO, crow state, seat, 3D tree fades, exit camera
  3D cam follow     : turn when idle (default) / fixed / facing (Phase 1 behaviour)
  2D outline        : 1 px (default) / 2 px / OFF
  Outline color     : #252B39 blue-grey / #3A2A22 brown / #000000 black
  Grass outline     : OFF (default) / soft
  Inner lines       : soft (default) / OFF
  2D see-through trees : ON (default) / OFF
  3D indoor walls   : cut camera side (default) / rays only (Phase 1/2 behaviour)
  3D see-through trees : ON (default) / OFF (hide, the Phase 3 behaviour)
  3D exit camera    : house front (default) / behind (Phase 3 behaviour)
  3D cam distance   : 3.4/3.6 standard / 2.5/2.9 Phase 3 / 3.0/3.3 / 3.8/4.0 (outdoor/indoor)

The published game (ver2.9, still titled HALKA WORLD) and the ver3.0 Preview are not
changed by this build. Phase 1, 2, 2.1 and 3 previews stay in their own folders.
