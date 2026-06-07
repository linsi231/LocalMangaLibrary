const state = {
  rootPath: "",
  rootExists: false,
  confirmed: false,
  items: [],
  recent: [],
  decisions: [],
  selected: null,
  query: "",
  sort: "name",
  scanPoll: null,
  reader: {
    work: null,
    images: [],
    mode: "horizontal",
    page: 0,
    preloaded: new Set(),
  },
};

const PLACEHOLDER_IMAGE =
  "data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///ywAAAAAAQABAAACAUwAOw==";

const els = {
  sidebarRoot: document.querySelector("#sidebarRoot"),
  scanTime: document.querySelector("#scanTime"),
  setupBand: document.querySelector("#setupBand"),
  setupHint: document.querySelector("#setupHint"),
  rootInput: document.querySelector("#rootInput"),
  saveRootButton: document.querySelector("#saveRootButton"),
  refreshButton: document.querySelector("#refreshButton"),
  searchInput: document.querySelector("#searchInput"),
  sortSelect: document.querySelector("#sortSelect"),
  libraryCount: document.querySelector("#libraryCount"),
  statusText: document.querySelector("#statusText"),
  grid: document.querySelector("#libraryGrid"),
  recentStrip: document.querySelector("#recentStrip"),
  cardTemplate: document.querySelector("#cardTemplate"),
  detailPanel: document.querySelector("#detailPanel"),
  closeDetail: document.querySelector("#closeDetail"),
  detailCover: document.querySelector("#detailCover"),
  detailTitle: document.querySelector("#detailTitle"),
  detailPath: document.querySelector("#detailPath"),
  detailFileCount: document.querySelector("#detailFileCount"),
  detailImageCount: document.querySelector("#detailImageCount"),
  detailSize: document.querySelector("#detailSize"),
  detailModified: document.querySelector("#detailModified"),
  decisionNote: document.querySelector("#decisionNote"),
  openFolderButton: document.querySelector("#openFolderButton"),
  readButton: document.querySelector("#readButton"),
  addRecentButton: document.querySelector("#addRecentButton"),
  markDuplicateButton: document.querySelector("#markDuplicateButton"),
  cacheInfo: document.querySelector("#cacheInfo"),
  clearCacheButton: document.querySelector("#clearCacheButton"),
  reader: document.querySelector("#reader"),
  readerClose: document.querySelector("#readerClose"),
  readerTitle: document.querySelector("#readerTitle"),
  readerCounter: document.querySelector("#readerCounter"),
  readerStage: document.querySelector("#readerStage"),
  readerPrev: document.querySelector("#readerPrev"),
  readerNext: document.querySelector("#readerNext"),
  verticalModeButton: document.querySelector("#verticalModeButton"),
  horizontalSingleModeButton: document.querySelector("#horizontalSingleModeButton"),
  horizontalModeButton: document.querySelector("#horizontalModeButton"),
};

async function requestJson(url, options = {}) {
  const response = await fetch(url, {
    headers: { "Content-Type": "application/json" },
    ...options,
  });
  const data = await response.json().catch(() => ({}));
  if (!response.ok) {
    throw new Error(data.error || "请求失败");
  }
  return data;
}

async function boot() {
  bindEvents();
  await loadStatus();
  renderRecent();
  renderLibrary();
}

function bindEvents() {
  els.saveRootButton.addEventListener("click", saveRoot);
  els.refreshButton.addEventListener("click", refreshIndex);
  els.searchInput.addEventListener("input", () => {
    state.query = els.searchInput.value.trim();
    renderLibrary();
  });
  els.sortSelect.addEventListener("change", () => {
    state.sort = els.sortSelect.value;
    renderLibrary();
  });
  els.closeDetail.addEventListener("click", closeDetail);
  els.readButton.addEventListener("click", openReader);
  els.openFolderButton.addEventListener("click", openSelectedFolder);
  els.addRecentButton.addEventListener("click", addSelectedRecent);
  els.markDuplicateButton.addEventListener("click", () => saveDecision("check_duplicate"));
  els.clearCacheButton.addEventListener("click", clearCache);
  els.readerClose.addEventListener("click", closeReader);
  els.readerPrev.addEventListener("click", readerPrev);
  els.readerNext.addEventListener("click", readerNext);
  els.verticalModeButton.addEventListener("click", () => setReaderMode("vertical"));
  els.horizontalSingleModeButton.addEventListener("click", () => setReaderMode("horizontal-single"));
  els.horizontalModeButton.addEventListener("click", () => setReaderMode("horizontal"));
  document.addEventListener("keydown", handleReaderKeydown);
  document.querySelectorAll(".status-actions button").forEach((button) => {
    button.addEventListener("click", () => saveDecision(button.dataset.status));
  });
}

async function loadStatus() {
  const data = await requestJson("/api/status");
  state.rootPath = data.root_path || "";
  state.rootExists = Boolean(data.root_exists);
  els.rootInput.value = state.rootPath;
  els.sidebarRoot.textContent = state.rootPath || "未设置";
  els.scanTime.textContent = data.last_scanned ? `扫描：${formatDate(data.last_scanned)}` : "尚未扫描";
  els.setupBand.classList.toggle("needs-setup", data.needs_setup);
  if (data.cache) {
    renderCacheInfo(data.cache);
  }
  els.setupHint.textContent = state.rootExists
    ? "路径正常，可以直接浏览或刷新索引。"
    : "启动前请填写可访问的漫画库根目录。";
}

async function loadLibrary() {
  if (!state.confirmed) {
    renderRecent();
    renderLibrary();
    return;
  }
  const data = await requestJson("/api/library");
  const index = data.index || {};
  const configRoot = (data.config && data.config.root_path) || "";
  const indexRoot = index.root_path || "";
  const sameRoot = !configRoot || !indexRoot || normalizePathKey(configRoot) === normalizePathKey(indexRoot);
  state.items = sameRoot ? index.items || [] : [];
  state.recent = sameRoot ? data.recent || [] : [];
  state.decisions = data.decisions || [];
  state.rootPath = configRoot || indexRoot || state.rootPath;
  els.sidebarRoot.textContent = state.rootPath || "未设置";
  els.scanTime.textContent = index.last_scanned ? `扫描：${formatDate(index.last_scanned)}` : "尚未扫描";
  renderRecent();
  renderLibrary();
  await loadCache();
}

async function loadCache() {
  try {
    const data = await requestJson("/api/cache");
    renderCacheInfo(data);
  } catch (error) {
    setBusy(error.message);
  }
}

function renderCacheInfo(cache) {
  els.cacheInfo.textContent = `缓存位置：${cache.cache_path} · ${cache.total_size_label} · ${cache.file_count} 个文件`;
  els.cacheInfo.title = cache.cache_path;
}

async function clearCache() {
  const confirmed = window.confirm("确认清除缩略图和阅读图片缓存？不会删除漫画库原始文件。");
  if (!confirmed) return;
  els.clearCacheButton.disabled = true;
  setBusy("正在清除缓存...");
  try {
    const data = await requestJson("/api/cache/clear", { method: "POST" });
    renderCacheInfo(data.cache);
    setBusy(data.skipped_count ? `缓存已清理，${data.skipped_count} 个文件正在使用中` : "缓存已清除");
  } catch (error) {
    setBusy(error.message);
  } finally {
    els.clearCacheButton.disabled = false;
  }
}

async function saveRoot() {
  setBusy("正在确认路径...");
  try {
    const rootPath = els.rootInput.value.trim();
    const data = await requestJson("/api/config", {
      method: "POST",
      body: JSON.stringify({ root_path: rootPath }),
    });
    state.rootPath = data.root_path;
    state.rootExists = true;
    state.confirmed = true;
    els.sidebarRoot.textContent = data.root_path;
    els.setupHint.textContent = "路径正常，可以直接浏览或刷新索引。";
    await loadLibrary();
    setBusy("路径已确认");
  } catch (error) {
    setBusy(error.message);
  }
}

async function refreshIndex() {
  if (!state.confirmed) {
    setBusy("请先确认漫画库路径，再刷新索引。");
    return;
  }
  setBusy("正在启动扫描任务...");
  els.refreshButton.disabled = true;
  if (state.scanPoll) {
    clearTimeout(state.scanPoll);
    state.scanPoll = null;
  }
  try {
    const rootPath = els.rootInput.value.trim() || state.rootPath;
    const data = await requestJson("/api/scan", {
      method: "POST",
      body: JSON.stringify({ root_path: rootPath }),
    });
    applyScanProgress(data.job || { index: data.index, status: "queued", done: 0, total: 0 });
    pollScan(data.job_id);
  } catch (error) {
    setBusy(error.message);
    els.refreshButton.disabled = false;
  }
}

async function pollScan(jobId) {
  if (!jobId) {
    els.refreshButton.disabled = false;
    return;
  }
  try {
    const data = await requestJson(`/api/scan/${jobId}`);
    const done = applyScanProgress(data.job);
    if (!done) {
      state.scanPoll = setTimeout(() => pollScan(jobId), 700);
    }
  } catch (error) {
    setBusy(error.message);
    els.refreshButton.disabled = false;
  }
}

function applyScanProgress(job) {
  const index = job.index || {};
  state.items = job.items || index.items || [];
  state.rootPath = job.root_path || index.root_path || state.rootPath;
  els.sidebarRoot.textContent = state.rootPath || "未设置";
  if (index.last_scanned) {
    els.scanTime.textContent = `扫描：${formatDate(index.last_scanned)}`;
  }
  renderLibrary();
  const total = Number(job.total || 0);
  const done = Number(job.done || 0);
  const count = state.items.length;
  if (job.status === "done") {
    setBusy(`扫描完成：${count} 个作品`);
    els.refreshButton.disabled = false;
    state.scanPoll = null;
    loadCache();
    return true;
  }
  if (job.status === "error") {
    setBusy(job.error || "扫描失败");
    els.refreshButton.disabled = false;
    state.scanPoll = null;
    return true;
  }
  if (total > 0) {
    setBusy(`${job.message || "正在扫描"}，已显示 ${count} 个作品`);
  } else {
    setBusy(job.message || "正在准备扫描...");
  }
  return false;
}

function renderLibrary() {
  if (!state.confirmed) {
    els.libraryCount.textContent = "合计：0 作品";
    els.grid.innerHTML = "";
    return;
  }
  const items = getVisibleItems();
  els.libraryCount.textContent = `合计：${items.length} 作品`;
  els.grid.innerHTML = "";
  if (!items.length) {
    els.grid.innerHTML = `<div class="empty-state">没有匹配的作品</div>`;
    return;
  }
  for (const item of items) {
    els.grid.appendChild(createCard(item));
  }
}

function renderRecent() {
  els.recentStrip.innerHTML = "";
  if (!state.confirmed) {
    return;
  }
  const byPath = new Map(state.items.map((item) => [item.folder_path, item]));
  const recentItems = state.recent.map((entry) => byPath.get(entry.folder_path)).filter(Boolean).slice(0, 10);
  if (!recentItems.length) {
    els.recentStrip.innerHTML = `<div class="empty-state">暂无最近浏览</div>`;
    return;
  }
  for (const item of recentItems) {
    const button = document.createElement("button");
    button.className = "recent-card";
    button.innerHTML = `<img src="${thumbSource(item)}" alt=""><strong title="${escapeHtml(item.folder_name)}">${escapeHtml(item.folder_name)}</strong>`;
    button.addEventListener("dblclick", (event) => {
      event.preventDefault();
      event.stopPropagation();
      openReaderFor(item);
    });
    button.addEventListener("contextmenu", (event) => {
      event.preventDefault();
      openDetail(item);
    });
    els.recentStrip.appendChild(button);
  }
}

function getVisibleItems() {
  const query = state.query.toLocaleLowerCase();
  const items = state.items.filter((item) => item.folder_name.toLocaleLowerCase().includes(query));
  return items.sort(compareItems);
}

function compareItems(a, b) {
  if (state.sort === "modified") {
    return Date.parse(b.last_modified || 0) - Date.parse(a.last_modified || 0);
  }
  if (state.sort === "files") {
    return b.file_count - a.file_count || a.folder_name.localeCompare(b.folder_name, "zh-Hans-CN");
  }
  if (state.sort === "size") {
    return b.total_size - a.total_size || a.folder_name.localeCompare(b.folder_name, "zh-Hans-CN");
  }
  return a.folder_name.localeCompare(b.folder_name, "zh-Hans-CN", { numeric: true });
}

function createCard(item) {
  const node = els.cardTemplate.content.firstElementChild.cloneNode(true);
  const button = node.querySelector(".card-button");
  const detailButton = node.querySelector(".card-detail-button");
  const img = node.querySelector("img");
  img.src = thumbSource(item);
  img.alt = item.folder_name;
  node.querySelector("strong").textContent = item.folder_name;
  node.querySelector("strong").title = item.folder_name;
  node.querySelector(".image-count").textContent = `${item.image_count} 图片`;
  node.querySelector(".size-label").textContent = item.total_size_label || formatBytes(item.total_size);
  node.querySelector(".count-badge").textContent = compactCount(item.image_count);
  button.addEventListener("dblclick", (event) => {
    event.preventDefault();
    event.stopPropagation();
    openReaderFor(item);
  });
  button.addEventListener("contextmenu", (event) => {
    event.preventDefault();
    openDetail(item);
  });
  detailButton.addEventListener("click", (event) => {
    event.preventDefault();
    event.stopPropagation();
    openDetail(item);
  });
  return node;
}

function openDetail(item) {
  state.selected = item;
  const decision = state.decisions.find((entry) => entry.folder_path === item.folder_path);
  els.detailCover.src = thumbSource(item);
  els.detailTitle.textContent = item.folder_name;
  els.detailPath.textContent = item.folder_path;
  els.detailFileCount.textContent = String(item.file_count);
  els.detailImageCount.textContent = String(item.image_count);
  els.detailSize.textContent = item.total_size_label || formatBytes(item.total_size);
  els.detailModified.textContent = formatDate(item.last_modified);
  els.decisionNote.value = decision?.note || "";
  els.detailPanel.classList.add("open");
  els.detailPanel.setAttribute("aria-hidden", "false");
}

function closeDetail() {
  els.detailPanel.classList.remove("open");
  els.detailPanel.setAttribute("aria-hidden", "true");
}

async function openSelectedFolder() {
  if (!state.selected) return;
  try {
    await requestJson("/api/open-directory", {
      method: "POST",
      body: JSON.stringify({ folder_path: state.selected.folder_path }),
    });
    setBusy("已请求打开目录");
  } catch (error) {
    setBusy(error.message);
  }
}

async function addSelectedRecent() {
  if (!state.selected) return;
  try {
    const data = await requestJson("/api/recent", {
      method: "POST",
      body: JSON.stringify({ folder_path: state.selected.folder_path }),
    });
    state.recent = data.recent || [];
    renderRecent();
    setBusy("已加入最近浏览");
  } catch (error) {
    setBusy(error.message);
  }
}

async function saveDecision(status) {
  if (!state.selected) return;
  try {
    const data = await requestJson("/api/decisions", {
      method: "POST",
      body: JSON.stringify({
        folder_path: state.selected.folder_path,
        status,
        note: els.decisionNote.value.trim(),
      }),
    });
    state.decisions = data.decisions || [];
    renderLibrary();
    setBusy("状态已保存");
  } catch (error) {
    setBusy(error.message);
  }
}

async function openReader() {
  if (!state.selected) return;
  if (!state.selected.image_count) {
    setBusy("当前目录没有可浏览的图片");
    return;
  }
  setBusy("正在载入漫画图片...");
  try {
    const data = await requestJson("/api/work-images", {
      method: "POST",
      body: JSON.stringify({ folder_path: state.selected.folder_path }),
    });
    state.reader.work = state.selected;
    state.reader.images = data.images || [];
    state.reader.mode = "horizontal";
    state.reader.page = 0;
    state.reader.preloaded = new Set();
    syncReaderModeButtons();
    closeDetail();
    await addSelectedRecent();
    startReaderPreload(state.selected.folder_path);
    prefetchReaderImages(0, 12);
    document.documentElement.classList.add("reader-open");
    document.body.classList.add("reader-open");
    els.reader.classList.add("open");
    els.reader.setAttribute("aria-hidden", "false");
    renderReader();
    setBusy("阅读器已打开");
  } catch (error) {
    setBusy(error.message);
  }
}

async function openReaderFor(item) {
  state.selected = item;
  await openReader();
}

function closeReader() {
  document.documentElement.classList.remove("reader-open");
  document.body.classList.remove("reader-open");
  els.reader.classList.remove("open");
  els.reader.setAttribute("aria-hidden", "true");
  els.readerStage.innerHTML = "";
}

function setReaderMode(mode) {
  state.reader.mode = mode;
  if (mode === "horizontal") {
    state.reader.page = Math.floor(state.reader.page / 2) * 2;
  }
  syncReaderModeButtons();
  renderReader();
}

function syncReaderModeButtons() {
  els.verticalModeButton.classList.toggle("active", state.reader.mode === "vertical");
  els.horizontalSingleModeButton.classList.toggle("active", state.reader.mode === "horizontal-single");
  els.horizontalModeButton.classList.toggle("active", state.reader.mode === "horizontal");
}

function renderReader() {
  const { work, images, mode, page } = state.reader;
  els.readerTitle.textContent = work?.folder_name || "漫画浏览";
  els.readerStage.className = `reader-stage ${mode}`;
  els.readerStage.innerHTML = "";
  if (!images.length) {
    els.readerCounter.textContent = "0 / 0";
    els.readerStage.innerHTML = `<div class="reader-empty">没有可浏览的图片</div>`;
    return;
  }

  if (mode === "horizontal") {
    const spread = document.createElement("div");
    spread.className = "reader-spread";
    const first = images[page];
    const second = images[page + 1];
    if (first) spread.appendChild(createReaderPage(first));
    if (second) spread.appendChild(createReaderPage(second));
    els.readerStage.appendChild(spread);
    els.readerCounter.textContent = `${page + 1}-${Math.min(page + 2, images.length)} / ${images.length}`;
    els.readerPrev.disabled = page <= 0;
    els.readerNext.disabled = page + 2 >= images.length;
    prefetchReaderImages(page, 12);
    return;
  }

  if (mode === "horizontal-single") {
    const spread = document.createElement("div");
    spread.className = "reader-spread single";
    const image = images[page];
    if (image) spread.appendChild(createReaderPage(image));
    els.readerStage.appendChild(spread);
    els.readerCounter.textContent = `${page + 1} / ${images.length}`;
    els.readerPrev.disabled = page <= 0;
    els.readerNext.disabled = page >= images.length - 1;
    prefetchReaderImages(page, 8);
    return;
  }

  const fragment = document.createDocumentFragment();
  for (const image of images) {
    fragment.appendChild(createReaderPage(image));
  }
  els.readerStage.appendChild(fragment);
  els.readerCounter.textContent = `${images.length} 张图片`;
  els.readerPrev.disabled = false;
  els.readerNext.disabled = false;
  prefetchReaderImages(0, 16);
}

function startReaderPreload(folderPath) {
  fetch("/api/preload-reader", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ folder_path: folderPath }),
  }).catch(() => {
    // 预读失败不影响手动浏览，翻页时仍会按需生成缓存。
  });
}

function prefetchReaderImages(startIndex, count) {
  const images = state.reader.images;
  const from = Math.max(0, startIndex);
  const to = Math.min(images.length, from + count);
  for (let idx = from; idx < to; idx += 1) {
    const url = images[idx]?.url;
    if (!url || state.reader.preloaded.has(url)) continue;
    state.reader.preloaded.add(url);
    const img = new Image();
    img.decoding = "async";
    img.src = url;
  }
}

function createReaderPage(image) {
  const figure = document.createElement("figure");
  figure.className = "reader-page";
  const img = document.createElement("img");
  img.src = image.url;
  img.alt = image.name;
  img.loading = state.reader.mode === "vertical" ? "lazy" : "eager";
  figure.appendChild(img);
  return figure;
}

function readerPrev() {
  if (!els.reader.classList.contains("open")) return;
  if (state.reader.mode === "horizontal") {
    state.reader.page = Math.max(0, state.reader.page - 2);
    renderReader();
    return;
  }
  if (state.reader.mode === "horizontal-single") {
    state.reader.page = Math.max(0, state.reader.page - 1);
    renderReader();
    return;
  }
  els.readerStage.scrollBy({ top: -window.innerHeight * 0.85, behavior: "smooth" });
}

function readerNext() {
  if (!els.reader.classList.contains("open")) return;
  if (state.reader.mode === "horizontal") {
    state.reader.page = Math.min(Math.max(0, state.reader.images.length - 1), state.reader.page + 2);
    renderReader();
    return;
  }
  if (state.reader.mode === "horizontal-single") {
    state.reader.page = Math.min(Math.max(0, state.reader.images.length - 1), state.reader.page + 1);
    renderReader();
    return;
  }
  els.readerStage.scrollBy({ top: window.innerHeight * 0.85, behavior: "smooth" });
}

function handleReaderKeydown(event) {
  if (!els.reader.classList.contains("open")) return;
  if (event.key === "Escape") {
    closeReader();
  }
  if (event.key === "ArrowLeft" || event.key === "ArrowUp") {
    event.preventDefault();
    readerPrev();
  }
  if (event.key === "ArrowRight" || event.key === "ArrowDown" || event.key === " ") {
    event.preventDefault();
    readerNext();
  }
}

function setBusy(text) {
  els.statusText.textContent = text;
}

function formatDate(value) {
  if (!value) return "-";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return date.toLocaleString("zh-CN", {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
  });
}

function formatBytes(size) {
  const units = ["B", "KB", "MB", "GB", "TB"];
  let value = Number(size) || 0;
  for (const unit of units) {
    if (value < 1024 || unit === units[units.length - 1]) {
      return unit === "B" ? `${value} ${unit}` : `${value.toFixed(1)} ${unit}`;
    }
    value /= 1024;
  }
  return `${size} B`;
}

function compactCount(value) {
  if (value >= 1000) return `${Math.round(value / 100) / 10}k`;
  return String(value);
}

function thumbSource(item) {
  if (item.thumb_url) return item.thumb_url;
  if (item.cover_image) return `/api/work-thumb?folder_path=${encodeURIComponent(item.folder_path)}`;
  return PLACEHOLDER_IMAGE;
}

function escapeHtml(value) {
  return String(value).replace(/[&<>"']/g, (char) => {
    const map = { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#039;" };
    return map[char];
  });
}

function normalizePathKey(value) {
  return String(value).trim().replace(/[\\/]+$/, "").replace(/\//g, "\\").toLocaleLowerCase();
}

boot().catch((error) => setBusy(error.message));
