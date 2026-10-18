// はるかわーるど Phase 6: the save system's browser side.
// - Export / Import: a browser opens a download or a file chooser only during a user's tap or
//   click, so the game tells this page where the menu's エクスポート / インポート buttons are
//   (canvas pixels, origin top-left) and what to export; the page acts on the pointerup that
//   lands there, inside the gesture. Import reads only the file the person picks (text, at most
//   maxBytes) and hands the text to the game; nothing in it is run.
// - Page hidden (tab switched, page closing): the game is told at once (SendMessage), so it
//   can write its save before the browser freezes the page. A closed tab may still lose the
//   last seconds: writing to IndexedDB finishes asynchronously.
// - The revision marker in localStorage (per page path) lets a second tab notice that another
//   tab of the same game has saved later.
mergeInto(LibraryManager.library, {
  HarukaSaveInstall: function (maxBytes) {
    var s = window.__harukaSave;
    if (s && s.installed) return;
    s = window.__harukaSave = {
      installed: true, maxBytes: maxBytes, exportRect: null, exportJson: null, exportPrefix: "",
      exportResult: 0, exportName: "", importRect: null, importState: 0, importText: null,
      hidden: 0, syncPending: false, syncAgain: false, syncOk: 0, syncFailed: 0
    };
    var notifyHidden = function () {
      s.hidden++;
      try {
        if (typeof SendMessage === "function") SendMessage("HarukaSaveSystem", "OnPageHidden", "");
        else if (typeof Module !== "undefined" && Module.SendMessage) Module.SendMessage("HarukaSaveSystem", "OnPageHidden", "");
      } catch (e) { /* the game saves on its next frame instead */ }
    };
    document.addEventListener("visibilitychange", function () {
      if (document.visibilityState === "hidden") notifyHidden();
    });
    window.addEventListener("pagehide", notifyHidden);
    var inside = function (rect, event) {
      if (!rect) return false;
      var canvas = document.querySelector("#unity-canvas") || document.querySelector("canvas");
      if (!canvas) return false;
      var box = canvas.getBoundingClientRect();
      if (box.width <= 0 || box.height <= 0) return false;
      var x = (event.clientX - box.left) * canvas.width / box.width;
      var y = (event.clientY - box.top) * canvas.height / box.height;
      return x >= rect[0] && y >= rect[1] && x <= rect[0] + rect[2] && y <= rect[1] + rect[3];
    };
    var two = function (n) { return (n < 10 ? "0" : "") + n; };
    var exportNow = function () {
      try {
        var now = new Date();
        var name = s.exportPrefix + now.getFullYear() + two(now.getMonth() + 1) + two(now.getDate()) + "_" +
          two(now.getHours()) + two(now.getMinutes()) + two(now.getSeconds()) + ".json";
        var blob = new Blob([s.exportJson], { type: "application/json;charset=utf-8" });
        var url = URL.createObjectURL(blob);
        var link = document.createElement("a");
        link.href = url;
        link.download = name;
        link.rel = "noopener";
        link.style.display = "none";
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        setTimeout(function () { URL.revokeObjectURL(url); }, 60000);
        s.exportName = name;
        s.exportResult = 1;
      } catch (e) {
        s.exportResult = 2;
      }
    };
    var importNow = function () {
      s.importState = 1;
      s.importText = null;
      var input = document.createElement("input");
      input.type = "file";
      input.accept = ".json,application/json,text/plain";
      input.style.position = "fixed";
      input.style.left = "-1000px";
      input.style.opacity = "0";
      var done = function () { if (input.parentNode) input.parentNode.removeChild(input); };
      input.addEventListener("cancel", function () { if (s.importState === 1) s.importState = 2; done(); });
      input.addEventListener("change", function () {
        var file = input.files && input.files[0];
        done();
        if (!file) { s.importState = 2; return; }
        if (file.size > s.maxBytes) { s.importState = 4; return; }
        var reader = new FileReader();
        reader.onload = function () { s.importText = String(reader.result || ""); s.importState = 3; };
        reader.onerror = function () { s.importState = 4; };
        reader.readAsText(file, "utf-8");
      });
      document.body.appendChild(input);
      try { input.click(); } catch (e) { s.importState = 4; done(); }
    };
    document.addEventListener("pointerup", function (event) {
      if (s.exportJson && inside(s.exportRect, event)) exportNow();
      else if (inside(s.importRect, event)) importNow();
    }, true);
  },

  HarukaSaveArmExport: function (json, prefix, x, y, w, h) {
    var s = window.__harukaSave;
    if (!s) return;
    if (w <= 0 || h <= 0) { s.exportRect = null; s.exportJson = null; return; }
    s.exportRect = [x, y, w, h];
    s.exportJson = UTF8ToString(json);
    s.exportPrefix = UTF8ToString(prefix);
  },

  HarukaSaveTakeExportResult: function () {
    var s = window.__harukaSave;
    if (!s) return 0;
    var result = s.exportResult;
    s.exportResult = 0;
    return result;
  },

  HarukaSaveArmImport: function (x, y, w, h) {
    var s = window.__harukaSave;
    if (!s) return;
    s.importRect = w > 0 && h > 0 ? [x, y, w, h] : null;
  },

  HarukaSaveImportState: function () {
    var s = window.__harukaSave;
    return s ? s.importState : 0;
  },

  HarukaSaveTakeImportText: function () {
    var s = window.__harukaSave;
    var text = s && s.importText !== null ? s.importText : "";
    if (s) { s.importText = null; s.importState = 0; }
    var size = lengthBytesUTF8(text) + 1;
    var buffer = _malloc(size);
    stringToUTF8(text, buffer, size);
    return buffer;
  },

  HarukaSaveResetImport: function () {
    var s = window.__harukaSave;
    if (s) { s.importState = 0; s.importText = null; }
  },

  HarukaSaveReadMarker: function () {
    try {
      var value = window.localStorage.getItem("harukaworld-save-revision:" + location.pathname);
      var n = value === null ? -1 : parseInt(value, 10);
      return isFinite(n) ? n : -1;
    } catch (e) { return -1; }
  },

  HarukaSaveWriteMarker: function (revision) {
    try { window.localStorage.setItem("harukaworld-save-revision:" + location.pathname, String(revision)); } catch (e) { }
  },

  HarukaSaveTimezoneOffsetMinutes: function () {
    return -new Date().getTimezoneOffset();
  },

  // Push the files written in persistentDataPath to IndexedDB now (one at a time; a request
  // during a sync runs once more after it).
  HarukaSaveSync: function () {
    var s = window.__harukaSave;
    if (!s || typeof FS === "undefined" || !FS.syncfs) return;
    if (s.syncPending) { s.syncAgain = true; return; }
    var run = function () {
      s.syncPending = true;
      s.syncAgain = false;
      FS.syncfs(false, function (error) {
        s.syncPending = false;
        if (error) s.syncFailed++; else s.syncOk++;
        if (s.syncAgain) run();
      });
    };
    run();
  }
});
