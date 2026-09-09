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
      iframe.setAttribute("allow", "autoplay; fullscreen; gamepad");
      iframe.setAttribute("allowfullscreen", "true");

      frame.replaceChildren(iframe);
    });
  });
})();
