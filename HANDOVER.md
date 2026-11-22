# 交接文档 · 必应壁纸编年史

最后更新：2026-11-21 · v3.6.9

---

## 一、这是什么

Windows 上的必应每日壁纸工具。初衷是替代微软官方客户端——那个客户端的推广浮层关不掉，而且浮层里显示什么由远端决定，代码不在自己手里。

现在的形态：**托盘 + PowerShell 脚本 + 计划任务**三层。托盘只是遥控器，真正的活在脚本里。

- 仓库：`bing-wallpaper-chronicle`
- 可执行文件：`BingWallpaperLite.exe`（沿用旧名，见文末「为什么叫 Lite」）
- 显示名：必应壁纸编年史
- 数据覆盖：**2016-03-05 至今，3915 天**，中英双语
- 体积：约 1 MB，无后台服务

---

## 二、架构

```
┌─────────────────┐
│ 计划任务 00:10  │──┐
│ 08:10/12:10/18:10│  │
└─────────────────┘  │
                     ├──► BingWallpaperLite.exe --run
┌─────────────────┐  │           │
│ 托盘菜单        │──┘           ▼
│ （用户手动）    │      bing-wallpaper.ps1
└─────────────────┘      （1951 行，全部业务逻辑）
```

**关键分工**：

| 层 | 做什么 | 不做 |
|---|---|---|
| 计划任务 | 定时触发 exe | 不含逻辑 |
| `BingWallpaperLite.exe` | 托盘、菜单、窗口、脚本调度 | 不直接下载、不改注册表壁纸 |
| `bing-wallpaper.ps1` | 下载、索引、清理、收藏、状态 | 不画界面 |

托盘调用脚本的方式全部走 `Program.RunScriptCapture`（捕获 stdout）或 `RunScriptEx`（带 opMode 回调弹气泡）。

---

## 三、文件地图

### C# 侧（`src\`，24 个文件）

| 文件 | 职责 |
|---|---|
| `Program.cs` | 启动、命令行分发、常量 `AppName`、`EnsurePayload`（释放脚本/图标/索引快照） |
| `Tray.cs` | NotifyIcon、菜单弹出、空闲收起 |
| `Menu.cs` | 托盘主菜单构建 |
| `Refresh.cs` | **最大的一个**：菜单动态刷新、壁纸库分页、收藏管理入口、`LoadArchiveIndex`、`FindImageFilePath` |
| `Script.cs` | 调脚本、解析输出、气泡反馈、`ExtractInt` |
| `Install.cs` | 计划任务注册、卸载项、开机自启、快捷方式 |
| `Info.cs` | 「这张图是哪儿」窗口 |
| `Strings.cs` | **全部中英文案**（约 500 行），改文案只动这里 |
| `Forms\` | 9 个窗口：About / Ai / Calendar / Clean / Download / Export / Fav / Info / Link / Pinned / Search / Setup / Uninstall |

### 脚本侧

| 文件 | 说明 |
|---|---|
| `bing-wallpaper.ps1` | 2422 行，全部业务逻辑。改它之前先读 `Get-ArchiveRecord`、`Get-OldIndexRecord`、`Read-Pinned` |
| `build.ps1` | 编译 + 打包，读源码版本号，刷新索引快照 |
| `test.ps1` | 22 条回归断言 |
| `assets\all-index.json` | 全量索引快照，编译时打进 exe 资源 |
| `assets\old-index.json` | 老存档索引快照（2009-07-13 ~ 2016-03-04，2427 天），字段 `d/t/c/n/a` |
| `assets\wilii-index.json` | wilii 兜底文件名表（2016-03-05 起，3913 条），字段 `d/n` |
| `assets\screenshot-desktop.jpg` | README 预览图 |

### 数据目录

| 路径 | 内容 | 谁在管 |
|---|---|---|
| `cache\` | 最近 30 天的图 | `Clear-OldCache`，30 天规则 |
| `favorites\` | 收藏 | 永不清理 |
| `archive\images\` | 归档图（按日期取的） | 500 MB 上限，两轮清理 |
| `archive\old\{YYYYMM}\` | 老存档图（2009-07 ~ 2016-03），2014 前 958×512、之后 1366×768/1080p | 算作归档的一部分，清理归档时一起清 |
| `archive\old-index.json` | 老存档索引（`d`/`t`/`c`/`n`/`a`） | 首次运行从 exe 释放 |
| `archive\wilii-index.json` | wilii 兜底文件名表（`d`/`n`），2016-03 起 | 首次运行从 exe 释放 |
| `cache\gallery\` | 画廊模式的图 | 关掉画廊模式就清空，不登记保护名单 |
| `archive\*.json` | 索引（月份 + 全量） | 清理归档时可勾选删除 |
| `pinned.json` | 自动清理保留的日期 | 「自动清理保留」窗口 |
| `state.json` | 当前壁纸、标题、版权、imageId | 换图时写 |

---

## 四、开发流程

### 编译

```powershell
D:\tools\bing-wallpaper\build.ps1                    # 用源码里的版本号
D:\tools\bing-wallpaper\build.ps1 -Version 3.6.10    # 指定版本
```

产物在 `dist\`，**不会自动部署到根目录**。

### 测试

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File test.ps1 -SkipBuild
```

21 条断言，必须全绿。

### 部署（本地）

```powershell
# 1. 停托盘
Get-Process BingWallpaperLite | Stop-Process -Force
# 2. 复制
Copy-Item dist\BingWallpaperLite.exe . -Force
# 3. 重启
Start-Process .\BingWallpaperLite.exe -WorkingDirectory $PWD
```

### 发布

```powershell
git add -A
git commit -m "v3.6.x: ..."
git tag v3.6.x
GIT_SSH_COMMAND='ssh -4' git push origin HEAD
GIT_SSH_COMMAND='ssh -4' git push origin v3.6.x
```

Release 需要在 GitHub 网页手动创建，上传 `dist\BingWallpaperLite-v3.6.x-Setup.exe`，正文用 `RELEASE_NOTES_v3.6.x.md`。

---

## 五、踩过的坑（重要）

### 1. 编辑脚本文件必须保留 BOM

`bing-wallpaper.ps1` 和所有 `.cs` 文件都是 **UTF-8 with BOM**。用 `edit` 工具直接改会吃 BOM，PowerShell 5.1 按 ANSI 读，中文变乱码，报几十处语法错误。

**正确姿势**：读字节判 `EF BB BF`，用 `WriteAllText(..., new UTF8Encoding($bom))` 写回。

模板在 `tools\Edit-KeepBom`（本次会话里反复用的那个函数，可以抄）。

**注意**：`build.ps1` 和 `Refresh.cs` 曾经处于**无 BOM** 状态。用 pwsh 7 跑一直没事（7 默认 UTF-8），换成 `powershell.exe`（5.1）就按 GBK 解码，中文全乱、脚本解析失败。这两个文件现在已补 BOM，改动时别弄丢。

### 1c. 「受管目录」白名单漏一处就会出现幽灵提示

`IsManagedWallpaper` 是判断「这张壁纸是不是我们自己换的」的唯一依据。任何新增的图片目录（当年加 `archive\old`、`cache\gallery` 时都漏过）都必须同步加进这个白名单，否则会出现：

- 用户设了那目录里的图 → 重启托盘 → 被当成外部改动
- 桌面多出一个 `壁纸备份_*.jpg`
- 弹「检测到你手动换了桌面壁纸」

排查方法：`RefreshAll` → `SyncExternalWallpaper` → `IsManagedWallpaper`，看目录表是不是少了新目录。

### 1b. PowerShell 5.1 的 `ConvertFrom-Json` 在管道里不展开数组

**这个坑踩了两次，第三次是 v3.6.14 才彻底清的**：换图失败（`urlbase` 拼接）和搜索失效都是它。

```powershell
# 错：循环只跑一次，得到一个 Object[]，$x.d 是 $null
foreach ($x in @(Get-Content $f -Raw | ConvertFrom-Json)) { ... }

# 对：先落到变量，再遍历
$arr = Get-Content $f -Raw | ConvertFrom-Json
foreach ($x in $arr) { ... }
```

老存档索引因此解析出 0 条，日志里只显示「已载入：0 天」，很容易被当成「文件不存在」去查错方向。

### 2. csc 编译的 exe 默认不带 TLS 1.2

`ServicePointManager.SecurityProtocol` 默认是 `Ssl3|Tls`，访问 GitHub API 会失败。`UpdateProbe.CheckLatestRelease` 里手动 `|= Tls12`。

### 3. `ReadToEndAsync` 双流并发

`Process` 同时读 stdout/stderr 时必须异步，否则 stderr 管道写满会死锁。

### 4. 变量名大小写不敏感

PowerShell 里 `$T` 和 `$t` 是同一个变量。清理脚本曾经因为 `$tmpDir` 和 `$T` 冲突导致静默失败。

### 5. `MonthCalendar` 收不到双击消息

`SysMonthCal32` 的窗口类不带 `CS_DBLCLKS`，`WM_LBUTTONDBLCLK` 根本不产生。只能拦 `WM_LBUTTONDOWN` 自己判时长和位置。

### 6. `ListView.CheckBoxes` 与 `MultiSelect = false`

复选框和"选中多行"是两回事。批量操作要走 `it.Checked` 而不是 `SelectedItems`。

### 7. 移动当前壁纸的顺序

`Move-Item` 之后桌面会短暂指向不存在的文件。必须「复制 → 切壁纸 → 删源」。

### 8. `build.ps1` 把 warning 当失败

`csc` 输出任何 warning 都会让 build 返回 1。`CS0219`（变量赋值未使用）最常见——删掉变量或用起来。

---

## 六、已知问题

| 问题 | 影响 | 备注 |
|---|---|---|
| 全量索引无版权字段 | 没下载过的日期，日历里版权行空 | 数据源限制，`all-index.json` 只有 `d`/`t`/`i` |
| `all-index.json` 707 KB | 首次打开菜单解析一次 | 已加静态缓存（`cachedIdxCount` / `cachedIdxSize`） |
| `SearchForm` 硬编码 `(无标题)` | 小瑕疵 | 应改用 `S.PinNoTitle` |
| exe 名仍带 Lite | 任务管理器里可见 | 改名要迁移计划任务/注册表/自启，见下 |
| 壁纸库不列老存档 | 在壁纸库看不到 2009–2016 的图 | **有意为之**：2400 多天会淹掉近期归档。找老图用日历或「按日期搜索」 |
| **多地区不可行** | 只能看当前出口 IP 对应的地区 | 实测 `mkt=ja-JP/en-US/de-DE` 与 `cc=jp/us/de` 全部返回同一张中文区图；历史图更拿不到（接口只给 8 天）。要别地区必须切代理节点，且拿不到历史 |
| **索引刷新是惰性的** | 搜索用的索引可能旧几天 | 没有定时器：文件不到 7 天用缓存，超过 7 天才在下次调用时联网。搜索已改为「历史索引 + 最近 8 天实时补齐」规避 |

---

## 七、待办

### 短期

- [ ] `SearchForm` 的 `(无标题)` 硬编码改成 `S.PinNoTitle`
- [ ] 壁纸库分页 `PageSize = 8` 偏小，收藏攒到 30 张要点三次「更多」（可改 12 / MaxDepth 4）
- [ ] 快捷方式目前只在**全新安装**时创建，升上来的老部署没有

### 短期（数据）

- [x] ~~缺口 309 天~~ —— 已清零：wilii 官方日期表 + anerg 镜像 + 中文标题交叉匹配
- [ ] wilii 自身缺的 2 天（2018-10-20、2018-12-21），影响仅限这两个日期取不到兜底，主源仍可用

### 中期

- [ ] 批量收藏能力已有（保留清单里勾选），但「收藏管理」窗口里还不能批量取消保护
- [ ] 归档超限的两轮清理只在手动压小 `archiveMaxMB` 时验证过，没有自动化断言

### 长期（v4.0 级）

- [ ] exe 改名 + 自动迁移（读旧注册表、改计划任务、搬数据、删旧 exe）

---

## 八、同类项目比较

### pandaligx/bing-wallpaper-lib

| 维度 | 本项目 | 对方 |
|---|---|---|
| 语言 / 许可 | C# + PowerShell | Rust / GPL-3.0 |
| 星标 | — | 66 |
| 数据起点 | **2016-03-05** | 2018-09-11 |
| 数据量 | **3915 条** | 2994 条 |
| 定位 | 完整桌面应用 | 库 / API |
| 离线索引 | 有（exe 内置） | 无 |

**结论**：对方的数据是我们 2018-09 以后那段的子集，少了 920 天。其纠错表与我们比对 0 差异。**数据无引进价值**，只在"离线快照"这个思路上一致（我们做得更彻底——直接打进 exe）。

### 微软官方 Bing Wallpaper

| 维度 | 本项目 | 官方 |
|---|---|---|
| 推广浮层 | 无 | **关不掉**，内容由远端决定 |
| 原图 | 3.5 MB UHD | 468 KB 带水印（旧版）/ 视版本 |
| 历史回溯 | 2016 年至今 | 仅 8 天 |
| 体积 | ~1 MB | 数十 MB |
| 数据流向 | 只调公开接口，无上报 | 未公开 |

**这是项目存在的理由**——不是"再做一个壁纸工具"，而是"把控制权拿回来"。

### 其他同类的通病

翻了一圈 GitHub 上类似项目，普遍问题：

1. **只做 8 天** —— 依赖 `HPImageArchive` 接口窗口，不做归档
2. **不处理清理** —— 缓存无限增长
3. **没有离线索引** —— 每次搜索都要联网
4. **中文标题缺失** —— 只取 `mkt=en-US`

本项目在这四点上都做了，代价是代码量比同类大一个数量级（脚本 1951 行 + C# 24 个文件）。

---

## 九、复盘

### 做对了什么

**1. 三层分离救了后期所有改动**

托盘只做界面调度，逻辑全在脚本里。所以改清理规则、加导出、改归档策略，都是改脚本 + 加一个菜单项，不用碰 UI 线程。

反例是 `InfoForm` 里那点 `BuildSearchUrl` 逻辑——它在 C# 里，改起来就要重编译。

**2. 断言机制**

21 条断言覆盖了关键路径（索引解析、日期归一化、清理计数、脚本参数）。每次改完跑一遍，比手工点快十倍。

**3. 诚实文档**

README 里写清了"2019 年 5 月前的图偏小"、"安装后桌面立刻变"这些限制。比藏着不说更建立信任。

### 做错了什么

**1. 保护机制没想清楚就落地**

`pinned.json` 最初的含义是"用户特意要留的"，结果归档下载时自动登记，变成了"凡是下载过的"。这个语义膨胀导致：

- 清理归档永远删不掉东西（100% 被保护）
- 「清空收藏」按钮因为统计口径自相矛盾而点不动
- 用户看到「保护 N 个」不知道在保护什么

这些问题花了好几轮才理清。**教训：一个机制落地前，先问"它的边界在哪"、"谁会写入它"、"它会不会吃掉别的功能"。**

**2. 文案和实现脱节**

「必应搜这张图」实际打开的是官方详情页；「不会删除」实际 30 天后会被清。这类问题写的时候不觉得，用起来处处别扭。

**教训：文案要跟着行为改，不是跟着意图写。**

**3. 替换式补丁误伤相邻代码**

加「收藏管理…」时写成替换，把「导出已下载壁纸…」那行吃掉了。用户第二天就发现入口没了。

**教训：用 `Edit` 加东西时，锚点要包含"前一行 + 新内容"，而不是"整行替换成新内容"。**

### 如果重新做

1. **先定数据模型的边界** —— 缓存 / 收藏 / 归档三者的语义、生命周期、谁能删谁，画一张表定死，再写代码
2. **文案和实现一起写** —— 每个按钮/菜单项，写代码时就写下"它实际做什么"，而不是"我希望它做什么"
3. **保护机制带"为什么"** —— 任何"不删"的判定，必须能回答"为什么这张不删"，并且这个答案要能显示给用户

---

## 十、为什么叫 Lite

`BingWallpaperLite.exe` 是历史名字。当前显示名是「必应壁纸编年史」（`Program.AppName`），托盘、窗口标题、消息框都用它。

**"Lite" 只在三个地方可见**：exe 文件名、安装包名、任务管理器。

不改的理由：

| 改名要连带 | 后果 |
|---|---|
| 计划任务 `\BingWallpaperLite` | 重注册 |
| 卸载注册表 `UninstallString` | 写死旧路径 |
| 开机自启 RunKey | 指向旧 exe |
| 用户已有部署 | 托盘进程、快捷方式、外部脚本 |
| 安装目录 | 用户手放的，不会自动搬 |

而 "Lite" 这个词本身不违和——定位是"替代官方客户端的轻量版"，1 MB、无后台服务，这点没变。**功能多 ≠ 不轻量。**

真要换名，是 v4.0 级别的迁移工程。

---

## 十一、快速上手

接手后第一件事：

```powershell
cd D:\tools\bing-wallpaper
git log --oneline -10          # 看最近改了什么
.\build.ps1                    # 确认能编译
powershell -File test.ps1 -SkipBuild   # 确认 21 条全绿
```

然后读这三个文件，基本就懂全貌了：

1. `README.md` —— 用户视角
2. `CHANGELOG.md` —— 按版本看演进
3. `src\Strings.cs` —— 所有文案，等于功能清单

改代码前先读 `src\Refresh.cs` 和 `bing-wallpaper.ps1` 的 `Read-Pinned` / `Clear-OldCache` / `Clear-ArchiveBySize`——清理相关的坑最多。
