// はるかわーるど: the menu's ぜんがめん row. A browser enters full screen only inside a user's
// tap or click, and Unity handles input a frame later, so the page itself switches full screen
// on the tap: while the menu is open the game tells it where the row's button is (canvas pixels,
// origin top-left), and a pointerup inside that rectangle toggles full screen on the page.
// Inside an iframe the frame needs allow="fullscreen" (the game page has it).
mergeInto(LibraryManager.library, {
  HalkaFullscreenSetButton: function (x, y, w, h) {
    var state = window.__halkaFullscreen || (window.__halkaFullscreen = { rect: null, installed: false, toggles: 0 });
    state.rect = w > 0 && h > 0 ? [x, y, w, h] : null;
    if (state.installed) return;
    state.installed = true;
    var canvas = Module.canvas;
    function inside(e) {
      if (!state.rect || !canvas) return false;
      var box = canvas.getBoundingClientRect();
      if (box.width <= 0 || box.height <= 0) return false;
      var px = (e.clientX - box.left) * canvas.width / box.width;
      var py = (e.clientY - box.top) * canvas.height / box.height;
      var r = state.rect;
      return px >= r[0] && px <= r[0] + r[2] && py >= r[1] && py <= r[1] + r[3];
    }
    canvas.addEventListener("pointerup", function (e) {
      if (!inside(e)) return;
      var d = document;
      var full = d.fullscreenElement || d.webkitFullscreenElement;
      try {
        if (full) {
          var exit = d.exitFullscreen || d.webkitExitFullscreen;
          if (exit) { var p = exit.call(d); if (p && p.catch) p.catch(function () {}); }
        } else {
          var root = d.documentElement;
          var request = root.requestFullscreen || root.webkitRequestFullscreen;
          if (request) { var q = request.call(root); if (q && q.catch) q.catch(function () {}); }
        }
        state.toggles++;
      } catch (ignored) {}
    });
  },
  HalkaIsFullscreen: function () {
    return document.fullscreenElement || document.webkitFullscreenElement ? 1 : 0;
  }
});
