// works/works.js
// works-data.js のデータから、目次と作品カードを自動生成します。

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
    thumb.onerror = () => {
      thumb.onerror = null;
      thumb.src = `https://img.youtube.com/vi/${videoId}/hqdefault.jpg`;
    };
    thumb.src = `https://img.youtube.com/vi/${videoId}/maxresdefault.jpg`;
  }

  thumbWrap.appendChild(thumb);

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

  card.appendChild(thumbWrap);
  card.appendChild(body);

  return card;
}

function renderWorksCategory(category) {
  const section = document.createElement("section");
  section.className = "works-category";
  section.id = category.id;
  section.setAttribute("aria-labelledby", `${category.id}-title`);

  const heading = document.createElement("h2");
  heading.id = `${category.id}-title`;
  heading.textContent = category.name;
  section.appendChild(heading);

  if (!category.works || category.works.length === 0) {
    const empty = document.createElement("p");
    empty.className = "works-empty";
    empty.textContent = "準備中";
    section.appendChild(empty);
    return section;
  }

  const grid = document.createElement("div");
  grid.className = "work-grid";

  category.works.forEach((work) => {
    grid.appendChild(createWorkCard(work));
  });

  section.appendChild(grid);
  return section;
}

document.addEventListener("DOMContentLoaded", () => {
  const toc = document.getElementById("works-toc");
  const container = document.getElementById("works-categories");
  if (!toc || !container || typeof worksData === "undefined") return;

  worksData.forEach((category) => {
    const link = document.createElement("a");
    link.href = `#${category.id}`;
    link.textContent = category.name;
    toc.appendChild(link);

    container.appendChild(renderWorksCategory(category));
  });
});
