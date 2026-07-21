// game/game.js：「ゲームを起動」ボタンが押された時だけiframeを生成します
(function () {
  const frame = document.getElementById("game-frame");
  const launchButton = document.getElementById("game-launch-button");
  const newTabButton = document.querySelector(".game-newtab-button");

  if (!frame || !launchButton || !newTabButton) return;

  const gameUrl = newTabButton.href;

  launchButton.addEventListener("click", () => {
    const iframe = document.createElement("iframe");
    iframe.src = gameUrl;
    iframe.title = "かがみねちゃれんじ";
    iframe.loading = "lazy";
    iframe.setAttribute("allow", "autoplay; fullscreen");
    iframe.setAttribute("allowfullscreen", "true");

    frame.replaceChildren(iframe);
  });
})();
