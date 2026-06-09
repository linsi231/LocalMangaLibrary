const state = {
  rootPath: "",
  rootExists: false,
  confirmed: false,
  items: [],
  recent: [],
  decisions: [],
  recentItems: [],
  recentRecords: [],
  rootHistory: [],
  selectedPaths: new Set(),
  appInfo: null,
  theme: "blue",
  libraryViewMode: "grid",
  view: "library",
  selected: null,
  query: "",
  sort: "name",
  status: "all",
  page: 1,
  pageSize: 120,
  totalCount: 0,
  totalPages: 1,
  scanPoll: null,
  scanJobId: "",
  reader: {
    work: null,
    images: [],
    mode: "horizontal",
    page: 0,
    preloaded: new Set(),
    progressTimer: null,
  },
};

const PLACEHOLDER_IMAGE =
  "data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///ywAAAAAAQABAAACAUwAOw==";

const els = {
  sidebarRoot: document.querySelector("#sidebarRoot"),
  pageTitle: document.querySelector("#pageTitle"),
  navButtons: document.querySelectorAll(".side-nav button[data-view]"),
  exitButton: document.querySelector("#exitButton"),
  scanTime: document.querySelector("#scanTime"),
  setupBand: document.querySelector("#setupBand"),
  setupHint: document.querySelector("#setupHint"),
  rootInput: document.querySelector("#rootInput"),
  saveRootButton: document.querySelector("#saveRootButton"),
  refreshButton: document.querySelector("#refreshButton"),
  cancelScanButton: document.querySelector("#cancelScanButton"),
  searchBox: document.querySelector(".search-box"),
  topActions: document.querySelector(".top-actions"),
  topSearchButton: document.querySelector("#topSearchButton"),
  searchInput: document.querySelector("#searchInput"),
  sortSelect: document.querySelector("#sortSelect"),
  quickFilterButtons: document.querySelectorAll("#quickFilterBar button[data-filter]"),
  gridViewButton: document.querySelector("#gridViewButton"),
  detailViewButton: document.querySelector("#detailViewButton"),
  selectVisibleButton: document.querySelector("#selectVisibleButton"),
  clearSelectionButton: document.querySelector("#clearSelectionButton"),
  logDeleteButton: document.querySelector("#logDeleteButton"),
  libraryCount: document.querySelector("#libraryCount"),
  statusText: document.querySelector("#statusText"),
  grid: document.querySelector("#libraryGrid"),
  loadMoreButton: document.querySelector("#loadMoreButton"),
  recentHead: document.querySelector("#recentHead"),
  recentStrip: document.querySelector("#recentStrip"),
  libraryHead: document.querySelector("#libraryHead"),
  historyRecentPanel: document.querySelector("#historyRecentPanel"),
  historyRecentGrid: document.querySelector("#historyRecentGrid"),
  clearRecentHistoryButton: document.querySelector("#clearRecentHistoryButton"),
  rootHistoryPanel: document.querySelector("#rootHistoryPanel"),
  rootHistoryList: document.querySelector("#rootHistoryList"),
  clearRootHistoryButton: document.querySelector("#clearRootHistoryButton"),
  clearAllHistoryButton: document.querySelector("#clearAllHistoryButton"),
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
  cacheSummary: document.querySelector("#cacheSummary"),
  settingsCachePath: document.querySelector("#settingsCachePath"),
  settingsCacheSize: document.querySelector("#settingsCacheSize"),
  settingsCacheFiles: document.querySelector("#settingsCacheFiles"),
  clearCacheButton: document.querySelector("#clearCacheButton"),
  settingsPanel: document.querySelector("#settingsPanel"),
  settingsRows: document.querySelectorAll(".settings-row[data-settings-section]"),
  themeOptions: document.querySelector("#themeOptions"),
  themeSummary: document.querySelector("#themeSummary"),
  versionSummary: document.querySelector("#versionSummary"),
  displayVersion: document.querySelector("#displayVersion"),
  internalVersion: document.querySelector("#internalVersion"),
  appName: document.querySelector("#appName"),
  appArchitecture: document.querySelector("#appArchitecture"),
  newFeatures: document.querySelector("#newFeatures"),
  bugFixes: document.querySelector("#bugFixes"),
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
  rowTemplate: document.querySelector("#rowTemplate"),
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
  await loadAppInfo();
  setActivePage("library");
  if (state.confirmed) {
    await loadLibraryPage(1, true);
    await loadDecisions();
  } else {
    renderLibrary();
  }
  renderLibrary();
}

function bindEvents() {
  els.saveRootButton.addEventListener("click", saveRoot);
  els.refreshButton.addEventListener("click", refreshIndex);
  els.cancelScanButton.addEventListener("click", cancelScan);
  els.exitButton.addEventListener("click", exitApp);
  els.navButtons.forEach((button) => {
    button.addEventListener("click", () => setActivePage(button.dataset.view));
  });
  els.searchInput.addEventListener("input", () => {
    state.query = els.searchInput.value.trim();
    loadLibraryPage(1, true);
  });
  els.topSearchButton?.addEventListener("click", () => {
    state.query = els.searchInput.value.trim();
    loadLibraryPage(1, true);
    els.searchInput.focus();
  });
  els.sortSelect?.addEventListener("change", () => {
    state.sort = els.sortSelect.value;
    loadLibraryPage(1, true);
  });
  els.quickFilterButtons.forEach((button) => {
    button.addEventListener("click", () => {
      state.status = button.dataset.filter || "all";
      els.quickFilterButtons.forEach((item) => item.classList.toggle("active", item === button));
      state.selectedPaths.clear();
      loadLibraryPage(1, true);
    });
  });
  els.selectVisibleButton?.addEventListener("click", selectVisibleRows);
  els.clearSelectionButton?.addEventListener("click", () => {
    state.selectedPaths.clear();
    renderLibrary();
  });
  els.logDeleteButton?.addEventListener("click", logDeleteSelection);
  els.gridViewButton?.addEventListener("click", () => setLibraryViewMode("grid"));
  els.detailViewButton?.addEventListener("click", () => setLibraryViewMode("detail"));
  els.clearRecentHistoryButton.addEventListener("click", () => clearHistory("recent"));
  els.clearRootHistoryButton.addEventListener("click", () => clearHistory("roots"));
  els.clearAllHistoryButton.addEventListener("click", () => clearHistory("all"));
  els.settingsRows.forEach((row) => {
    row.addEventListener("click", () => toggleSettingsSection(row.dataset.settingsSection));
  });
  els.themeOptions.querySelectorAll("button").forEach((button) => {
    button.addEventListener("click", () => saveTheme(button.dataset.theme));
  });
  els.loadMoreButton.addEventListener("click", () => {
    if (state.page < state.totalPages) {
      loadLibraryPage(state.page + 1, false);
    }
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
  state.confirmed = Boolean(state.rootPath && state.rootExists);
  state.theme = data.theme || "blue";
  state.libraryViewMode = data.library_view_mode || "grid";
  applyTheme(state.theme);
  syncLibraryViewModeButtons();
  els.rootInput.value = state.rootPath;
  els.sidebarRoot.textContent = state.rootPath || "未设置";
  els.scanTime.textContent = data.index_matches_root && data.last_scanned ? `扫描：${formatDate(data.last_scanned)}` : "当前库尚未扫描";
  els.setupBand.classList.toggle("needs-setup", data.needs_setup);
  if (data.cache) {
    renderCacheInfo(data.cache);
  }
  els.setupHint.textContent = state.rootExists
    ? "路径正常，可以直接浏览或刷新索引。"
    : "启动前请填写可访问的漫画库根目录。";
}

async function loadAppInfo() {
  try {
    const data = await requestJson("/api/app/info");
    state.appInfo = data;
    renderAppInfo();
  } catch {
    state.appInfo = {
      display_version: "读取中",
      internal_version: "读取中",
      name: "Local Manga Library",
      architecture: "WPF + WebView2",
      new_features: [],
      bug_fixes: [],
    };
    renderAppInfo();
  }
}

async function loadLibrary() {
  if (!state.confirmed) {
    renderLibrary();
    return;
  }
  await loadLibraryPage(1, true);
  await loadDecisions();
}

async function loadLibraryPage(page = 1, replace = true) {
  if (!state.confirmed) return;
  const params = new URLSearchParams({
    q: state.query,
    sort: state.sort,
    status: state.status,
    page: String(page),
    pageSize: String(state.pageSize),
  });
  const data = await requestJson(`/api/library/query?${params.toString()}`);
  if (replace && data.needs_scan) {
    state.page = 1;
    state.totalPages = 1;
    state.totalCount = 0;
    state.items = [];
    renderLibrary();
    setBusy(data.message || "当前库还没有可用索引，请刷新索引。");
    const stats = data.index_stats || {};
    els.sidebarRoot.textContent = stats.root_path || state.rootPath || "未设置";
    els.scanTime.textContent = "当前库尚未扫描";
    return;
  }
  state.page = data.page || page;
  state.totalPages = data.total_pages || 1;
  state.totalCount = data.total_count || 0;
  state.items = replace ? data.items || [] : state.items.concat(data.items || []);
  const stats = data.index_stats || {};
  state.rootPath = stats.root_path || state.rootPath;
  els.sidebarRoot.textContent = state.rootPath || "未设置";
  els.scanTime.textContent = stats.last_scanned ? `扫描：${formatDate(stats.last_scanned)}` : "尚未扫描";
  renderLibrary();
}

async function loadRecent() {
  try {
    const data = await requestJson("/api/history/recent");
    state.recent = data.recent || [];
    state.recentRecords = data.records || [];
    state.recentItems = data.items || [];
    renderRecent();
    renderHistoryRecent();
  } catch {
    state.recentItems = [];
    state.recentRecords = [];
    renderRecent();
    renderHistoryRecent();
  }
}

async function loadRootHistory() {
  try {
    const data = await requestJson("/api/history/roots");
    state.rootHistory = data.roots || [];
    renderRootHistory();
  } catch {
    state.rootHistory = [];
    renderRootHistory();
  }
}

async function loadDecisions() {
  try {
    const data = await requestJson("/api/decisions");
    state.decisions = data.decisions || [];
  } catch {
    state.decisions = [];
  }
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
  const limit = cache.max_size_bytes ? ` / ${formatBytes(cache.max_size_bytes)}` : "";
  if (els.cacheInfo) {
    els.cacheInfo.textContent = `缓存位置：${cache.current_session || "session"} · ${cache.total_size_label}${limit} · ${cache.file_count} 个文件`;
    els.cacheInfo.title = cache.session_path || cache.cache_path || "";
  }
  if (els.cacheSummary) els.cacheSummary.textContent = `${cache.total_size_label}${limit}`;
  if (els.settingsCachePath) els.settingsCachePath.textContent = cache.session_path || cache.cache_path || "-";
  if (els.settingsCacheSize) els.settingsCacheSize.textContent = `${cache.total_size_label}${limit}`;
  if (els.settingsCacheFiles) els.settingsCacheFiles.textContent = `${cache.file_count} 个文件`;
}

async function clearCache() {
  const confirmed = window.confirm("确认清除缩略图和阅读图片缓存？不会删除漫画库原始文件。");
  if (!confirmed) return;
  els.clearCacheButton.disabled = true;
  setBusy("正在清除缓存...");
  try {
    const data = await requestJson("/api/cache/clear", { method: "POST" });
    renderCacheInfo(data.cache);
    setBusy(data.ok ? "当前 session 缓存已清除" : "缓存清理未完全完成");
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
    resetLibraryState();
    els.sidebarRoot.textContent = data.root_path;
    els.scanTime.textContent = "待扫描";
    els.setupHint.textContent = "路径正常，正在扫描当前目录。";
    renderLibrary();
    setBusy("路径已确认，正在扫描当前目录...");
    await refreshIndex();
    if (state.view === "history") await loadRootHistory();
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
  els.cancelScanButton.hidden = false;
  els.cancelScanButton.disabled = false;
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
    state.scanJobId = data.job_id || "";
    applyScanProgress(data.job || { index: data.index, status: "queued", done: 0, total: 0 });
    pollScan(data.job_id);
  } catch (error) {
    setBusy(error.message);
    els.refreshButton.disabled = false;
    els.cancelScanButton.hidden = true;
  }
}

async function cancelScan() {
  if (!state.scanJobId) return;
  els.cancelScanButton.disabled = true;
  setBusy("正在取消扫描...");
  try {
    const data = await requestJson(`/api/scan/${state.scanJobId}/cancel`, { method: "POST" });
    applyScanProgress(data.job || {});
  } catch (error) {
    setBusy(error.message);
    els.cancelScanButton.disabled = false;
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
    els.cancelScanButton.hidden = true;
  }
}

function applyScanProgress(job) {
  state.rootPath = job.root_path || state.rootPath;
  els.sidebarRoot.textContent = state.rootPath || "未设置";
  const total = Number(job.total || 0);
  const done = Number(job.done || 0);
  const imageCount = Number(job.total_image_count || 0);
  const errorCount = Number(job.error_count || 0);
  const elapsed = Number(job.elapsed_seconds || 0);
  const progress = total > 0 ? `${done} / ${total}` : "准备中";
  if (job.status === "done") {
    setBusy(`扫描完成：${total} 个作品，${imageCount} 张图片，${errorCount} 个错误，用时 ${elapsed}s`);
    els.refreshButton.disabled = false;
    els.cancelScanButton.hidden = true;
    state.scanPoll = null;
    state.scanJobId = "";
    loadLibraryPage(1, true);
    if (state.view === "history") loadRootHistory();
    if (state.view === "settings") loadCache();
    return true;
  }
  if (job.status === "cancelled") {
    setBusy("扫描已取消，旧索引已保留。");
    els.refreshButton.disabled = false;
    els.cancelScanButton.hidden = true;
    state.scanPoll = null;
    state.scanJobId = "";
    return true;
  }
  if (job.status === "error") {
    setBusy(job.error || "扫描失败");
    els.refreshButton.disabled = false;
    els.cancelScanButton.hidden = true;
    state.scanPoll = null;
    state.scanJobId = "";
    return true;
  }
  setBusy(`${job.message || "正在扫描"}：${progress}，图片 ${imageCount}，错误 ${errorCount}`);
  return false;
}

function setActivePage(view) {
  state.view = ["library", "history", "settings"].includes(view) ? view : "library";
  els.navButtons.forEach((button) => button.classList.toggle("active", button.dataset.view === state.view));
  const titles = {
    library: "媒体库",
    history: "历史记录",
    settings: "设置",
  };
  els.pageTitle.textContent = titles[state.view] || "媒体库";
  const libraryVisible = state.view === "library";
  const historyVisible = state.view === "history";
  const settingsVisible = state.view === "settings";
  els.setupBand.hidden = !libraryVisible;
  if (els.recentHead) els.recentHead.hidden = true;
  if (els.recentStrip) els.recentStrip.hidden = true;
  els.libraryHead.hidden = !libraryVisible;
  els.grid.hidden = !libraryVisible;
  const quickFilterBar = document.querySelector("#quickFilterBar");
  if (quickFilterBar) quickFilterBar.hidden = !libraryVisible;
  document.querySelector(".load-more-row").hidden = !libraryVisible;
  els.historyRecentPanel.hidden = !historyVisible;
  els.rootHistoryPanel.hidden = !historyVisible;
  els.settingsPanel.hidden = !settingsVisible;
  els.searchInput.disabled = !libraryVisible;
  if (els.sortSelect) els.sortSelect.disabled = !libraryVisible;
  els.searchBox.hidden = !libraryVisible;
  els.topActions.hidden = !libraryVisible;
  window.scrollTo({ top: 0, left: 0, behavior: "auto" });
  if (historyVisible) {
    loadRecent();
    loadRootHistory();
  }
  if (settingsVisible) {
    loadCache();
    loadAppInfo();
  }
}

function resetLibraryState() {
  state.items = [];
  state.selectedPaths.clear();
  state.query = "";
  state.sort = "name";
  state.status = "all";
  state.page = 1;
  state.totalCount = 0;
  state.totalPages = 1;
  els.searchInput.value = "";
  if (els.sortSelect) els.sortSelect.value = "name";
  els.quickFilterButtons.forEach((button) => button.classList.toggle("active", button.dataset.filter === "all"));
  renderLibrary();
}

function syncLibraryViewModeButtons() {
  els.gridViewButton?.classList.toggle("active", state.libraryViewMode === "grid");
  els.detailViewButton?.classList.toggle("active", state.libraryViewMode === "detail");
}

async function setLibraryViewMode(mode) {
  if (!["grid", "detail"].includes(mode) || state.libraryViewMode === mode) return;
  state.libraryViewMode = mode;
  syncLibraryViewModeButtons();
  renderLibrary();
  try {
    await requestJson("/api/settings/library-view-mode", {
      method: "POST",
      body: JSON.stringify({ mode }),
    });
    setBusy(mode === "grid" ? "已切换到大缩略图模式" : "已切换到详细列表模式");
  } catch (error) {
    setBusy(error.message);
  }
}

function renderLibrary() {
  syncLibraryViewModeButtons();
  if (!state.confirmed) {
    els.libraryCount.textContent = "合计：0 作品";
    els.grid.innerHTML = "";
    els.loadMoreButton.hidden = true;
    return;
  }
  const items = state.items;
  els.libraryCount.textContent = `合计：${state.totalCount} 作品 · 已勾选 ${state.selectedPaths.size}`;
  els.grid.innerHTML = "";
  els.grid.className = state.libraryViewMode === "detail" ? "library-list" : "grid";
  if (!items.length) {
    els.grid.innerHTML = `<div class="empty-state">没有匹配的作品</div>`;
    els.loadMoreButton.hidden = true;
    return;
  }
  for (const item of items) {
    els.grid.appendChild(state.libraryViewMode === "detail" ? createRow(item) : createCard(item));
  }
  els.loadMoreButton.hidden = state.page >= state.totalPages;
  els.loadMoreButton.textContent = `加载更多（已显示 ${items.length} / ${state.totalCount}）`;
}

function createRow(item) {
  const node = els.rowTemplate.content.firstElementChild.cloneNode(true);
  const checkbox = node.querySelector("input");
  const key = normalizePathKey(item.folder_path);
  checkbox.checked = state.selectedPaths.has(key);
  checkbox.addEventListener("change", () => {
    if (checkbox.checked) state.selectedPaths.add(key);
    else state.selectedPaths.delete(key);
    els.libraryCount.textContent = `合计：${state.totalCount} 作品 · 已勾选 ${state.selectedPaths.size}`;
  });
  node.addEventListener("dblclick", (event) => {
    event.preventDefault();
    openReaderFor(item);
  });
  node.querySelector("strong").textContent = item.folder_name;
  node.querySelector("strong").title = item.folder_name;
  node.querySelector(".row-path").textContent = item.relative_path || item.folder_path;
  node.querySelector(".row-path").title = item.folder_path;
  node.querySelector(".image-count").textContent = `${item.image_count || 0} 图片`;
  node.querySelector(".file-count").textContent = `${item.file_count || 0} 文件`;
  node.querySelector(".size-label").textContent = item.total_size_label || formatBytes(item.total_size);
  node.querySelector(".modified-label").textContent = formatDate(item.last_modified);
  node.querySelector(".row-badge").textContent = item.is_missing ? "已失效" : "可用";
  node.querySelector(".card-detail-button").addEventListener("click", () => openDetail(item));
  node.addEventListener("contextmenu", (event) => {
    event.preventDefault();
    openDetail(item);
  });
  return node;
}

function selectVisibleRows() {
  for (const item of state.items) {
    state.selectedPaths.add(normalizePathKey(item.folder_path));
  }
  renderLibrary();
}

async function logDeleteSelection() {
  const selectedItems = state.items.filter((item) => state.selectedPaths.has(normalizePathKey(item.folder_path)));
  if (!selectedItems.length) {
    setBusy("请先勾选要记录的作品。");
    return;
  }
  const confirmed = window.confirm(`确认记录 ${selectedItems.length} 个作品的删除意向？只写入日志和状态，不会删除原始文件。`);
  if (!confirmed) return;
  for (const item of selectedItems) {
    await requestJson("/api/actions/delete-log", {
      method: "POST",
      body: JSON.stringify({
        folder_path: item.folder_path,
        folder_name: item.folder_name,
      }),
    });
  }
  state.selectedPaths.clear();
  await loadDecisions();
  await loadLibraryPage(1, true);
  setBusy(`已记录 ${selectedItems.length} 个删除意向，未删除任何原始文件。`);
}

async function clearHistory(kind) {
  const messages = {
    recent: ["确认清除历史阅读记录？不会删除漫画文件，也不会删除媒体库索引。", "/api/history/recent/clear", "历史阅读已清除"],
    roots: ["确认清除历史访问目录？不会删除漫画文件，也不会删除媒体库索引。", "/api/history/roots/clear", "历史访问目录已清除"],
    all: ["确认清除全部历史记录？只会清空阅读历史和访问目录历史。", "/api/history/clear", "历史记录已清除"],
  };
  const config = messages[kind];
  if (!config || !window.confirm(config[0])) return;
  try {
    await requestJson(config[1], { method: "POST" });
    await loadRecent();
    await loadRootHistory();
    setBusy(config[2]);
  } catch (error) {
    setBusy(error.message);
  }
}

function renderRecent() {
  if (!els.recentStrip) return;
  els.recentStrip.innerHTML = "";
  if (!state.confirmed) {
    return;
  }
  const recentItems = state.recentItems.slice(0, 10);
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

function renderHistoryRecent() {
  els.historyRecentGrid.innerHTML = "";
  if (!state.recentRecords.length) {
    els.historyRecentGrid.innerHTML = `<div class="empty-state">暂无历史阅读</div>`;
    return;
  }
  for (const record of state.recentRecords) {
    const item = record.item || recentRecordToItem(record);
    const node = document.createElement("article");
    node.className = "history-row";
    node.innerHTML = `
      <div class="history-main">
        <strong title="${escapeHtml(item.folder_name)}">${escapeHtml(item.folder_name)}</strong>
        <span title="${escapeHtml(item.folder_path)}">${escapeHtml(item.folder_path)}</span>
      </div>
      <span class="history-stat">页码 ${Number(record.last_page || 0) + 1}</span>
      <span class="history-stat">${readerModeLabel(record.reader_mode)}</span>
      <span class="history-stat">${escapeHtml(formatDate(record.last_opened_at))}</span>
      <span class="row-badge">${record.missing ? "已失效" : "可用"}</span>
      <div class="history-actions">
        <button class="primary continue-button" ${record.missing ? "disabled" : ""}>继续阅读</button>
        <button class="open-folder-button" ${record.missing ? "disabled" : ""}>打开目录</button>
      </div>
    `;
    node.querySelector(".continue-button").addEventListener("click", () => {
      openReaderFor(item, record.last_page || 0, record.reader_mode || "horizontal");
    });
    node.querySelector(".open-folder-button").addEventListener("click", () => openFolderFor(item.folder_path));
    els.historyRecentGrid.appendChild(node);
  }
}

function renderRootHistory() {
  els.rootHistoryList.innerHTML = "";
  if (!state.rootHistory.length) {
    els.rootHistoryList.innerHTML = `<div class="empty-state">暂无历史访问目录</div>`;
    return;
  }
  for (const root of state.rootHistory) {
    const row = document.createElement("article");
    row.className = "root-row";
    row.innerHTML = `
      <div>
        <strong title="${escapeHtml(root.root_path)}">${escapeHtml(root.root_path)}</strong>
        <span>${root.missing ? `<span class="missing-label">目录失效</span>` : "可访问"} · ${root.item_count || 0} 作品 · ${root.total_image_count || 0} 图片 · 最近打开 ${escapeHtml(formatDate(root.last_opened_at))} · 最近扫描 ${escapeHtml(formatDate(root.last_scanned_at))}</span>
      </div>
      <button ${root.missing ? "disabled" : ""}>设为当前库</button>
    `;
    row.querySelector("button").addEventListener("click", () => openRootHistory(root.root_path));
    els.rootHistoryList.appendChild(row);
  }
}

function recentRecordToItem(record) {
  return {
    folder_name: record.title || folderName(record.folder_path),
    folder_path: record.folder_path,
    image_count: 0,
    file_count: 0,
    total_size: 0,
    total_size_label: "",
    cover_image: record.cover_path || "",
    thumb_url: "",
    last_modified: record.last_opened_at || record.updated_at || "",
  };
}

async function openRootHistory(rootPath) {
  setBusy("正在切换漫画库...");
  try {
    const data = await requestJson("/api/history/roots/open", {
      method: "POST",
      body: JSON.stringify({ root_path: rootPath }),
    });
    if (!data.ok) {
      setBusy(data.message || "目录不可访问");
      await loadRootHistory();
      return;
    }
    state.rootPath = data.root_path;
    state.confirmed = true;
    state.rootExists = true;
    els.rootInput.value = data.root_path;
    els.sidebarRoot.textContent = data.root_path;
    els.scanTime.textContent = data.has_index ? `扫描：${formatDate(data.last_scanned)}` : "当前库尚未扫描";
    resetLibraryState();
    setActivePage("library");
    await loadStatus();
    if (data.has_index) {
      await loadLibraryPage(1, true);
    } else {
      renderLibrary();
      setBusy("当前库还没有可用索引，正在重新扫描...");
      await refreshIndex();
    }
    await loadRootHistory();
    setBusy(data.message || (data.has_index ? "已切换漫画库目录" : "当前库还没有可用索引，请刷新索引。"));
  } catch (error) {
    setBusy(error.message);
  }
}

function createCard(item, options = {}) {
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
    openReaderFor(item, options.startPage || 0, options.readerMode || "horizontal");
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
  await openFolderFor(state.selected.folder_path);
}

async function openFolderFor(folderPath) {
  try {
    await requestJson("/api/open-directory", {
      method: "POST",
      body: JSON.stringify({ folder_path: folderPath }),
    });
    setBusy("已请求打开目录");
  } catch (error) {
    setBusy(error.message);
  }
}

async function addSelectedRecent(extra = {}) {
  if (!state.selected) return;
  try {
    const data = await requestJson("/api/history/recent/update", {
      method: "POST",
      body: JSON.stringify({
        folder_path: state.selected.folder_path,
        title: state.selected.folder_name,
        cover_path: state.selected.cover_image,
        ...extra,
      }),
    });
    state.recent = data.recent || [];
    await loadRecent();
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

async function openReader(startPage = 0, mode = "horizontal") {
  if (!state.selected) return;
  setBusy("正在载入漫画图片...");
  try {
    const data = await requestJson("/api/work-images", {
      method: "POST",
      body: JSON.stringify({ folder_path: state.selected.folder_path }),
    });
    state.reader.work = state.selected;
    state.reader.images = data.images || [];
    if (!state.reader.images.length) {
      setBusy("当前目录没有可浏览的图片");
      return;
    }
    state.selected.image_count = state.reader.images.length;
    state.reader.mode = mode || "horizontal";
    state.reader.page = Math.max(0, Math.min(Number(startPage || 0), Math.max(0, state.reader.images.length - 1)));
    if (state.reader.mode === "horizontal") {
      state.reader.page = Math.floor(state.reader.page / 2) * 2;
    }
    state.reader.preloaded = new Set();
    syncReaderModeButtons();
    closeDetail();
    await addSelectedRecent({ last_page: state.reader.page, reader_mode: state.reader.mode });
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

async function openReaderFor(item, startPage = 0, mode = "horizontal") {
  state.selected = item;
  await openReader(startPage, mode);
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
  saveReaderProgressSoon();
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
    saveReaderProgressSoon();
    return;
  }
  if (state.reader.mode === "horizontal-single") {
    state.reader.page = Math.max(0, state.reader.page - 1);
    renderReader();
    saveReaderProgressSoon();
    return;
  }
  els.readerStage.scrollBy({ top: -window.innerHeight * 0.85, behavior: "smooth" });
}

function readerNext() {
  if (!els.reader.classList.contains("open")) return;
  if (state.reader.mode === "horizontal") {
    state.reader.page = Math.min(Math.max(0, state.reader.images.length - 1), state.reader.page + 2);
    renderReader();
    saveReaderProgressSoon();
    return;
  }
  if (state.reader.mode === "horizontal-single") {
    state.reader.page = Math.min(Math.max(0, state.reader.images.length - 1), state.reader.page + 1);
    renderReader();
    saveReaderProgressSoon();
    return;
  }
  els.readerStage.scrollBy({ top: window.innerHeight * 0.85, behavior: "smooth" });
}

function saveReaderProgressSoon() {
  if (!state.reader.work) return;
  clearTimeout(state.reader.progressTimer);
  state.reader.progressTimer = setTimeout(() => {
    addSelectedRecent({ last_page: state.reader.page, reader_mode: state.reader.mode });
  }, 250);
}

function toggleSettingsSection(section) {
  const target = document.querySelector(`#${section}Settings`);
  if (!target) return;
  target.hidden = !target.hidden;
}

function renderAppInfo() {
  const info = state.appInfo || {};
  els.displayVersion.textContent = info.display_version || "读取中";
  els.internalVersion.textContent = info.internal_version || "读取中";
  els.versionSummary.textContent = info.display_version || "读取中";
  els.appName.textContent = info.name || "Local Manga Library";
  els.appArchitecture.textContent = info.architecture || "WPF + WebView2";
  els.newFeatures.innerHTML = "";
  els.bugFixes.innerHTML = "";
  for (const text of info.new_features || []) {
    const li = document.createElement("li");
    li.textContent = text;
    els.newFeatures.appendChild(li);
  }
  for (const text of info.bug_fixes || []) {
    const li = document.createElement("li");
    li.textContent = text;
    els.bugFixes.appendChild(li);
  }
}

function applyTheme(theme) {
  state.theme = theme || "blue";
  document.documentElement.dataset.theme = state.theme === "blue" ? "" : state.theme;
  const labels = {
    blue: "默认蓝色",
    dark: "黑色主题",
    green: "墨绿色",
    gray: "灰色主题",
    purple: "紫色主题",
    orange: "橙色主题",
  };
  els.themeSummary.textContent = labels[state.theme] || labels.blue;
  els.themeOptions.querySelectorAll("button").forEach((button) => {
    button.classList.toggle("active", button.dataset.theme === state.theme);
  });
}

async function saveTheme(theme) {
  applyTheme(theme);
  try {
    await requestJson("/api/settings/theme", {
      method: "POST",
      body: JSON.stringify({ theme }),
    });
    setBusy("主题已保存");
  } catch (error) {
    setBusy(error.message);
  }
}

async function exitApp() {
  const confirmed = window.confirm("确认退出 Local Manga Library？本次运行的路径、索引、历史、设置和缓存都会被清理。");
  if (!confirmed) return;
  setBusy("正在退出...");
  await requestJson("/api/app/exit", { method: "POST" }).catch((error) => setBusy(error.message));
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

function folderName(path) {
  return String(path || "").split(/[\\/]/).filter(Boolean).pop() || "未命名作品";
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

function readerModeLabel(mode) {
  const labels = {
    vertical: "竖版",
    "horizontal-single": "横板单页",
    horizontal: "横板双页",
  };
  return labels[mode] || labels.horizontal;
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
