// みねはうす: browser check panel (development use). Loaded only when the page address has ?debug=1.
// Shows what this browser reports about itself, the game canvas, frame rate, input and the game's own state
// (time of day, what Rin and Len are doing, hugs), and errors. Nothing is sent anywhere and nothing about
// the player or the device is stored; "コピー" puts the text on the clipboard for pasting it by hand.
(function () {
  "use strict";

  var started = performance.now();
  var game = null, gameAt = 0;
  var errors = [];
  var input = { type: "なし", event: "なし", buttons: 0, pointers: 0, touches: 0, at: 0 };
  var returns = 0, frames = 0, fps = null, lastFpsAt = performance.now();

  function stamp() { return ((performance.now() - started) / 1000).toFixed(1) + "s"; }

  function addError(message) {
    errors.push(stamp() + " " + String(message).slice(0, 300));
    if (errors.length > 8) errors.shift();
  }

  window.mineHouseDiag = {
    // the game's last report (for automated checks of the page)
    last: function () { return game; },
    report: function (json) {
      try { game = JSON.parse(json); gameAt = performance.now(); }
      catch (e) { addError("diag: " + e.message); }
    },
    error: addError,
  };

  window.addEventListener("error", function (e) {
    var where = e.filename ? " @" + e.filename.split("/").pop() + ":" + e.lineno : "";
    addError((e.message || "error") + where);
  });
  window.addEventListener("unhandledrejection", function (e) {
    var r = e.reason;
    addError("promise: " + (r && r.message ? r.message : r));
  });

  // --- what the browser says about itself (unknown stays unknown) ---
  function browser() {
    var ua = navigator.userAgent, m;
    var rules = [
      [/EdgA?\/([\d.]+)/, "Edge"], [/EdgiOS\/([\d.]+)/, "Edge (iOS)"], [/OPR\/([\d.]+)/, "Opera"],
      [/SamsungBrowser\/([\d.]+)/, "Samsung Internet"], [/CriOS\/([\d.]+)/, "Chrome (iOS)"],
      [/FxiOS\/([\d.]+)/, "Firefox (iOS)"], [/Firefox\/([\d.]+)/, "Firefox"], [/Chrome\/([\d.]+)/, "Chrome"],
      [/Version\/([\d.]+).*Safari/, "Safari"],
    ];
    for (var i = 0; i < rules.length; i++)
      if ((m = ua.match(rules[i][0]))) return rules[i][1] + " " + m[1];
    return "不明";
  }

  function os() {
    var ua = navigator.userAgent, m;
    if ((m = ua.match(/iPhone OS ([\d_]+)/))) return "iOS " + m[1].replace(/_/g, ".");
    if ((m = ua.match(/iPad.*OS ([\d_]+)/))) return "iPadOS " + m[1].replace(/_/g, ".");
    if ((m = ua.match(/Android ([\d.]+)/))) return "Android " + m[1];
    if (/Windows NT 10\.0/.test(ua)) return "Windows 10 以降（10 と 11 は区別できません）";
    if ((m = ua.match(/Windows NT ([\d.]+)/))) return "Windows NT " + m[1];
    if (/Macintosh/.test(ua)) return navigator.maxTouchPoints > 1 ? "macOS または iPadOS（デスクトップ表示）" : "macOS";
    if (/CrOS/.test(ua)) return "ChromeOS";
    if (/Linux/.test(ua)) return "Linux";
    return "不明";
  }

  function webgl() {
    try {
      var c = document.createElement("canvas");
      if (c.getContext("webgl2")) return "WebGL 2 使用可";
      if (c.getContext("webgl") || c.getContext("experimental-webgl")) return "WebGL 1 のみ";
      return "使用不可";
    } catch (e) { return "使用不可 (" + e.message + ")"; }
  }
  var webglText = webgl();

  // --- input (listened to only while this panel is on; nothing is recorded) ---
  var activePointers = {};
  function onPointer(e) {
    input.type = e.pointerType || "不明";
    input.event = e.type;
    input.buttons = e.buttons;
    input.at = performance.now();
    if (e.type === "pointerdown") activePointers[e.pointerId] = true;
    if (e.type === "pointerup" || e.type === "pointercancel") delete activePointers[e.pointerId];
    input.pointers = Object.keys(activePointers).length;
  }
  ["pointerdown", "pointermove", "pointerup", "pointercancel"].forEach(function (t) {
    window.addEventListener(t, onPointer, { capture: true, passive: true });
  });
  ["touchstart", "touchmove", "touchend", "touchcancel"].forEach(function (t) {
    window.addEventListener(t, function (e) { input.touches = e.touches ? e.touches.length : 0; }, { capture: true, passive: true });
  });
  document.addEventListener("visibilitychange", function () {
    if (document.visibilityState === "visible") returns++;
  });

  function tick(now) {
    frames++;
    if (now - lastFpsAt >= 1000) {
      fps = Math.round(frames * 1000 / (now - lastFpsAt));
      frames = 0;
      lastFpsAt = now;
    }
    requestAnimationFrame(tick);
  }
  requestAnimationFrame(tick);

  // --- the panel ---
  var PHASES = { Morning: "朝", Day: "昼", Evening: "夕", Night: "夜", LateNight: "深夜" };
  var STATES = { idle: "待機", walk: "歩く", sit: "座る", lieDown: "寝転ぶ", hug: "ハグ", grabbed: "つままれ", falling: "落下" };

  function text() {
    var canvas = document.getElementById("game-canvas");
    var lines = [
      "みねはうす 診断 (?debug=1)",
      "ブラウザ: " + browser(),
      "OS: " + os(),
      "UA: " + navigator.userAgent,
      "画面: " + screen.width + "×" + screen.height + " / 表示領域 " + innerWidth + "×" + innerHeight +
        " / 倍率 " + (window.devicePixelRatio || 1) +
        " / 向き " + (screen.orientation && screen.orientation.type ? screen.orientation.type :
          (matchMedia("(orientation: portrait)").matches ? "縦" : "横")),
      "Canvas: " + (canvas ? canvas.clientWidth + "×" + canvas.clientHeight + " (CSS) / " +
        canvas.width + "×" + canvas.height + " (描画)" : "なし"),
      "FPS: " + (fps === null ? "計測中" : fps),
      "WebGL: " + webglText,
      "入力: " + input.type + " / " + input.event + " / ボタン " + input.buttons + " / ポインター " +
        input.pointers + " / タッチ " + input.touches +
        (input.at ? " (" + ((performance.now() - input.at) / 1000).toFixed(1) + "秒前)" : ""),
      "表示: " + document.visibilityState + " / 復帰 " + returns + " 回" +
        (document.fullscreenElement ? " / 全画面" : ""),
    ];
    if (game) {
      lines.push("時間帯: " + (PHASES[game.phase] || game.phase || "不明") +
        " (ゲーム内 " + game.gameSeconds + "秒, " + ((performance.now() - gameAt) / 1000).toFixed(1) + "秒前の値)");
      (game.residents || []).forEach(function (r) {
        lines.push(r.name + ": " + (STATES[r.state] || r.state) + " x=" + r.x + (r.facing > 0 ? " →" : " ←"));
      });
      if (game.hugs !== undefined)
        lines.push("ハグ: " + game.hugs + " 回 (抽選 " + game.hugDraws + " 回)" + (game.hugging ? " / ハグ中" : "") +
          (game.hugCooldown > 0 ? " / 待ち " + game.hugCooldown + "秒" : ""));
      if (game.furniture)
        lines.push("家具: 置いている " + game.furniture.placed + " / 持っている " + game.furniture.owned +
          (game.furniture.editing ? " / 模様替え中" : "") +
          " / 保存 " + (!game.furniture.canSave ? "できない" : game.furniture.saveFailed ? "失敗" : "OK") +
          " (" + game.furniture.saves + " 回, 読み込み " + game.furniture.loaded + ")");
    } else {
      lines.push("ゲーム: " + (window.mineHouse ? "起動済み・状態待ち" : "読み込み中"));
    }
    lines.push("エラー: " + (errors.length ? "" : "なし"));
    errors.forEach(function (e) { lines.push("  " + e); });
    return lines.join("\n");
  }

  function build() {
    var panel = document.createElement("section");
    panel.id = "diag";
    panel.setAttribute("aria-label", "診断情報");
    panel.innerHTML =
      '<div id="diag-bar"><button type="button" id="diag-toggle" aria-expanded="true">診断 ▾</button>' +
      '<button type="button" id="diag-copy">コピー</button><span id="diag-note" aria-live="polite"></span></div>' +
      '<pre id="diag-text"></pre>';
    document.body.appendChild(panel);
    var pre = document.getElementById("diag-text");
    var toggle = document.getElementById("diag-toggle");
    var note = document.getElementById("diag-note");
    toggle.addEventListener("click", function () {
      var open = pre.style.display !== "none";
      pre.style.display = open ? "none" : "block";
      toggle.textContent = open ? "診断 ▸" : "診断 ▾";
      toggle.setAttribute("aria-expanded", open ? "false" : "true");
    });
    document.getElementById("diag-copy").addEventListener("click", function () {
      var t = text();
      function fallback() {
        var area = document.createElement("textarea");
        area.value = t;
        area.setAttribute("readonly", "");
        area.style.position = "fixed";
        area.style.opacity = "0";
        document.body.appendChild(area);
        area.select();
        var ok = false;
        try { ok = document.execCommand("copy"); } catch (e) {}
        document.body.removeChild(area);
        note.textContent = ok ? "コピーしました" : "コピーできませんでした（表示を長押しで選択してください）";
      }
      if (navigator.clipboard && navigator.clipboard.writeText)
        navigator.clipboard.writeText(t).then(function () { note.textContent = "コピーしました"; }, fallback);
      else
        fallback();
    });
    setInterval(function () { pre.textContent = text(); }, 500);
    pre.textContent = text();
  }

  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", build);
  else build();
})();
