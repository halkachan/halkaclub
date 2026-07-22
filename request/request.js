// request/request.js：メールの件名・本文の組み立てと、依頼テンプレートのコピー機能を担当します

const REQUEST_COPY_RESET_DELAY = 2500;
let requestCopyResetTimer = null;

function showRequestCopyStatus(statusEl, message) {
  statusEl.textContent = message;
  if (requestCopyResetTimer) {
    window.clearTimeout(requestCopyResetTimer);
  }
  requestCopyResetTimer = window.setTimeout(() => {
    statusEl.textContent = "";
    requestCopyResetTimer = null;
  }, REQUEST_COPY_RESET_DELAY);
}

function legacyCopyRequestTemplate(text) {
  const textarea = document.createElement("textarea");
  textarea.value = text;
  textarea.setAttribute("readonly", "");
  textarea.style.position = "fixed";
  textarea.style.top = "0";
  textarea.style.left = "-9999px";
  document.body.appendChild(textarea);
  textarea.select();
  textarea.setSelectionRange(0, text.length);

  let succeeded = false;
  try {
    succeeded = document.execCommand("copy");
  } catch (error) {
    succeeded = false;
  }

  document.body.removeChild(textarea);
  return succeeded;
}

function selectRequestTemplateForManualCopy(target) {
  const selection = window.getSelection();
  if (!selection) return;
  const range = document.createRange();
  range.selectNodeContents(target);
  selection.removeAllRanges();
  selection.addRange(range);
}

async function copyRequestTemplate(templateEl, statusEl) {
  const text = templateEl.textContent;

  if (navigator.clipboard && navigator.clipboard.writeText) {
    try {
      await navigator.clipboard.writeText(text);
      showRequestCopyStatus(statusEl, "コピーしました");
      return;
    } catch (error) {
      // Clipboard APIが使えない/拒否された場合は下のフォールバックへ進みます。
    }
  }

  if (legacyCopyRequestTemplate(text)) {
    showRequestCopyStatus(statusEl, "コピーしました");
    return;
  }

  selectRequestTemplateForManualCopy(templateEl);
  showRequestCopyStatus(statusEl, "テキストを選択しました。Ctrl+C（Macは⌘+C）でコピーしてください");
}

document.addEventListener("DOMContentLoaded", () => {
  const templateEl = document.getElementById("request-template-text");

  const mailButton = document.getElementById("request-mail-button");
  if (mailButton && templateEl) {
    const subject = "【ご依頼相談】";
    const body = templateEl.textContent;
    mailButton.href = `mailto:kanatahalka@gmail.com?subject=${encodeURIComponent(subject)}&body=${encodeURIComponent(body)}`;
  }

  const copyButton = document.getElementById("request-copy-button");
  const statusEl = document.getElementById("request-copy-status");
  if (copyButton && templateEl && statusEl) {
    copyButton.addEventListener("click", () => {
      copyRequestTemplate(templateEl, statusEl);
    });
  }
});
