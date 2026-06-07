# LocalMangaLibrary

📚 **本地解压漫画目录快速浏览器 / Portable Local Manga Folder Browser**

LocalMangaLibrary 是一个面向 Windows 的便携式本地漫画浏览工具。
LocalMangaLibrary is a portable local manga folder browser for Windows.

它主要用于浏览已经下载并解压到本地的图片目录，适合移动硬盘、本地下载目录、临时预览和快速筛选。
It is designed for browsing extracted local image folders, especially for portable drives, local download folders, temporary preview, and quick filtering.

当前项目处于 **alpha 阶段**，界面与功能说明以中文为主。
The project is currently in the **alpha stage**, and the user interface is mainly in Chinese.

---

## ✨ 功能特点 / Features

* 🚀 **双击即用 / Double-click to run**
  解压后直接运行 EXE，选择本地漫画根目录即可使用。
  Unzip the package, run the EXE, select a local manga root folder, and start browsing.

* 📁 **本地目录扫描 / Local folder scanning**
  支持扫描本地已解压的漫画图片目录，适合快速查看大量文件夹内容。
  Supports scanning extracted local manga image folders, suitable for quickly checking large folder collections.

* 🖼️ **封面缩略图 / Cover thumbnails**
  自动生成封面预览，方便快速判断作品内容。
  Automatically generates cover thumbnails for quick visual identification.

* 🔍 **搜索与排序 / Search and sorting**
  支持按目录名搜索，并可按名称、修改时间、文件数量、总大小等方式排序。
  Supports searching by folder name and sorting by name, modified time, file count, and total size.

* 🕘 **最近浏览 / Recent items**
  记录最近浏览过的作品，方便快速返回。
  Keeps a list of recently opened items for quick access.

* 📖 **基础阅读模式 / Basic reader modes**
  支持基础图片浏览与阅读，适合临时预览和快速翻看。
  Provides basic image reading modes for quick preview and casual browsing.

* 🧹 **缓存可清理 / Clearable cache**
  支持清理本地缓存，减少额外存储压力。
  Supports clearing local cache to reduce extra storage usage.

* 🔒 **不修改原始文件 / Original files are not modified**
  程序不会主动修改、上传或删除原始漫画文件。
  The app does not actively modify, upload, or delete original manga files.

---

## 📦 下载与使用 / Download and Usage

1. 前往 **Releases** 页面下载最新版本压缩包。
   Go to the **Releases** page and download the latest package.

2. 解压压缩包。
   Unzip the package.

3. 运行：
   Run:

   ```text
   LocalMangaLibrary.exe
   ```

4. 在程序中选择本地漫画根目录。
   Select a local manga root folder in the app.

5. 点击 **刷新索引**。
   Click **Refresh Index**.

6. 开始浏览本地目录中的漫画图片。
   Start browsing manga images from your local folders.

---

## 🧩 适用场景 / Use Cases

LocalMangaLibrary 更适合这些场景：
LocalMangaLibrary is suitable for these scenarios:

* 移动硬盘中有大量本地漫画目录；
  You have many local manga folders on a portable drive.

* 下载来源较多，目录结构不完全统一；
  Your files come from different sources and have inconsistent folder structures.

* 只想快速预览、筛选、确认内容；
  You only want to preview, filter, and check contents quickly.

* 不想搭建漫画服务器；
  You do not want to set up a manga server.

* 不想把文件导入大型媒体库；
  You do not want to import files into a large media library.

* 不想长期保留大量缩略图缓存。
  You do not want to keep a large amount of thumbnail cache for a long time.

它的目标不是替代大型漫画管理器，而是提供一个更直接的本地浏览流程：
The goal is not to replace full-featured manga managers, but to provide a direct local browsing workflow:

```text
双击运行 → 选择目录 → 即时扫描 → 快速浏览 → 清理缓存
Double-click → Select folder → Scan instantly → Browse quickly → Clear cache
```

---

## 🚫 不提供的功能 / What This App Does Not Do

本项目不提供以下功能：
This project does not provide the following features:

* 不提供在线漫画下载；
  No online manga downloading.

* 不提供资源站搜索；
  No resource site searching.

* 不提供账号同步；
  No account sync.

* 不上传任何本地文件；
  No local files are uploaded.

* 不内置任何漫画内容；
  No manga content is included.

* 不主动整理、移动或修改原始文件。
  Original files are not actively organized, moved, or modified.

本工具只读取用户本地已有的图片目录。
This tool only reads local image folders selected by the user.

---

```

---

## 🛠️ 技术说明 / Technical Notes

当前版本基于 **.NET / WPF** 实现，前端界面资源内置在程序中。
The current version is built with **.NET / WPF**, with web UI resources embedded in the application.

项目主要结构：
Main project structure:

```text
LocalMangaLibrary.Wpf/
├─ Models/
├─ Resources/
│  └─ Web/
│     ├─ index.html
│     ├─ style.css
│     └─ app.js
├─ Services/
├─ App.xaml
├─ MainWindow.xaml
└─ LocalMangaLibrary.Wpf.csproj
```

运行时会在本地生成必要的数据与缓存文件。
Runtime data and cache files are generated locally.


---

## ⚠️ 当前状态 / Current Status

当前版本为：
Current version:

```text
v0.1.0-alpha
```

这是一个早期测试版本，可能仍存在 bug。
This is an early alpha release and may still contain bugs.
---

## 🚧 已知限制 / Known Limitations

* 主要面向已解压的本地图片目录；
  Mainly designed for extracted local image folders.

* 特殊目录结构仍可能需要继续优化；
  Unusual folder structures may still need further optimization.

* 损坏图片、超长路径、异常权限目录等场景仍需测试；
  Damaged images, very long paths, and permission-related folders still need more testing.

* 当前界面主要为中文；
  The current user interface is mainly in Chinese.

* 暂未定位为完整漫画管理器；
  This is not intended to be a full manga management system at this stage.

* 暂不提供在线源、刮削、标签系统、云同步等功能。
  Online sources, metadata scraping, tag systems, and cloud sync are not provided.

---

## 🔐 数据与隐私 / Data and Privacy

LocalMangaLibrary 只在本地读取用户选择的文件夹。
LocalMangaLibrary only reads folders selected by the user locally.

程序不会上传文件，不会连接资源站，也不会修改原始漫画文件。
The app does not upload files, connect to resource sites, or modify original manga files.

缓存与索引仅用于提升本地浏览体验，可手动清理。
Cache and indexes are only used to improve local browsing experience and can be cleared manually.

---

## 📄 License / 授权

当前项目暂未选择开源许可证。
No open-source license has been selected yet.

在未明确添加 License 前，默认保留所有权利。
Until a License file is added, all rights are reserved by default.

如果后续决定开放使用或二次开发授权，会在本仓库中补充 License 文件。
If usage or redistribution permissions are granted in the future, a License file will be added to this repository.

---

## 📝 更新记录 / Changelog

### v0.1.0-alpha

* 初始 alpha 版本发布；
  Initial alpha release.

* 支持本地目录选择；
  Added local folder selection.

* 支持扫描本地漫画图片目录；
  Added local manga image folder scanning.

* 支持封面缩略图；
  Added cover thumbnail generation.

* 支持搜索、排序、最近浏览；
  Added search, sorting, and recent items.

* 支持基础阅读功能；
  Added basic reading features.

* 支持缓存清理。
  Added cache clearing.
