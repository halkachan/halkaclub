// works/works.js
// works-data.js のデータから、カテゴリーをアコーディオン表示し、開いたときだけ作品カードを描画します。

const WORKS_PAGE_SIZE = 12;
let closeOpenWorksCategory = null;

function formatPublishedAt(publishedAt) {
  if (!publishedAt) return "";
  return publishedAt.replace(/-/g, ".");
}

function createWorkCard(work) {
  const videoId = extractYouTubeId(work.youtubeUrl);

  const card = document.createElement("a");
  card.className = "work-card";
  card.href = videoId ? `https://www.youtube.com/watch?v=${videoId}` : work.youtubeUrl;
  card.target = "_blank";
  card.rel = "noreferrer";
  card.setAttribute("aria-label", `${work.title} を再生`);

  const thumbWrap = document.createElement("span");
  thumbWrap.className = "work-thumb-wrap";

  const thumb = document.createElement("img");
  thumb.className = "work-thumb";
  thumb.alt = "";
  thumb.loading = "lazy";
  thumb.decoding = "async";

  if (videoId) {
    setYouTubeThumbnail(thumb, videoId);
  }

  thumbWrap.appendChild(thumb);
  card.appendChild(thumbWrap);

  const body = document.createElement("span");
  body.className = "work-body";

  const title = document.createElement("span");
  title.className = "work-title";
  title.textContent = work.title;
  body.appendChild(title);

  if (work.publishedAt) {
    const date = document.createElement("span");
    date.className = "work-date";
    date.textContent = formatPublishedAt(work.publishedAt);
    body.appendChild(date);
  }

  card.appendChild(body);

  return card;
}

function renderWorksCategory(category) {
  const works = category.works || [];
  const panelId = `${category.id}-panel`;

  const section = document.createElement("div");
  section.className = "works-category";
  section.id = category.id;

  const toggle = document.createElement("button");
  toggle.type = "button";
  toggle.className = "works-category-toggle";
  toggle.setAttribute("aria-expanded", "false");
  toggle.setAttribute("aria-controls", panelId);

  const info = document.createElement("span");
  info.className = "works-category-info";

  const name = document.createElement("strong");
  name.className = "works-category-name";
  name.id = `${category.id}-title`;
  name.textContent = category.name;
  info.appendChild(name);

  if (category.description) {
    const desc = document.createElement("small");
    desc.className = "works-category-desc";
    desc.textContent = category.description;
    info.appendChild(desc);
  }

  const count = document.createElement("span");
  count.className = "works-category-count";
  count.textContent = `${works.length}曲`;
  info.appendChild(count);

  const cta = document.createElement("span");
  cta.className = "works-category-cta";

  const ctaLabel = document.createElement("span");
  ctaLabel.textContent = "作品を見る";
  cta.appendChild(ctaLabel);

  const ctaArrow = document.createElement("span");
  ctaArrow.className = "works-category-arrow";
  ctaArrow.setAttribute("aria-hidden", "true");
  ctaArrow.textContent = "▶";
  cta.appendChild(ctaArrow);

  toggle.appendChild(info);
  toggle.appendChild(cta);

  const panel = document.createElement("div");
  panel.className = "works-category-panel";
  panel.id = panelId;
  panel.setAttribute("aria-labelledby", `${category.id}-title`);
  panel.hidden = true;

  let renderMore = null;

  if (works.length === 0) {
    const empty = document.createElement("p");
    empty.className = "works-empty";
    empty.textContent = "準備中";
    panel.appendChild(empty);
  } else {
    const grid = document.createElement("div");
    grid.className = "work-grid";
    panel.appendChild(grid);

    const moreWrap = document.createElement("div");
    moreWrap.className = "work-more-wrap";

    const moreButton = document.createElement("button");
    moreButton.type = "button";
    moreButton.className = "work-more-button";
    moreButton.textContent = "もっと見る";
    moreWrap.appendChild(moreButton);
    panel.appendChild(moreWrap);

    let renderedCount = 0;

    renderMore = () => {
      const next = works.slice(renderedCount, renderedCount + WORKS_PAGE_SIZE);
      next.forEach((work) => grid.appendChild(createWorkCard(work)));
      renderedCount += next.length;
      moreWrap.hidden = renderedCount >= works.length;
    };

    moreButton.addEventListener("click", renderMore);
  }

  let built = false;

  function openPanel() {
    if (!built) {
      built = true;
      if (renderMore) renderMore();
    }
    panel.hidden = false;
    toggle.setAttribute("aria-expanded", "true");
  }

  function closePanel() {
    panel.hidden = true;
    toggle.setAttribute("aria-expanded", "false");
  }

  toggle.addEventListener("click", () => {
    const isOpen = toggle.getAttribute("aria-expanded") === "true";
    if (isOpen) {
      closePanel();
      closeOpenWorksCategory = null;
      return;
    }
    if (closeOpenWorksCategory) closeOpenWorksCategory();
    openPanel();
    closeOpenWorksCategory = closePanel;
  });

  section.appendChild(toggle);
  section.appendChild(panel);
  return section;
}

document.addEventListener("DOMContentLoaded", () => {
  const container = document.getElementById("works-categories");
  if (!container || typeof worksData === "undefined") return;

  worksData.forEach((category) => {
    container.appendChild(renderWorksCategory(category));
  });
});
