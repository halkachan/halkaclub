// 最新動画を変更するときは、この1行のURLだけ書き換えてください。
const latestVideoUrl = "https://youtu.be/RFQw7HejNZ0";

function extractYouTubeId(url) {
  try {
    const parsed = new URL(url);
    const host = parsed.hostname.replace(/^www\./, "");

    if (host === "youtu.be") {
      return parsed.pathname.split("/").filter(Boolean)[0] || null;
    }

    if (host.endsWith("youtube.com")) {
      if (parsed.pathname.startsWith("/shorts/")) {
        return parsed.pathname.split("/")[2] || null;
      }
      return parsed.searchParams.get("v");
    }
  } catch (error) {
    return null;
  }

  return null;
}

document.addEventListener("DOMContentLoaded", () => {
  const videoId = extractYouTubeId(latestVideoUrl);
  if (!videoId) return;

  const link = document.getElementById("latest-video-link");
  const thumb = document.getElementById("latest-video-thumb");
  if (!link || !thumb) return;

  link.href = `https://www.youtube.com/watch?v=${videoId}`;

  thumb.onerror = () => {
    thumb.onerror = null;
    thumb.src = `https://img.youtube.com/vi/${videoId}/hqdefault.jpg`;
  };
  thumb.src = `https://img.youtube.com/vi/${videoId}/maxresdefault.jpg`;
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

  thumb.onerror = () => {
    thumb.onerror = null;
    thumb.src = `https://img.youtube.com/vi/${work.videoId}/hqdefault.jpg`;
  };
  thumb.src = `https://img.youtube.com/vi/${work.videoId}/maxresdefault.jpg`;

  section.hidden = false;
});
