// 最新動画を変更するときは、この1行のURLだけ書き換えてください。
const latestVideoUrl = "https://youtu.be/RFQw7HejNZ0";

document.addEventListener("DOMContentLoaded", () => {
  const videoId = extractYouTubeId(latestVideoUrl);
  if (!videoId) return;

  const link = document.getElementById("latest-video-link");
  const thumb = document.getElementById("latest-video-thumb");
  if (!link || !thumb) return;

  link.href = `https://www.youtube.com/watch?v=${videoId}`;
  setYouTubeThumbnail(thumb, videoId);
});

// works/works-data.js の全カテゴリーから、ランダムで1本を選んで表示します。
const RANDOM_WORK_STORAGE_KEY = "halka-last-random-work-id";

function getAllWorksWithVideoId() {
  if (typeof worksData === "undefined") return [];

  const all = [];
  worksData.forEach((category) => {
    (category.works || []).forEach((work) => {
      const videoId = extractYouTubeId(work.youtubeUrl);
      if (videoId) all.push({ ...work, videoId });
    });
  });
  return all;
}

function pickRandomWork(works) {
  const lastId = sessionStorage.getItem(RANDOM_WORK_STORAGE_KEY);

  let pool = works;
  if (works.length > 1 && lastId) {
    const filtered = works.filter((work) => work.videoId !== lastId);
    if (filtered.length > 0) pool = filtered;
  }

  const choice = pool[Math.floor(Math.random() * pool.length)];
  sessionStorage.setItem(RANDOM_WORK_STORAGE_KEY, choice.videoId);
  return choice;
}

document.addEventListener("DOMContentLoaded", () => {
  const section = document.getElementById("random-work-section");
  const link = document.getElementById("random-work-link");
  const thumb = document.getElementById("random-work-thumb");
  const titleEl = document.getElementById("random-work-title");
  const dateEl = document.getElementById("random-work-date");
  if (!section || !link || !thumb || !titleEl || !dateEl) return;

  const works = getAllWorksWithVideoId();
  if (works.length === 0) return;

  const work = pickRandomWork(works);

  link.href = `https://www.youtube.com/watch?v=${work.videoId}`;
  link.setAttribute("aria-label", `${work.title} をYouTubeで見る`);
  titleEl.textContent = work.title;
  dateEl.textContent = work.publishedAt ? work.publishedAt.replace(/-/g, ".") : "";

  setYouTubeThumbnail(thumb, work.videoId);

  section.hidden = false;
});

// GIF(#halka-gif-button)をクリックすると、ランダムな吹き出しと星・音符・線の演出を表示します。
const HALKA_GIF_PHRASES = [
  "歌い手になりたい",
  "ボカロPになりたい",
  "歌い手とボカロPになりたい",
  "さわるなえっち！",
  "ﾋｶｷﾝになりたい",
];
const HALKA_GIF_FX_GLYPHS = ["★", "♪", "〜"];

function halkaGifPrefersReducedMotion() {
  return window.matchMedia("(prefers-reduced-motion: reduce)").matches;
}

let halkaGifLastPhraseIndex = -1;

function pickHalkaGifPhrase() {
  let index = Math.floor(Math.random() * HALKA_GIF_PHRASES.length);
  while (HALKA_GIF_PHRASES.length > 1 && index === halkaGifLastPhraseIndex) {
    index = Math.floor(Math.random() * HALKA_GIF_PHRASES.length);
  }
  halkaGifLastPhraseIndex = index;
  return HALKA_GIF_PHRASES[index];
}

function showHalkaGifBubble(stage, text) {
  const existing = stage.querySelector(".halka-gif-bubble");
  if (existing) existing.remove();

  const bubble = document.createElement("div");
  bubble.className = "halka-gif-bubble";
  bubble.textContent = text;
  stage.appendChild(bubble);

  window.setTimeout(() => {
    if (bubble.isConnected) bubble.remove();
  }, 2600);
}

function spawnHalkaGifFx(stage) {
  if (halkaGifPrefersReducedMotion()) return;

  const count = 4;
  for (let i = 0; i < count; i += 1) {
    const fx = document.createElement("span");
    fx.className = "halka-gif-fx";
    fx.setAttribute("aria-hidden", "true");
    fx.textContent = HALKA_GIF_FX_GLYPHS[i % HALKA_GIF_FX_GLYPHS.length];

    const angle = (Math.PI * 2 * i) / count + (Math.random() - 0.5);
    const distance = 50 + Math.random() * 30;
    fx.style.setProperty("--fx-x", `${Math.cos(angle) * distance}px`);
    fx.style.setProperty("--fx-y", `${Math.sin(angle) * distance}px`);
    fx.style.setProperty("--fx-rot", `${(Math.random() - 0.5) * 60}deg`);

    fx.addEventListener("animationend", () => fx.remove());
    stage.appendChild(fx);
  }
}

document.addEventListener("DOMContentLoaded", () => {
  const button = document.getElementById("halka-gif-button");
  const stage = document.querySelector(".halka-gif-stage");
  if (!button || !stage) return;

  button.addEventListener("click", () => {
    showHalkaGifBubble(stage, pickHalkaGifPhrase());
    spawnHalkaGifFx(stage);

    if (!halkaGifPrefersReducedMotion()) {
      button.classList.remove("is-clicked");
      void button.offsetWidth;
      button.classList.add("is-clicked");
    }
  });
});
