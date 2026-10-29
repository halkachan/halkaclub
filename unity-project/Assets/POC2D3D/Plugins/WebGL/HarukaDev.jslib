// はるかわーるど ver3.2, development only (PocDevStats, ?debug=1 or ?devstats=1): the game's World /
// chunk / frame numbers for the browser checks. window.__halkaDev is the latest sample, with the
// WebAssembly heap size added here (the whole Unity memory: not the JS heap); __halkaDevHistory
// keeps the last 4000 samples. window.__halkaDevCmd("borders:1") sends a dev command to the game.
mergeInto(LibraryManager.library, {
  HalkaDevPublish: function (textPtr) {
    var sample;
    try { sample = JSON.parse(UTF8ToString(textPtr)); } catch (e) { return; }
    try { sample.wasmHeapBytes = HEAPU8.buffer.byteLength; } catch (e) { sample.wasmHeapBytes = -1; }
    sample.pageTime = performance.now();
    window.__halkaDev = sample;
    var history = window.__halkaDevHistory || (window.__halkaDevHistory = []);
    history.push(sample);
    if (history.length > 4000) history.shift();
    if (!window.__halkaDevCmd) {
      window.__halkaDevCmd = function (command) {
        var name = document.__halkaDevTarget || "HARUKA WORLD ver3.7";
        if (typeof SendMessage === "function") SendMessage(name, "DevCommand", command);
        else if (typeof Module !== "undefined" && Module.SendMessage) Module.SendMessage(name, "DevCommand", command);
      };
    }
  }
});
