// 各ゲームは「ゲームを起動」が押された時だけiframeを生成します。
(function () {
  document.querySelectorAll("[data-game-frame]").forEach((frame) => {
    const launchButton = frame.querySelector(".game-launch-button");
    if (!launchButton) return;

    launchButton.addEventListener("click", () => {
      const iframe = document.createElement("iframe");
      iframe.src = launchButton.dataset.gameUrl;
      iframe.title = launchButton.dataset.gameTitle;
      iframe.loading = "eager";
      // web-share はスマホでスクショを共有シートへ渡すのに要る。
      // 付けないと navigator.share が iframe の中で使えず、
      // 画像を添えた投稿ができない（PCは保存＋投稿画面なので影響しない）。
      iframe.setAttribute("allow", "autoplay; fullscreen; gamepad; web-share");
      iframe.setAttribute("allowfullscreen", "true");

      frame.replaceChildren(iframe);
    });
  });
})();
