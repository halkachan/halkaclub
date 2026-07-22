// harukaijiri/harukaijiri.js：はるかいじり（HARUKA IJIRI）クリッカーゲーム本体
// 構成：HarukaijiriConfig（設定）→ 状態操作 → 保存/読込 → 描画 → イベント配線/初期化

// ============================================================
// HarukaijiriConfig：価格・確率・しきい値・文言など、調整したい値はここにまとめます
// ============================================================
const HarukaijiriConfig = {
  storageKey: "halka-harukaijiri-save-v1",
  gifSrc: "../assets/profile/halgif1.gif",

  autosaveIntervalMs: 5000,
  tickIntervalMs: 250,
  offlineCapMs: 8 * 60 * 60 * 1000, // 放置報酬の上限：8時間

  dragThreshold: 8, // これ以上動いたらドラッグ/スクロール扱い（クリックとして数えない）

  comboWindowMs: 3000,
  comboThreshold: 8,
  comboCooldownMs: 5000,
  comboMessage: "さわりすぎ！",

  reactionThresholds: [
    { clicks: 1, text: "……" },
    { clicks: 10, text: "なに" },
    { clicks: 50, text: "まださわるの" },
    { clicks: 100, text: "観察されてる" },
    { clicks: 500, text: "慣れてきたかも" },
    { clicks: 1000, text: "もう戻れないよ" },
  ],

  // 増殖イベント：確率・増加数・クールダウンは後から調整してください
  multiplyChance: 0.03, // クリックごとにこの確率で判定（クールダウン中は判定しない）
  multiplyMinAdd: 4,
  multiplyMaxAdd: 6,
  multiplyCooldownMs: 15000,

  characterAutoRate: 1, // 追加された(本体以外の)ﾊﾙｶﾁｬﾝ1体につき毎秒+いくつか
  zoneSize: 50, // 1区画あたりの最大表示数

  facilities: [
    { id: "poke", name: "つっつき棒", type: "click", value: 1, basePrice: 25, priceMultiplier: 1.55, effectLabel: "クリック獲得量 +1" },
    { id: "hammer", name: "ハンマー", type: "auto", value: 1, basePrice: 50, priceMultiplier: 1.6, effectLabel: "毎秒 +1" },
    { id: "imomushi", name: "いもむし", type: "auto", value: 5, basePrice: 250, priceMultiplier: 1.7, effectLabel: "毎秒 +5" },
    { id: "subscribe", name: "チャンネル登録", type: "auto", value: 15, basePrice: 1000, priceMultiplier: 1.8, effectLabel: "毎秒 +15" },
  ],

  achievements: [
    { id: "first-touch", no: 1, label: "初めていじった", check: (s) => s.totalClicks >= 1 },
    { id: "click-100", no: 2, label: "100回いじった", check: (s) => s.totalClicks >= 100 },
    { id: "first-tool", no: 3, label: "初めていじり道具を導入した", check: (s) => totalFacilitiesOwned(s) >= 1 },
    { id: "earned-1000", no: 4, label: "累計いじり値1,000", check: (s) => s.totalEarned >= 1000 },
    { id: "first-multiply", no: 5, label: "初めて増殖した", check: (s) => s.multiplyEventCount >= 1 },
    { id: "characters-10", no: 6, label: "ﾊﾙｶﾁｬﾝが10体になった", check: (s) => s.characterCount >= 10 },
    { id: "characters-50", no: 7, label: "ﾊﾙｶﾁｬﾝが50体になった", check: (s) => s.characterCount >= 50 },
    { id: "zone-2", no: 8, label: "第2区画が解放された", check: (s) => s.highestUnlockedZone >= 2 },
    { id: "characters-100", no: 9, label: "ﾊﾙｶﾁｬﾝが100体になった", check: (s) => s.characterCount >= 100 },
    { id: "mystery", no: 10, label: "？？？？？？？？", check: () => false },
  ],
};

// ============================================================
// 状態：ゲーム状態そのものと、状態を変更する関数群
// ============================================================
function createDefaultState() {
  const owned = {};
  HarukaijiriConfig.facilities.forEach((facility) => {
    owned[facility.id] = 0;
  });

  return {
    currentValue: 0,
    totalEarned: 0,
    totalClicks: 0,
    owned,
    unlockedAchievements: [],
    characterCount: 1,
    currentZone: 1,
    highestUnlockedZone: 1,
    multiplyEventCount: 0,
    lastSavedAt: Date.now(),
  };
}

function getClickPower(state) {
  const poke = HarukaijiriConfig.facilities.find((f) => f.id === "poke");
  return 1 + state.owned[poke.id] * poke.value;
}

function getAutoPerSecond(state) {
  const facilityAuto = HarukaijiriConfig.facilities
    .filter((f) => f.type === "auto")
    .reduce((sum, f) => sum + state.owned[f.id] * f.value, 0);
  const characterAuto = Math.max(0, state.characterCount - 1) * HarukaijiriConfig.characterAutoRate;
  return facilityAuto + characterAuto;
}

function getFacilityPrice(facility, owned) {
  return Math.round(facility.basePrice * Math.pow(facility.priceMultiplier, owned));
}

function totalFacilitiesOwned(state) {
  return Object.values(state.owned).reduce((sum, n) => sum + n, 0);
}

function getZoneCount(state) {
  return Math.max(1, Math.ceil(state.characterCount / HarukaijiriConfig.zoneSize));
}

// 区画に属する「本体以外の」ﾊﾙｶﾁｬﾝの通し番号の範囲（本体=1番、それ以外=2番以降）を返します。
function getZoneRange(zone) {
  const start = (zone - 1) * HarukaijiriConfig.zoneSize + 1;
  const end = Math.min(zone * HarukaijiriConfig.zoneSize, Number.MAX_SAFE_INTEGER);
  return { start: Math.max(start, 2), end };
}

function addValue(state, amount) {
  state.currentValue += amount;
  state.totalEarned += amount;
}

function buyFacility(state, facilityId) {
  const facility = HarukaijiriConfig.facilities.find((f) => f.id === facilityId);
  if (!facility) return false;

  const price = getFacilityPrice(facility, state.owned[facilityId]);
  if (state.currentValue < price) return false;

  state.currentValue -= price;
  state.owned[facilityId] += 1;
  return true;
}

// ============================================================
// 保存/読込
// ============================================================
function isFiniteNonNegative(value) {
  return typeof value === "number" && Number.isFinite(value) && value >= 0;
}

function loadState() {
  const fallback = createDefaultState();
  let raw;

  try {
    raw = localStorage.getItem(HarukaijiriConfig.storageKey);
  } catch (error) {
    return fallback;
  }

  if (!raw) return fallback;

  let parsed;
  try {
    parsed = JSON.parse(raw);
  } catch (error) {
    return fallback;
  }

  if (!parsed || typeof parsed !== "object") return fallback;

  const state = createDefaultState();

  if (isFiniteNonNegative(parsed.currentValue)) state.currentValue = parsed.currentValue;
  if (isFiniteNonNegative(parsed.totalEarned)) state.totalEarned = parsed.totalEarned;
  if (isFiniteNonNegative(parsed.totalClicks)) state.totalClicks = Math.floor(parsed.totalClicks);

  if (parsed.owned && typeof parsed.owned === "object") {
    HarukaijiriConfig.facilities.forEach((facility) => {
      const value = parsed.owned[facility.id];
      if (isFiniteNonNegative(value)) state.owned[facility.id] = Math.floor(value);
    });
  }

  if (Array.isArray(parsed.unlockedAchievements)) {
    const validIds = new Set(HarukaijiriConfig.achievements.map((a) => a.id));
    state.unlockedAchievements = parsed.unlockedAchievements.filter((id) => validIds.has(id));
  }

  state.characterCount = isFiniteNonNegative(parsed.characterCount) ? Math.max(1, Math.floor(parsed.characterCount)) : 1;
  state.multiplyEventCount = isFiniteNonNegative(parsed.multiplyEventCount) ? Math.floor(parsed.multiplyEventCount) : 0;

  const zoneCount = getZoneCount(state);

  let highestUnlockedZone = isFiniteNonNegative(parsed.highestUnlockedZone) ? Math.floor(parsed.highestUnlockedZone) : 1;
  if (highestUnlockedZone < 1) highestUnlockedZone = 1;
  state.highestUnlockedZone = Math.max(highestUnlockedZone, zoneCount);

  let currentZone = isFiniteNonNegative(parsed.currentZone) ? Math.floor(parsed.currentZone) : 1;
  if (currentZone < 1) currentZone = 1;
  state.currentZone = Math.min(currentZone, zoneCount);

  state.lastSavedAt = isFiniteNonNegative(parsed.lastSavedAt) ? parsed.lastSavedAt : Date.now();

  return state;
}

function saveState(state) {
  state.lastSavedAt = Date.now();
  try {
    localStorage.setItem(HarukaijiriConfig.storageKey, JSON.stringify({ version: 1, ...state }));
  } catch (error) {
    // 保存できない環境（プライベートモード等の容量制限）でも、ゲーム自体は継続させます。
  }
}

// 放置報酬：呼び出し直後に lastSavedAt を更新して即保存することで、リロード連打による二重取得を防ぎます。
function applyOfflineReward(state) {
  const now = Date.now();
  const elapsedMs = now - state.lastSavedAt;
  state.lastSavedAt = now;

  if (elapsedMs <= 0) return 0;

  const cappedMs = Math.min(elapsedMs, HarukaijiriConfig.offlineCapMs);
  const autoPerSecond = getAutoPerSecond(state);
  if (autoPerSecond <= 0) return 0;

  const reward = autoPerSecond * (cappedMs / 1000);
  addValue(state, reward);
  return reward;
}

// ============================================================
// 描画
// ============================================================
function prefersReducedMotion() {
  return window.matchMedia("(prefers-reduced-motion: reduce)").matches;
}

function formatNumber(value) {
  const rounded = Math.floor(value);
  if (rounded >= 1000000) {
    return new Intl.NumberFormat("ja-JP", { notation: "compact" }).format(rounded);
  }
  return rounded.toLocaleString("ja-JP");
}

document.addEventListener("DOMContentLoaded", () => {
  const heroStage = document.getElementById("harukaijiri-hero-stage");
  const heroButton = document.getElementById("harukaijiri-hero-button");
  const charactersEl = document.getElementById("harukaijiri-characters");
  const zonesEl = document.getElementById("harukaijiri-zones");
  const noticeEl = document.getElementById("harukaijiri-notice");
  const valueEl = document.getElementById("harukaijiri-stat-value");
  const clickPowerEl = document.getElementById("harukaijiri-stat-click-power");
  const autoRateEl = document.getElementById("harukaijiri-stat-auto-rate");
  const totalClicksEl = document.getElementById("harukaijiri-stat-total-clicks");
  const characterCountEl = document.getElementById("harukaijiri-stat-character-count");
  const facilitiesEl = document.getElementById("harukaijiri-facilities");
  const recordsListEl = document.getElementById("harukaijiri-records-list");
  const resetButton = document.getElementById("harukaijiri-reset-button");
  const resetConfirmEl = document.getElementById("harukaijiri-reset-confirm");
  const resetCancelButton = document.getElementById("harukaijiri-reset-cancel-button");
  const resetConfirmButton = document.getElementById("harukaijiri-reset-confirm-button");

  if (!heroStage || !heroButton || !charactersEl || !zonesEl || !noticeEl || !valueEl || !facilitiesEl || !recordsListEl) return;

  let state = loadState();
  let facilityButtons = [];
  let valueTweenFrame = null;
  let clickTimestamps = [];
  let comboCooldownUntil = 0;
  let comboRunning = false;
  let multiplyCooldownUntil = 0;
  let lastRenderedZone = null;
  let noticeQueue = [];
  let noticeTimer = null;

  // ---- 通知（aria-live） ----
  function showNotice(text) {
    noticeQueue.push(text);
    if (!noticeTimer) processNoticeQueue();
  }

  function processNoticeQueue() {
    const text = noticeQueue.shift();
    if (text === undefined) {
      noticeTimer = null;
      return;
    }
    noticeEl.textContent = text;
    noticeTimer = window.setTimeout(() => {
      noticeEl.textContent = "";
      window.setTimeout(processNoticeQueue, 200);
    }, 2600);
  }

  // ---- 吹き出し ----
  function showBubble(text) {
    const existing = heroStage.querySelector(".harukaijiri-bubble");
    if (existing) existing.remove();

    const bubble = document.createElement("div");
    bubble.className = "harukaijiri-bubble";
    bubble.textContent = text;
    heroStage.appendChild(bubble);

    window.setTimeout(() => {
      if (bubble.isConnected) bubble.remove();
    }, 2400);
  }

  // ---- 浮遊する「+N」（本体付近に表示） ----
  function spawnFloatNumber(amount) {
    const float = document.createElement("span");
    float.className = "harukaijiri-float";
    float.setAttribute("aria-hidden", "true");
    float.textContent = `+${formatNumber(amount)}`;
    heroStage.appendChild(float);

    window.setTimeout(() => {
      if (float.isConnected) float.remove();
    }, 750);
  }

  // ---- 本体の反応 ----
  function triggerClickSquish() {
    if (prefersReducedMotion()) return;
    heroButton.classList.remove("is-clicked");
    void heroButton.offsetWidth;
    heroButton.classList.add("is-clicked");
  }

  function triggerShake() {
    if (prefersReducedMotion()) return;
    heroButton.classList.remove("is-shaking");
    void heroButton.offsetWidth;
    heroButton.classList.add("is-shaking");
  }

  // ---- ステータス表示 ----
  function renderStatsInstant() {
    valueEl.textContent = formatNumber(state.currentValue);
    clickPowerEl.textContent = formatNumber(getClickPower(state));
    autoRateEl.textContent = formatNumber(getAutoPerSecond(state));
    totalClicksEl.textContent = formatNumber(state.totalClicks);
    characterCountEl.textContent = `${formatNumber(state.characterCount)}体`;
  }

  function renderDerivedStats() {
    clickPowerEl.textContent = formatNumber(getClickPower(state));
    autoRateEl.textContent = formatNumber(getAutoPerSecond(state));
    characterCountEl.textContent = `${formatNumber(state.characterCount)}体`;
  }

  function renderValueWithTween(fromValue, toValue) {
    if (prefersReducedMotion()) {
      valueEl.textContent = formatNumber(toValue);
      return;
    }

    if (valueTweenFrame) cancelAnimationFrame(valueTweenFrame);
    const duration = 260;
    const start = performance.now();

    function step(now) {
      const progress = Math.min((now - start) / duration, 1);
      const current = fromValue + (toValue - fromValue) * progress;
      valueEl.textContent = formatNumber(current);
      if (progress < 1) {
        valueTweenFrame = requestAnimationFrame(step);
      } else {
        valueTweenFrame = null;
        valueEl.textContent = formatNumber(toValue);
      }
    }

    valueTweenFrame = requestAnimationFrame(step);
  }

  // ---- いじり道具 ----
  function updateFacilityAffordability() {
    facilityButtons.forEach(({ btn, price }) => {
      btn.disabled = state.currentValue < price;
    });
  }

  function renderFacilities() {
    facilitiesEl.replaceChildren();
    facilityButtons = [];

    HarukaijiriConfig.facilities.forEach((facility) => {
      const owned = state.owned[facility.id] || 0;
      const price = getFacilityPrice(facility, owned);

      const card = document.createElement("div");
      card.className = "harukaijiri-facility";

      const info = document.createElement("div");

      const name = document.createElement("p");
      name.className = "harukaijiri-facility-name";
      name.textContent = facility.name;
      info.appendChild(name);

      const effect = document.createElement("p");
      effect.className = "harukaijiri-facility-effect";
      effect.textContent = facility.effectLabel;
      info.appendChild(effect);

      const ownedText = document.createElement("p");
      ownedText.className = "harukaijiri-facility-owned";
      ownedText.textContent = `所有数：${owned}`;
      info.appendChild(ownedText);

      card.appendChild(info);

      const buyButton = document.createElement("button");
      buyButton.type = "button";
      buyButton.className = "harukaijiri-facility-buy";
      buyButton.setAttribute("aria-label", `${facility.name}を${formatNumber(price)}いじり値で購入`);

      const buyLabel = document.createElement("span");
      buyLabel.textContent = "購入";
      buyButton.appendChild(buyLabel);

      const priceLabel = document.createElement("span");
      priceLabel.className = "harukaijiri-facility-buy-price";
      priceLabel.textContent = formatNumber(price);
      buyButton.appendChild(priceLabel);

      buyButton.addEventListener("click", () => {
        const before = state.currentValue;
        if (buyFacility(state, facility.id)) {
          renderValueWithTween(before, state.currentValue);
          renderFacilities();
          renderDerivedStats();
          checkAchievements();
          saveState(state);
        }
      });

      card.appendChild(buyButton);
      facilitiesEl.appendChild(card);

      facilityButtons.push({ btn: buyButton, price });
    });

    updateFacilityAffordability();
  }

  // ---- いじり記録 ----
  function renderRecords() {
    recordsListEl.replaceChildren();

    HarukaijiriConfig.achievements.forEach((achievement) => {
      const unlocked = state.unlockedAchievements.includes(achievement.id);

      const li = document.createElement("li");
      li.className = `harukaijiri-record ${unlocked ? "is-unlocked" : "is-locked"}`;

      const noSpan = document.createElement("span");
      noSpan.className = "harukaijiri-record-id";
      noSpan.textContent = `No.${String(achievement.no).padStart(3, "0")}`;
      li.appendChild(noSpan);

      const textSpan = document.createElement("span");
      textSpan.textContent = unlocked ? achievement.label : "？？？？？？？？";
      li.appendChild(textSpan);

      if (unlocked) {
        const statusSpan = document.createElement("span");
        statusSpan.className = "harukaijiri-record-status";
        statusSpan.textContent = "解除済み";
        li.appendChild(statusSpan);
      }

      recordsListEl.appendChild(li);
    });
  }

  function checkAchievements() {
    HarukaijiriConfig.achievements.forEach((achievement) => {
      if (state.unlockedAchievements.includes(achievement.id)) return;
      if (!achievement.check(state)) return;

      state.unlockedAchievements.push(achievement.id);
      renderRecords();
      showNotice(`いじり記録を解放しました：${achievement.label}`);
      saveState(state);
    });
  }

  // ---- 区画 ----
  function renderZoneButtons() {
    const zoneCount = getZoneCount(state);
    zonesEl.replaceChildren();

    for (let zone = 1; zone <= zoneCount; zone += 1) {
      const btn = document.createElement("button");
      btn.type = "button";
      btn.className = "harukaijiri-zone-button";
      btn.textContent = `第${zone}区画`;
      if (zone === state.currentZone) {
        btn.setAttribute("aria-current", "true");
      }

      btn.addEventListener("click", () => {
        if (state.currentZone === zone) return;
        state.currentZone = zone;
        renderZoneButtons();
        renderCharacters(false);
        saveState(state);
      });

      zonesEl.appendChild(btn);
    }
  }

  function createCharacterImg(animate) {
    const img = document.createElement("img");
    img.src = HarukaijiriConfig.gifSrc;
    img.alt = "";
    img.setAttribute("aria-hidden", "true");
    img.draggable = false;
    img.className = "harukaijiri-character";
    const rotation = (Math.random() * 30 - 15).toFixed(1);
    img.style.transform = `rotate(${rotation}deg)`;
    if (animate && !prefersReducedMotion()) {
      img.classList.add("is-appearing");
    }
    return img;
  }

  // ---- 増殖した個体（本体以外）の描画：現在の区画分だけをDOMに反映します ----
  function renderCharacters(appendNewOnly) {
    const zone = state.currentZone;
    const { start, end: rawEnd } = getZoneRange(zone);
    const end = Math.min(rawEnd, state.characterCount);
    const count = Math.max(0, end - start + 1);
    const existingCount = charactersEl.children.length;

    if (!appendNewOnly || zone !== lastRenderedZone || count < existingCount) {
      charactersEl.replaceChildren();
      for (let i = 0; i < count; i += 1) {
        charactersEl.appendChild(createCharacterImg(false));
      }
    } else if (count > existingCount) {
      for (let i = existingCount; i < count; i += 1) {
        charactersEl.appendChild(createCharacterImg(true));
      }
    }

    lastRenderedZone = zone;
  }

  function maybeUnlockNewZone() {
    const zoneCount = getZoneCount(state);
    if (zoneCount > state.highestUnlockedZone) {
      state.highestUnlockedZone = zoneCount;
      showNotice(`第${zoneCount}区画が解放されました`);
    }
  }

  // ---- 増殖（永続的にﾊﾙｶﾁｬﾝの数を増やします。消滅せず、多重発生も制限しません） ----
  function runMultiply() {
    const range = HarukaijiriConfig.multiplyMaxAdd - HarukaijiriConfig.multiplyMinAdd + 1;
    const added = HarukaijiriConfig.multiplyMinAdd + Math.floor(Math.random() * range);

    state.characterCount += added;
    state.multiplyEventCount += 1;
    multiplyCooldownUntil = Date.now() + HarukaijiriConfig.multiplyCooldownMs;

    showNotice(`ﾊﾙｶﾁｬﾝが${added}体増えました`);
    maybeUnlockNewZone();
    renderZoneButtons();
    renderCharacters(true);
    renderDerivedStats();
    checkAchievements();
    saveState(state);
  }

  function maybeTriggerMultiply() {
    if (Date.now() < multiplyCooldownUntil) return;
    if (Math.random() < HarukaijiriConfig.multiplyChance) {
      runMultiply();
    }
  }

  // ---- 連打イベント／通常のマイルストーン反応 ----
  function maybeShowReaction(totalClicks) {
    const match = HarukaijiriConfig.reactionThresholds.find((r) => r.clicks === totalClicks);
    if (!match) return false;
    showBubble(match.text);
    return true;
  }

  function handleComboAndReactions(now) {
    const inCooldown = now < comboCooldownUntil;

    let comboCount = 0;
    if (!inCooldown && !comboRunning) {
      clickTimestamps.push(now);
      clickTimestamps = clickTimestamps.filter((t) => now - t < HarukaijiriConfig.comboWindowMs);
      comboCount = clickTimestamps.length;
    } else {
      clickTimestamps = [];
    }

    if (!inCooldown && !comboRunning && comboCount >= HarukaijiriConfig.comboThreshold) {
      clickTimestamps = [];
      comboRunning = true;
      showBubble(HarukaijiriConfig.comboMessage);
      triggerShake();

      window.setTimeout(() => {
        comboRunning = false;
        comboCooldownUntil = Date.now() + HarukaijiriConfig.comboCooldownMs;
      }, 400);

      return true;
    }

    return false;
  }

  // ---- クリック共通処理（本体・増殖個体のどちらからでも呼ばれます） ----
  function applyClickGain() {
    const now = Date.now();

    state.totalClicks += 1;
    const gain = getClickPower(state);
    const before = state.currentValue;
    addValue(state, gain);

    renderValueWithTween(before, state.currentValue);
    totalClicksEl.textContent = formatNumber(state.totalClicks);
    spawnFloatNumber(gain);
    updateFacilityAffordability();

    const comboTriggered = handleComboAndReactions(now);
    if (!comboTriggered) {
      const reactionShown = maybeShowReaction(state.totalClicks);
      if (!reactionShown) {
        maybeTriggerMultiply();
      }
    }

    checkAchievements();
  }

  // ---- 本体：ドラッグ判定つきのクリック検出（Pointer Events、キーボードにも対応） ----
  function initHeroInteraction() {
    let pointerId = null;
    let startX = 0;
    let startY = 0;
    let moved = false;
    let suppressNextClick = false;

    function markPointerHandled() {
      suppressNextClick = true;
      window.setTimeout(() => {
        suppressNextClick = false;
      }, 0);
    }

    function onPointerMove(event) {
      if (event.pointerId !== pointerId) return;
      const dx = event.clientX - startX;
      const dy = event.clientY - startY;
      if (Math.hypot(dx, dy) > HarukaijiriConfig.dragThreshold) {
        moved = true;
      }
    }

    function endSession() {
      heroButton.removeEventListener("pointermove", onPointerMove);
      heroButton.removeEventListener("pointerup", onPointerUp);
      heroButton.removeEventListener("pointercancel", onPointerCancel);
      if (pointerId !== null) {
        try {
          heroButton.releasePointerCapture(pointerId);
        } catch (error) {
          // すでに解放済みの場合は無視します。
        }
      }
      pointerId = null;
    }

    function onPointerUp(event) {
      if (event.pointerId !== pointerId) return;
      const wasMoved = moved;
      endSession();
      markPointerHandled();
      if (!wasMoved) {
        triggerClickSquish();
        applyClickGain();
      }
    }

    function onPointerCancel(event) {
      if (event.pointerId !== pointerId) return;
      endSession();
      markPointerHandled();
    }

    heroButton.addEventListener("pointerdown", (event) => {
      if (pointerId !== null) return;
      if (event.pointerType === "mouse" && event.button !== 0) return;

      pointerId = event.pointerId;
      startX = event.clientX;
      startY = event.clientY;
      moved = false;

      heroButton.setPointerCapture(pointerId);
      heroButton.addEventListener("pointermove", onPointerMove);
      heroButton.addEventListener("pointerup", onPointerUp);
      heroButton.addEventListener("pointercancel", onPointerCancel);
    });

    // キーボードのEnter/Spaceはclickイベントとして発火するため、ここで拾います。
    // ポインター操作後に発火する重複clickは、上のpointerup処理で抑制済みです。
    heroButton.addEventListener("click", () => {
      if (suppressNextClick) {
        suppressNextClick = false;
        return;
      }
      triggerClickSquish();
      applyClickGain();
    });
  }

  // ---- 増殖individual：イベント委譲（個々の画像へはリスナーを付けません） ----
  function initCharacterDelegation() {
    let pointerId = null;
    let startX = 0;
    let startY = 0;
    let moved = false;

    function onPointerMove(event) {
      if (event.pointerId !== pointerId) return;
      const dx = event.clientX - startX;
      const dy = event.clientY - startY;
      if (Math.hypot(dx, dy) > HarukaijiriConfig.dragThreshold) {
        moved = true;
      }
    }

    function endSession() {
      charactersEl.removeEventListener("pointermove", onPointerMove);
      charactersEl.removeEventListener("pointerup", onPointerUp);
      charactersEl.removeEventListener("pointercancel", onPointerCancel);
      if (pointerId !== null) {
        try {
          charactersEl.releasePointerCapture(pointerId);
        } catch (error) {
          // すでに解放済みの場合は無視します。
        }
      }
      pointerId = null;
    }

    function onPointerUp(event) {
      if (event.pointerId !== pointerId) return;
      const wasMoved = moved;
      endSession();
      if (!wasMoved) {
        applyClickGain();
      }
    }

    function onPointerCancel(event) {
      if (event.pointerId !== pointerId) return;
      endSession();
    }

    charactersEl.addEventListener("pointerdown", (event) => {
      const target = event.target.closest(".harukaijiri-character");
      if (!target) return;
      if (pointerId !== null) return;
      if (event.pointerType === "mouse" && event.button !== 0) return;

      pointerId = event.pointerId;
      startX = event.clientX;
      startY = event.clientY;
      moved = false;

      charactersEl.setPointerCapture(pointerId);
      charactersEl.addEventListener("pointermove", onPointerMove);
      charactersEl.addEventListener("pointerup", onPointerUp);
      charactersEl.addEventListener("pointercancel", onPointerCancel);
    });
  }

  // ---- リセット ----
  function initReset() {
    if (!resetButton || !resetConfirmEl || !resetCancelButton || !resetConfirmButton) return;

    resetButton.addEventListener("click", () => {
      resetConfirmEl.hidden = false;
    });

    resetCancelButton.addEventListener("click", () => {
      resetConfirmEl.hidden = true;
    });

    resetConfirmButton.addEventListener("click", () => {
      try {
        localStorage.removeItem(HarukaijiriConfig.storageKey);
      } catch (error) {
        // 削除に失敗しても、メモリ上の状態は初期化を続行します。
      }

      state = createDefaultState();
      clickTimestamps = [];
      comboCooldownUntil = 0;
      comboRunning = false;
      multiplyCooldownUntil = 0;
      lastRenderedZone = null;
      resetConfirmEl.hidden = true;

      renderFacilities();
      renderRecords();
      renderZoneButtons();
      renderCharacters(false);
      renderStatsInstant();
      showNotice("いじり記録を初期化しました");
      saveState(state);
    });
  }

  // ---- ゲームループ（経過時間ベースの自動加算） ----
  let lastTickAt = Date.now();

  function tick() {
    const now = Date.now();
    const deltaSeconds = (now - lastTickAt) / 1000;
    lastTickAt = now;

    const autoPerSecond = getAutoPerSecond(state);
    if (autoPerSecond > 0) {
      addValue(state, autoPerSecond * deltaSeconds);
      valueEl.textContent = formatNumber(state.currentValue);
      updateFacilityAffordability();
    }
  }

  // ---- 初期化 ----
  const offlineReward = applyOfflineReward(state);
  saveState(state);

  renderStatsInstant();
  renderFacilities();
  renderRecords();
  renderZoneButtons();
  renderCharacters(false);
  checkAchievements();
  initHeroInteraction();
  initCharacterDelegation();
  initReset();

  if (offlineReward > 0) {
    showNotice(`留守中に${formatNumber(offlineReward)}いじり値が増えました`);
  }

  window.setInterval(tick, HarukaijiriConfig.tickIntervalMs);
  window.setInterval(() => saveState(state), HarukaijiriConfig.autosaveIntervalMs);

  document.addEventListener("visibilitychange", () => {
    if (document.hidden) saveState(state);
  });

  window.addEventListener("beforeunload", () => {
    saveState(state);
  });
});
