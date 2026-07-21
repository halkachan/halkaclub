// youtube-thumbnail.js
// YouTube動画IDの抽出とサムネイル表示を、TOPページ・作品一覧ページで共通利用します。

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

// maxresdefault非対応の動画では、YouTubeが小さな代替画像(120px程度)を200 OKで返すため、
// onerrorだけでなくnaturalWidthでも失敗判定します。
const YOUTUBE_THUMBNAIL_QUALITIES = ["maxresdefault", "hqdefault", "mqdefault"];
const YOUTUBE_THUMBNAIL_MIN_WIDTH = 300;

function setYouTubeThumbnail(imgElement, videoId) {
  let qualityIndex = 0;

  function showFallback() {
    imgElement.onload = null;
    imgElement.onerror = null;
    imgElement.style.display = "none";

    const wrap = imgElement.parentElement;
    if (wrap && !wrap.querySelector(".youtube-thumbnail-fallback")) {
      const fallback = document.createElement("span");
      fallback.className = "youtube-thumbnail-fallback";
      fallback.textContent = "NO IMAGE";
      wrap.appendChild(fallback);
    }
  }

  function tryNext() {
    if (qualityIndex >= YOUTUBE_THUMBNAIL_QUALITIES.length) {
      showFallback();
      return;
    }

    const quality = YOUTUBE_THUMBNAIL_QUALITIES[qualityIndex];
    qualityIndex += 1;

    imgElement.onerror = tryNext;
    imgElement.onload = () => {
      if (imgElement.naturalWidth > 0 && imgElement.naturalWidth < YOUTUBE_THUMBNAIL_MIN_WIDTH) {
        tryNext();
      }
    };
    imgElement.src = `https://img.youtube.com/vi/${videoId}/${quality}.jpg`;
  }

  tryNext();
}
