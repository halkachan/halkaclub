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

// GIF(#halka-gif-button)：ドラッグ・クリックでの吹き出し・低確率の増殖・連打イベントを扱います。
const HALKA_GIF_PHRASES = [
  "歌い手になりたい",
  "ボカロPになりたい",
  "歌い手とボカロPになりたい",
  "さわるなえっち！",
  "ﾋｶｷﾝになりたい",
];
const HALKA_GIF_MULTIPLY_PHRASES = ["ふえた", "どーも", "いっぱい", "うお"];
const HALKA_GIF_COMBO_PHRASES = { 3: "なーに", 5: "おこだよ", 8: "さわるなー！" };
const HALKA_GIF_FX_GLYPHS = ["★", "♪", "〜"];

const HALKA_GIF_DRAG_THRESHOLD = 8;
const HALKA_GIF_VISIBLE_MARGIN = 24;
const HALKA_GIF_COMBO_WINDOW_MS = 3000;
const HALKA_GIF_COMBO_COOLDOWN_MS = 5000;
const HALKA_GIF_MULTIPLY_CHANCE = 0.2;
const HALKA_GIF_MAX_CLONES = 10;
const HALKA_GIF_SRC = "assets/profile/halgif1.gif";

function halkaGifPrefersReducedMotion() {
  return window.matchMedia("(prefers-reduced-motion: reduce)").matches;
}

function makeHalkaGifNoRepeatPicker(pool) {
  let lastIndex = -1;
  return function pick() {
    let index = Math.floor(Math.random() * pool.length);
    while (pool.length > 1 && index === lastIndex) {
      index = Math.floor(Math.random() * pool.length);
    }
    lastIndex = index;
    return pool[index];
  };
}

const pickHalkaGifPhrase = makeHalkaGifNoRepeatPicker(HALKA_GIF_PHRASES);
const pickHalkaGifMultiplyPhrase = makeHalkaGifNoRepeatPicker(HALKA_GIF_MULTIPLY_PHRASES);

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

function triggerHalkaGifTapFeedback(button) {
  if (halkaGifPrefersReducedMotion()) return;
  button.classList.remove("is-clicked");
  void button.offsetWidth;
  button.classList.add("is-clicked");
}

// 低確率で増殖：分身はクリック・ドラッグ・フォーカスの対象にならず、約3秒後に自動で消えます。
let halkaGifMultiplyRunning = false;

function runHalkaGifMultiply(cloneLayer) {
  if (!cloneLayer) return;
  halkaGifMultiplyRunning = true;

  const reduceMotion = halkaGifPrefersReducedMotion();
  const count = Math.min(5 + Math.floor(Math.random() * 6), HALKA_GIF_MAX_CLONES);
  const clones = [];

  for (let i = 0; i < count; i += 1) {
    const clone = document.createElement("img");
    clone.src = HALKA_GIF_SRC;
    clone.alt = "";
    clone.setAttribute("aria-hidden", "true");
    clone.className = "halka-gif-clone";

    const size = 48 + Math.floor(Math.random() * 24);
    clone.style.width = `${size}px`;
    clone.style.height = `${size}px`;
    clone.style.left = `${Math.random() * Math.max(0, window.innerWidth - size)}px`;
    clone.style.top = `${Math.random() * Math.max(0, window.innerHeight - size)}px`;

    if (!reduceMotion) {
      const angle = Math.random() * Math.PI * 2;
      const speed = 40 + Math.random() * 60;
      clone.style.setProperty("--clone-x", `${Math.cos(angle) * speed}px`);
      clone.style.setProperty("--clone-y", `${Math.sin(angle) * speed}px`);
      clone.style.animationDuration = `${2 + Math.random() * 2}s`;
      clone.classList.add("is-flying");
    }

    cloneLayer.appendChild(clone);
    clones.push(clone);
  }

  const lifespan = reduceMotion ? 1400 : 3000;

  window.setTimeout(() => {
    let remaining = clones.length;
    if (remaining === 0) {
      halkaGifMultiplyRunning = false;
      return;
    }

    clones.forEach((clone) => {
      let done = false;
      const cleanup = () => {
        if (done) return;
        done = true;
        if (clone.isConnected) clone.remove();
        remaining -= 1;
        if (remaining <= 0) halkaGifMultiplyRunning = false;
      };
      clone.addEventListener("transitionend", cleanup, { once: true });
      window.setTimeout(cleanup, 500);
      clone.classList.add("is-fading");
    });
  }, lifespan);
}

// 連打イベント：直近3秒間のクリック数が3・5・8回に達すると反応します。
let halkaGifComboEventRunning = false;
let halkaGifComboCooldownUntil = 0;
let halkaGifClickTimestamps = [];
let halkaGifLastClickAt = 0;

function runHalkaGifComboEscape(button) {
  halkaGifComboEventRunning = true;

  function finish() {
    button.classList.remove("is-combo-shake", "is-combo-flee");
    button.style.transition = "";
    button.style.transform = "";
    halkaGifComboEventRunning = false;
    halkaGifComboCooldownUntil = Date.now() + HALKA_GIF_COMBO_COOLDOWN_MS;
  }

  if (halkaGifPrefersReducedMotion()) {
    window.setTimeout(finish, 0);
    return;
  }

  button.classList.add("is-combo-shake");

  window.setTimeout(() => {
    button.classList.remove("is-combo-shake");

    const fleeX = (Math.random() < 0.5 ? -1 : 1) * (window.innerWidth / 2 - 60);
    const fleeY = (Math.random() < 0.5 ? -1 : 1) * (window.innerHeight / 2 - 60);
    button.style.setProperty("--combo-flee-x", `${fleeX}px`);
    button.style.setProperty("--combo-flee-y", `${fleeY}px`);
    button.classList.add("is-combo-flee");

    window.setTimeout(() => {
      button.classList.remove("is-combo-flee");
      button.style.transition = "transform 480ms ease";
      button.style.transform = "";
      window.setTimeout(finish, 480);
    }, 1150);
  }, 350);
}

function handleHalkaGifNormalClick(button, stage, cloneLayer) {
  const now = Date.now();

  const hadRecentClickBefore = now - halkaGifLastClickAt < HALKA_GIF_COMBO_WINDOW_MS;
  halkaGifLastClickAt = now;

  const inCooldown = now < halkaGifComboCooldownUntil;
  let comboCount = 0;

  if (!inCooldown && !halkaGifComboEventRunning) {
    halkaGifClickTimestamps.push(now);
    halkaGifClickTimestamps = halkaGifClickTimestamps.filter((t) => now - t < HALKA_GIF_COMBO_WINDOW_MS);
    comboCount = halkaGifClickTimestamps.length;
  } else {
    halkaGifClickTimestamps = [];
  }

  const comboPhrase = HALKA_GIF_COMBO_PHRASES[comboCount];

  if (comboPhrase) {
    showHalkaGifBubble(stage, comboPhrase);
    if (comboCount === 8) {
      halkaGifClickTimestamps = [];
      runHalkaGifComboEscape(button);
    } else {
      triggerHalkaGifTapFeedback(button);
    }
    return;
  }

  if (!halkaGifMultiplyRunning && !hadRecentClickBefore && Math.random() < HALKA_GIF_MULTIPLY_CHANCE) {
    showHalkaGifBubble(stage, pickHalkaGifMultiplyPhrase());
    triggerHalkaGifTapFeedback(button);
    runHalkaGifMultiply(cloneLayer);
    return;
  }

  showHalkaGifBubble(stage, pickHalkaGifPhrase());
  spawnHalkaGifFx(stage);
  triggerHalkaGifTapFeedback(button);
}

// つまめるﾊﾙｶﾁｬﾝ：Pointer Eventsでマウス・タッチを共通処理し、8pxを超えたらドラッグとして扱います。
function initHalkaGifDrag(button, stage, cloneLayer) {
  let pointerId = null;
  let startX = 0;
  let startY = 0;
  let originRect = null;
  let isDragging = false;
  let suppressNextClick = false;

  function markPointerHandled() {
    suppressNextClick = true;
    window.setTimeout(() => {
      suppressNextClick = false;
    }, 0);
  }

  function clamp(value, min, max) {
    return Math.max(min, Math.min(max, value));
  }

  function onPointerMove(event) {
    if (event.pointerId !== pointerId) return;
    const dx = event.clientX - startX;
    const dy = event.clientY - startY;

    if (!isDragging && Math.hypot(dx, dy) > HALKA_GIF_DRAG_THRESHOLD) {
      isDragging = true;
      button.classList.add("is-dragging");
      button.style.transition = "none";
    }

    if (isDragging) {
      event.preventDefault();

      const minDX = HALKA_GIF_VISIBLE_MARGIN - originRect.width - originRect.left;
      const maxDX = window.innerWidth - HALKA_GIF_VISIBLE_MARGIN - originRect.left;
      const minDY = HALKA_GIF_VISIBLE_MARGIN - originRect.height - originRect.top;
      const maxDY = window.innerHeight - HALKA_GIF_VISIBLE_MARGIN - originRect.top;

      const clampedX = clamp(dx, minDX, maxDX);
      const clampedY = clamp(dy, minDY, maxDY);

      button.style.transform = `translate(${clampedX}px, ${clampedY}px) scale(1.08)`;
    }
  }

  function endPointerSession() {
    button.removeEventListener("pointermove", onPointerMove);
    button.removeEventListener("pointerup", onPointerUp);
    button.removeEventListener("pointercancel", onPointerCancel);
    if (pointerId !== null) {
      try {
        button.releasePointerCapture(pointerId);
      } catch (error) {
        // すでに解放済みの場合は無視します。
      }
    }
    pointerId = null;
  }

  function snapBack() {
    button.classList.remove("is-dragging");
    if (halkaGifPrefersReducedMotion()) {
      button.style.transition = "none";
      button.style.transform = "";
    } else {
      button.style.transition = "transform 480ms cubic-bezier(0.34, 1.56, 0.64, 1)";
      button.style.transform = "";
    }
  }

  function onPointerUp(event) {
    if (event.pointerId !== pointerId) return;
    const wasDragging = isDragging;
    endPointerSession();
    isDragging = false;

    if (wasDragging) {
      snapBack();
      markPointerHandled();
    } else {
      handleHalkaGifNormalClick(button, stage, cloneLayer);
      markPointerHandled();
    }
  }

  function onPointerCancel(event) {
    if (event.pointerId !== pointerId) return;
    const wasDragging = isDragging;
    endPointerSession();
    isDragging = false;
    if (wasDragging) snapBack();
    markPointerHandled();
  }

  button.addEventListener("pointerdown", (event) => {
    if (pointerId !== null) return;
    if (event.pointerType === "mouse" && event.button !== 0) return;

    pointerId = event.pointerId;
    startX = event.clientX;
    startY = event.clientY;
    originRect = button.getBoundingClientRect();
    isDragging = false;

    button.setPointerCapture(pointerId);
    button.addEventListener("pointermove", onPointerMove);
    button.addEventListener("pointerup", onPointerUp);
    button.addEventListener("pointercancel", onPointerCancel);
  });

  // キーボード操作(Enter/Space)はpointerイベントを発生させないため、clickで拾います。
  // ポインター操作後に自動発火するclickは、上のpointerup処理で二重に反応しないよう抑制します。
  button.addEventListener("click", () => {
    if (suppressNextClick) {
      suppressNextClick = false;
      return;
    }
    handleHalkaGifNormalClick(button, stage, cloneLayer);
  });
}

document.addEventListener("DOMContentLoaded", () => {
  const button = document.getElementById("halka-gif-button");
  const stage = document.querySelector(".halka-gif-stage");
  const cloneLayer = document.getElementById("halka-gif-clone-layer");
  if (!button || !stage) return;

  initHalkaGifDrag(button, stage, cloneLayer);
});
