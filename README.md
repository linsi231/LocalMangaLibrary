# LocalMangaLibrary

📚 **本地漫画快速浏览工具 / Portable Local Manga Browser**

LocalMangaLibrary 是一个面向 Windows 的本地漫画快速浏览工具。
它不是长期媒体库管理器，而是一个更偏“一次性使用”的便携浏览器。

核心使用方式：

```text
双击运行 → 选择本地漫画目录 → 扫描当前目录 → 快速浏览 → 关闭后清理运行数据
```

LocalMangaLibrary is a portable local manga browser for Windows.
It is designed for temporary local browsing instead of long-term library management.

-支持系统：
- 推荐：Windows 10 x64 / Windows 11 x64
- 需要：Microsoft Edge WebView2 Runtime
- 不保证支持：Windows 7 / Windows 8.1

架构：
- 推荐下载 win-x64 版本
- win-x86 版本暂不作为主要支持目标--

## 当前定位 / Project Positioning

本工具主要面向以下场景：

* 本地已有大量已解压漫画目录；
* 需要快速预览、筛选、确认内容；
* 不想搭建漫画服务器；
* 不想导入大型媒体库；
* 不想长期保留历史记录、索引、缓存和设置；
* 希望关闭程序后清理运行数据，减少存储压力。

This app is designed for quick browsing of local extracted manga folders.
It does not work as a downloader, online source manager, or persistent media server.

---

## Alpha 3 设计变化

从 Alpha 3 开始，项目重新明确为：

```text
单文件一次性本地漫画浏览器
```

这意味着：

* 启动时不自动加载上次漫画库路径；
* 启动时不自动加载旧索引；
* 启动时不恢复上次历史记录；
* 启动时不恢复上次设置或主题；
* 本地媒体库合计数据只来自当前选择的漫画库；
* 切换漫画库后，旧库列表和旧库统计必须立即清空；
* 关闭程序后清理本次运行产生的缓存、索引、历史和临时数据。

---

## 功能特点 / Features

* 🚀 **双击即用**
  解压后运行 `LocalMangaLibrary.exe`，选择本地漫画目录即可开始扫描和浏览。

* 📁 **当前库扫描**
  每次只针对当前选择的漫画库目录工作，合计数据只来自当前库。

* 🔄 **实时刷新当前库**
  切换目录或刷新索引时，会清空旧列表、旧统计和旧分页状态，避免不同目录数据混合。

* 🖼️ **大缩略图模式**
  默认使用旧版本的大封面缩略图模式，适合快速视觉浏览。

* 📋 **横向详细模式**
  可切换为更像文件管理器的详细信息列表，适合大量目录快速确认。

* 🔍 **搜索与排序**
  支持在当前库内搜索和排序，不跨库累计数据。

* 📖 **基础阅读功能**
  支持本地图片阅读、翻页和预览。

* 🧹 **本次运行缓存**
  缩略图和阅读缓存只服务当前运行 session，关闭程序后清理。

* 🔒 **不修改原始文件**
  程序不会删除、移动、重命名、覆盖用户漫画源目录中的任何文件。

---

## 不提供的功能 / What This App Does Not Do

本项目不提供以下功能：

* 不提供在线漫画下载；
* 不提供资源站搜索；
* 不提供账号同步；
* 不提供云同步；
* 不上传任何本地文件；
* 不内置任何漫画内容；
* 不长期保存漫画库数据库；
* 不主动整理、移动或删除原始文件。

This app only reads local folders selected by the user.

---

## 数据生命周期 / Data Lifecycle

Alpha 3 开始，LocalMangaLibrary 的运行数据只在本次程序运行期间有效。

启动时默认状态：

```text
当前库：未设置
合计：0
历史记录：空
设置：默认值
媒体库列表：空
缓存：新 session
```

运行期间可能创建临时 session 目录：

```text
.cache/
└─ session_yyyyMMdd_HHmmss_pid/
   ├─ thumbs/
   ├─ reader/
   └─ runtime/
```

关闭程序后应清理：

* 当前库路径；
* 当前媒体库索引；
* 当前统计数据；
* 搜索、排序、筛选状态；
* 浏览模式；
* 主题设置；
* 历史阅读；
* 历史访问目录；
* 缩略图缓存；
* 阅读缓存；
* 本次运行生成的临时 JSON。

不会清理或修改：

* 用户漫画源目录；
* 用户原始图片文件；
* `LocalMangaLibrary.exe`；
* 用户其它文件。

---

## 页面结构 / UI Structure

Alpha 3 将重构为更清晰的 Shell + Views 结构：

```text
LocalMangaLibrary.Wpf/
├─ MainWindow.xaml
├─ MainWindow.xaml.cs
├─ Views/
│  ├─ LibraryView.xaml
│  ├─ LibraryView.xaml.cs
│  ├─ HistoryView.xaml
│  ├─ HistoryView.xaml.cs
│  ├─ SettingsView.xaml
│  └─ SettingsView.xaml.cs
├─ Services/
├─ Models/
└─ Resources/
```

页面职责：

* **MainWindow**：应用外壳、左侧导航、页面切换、退出确认；
* **LibraryView**：当前漫画库扫描、浏览、搜索、排序、加载更多；
* **HistoryView**：仅显示本次运行内历史；
* **SettingsView**：仅显示本次运行内设置、缓存状态、版本信息和关于。

设置页不应显示媒体库列表。
历史页不应显示媒体库列表。
本地媒体库页不应显示设置项和历史记录。

---

## 下载与使用 / Usage

1. 前往 Releases 下载最新版本。
2. 解压发布包。
3. 运行：

```text
LocalMangaLibrary.exe
```

4. 选择本地漫画根目录。
5. 点击刷新索引。
6. 开始浏览。
7. 关闭程序后，本次运行数据会被清理。

---

## 安全边界 / Safety Rules

LocalMangaLibrary 必须遵守以下安全边界：

1. 不删除用户漫画源目录中的任何文件；
2. 不移动用户漫画源目录中的任何文件；
3. 不重命名用户漫画源目录中的任何文件；
4. 不覆盖用户漫画源目录中的任何文件；
5. 不向漫画源目录写入缓存或索引；
6. 只清理程序自己创建的 session/cache/runtime 文件；
7. 所有清理操作必须限定在程序自己的 session 目录中。

---

## 当前版本 / Current Version

```text
Alpha 3
0.3.0-alpha
```

当前版本重点：

* 重构 UI 页面结构；
* 修复当前库刷新与统计污染问题；
* 改为本次运行内数据生命周期；
* 关闭后清理运行数据；
* 明确一次性浏览工具定位。

---

## 已知限制 / Known Limitations

* 当前仍为 alpha 版本；
* 主要面向已解压的本地图片目录；
* 大规模 10000+ 目录仍需继续测试；
* 特殊目录结构、损坏图片、超长路径、权限异常等场景仍可能存在问题；
* 暂不提供长期媒体库管理能力；
* 暂不提供在线源、刮削、云同步等功能。

---

## License / 授权

当前项目暂未选择开源许可证。
在未明确添加 License 前，默认保留所有权利。
