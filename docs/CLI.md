# 命令行接口

软件的所有操作最终都落到一个 PowerShell 脚本 `bing-wallpaper.ps1`。托盘界面只是它的外壳，脚本本身可以直接被命令行、批处理、计划任务或 AI 助手驱动。

这份文档是窗口「命令行说明（给 AI 用）」的完整版，参数以脚本 `param` 块为准。

---

## 一、调用方式

```powershell
pwsh -NoProfile -File "D:\tools\bing-wallpaper\bing-wallpaper.ps1" -Date 20230615
```

- 必须用 **PowerShell 7**（`pwsh`）。Windows PowerShell 5.1 会因为脚本声明与编码问题出错。
- 路径含空格时要加引号。
- 一次只执行一个主操作；同时给多个主参数时，脚本按内部优先级挑一个执行。

---

## 二、退出码

| 码 | 含义 | 调用方该做什么 |
|---|---|---|
| 0 | 成功 | — |
| 1 | 一般错误 | 看日志定位 |
| 2 | 撞锁 | 已有实例在跑，稍后重试 |
| 3 | 没有可取消的收藏 | 提示用户 |
| 4 | 翻到最早 / 最新边界 | 停下，不要继续翻 |
| 5 | 这一天没有图 | 换一天 |
| 6 | 缺历史归档，需先取索引 | 先 `-UpdateIndex` |
| 7 | 归档源不可用 | 稍后重试或换源 |
| 8 | 成功但画质降级（非 UHD） | 可用，只是不是 4K |
| 9 | 今天的图还没发布 | 等当天晚些时候再试 |
| 11 | 图已就位，桌面壁纸未生效 | 检查壁纸设置权限 |
| 12 | 日期越界（未来日期） | 校验输入 |

---

## 三、参数

### 换图

| 参数 | 说明 |
|---|---|
| `-Index 0` | 换今天的图 |
| `-Index 1..7` | 往前翻 N 天 |
| `-Step 1` / `-Step -1` | 相对当前日期前后翻一天 |
| `-Date YYYYMMDD` | 换到指定日期，范围 2009-07-13 ~ 今天 |
| `-Random` | 随机一张，配合 `-RandomScope` |
| `-RandomScope fav / local / any` | 随机池：收藏 / 本地全部 / 连不在本地的也算 |
| `-Use "路径"` | 把本地图片设为壁纸（不下载） |

> 2009-07-13 ~ 2016-03-04 这段来自镜像档，拿到的是 1366×768 或 958×512，**不是出错**。2019 年前的日期普遍没有 UHD，同理。

### 收藏

| 参数 | 说明 |
|---|---|
| `-Favorite` | 收藏当前这张 |
| `-Unfavorite` | 取消收藏当前这张 |
| `-FavDate YYYYMMDD` | 收藏指定日期 |
| `-UnfavDate YYYYMMDD` | 取消指定日期的收藏（图移回缓存） |

### 搜索

| 参数 | 说明 |
|---|---|
| `-Query "雪 山"` | 搜索并列出匹配项，**不改图** |
| `-Search "雪 山"` | 搜索并换第一张匹配的 |
| `-KwFile kw.txt` | 从 UTF-8 文件读关键词，避免中文参数转义问题 |

搜索是**字面匹配**（标题 + 版权），没有分词。中文要靠调用方自己扩词——「雪山」拆成 `"雪 山 冰川 雪峰"` 才能召回。

### 状态与结构化输出

| 参数 | 说明 |
|---|---|
| `-Status` | 查当前状态（壁纸路径、各段计数、配置） |
| `-Json` | 让任何操作返回结构化 JSON，而不是给人看的文字 |
| `-DryRun` | 只演示要做什么，不下载、不设壁纸 |

`-Status -Json` 输出示例：

```json
{
  "version": "3.6.102",
  "current": { "date": "20261127", "title": "灰金交织的峡谷", "file": "D:\\BingWall-全量\\20261127_OHR.FaceCanyon.jpg" },
  "fav": 0, "cache": 2, "archive": 6341,
  "index": { "count": 6341, "ageDays": 0 }
}
```

换图类命令加 `-Json` 后会带回实际落地的日期与文件路径。

### 清理

| 参数 | 说明 |
|---|---|
| `-Clean -Keep 7` | 清理 7 天前的缓存 |
| `-Clean -CleanItems cache,thumbs` | 只清指定项（`cache` / `thumbs` / `wall` / `stories`） |
| `-CleanAll` | 清空缓存，保留当前壁纸 |
| `-CleanArchive` | 清历史归档（加 `-WithIndex` 连索引一起） |
| `-CleanFavorites` | 清空收藏，当前壁纸移回缓存 |
| `-CleanStories` | 清每日故事缓存 |
| `-CleanMode recycle` | 删除方式：`permanent`（默认，临时件直接删）或 `recycle`（进回收站） |

### 批量导出与补齐

| 参数 | 说明 |
|---|---|
| `-ExportLinks -RangeFrom 20260101 -RangeTo 20260131` | 只导出下载链接（JSON） |
| `-ExportRange -RangeFrom … -RangeTo … -DestDir <目录>` | 批量下载并落到指定目录 |
| `-MaxRes uhd / fhd / hd` | 导出时的目标分辨率 |
| `-Source auto / cdn / anerg` | 导出时优先哪个源 |
| `-KeepExisting` | 目标已存在就跳过（默认会检查画质并升级） |
| `-OnlyLowRes` | 只处理画质不达标的，不新下载 |
| `-FillLibrary -RangeFrom … -RangeTo …` | 把缺失日期补进接管图库，**仅接管模式可用** |

### 照片墙与预取

| 参数 | 说明 |
|---|---|
| `-WallDate YYYYMMDD` | 把那天抓进 `cache\wall`，不改壁纸 |
| `-WallFull` | 配合 `-WallDate`，要 UHD 而不是 1080p 预览 |
| `-WallKeep` | 把 `cache\wall` 提进归档（接管模式下禁用） |
| `-WallDrop` | 丢弃 `cache\wall` |
| `-PrefetchDate YYYYMMDD` | 预取进 `cache\prefetch`，只下载不设壁纸 |
| `-PrefetchClear` | 清预取目录 |

### 画廊

| 参数 | 说明 |
|---|---|
| `-Gallery` | 进画廊模式，新图落到 `cache\gallery`，不进归档 |
| `-GalleryClear` | 退出画廊并清 `cache\gallery` |

### 节日

| 参数 | 说明 |
|---|---|
| `-Festivals` | 九个节日各有多少天 |
| `-Festival 中秋` | 该节日的全部日期（JSON，农历现算） |

### 索引与维护

| 参数 | 说明 |
|---|---|
| `-UpdateIndex` | 强制刷新全量索引（约 1.6 MB） |
| `-UpdateStoryMap` | 校准故事映射表 |
| `-AuditDates` | 校验索引日期覆盖 |
| `-AuditOld` | 校验老图索引 |
| `-AuditQuality` | 逐日核对画质档位 |
| `-MigrateOld` | 把错位的老图搬回 `archive\old\<月>\` |
| `-RebuildCacheIndex` | 重建缓存索引 |

### 内部参数

以下由托盘程序调用，命令行直接用手动指定没意义：

`-UpgradeFile`（后台补原图指定文件）、`-PickRandom`、`-DateInfo`、`-StoryFor`、`-ProbeLockWait`、`-BudgetSec`、`-FixLegacyFav`、`-FixLegacyDates`、`-FixDatedFiles`、`-TestArchiveDir`、`-TestArchiveSweep`、`-WallArchive`、`-IncludeOld`。

---

## 四、给 AI 用的建议

如果你要把这个工具交给 AI 操作，把下面几条一起给它：

1. **先 `-Status -Json` 探状态**，知道现在停在哪张、图库有多少，再决定动作。
2. **换图一律加 `-Json`**，从返回里取 `date` 和 `file`，不要靠猜。
3. **搜索先 `-Query` 后 `-Date`**：`-Query` 只列不换，看清楚再动手。
4. **中文搜索要自己扩词**，脚本不做分词。
5. **退出码要看**：`5` 是那天真没图，别反复重试；`2` 是撞锁，退避几十秒；`9` 是今天的图还没发，等到傍晚再试。
6. **不要并发跑换图命令**，脚本有运行锁，第二个会拿到 `2`。
7. **`-FillLibrary` 只在接管模式可用**，普通模式下会直接报错退出。

---

## 五、图库现状（2026-11-27）

| 项 | 值 |
|---|---|
| 覆盖 | 2009-07-13 ~ 今天，共 6341 天 |
| 缺失 | 6 天：`20091026`、`20101201`、`20121009` 原图已从必应下架；`20100416`、`20100417`、`20120814` 官方索引从未收录 |
| 画质 | 4K 2670 张、1080p 1919 张、1366 768 张级别的 503 张、958×512 的 1162 张 |
| 明细 | 图库目录里的 `_quality-audit.csv` 逐日列出 |

缺失清单与已试过的全部来源见仓库根目录的 [MISSING.md](../MISSING.md)。
