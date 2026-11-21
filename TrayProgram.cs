using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using Microsoft.Win32;

[assembly: AssemblyTitle("必应壁纸编年史")]
[assembly: AssemblyProduct("必应壁纸编年史")]
[assembly: AssemblyDescription("从必应官方接口获取每日图片并设为桌面壁纸。无水印、无广告、无后台守护进程。")]
[assembly: AssemblyCompany("BingWallpaperChronicle")]
[assembly: AssemblyCopyright("© 2026 BingWallpaperChronicle")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyConfiguration("Release")]
[assembly: AssemblyCulture("")]
[assembly: AssemblyVersion("3.1.0.0")]
[assembly: AssemblyFileVersion("3.1.0.0")]
[assembly: AssemblyInformationalVersion("3.1.0")]
[assembly: ComVisible(false)]

namespace BingWallpaperLite
{
    /// <summary>界面文案表：中文 / 英文两档，由系统语言决定。</summary>
    internal static class S
    {
        private static bool _en;

        public static void Init(bool en) { _en = en; }

        public static bool IsEn { get { return _en; } }

        private static string T(string zh, string en) { return _en ? en : zh; }

        // ---------------- 菜单
        public static string Loading          { get { return T("正在读取…", "Loading…"); } }
        public static string TaskChecking     { get { return T("计划任务：检查中…", "Scheduled task: checking…"); } }
        public static string WhereIsThis      { get { return T("这张图是哪儿", "Where is this?"); } }
        public static string FavOff           { get { return T("☐ 收藏当前壁纸", "☐ Add to favorites"); } }
        public static string FavOn            { get { return T("☑ 已收藏（点击取消）", "☑ Favorited (click to undo)"); } }
        public static string ChangeToday      { get { return T("回到今天", "Back to today"); } }
        public static string YearsAgo         { get { return T("往年的今天", "This day in past years"); } }
        public static string PrevOlder        { get { return T("上一张（更早）", "Previous (older)"); } }
        public static string NextNewer        { get { return T("下一张（更新）", "Next (newer)"); } }
        public static string Library          { get { return T("壁纸库", "Library"); } }
        public static string PickDate         { get { return T("按日期搜索…", "Search by date…"); } }
        public static string RandomFav        { get { return T("随机一张收藏", "Random favorite"); } }
        public static string LockOff          { get { return T("☐ 暂停自动换图", "☐ Pause auto-change"); } }
        public static string LockOn           { get { return T("☑ 已暂停自动换图", "☑ Auto-change paused"); } }
        public static string NotifyOn         { get { return T("☑ 换图后通知", "☑ Notify on change"); } }
        public static string NotifyOff        { get { return T("☐ 换图后通知", "☐ Notify on change"); } }
        public static string StartupOn        { get { return T("☑ 开机启动", "☑ Run at startup"); } }
        public static string StartupOff       { get { return T("☐ 开机启动", "☐ Run at startup"); } }
        public static string CleanMenu        { get { return T("清理缓存", "Cache"); } }
        public static string AutoCleanOn      { get { return T("☑ 自动清理缓存", "☑ Auto-clean cache"); } }
        public static string AutoCleanOff     { get { return T("☐ 自动清理缓存", "☐ Auto-clean cache"); } }
        public static string Clean7           { get { return T("清理 7 天前", "Clear older than 7 days"); } }
        public static string Clean30          { get { return T("清理 30 天前", "Clear older than 30 days"); } }
        public static string CleanArchive     { get { return T("清理历史归档", "Clear history archive"); } }
        public static string CleanAll         { get { return T("清除全部（保留当前壁纸）", "Clear all (keep current)"); } }
        public static string ArchiveMenu      { get { return T("增强数据源", "Data sources"); } }
        public static string ArchiveOn        { get { return T("☑ 启用第三方归档", "☑ Enable third-party archive"); } }
        public static string ArchiveOff       { get { return T("☐ 启用第三方归档", "☐ Enable third-party archive"); } }
        public static string OpenArchiveDir   { get { return T("打开归档目录", "Open archive folder"); } }
        public static string ClearArchiveIdx  { get { return T("清除归档索引缓存", "Clear archive index cache"); } }
        public static string UpdateTimeMenu   { get { return T("更新时间", "Update time"); } }
        public static string SearchMenu       { get { return T("按关键词找…", "Search by keyword…"); } }
        public static string AiMenu           { get { return T("AI 调用…", "AI access…"); } }

        public static string SrchTitle        { get { return T(" · 按关键词找壁纸", " · Search wallpapers"); } }
        public static string SrchLabel        { get { return T("关键词（空格分隔，可多个）：", "Keywords (space-separated):"); } }
        public static string SrchBtn          { get { return T("搜索", "Search"); } }
        public static string SrchApply        { get { return T("换这张", "Use this"); } }
        public static string SrchRefresh      { get { return T("刷新索引", "Refresh index"); } }
        public static string SrchHint         { get { return T("索引覆盖 2016-03 至今；7 天自动更新一次。", "Index covers Mar 2016 onward; refreshed every 7 days."); } }
        public static string SrchSearching    { get { return T("搜索中…", "Searching…"); } }
        public static string SrchNone         { get { return T("没有匹配的壁纸，换个词试试", "No matches — try different keywords"); } }
        public static string SrchCount        { get { return T("找到 ", "Found "); } }
        public static string SrchCountTail    { get { return _en ? " results" : " 张"; } }
        public static string SrchConfirm      { get { return T("要把桌面壁纸换成这张吗？", "Change the desktop wallpaper to this one?"); } }
        public static string SrchConfirmTtl   { get { return T(" · 换壁纸", " · Change wallpaper"); } }
        public static string SrchIndexRefresh { get { return T("正在刷新索引…", "Refreshing index…"); } }
        public static string SrchIndexDone    { get { return T("索引已刷新", "Index refreshed"); } }

        public static string AiTitle          { get { return T(" · AI 调用", " · AI access"); } }
        public static string AiIntro          { get { return T("命令行接口可被任何 AI 助手直接调用。\r\n把下面这段复制给你的 AI，再把需求写在最后一行：", "The CLI can be driven by any AI assistant.\r\nCopy the block below to your AI, then append your request as the last line:"); } }
        public static string AiCopyPath       { get { return T("复制路径", "Copy path"); } }
        public static string AiCopyCmds       { get { return T("复制命令示例", "Copy examples"); } }
        public static string AiCopyPrompt     { get { return T("复制给 AI 的说明", "Copy AI prompt"); } }
        public static string AiCopied         { get { return T("已复制到剪贴板", "Copied to clipboard"); } }

        public static string ExtChangedTtl    { get { return T("壁纸被外部更换", "Wallpaper changed externally"); } }
        public static string CleanTtlArchive  { get { return T(" · 清理历史归档", " · Clear history archive"); } }
        public static string CleanTtlCache    { get { return T(" · 清空缓存", " · Clear cache"); } }
        public static string CleanTtlIndex    { get { return T(" · 清理归档索引", " · Clear archive index"); } }
        public static string CleanTtlFav      { get { return T(" · 清空收藏", " · Clear favorites"); } }
        public static string CleanFavMenu     { get { return T("清空收藏…", "Clear favorites…"); } }
        public static string CleanArchiveIndexMenu { get { return T("清理归档索引…", "Clear archive index…"); } }
        public static string CleanTotal       { get { return T("图片总数", "Total files"); } }
        public static string CleanSize        { get { return T("占用空间", "Total size"); } }
        public static string CleanFavProtected{ get { return T("已被收藏（保留）", "Favorited (kept)"); } }
        public static string CleanPinProtected{ get { return T("清单保护（保留）", "Pinned (kept)"); } }
        public static string CleanCurProtected{ get { return T("当前壁纸（保留）", "Current (kept)"); } }
        public static string CleanActual      { get { return T("实际可清理", "Will remove"); } }
        public static string CleanModeTitle   { get { return T("删除方式", "Delete method"); } }
        public static string CleanToRecycle   { get { return T("进回收站", "Move to Recycle Bin"); } }
        public static string CleanToRecycle2  { get { return T("可恢复，但磁盘空间不释放", "Recoverable; frees no disk space"); } }
        public static string CleanToDelete    { get { return T("彻底删除", "Delete permanently"); } }
        public static string CleanToDelete2   { get { return T("立即释放空间，不可恢复", "Frees space now; not recoverable"); } }
        public static string CleanStart       { get { return T("开始清理", "Start"); } }
        public static string CleanNothing     { get { return T("全部受保护，没有可清理的内容。", "Nothing to clean — everything is protected."); } }
        public static string CleanIndexHint   { get { return T("删除后首次取历史图会重新下载（约 2-3 秒）。", "Indexes will be re-downloaded on next use (2-3 s)."); } }
        public static string CleanDone        { get { return T("清理完成", "Cleanup finished"); } }
        public static string CleanWorking     { get { return T("清理中…", "Cleaning…"); } }
        public static string ExtChangedMsg    { get { return T("检测到你手动换了桌面壁纸，本程序原先管理的那张已备份到桌面：", "The desktop wallpaper was changed outside this app. The previously managed image was backed up to Desktop:"); } }
        public static string ThemeMenu        { get { return T("外观", "Appearance"); } }
        public static string ThemeAuto        { get { return T("跟随系统", "Follow system"); } }
        public static string ThemeLight       { get { return T("浅色", "Light"); } }
        public static string ThemeDark        { get { return T("深色", "Dark"); } }
        public static string SysMenu          { get { return T("关于与卸载", "About & uninstall"); } }
        public static string AboutItem        { get { return T("关于 ", "About "); } }
        public static string OpenLog          { get { return T("打开日志", "Open log"); } }
        public static string UninstallItem    { get { return T("卸载（清理自启与计划任务）", "Uninstall (remove startup & task)"); } }
        public static string ExitItem         { get { return T("退出", "Exit"); } }

        // ---------------- 壁纸库
        public static string FavoritesHeader  { get { return T("★ 我的收藏（", "★ Favorites ("); } }
        public static string NoFavorites      { get { return T("　（还没有收藏）", "  (nothing favorited yet)"); } }
        public static string RecentHeader     { get { return T("最近 30 天（", "Last 30 days ("); } }
        public static string NoRecent         { get { return T("　（没有最近的缓存）", "  (no recent cache)"); } }
        public static string ArchiveHeader    { get { return T("历史归档（", "History archive ("); } }
        public static string OpenArchiveImg   { get { return T("打开历史归档目录", "Open history archive folder"); } }
        public static string OpenCacheDir     { get { return T("打开缓存目录", "Open cache folder"); } }
        public static string OpenFavDir       { get { return T("打开收藏夹目录", "Open favorites folder"); } }
        public static string More             { get { return T("更多 ▸", "More ▸"); } }
        public static string CountTail        { get { return _en ? " items)" : " 张）"; } }

        // ---------------- 往年的今天
        public static string NoPastYears      { get { return T("（还没有可回溯的往年同日）", "(no past same-day wallpapers yet)"); } }
        public static string ClickFetch       { get { return T("（点击获取）", "(click to fetch)"); } }
        public static string NeedArchive      { get { return T("（需要增强数据源）", "(requires Data sources)"); } }

        // ---------------- 气泡
        public static string BUpdated         { get { return T("已更新", "Updated"); } }
        public static string BSwitched        { get { return T("已切换", "Switched"); } }
        public static string BFavDone         { get { return T("已收藏", "Favorited"); } }
        public static string BFavUndo         { get { return T("已取消收藏", "Removed"); } }
        public static string BLocked          { get { return T("已锁定", "Paused"); } }
        public static string BReady           { get { return T("已就绪", "Ready"); } }
        public static string BBusy            { get { return T("处理中", "Busy"); } }
        public static string BFailed          { get { return T("操作失败", "Failed"); } }
        public static string BDegraded        { get { return T("已切换（压缩版）", "Switched (compressed)"); } }
        public static string BLatest          { get { return T("已是最新", "Already latest"); } }
        public static string BOldest          { get { return T("已到 8 天边界", "Reached 8-day limit"); } }
        public static string BEdge            { get { return T("到边界了", "At the limit"); } }
        public static string BNoImage         { get { return T("该日期没有壁纸", "No wallpaper for that date"); } }
        public static string BNeedArch        { get { return T("需要增强数据源", "Needs Data sources"); } }
        public static string BArchDown        { get { return T("归档源连不上", "Archive unreachable"); } }
        public static string BNoFav           { get { return T("无法收藏", "Cannot favorite"); } }
        public static string BRepaired        { get { return T("已修复", "Repaired"); } }
        public static string BRepairKO        { get { return T("修复失败", "Repair failed"); } }
        public static string BCleared         { get { return T("已清理", "Cleared"); } }
        public static string BArchEmpty       { get { return T("历史归档是空的", "History archive is empty"); } }
        public static string BWaiting         { get { return T("等待新图", "Waiting for new image"); } }
        public static string BWaitingHint     { get { return T("Bing 还没发布今天的图，8:30 和 12:00 会自动再查一次", "Bing hasn't published today's image yet; it will retry at 08:30 and 12:00"); } }

        // ---------------- 计划任务状态
        public static string TaskMissing      { get { return T("计划任务：未注册 ⚠ 点这里修复", "Scheduled task: not registered ⚠ click to fix"); } }
        public static string TaskNormal       { get { return T("计划任务：正常", "Scheduled task: OK"); } }
        public static string TaskUnknown      { get { return T("计划任务：状态未知", "Scheduled task: unknown"); } }
        public static string TaskNext         { get { return T(" · 下次 ", " · next "); } }

        // ---------------- 按钮
        public static string BtnUninstall     { get { return T("卸载", "Uninstall"); } }
        public static string BtnCancel        { get { return T("取消", "Cancel"); } }
        public static string BtnInstall       { get { return T("安装", "Install"); } }
        public static string BtnRunPortable   { get { return T("绿色运行", "Run portable"); } }
        public static string BtnBrowse        { get { return T("浏览…", "Browse…"); } }
        public static string BtnApply         { get { return T("换这张", "Use this"); } }
        public static string BtnOK            { get { return T("确定", "OK"); } }

        // ---------------- 对话框
        public static string DlHead            { get { return T("将清理：", "Will remove:"); } }
        public static string DlStartup         { get { return T("· 开机启动项", "· Startup entry"); } }
        public static string DlTask            { get { return T("· 计划任务（定时换图）", "· Scheduled task (daily change)"); } }
        public static string DlUninstEntry     { get { return T("· 控制面板中的卸载条目", "· Control Panel uninstall entry"); } }
        public static string DlCachePrefix     { get { return T("· 壁纸缓存（", "· Wallpaper cache ("); } }
        public static string DlCacheSuffix     { get { return T("，保留当前壁纸）", ", current kept)"); } }
        public static string DlArchiveIdx      { get { return T("· 历史归档的月份索引", "· History archive index"); } }
        public static string DlKeepProgram     { get { return T("程序文件不会被删除。", "Program files will not be deleted."); } }
        public static string DlKeepFav         { get { return T("保留收藏夹里的壁纸（", "Keep favorites ("); } }
        public static string DlKeepArch        { get { return T("保留历史归档的壁纸（", "Keep history archive ("); } }
        public static string DlUninstHint      { get { return T("取消勾选会把对应文件夹整个移入回收站，之后可从回收站找回。\r\n", "Unchecking moves that folder to the Recycle Bin; you can restore it later.\r\n"); } }
        public static string DlArchNote        { get { return T("归档图是主动从历史里挑的，保留后可用「选日期」重新设为壁纸。", "Archived images were hand-picked; keep them to reuse via Pick a date."); } }
        public static string DlNoArch          { get { return T("（归档里还没有图）", "(archive is empty)"); } }
        public static string DlEmptyFav        { get { return T("（收藏夹是空的，无需选择）", "(favorites empty, nothing to choose)"); } }
        public static string DlProgramDir      { get { return T("程序目录：", "Program folder: "); } }
        public static string DlDone            { get { return T("已清理：", "Cleaned up:"); } }
        public static string DlCacheCleared    { get { return T("· 壁纸缓存已清理（", "· Cache cleared ("); } }
        public static string DlCacheClearedS   { get { return T(" 张，当前壁纸保留）", " items, current kept)"); } }
        public static string DlCacheNone       { get { return T("· 壁纸缓存无需清理", "· Cache already clean"); } }
        public static string DlIdxCleared      { get { return T("· 历史归档索引已清理", "· Archive index cleared"); } }
        public static string DlArchKept        { get { return T("· 归档壁纸保留（", "· Archived images kept ("); } }
        public static string DlArchKeptS       { get { return T(" 张）", " items)"); } }
        public static string DlArchGone        { get { return T("· 归档壁纸已移入回收站", "· Archived images moved to Recycle Bin"); } }
        public static string DlArchNone        { get { return T("· 归档里没有图片", "· No archived images"); } }
        public static string DlFavKept         { get { return T("· 收藏夹保留（", "· Favorites kept ("); } }
        public static string DlFavGone         { get { return T("· 收藏夹已移入回收站", "· Favorites moved to Recycle Bin"); } }
        public static string DlFavNone         { get { return T("· 收藏夹为空", "· Favorites empty"); } }
        public static string DlKeepFiles       { get { return T("程序文件保留在此目录，可手动删除：", "Program files remain here; you may delete them manually:"); } }

        // ---------------- 其它
        public static string WarnNoScript      { get { return T("找不到核心脚本：", "Core script not found: "); } }
        public static string WarnNoScript2     { get { return T("请确认安装目录完整。", "Please verify the install folder is intact."); } }
        public static string ConfirmCleanAll   { get { return T("将删除缓存目录里除当前壁纸以外的所有图片。\r\n当前壁纸会保留，其它全部清掉。确定吗？", "Delete every image in the cache except the current wallpaper?\r\nThe current one is kept. Continue?"); } }
        public static string ConfirmCleanTtl   { get { return T(" · 清理缓存", " · Clear cache"); } }
        public static string ConfirmArchTtl    { get { return T(" · 清理历史归档", " · Clear history archive"); } }
        public static string ConfirmArchBody   { get { return T(" 张图移入回收站？\r\n\r\n当前正在显示的壁纸会保留。之后可从回收站找回，\r\n也可以通过「选日期」重新下载。", " images will be moved to the Recycle Bin.\r\n\r\nThe current wallpaper is kept. You can restore from the Recycle Bin\r\nor re-download later via Pick a date."); } }
        public static string BRepairKOHint     { get { return T("计划任务仍不可用，请用管理员身份运行一次或查看日志", "Task still unavailable; run as administrator once, or check the log"); } }
        public static string BRepairedHint     { get { return T("计划任务已重新注册，自动换图恢复正常", "Task re-registered; daily changes resumed"); } }
        public static string BReadyHint        { get { return T("壁纸每天 0:10 自动更新，8:30 与 12:00 会补查一次。右键托盘图标可换图、收藏、暂停。", "Wallpaper updates daily at 00:10, with retries at 08:30 and 12:00. Right-click the tray icon to change, favorite, or pause."); } }
        public static string NoImageHint       { get { return T("换一天试试", "Try another date"); } }
        public static string NeedArchHint      { get { return T("2025-03 之前的日期要开启「增强数据源 ▸ 启用第三方归档」才能取到", "Dates before Mar 2025 need Data sources → Enable third-party archive"); } }
        public static string ArchDownHint      { get { return T("检查网络或代理，或在「增强数据源」里临时关闭后重试", "Check network or proxy, or disable Data sources and retry"); } }
        public static string LatestHint        { get { return T("当前就是最新的一张；更早的可用「选日期」", "This is already the newest; use Pick a date for older ones"); } }
        public static string OldestHint        { get { return T("接口只提供最近 8 天，更早的请用「选日期」", "The API only covers 8 days; use Pick a date for older ones"); } }
        public static string EdgeHint          { get { return T("换个方向，或用「选日期」指定", "Try the other direction, or pick a date"); } }
        public static string NoFavHint         { get { return T("只有缓存目录里的壁纸才能收藏", "Only wallpapers in the cache folder can be favorited"); } }
        public static string DegradedHint      { get { return T("归档原图取不到，这张用的是微软 CDN 的 3840×2400", "Archive original unavailable; using Microsoft CDN 3840×2400"); } }
        public static string CleanedHint       { get { return T("历史归档里的图已移入回收站", "Archived images moved to the Recycle Bin"); } }
        public static string LockedHint        { get { return T("新壁纸会继续下载，但不会切换。取消勾选即可恢复。", "New wallpapers keep downloading but won't apply. Uncheck to resume."); } }
        public static string FavDoneHint       { get { return T("已加入收藏夹，可从菜单「壁纸库」随时切换", "Added to favorites; find it under Library"); } }
        public static string FavUndoHint       { get { return T("已从收藏夹移除", "Removed from favorites"); } }
        public static string ArchEmptyHint     { get { return T("没有需要清理的图", "Nothing to clear"); } }
        public static string SwitchedPaused    { get { return T("（自动换图仍暂停，锁定对象已跟随）", " (auto-change still paused, lock target updated)"); } }

        public static string WarnWriteCfg   { get { return T("写入配置失败：", "Failed to write config: "); } }
        public static string WarnMkDir      { get { return T("创建目录失败：", "Failed to create folder: "); } }
        public static string WarnCopyExe    { get { return T("复制程序失败：", "Failed to copy program: "); } }
        public static string WarnCopyHint   { get { return T("请确认该目录可写。", "Please verify the folder is writable."); } }
        public static string WarnPayload    { get { return T("释放配套文件失败，请换一个可写目录。", "Failed to extract support files; choose a writable folder."); } }
        public static string WarnLaunch     { get { return T("启动失败：", "Failed to start: "); } }
        public static string WarnNoFiles    { get { return T("无法在程序目录释放必要文件：", "Cannot extract required files into the program folder: "); } }
        public static string WarnNoFiles2   { get { return T("请把程序移到可写目录（例如桌面或文档）后重试。", "Move the program to a writable folder (e.g. Desktop or Documents) and retry."); } }
        public static string ExtractedTo    { get { return T("已释放配套文件到：", "Support files extracted to: "); } }
        public static string ExtractFailed  { get { return T("释放失败，目录可能不可写：", "Extraction failed; folder may be read-only: "); } }
        public static string FallbackTitle  { get { return T("壁纸已切换", "Wallpaper switched"); } }
        public static string WorkingDots    { get { return T(" · 处理中…", " · working…"); } }
        public static string DaysSuffix     { get { return _en ? " days)" : " 天）"; } }
        public static string DaysPrefix     { get { return _en ? " (" : "（"; } }
        public static string NeedArchiveSfx { get { return T("  （需要增强数据源）", "  (requires Data sources)"); } }
        public static string ClickFetchSfx  { get { return T("  （点击获取）", "  (click to fetch)"); } }
        public static string ArchSrcOn      { get { return T("来源 Zhu-junwei（2016 起）+ niumoo 兜底", "Source: Zhu-junwei (since 2016) + niumoo fallback"); } }
        public static string ArchSrcOff     { get { return T("已关闭，8 天以外的日期只能走压缩版", "Disabled; dates older than 8 days fall back to compressed"); } }
        public static string ArchCacheInfo(int months, double kb)
        {
            return _en
                ? ("  " + "｜cached " + months + " months (" + kb.ToString("0") + " KB)")
                : ("　｜已缓存 " + months + " 个月（" + kb.ToString("0") + " KB）");
        }
        public static string ArchInfoPrefix { get { return _en ? "  " : "　"; } }
        public static string ArchSrcLine1   { get { return T("主源 Zhu-junwei  ·  2016-03 起  ·  带中文标题", "Primary  Zhu-junwei  ·  since Mar 2016  ·  with titles"); } }
        public static string ArchSrcLine2   { get { return T("兜底 niumoo      ·  2021-02 起  ·  仅 ID", "Fallback niumoo    ·  since Feb 2021  ·  IDs only"); } }
        public static string ArchOffLine    { get { return T("已关闭，8 天以外的日期只能走压缩版", "Disabled — dates older than 8 days fall back to compressed"); } }

        // ---------------- 信息窗口
        public static string InfoTitle       { get { return T(" · 这张图是哪儿", " · Where is this?"); } }
        public static string InfoNone        { get { return T("还没有换图记录。", "No wallpaper record yet."); } }
        public static string InfoDate        { get { return T("日期：", "Date: "); } }
        public static string InfoTitleLine   { get { return T("标题：", "Title: "); } }
        public static string InfoCopy        { get { return T("版权：", "Copyright: "); } }
        public static string InfoFile        { get { return T("文件：", "File: "); } }
        public static string InfoSize        { get { return T("大小：", "Size: "); } }
        public static string InfoBtnSearch   { get { return T("必应搜这张图", "Search this image"); } }
        public static string InfoBtnMore     { get { return T("浏览更多壁纸", "Browse more"); } }

        // ---------------- 关于正文
        public static string AboutFeat1      { get { return T("· 每天 00:10 自动换当天的图，8:30 / 12:00 补查", "· Switches to today's image at 00:10, with retries at 08:30 / 12:00"); } }
        public static string AboutFeat2      { get { return T("· 往回翻最近 8 天，或用日历跳到 2016-03 以来的任意一天", "· Browse the last 8 days, or jump to any day since Mar 2016"); } }
        public static string AboutFeat3      { get { return T("· 收藏喜欢的图，收藏与缓存清理互不影响", "· Favorite images; favorites survive cache cleanup"); } }
        public static string AboutFeat4      { get { return T("· 关掉托盘也照常换图（计划任务直接调脚本）", "· Keeps working with the tray closed (the task runs the script)"); } }
        public static string AboutCanDo      { get { return T("【能做什么】", "[What it does]"); } }
        public static string AboutSources    { get { return T("【数据来源】", "[Data sources]"); } }
        public static string AboutComponents { get { return T("【运行组件】", "[Components]"); } }
        public static string AboutStorage    { get { return T("【占用】", "[Storage]"); } }
        public static string AboutEngineLbl  { get { return T("【引擎】", "[Engine]"); } }
        public static string AboutDailyLine  { get { return T("每日图    ", "Daily     "); } }
        public static string AboutTagline  { get { return T("每日必应壁纸，自动设桌面。无水印、无广告、无后台常驻。", "Daily Bing wallpaper, set automatically. No watermark, no ads, no background daemon."); } }
        public static string StateReg      { get { return T("已注册", "registered"); } }
        public static string StateUnreg    { get { return T("未注册", "not registered"); } }
        public static string StateOn       { get { return T("已启用", "enabled"); } }
        public static string StateOff      { get { return T("未启用", "disabled"); } }
        public static string StatePaused   { get { return T("已暂停", "paused"); } }
        public static string StateRunning  { get { return T("运行中", "running"); } }

        public static string WarnPathMissing { get { return T("路径不存在：", "Path does not exist: "); } }
        public static string WarnOpenFailed  { get { return T("打开失败：", "Failed to open: "); } }
        public static string MsgRegistered   { get { return T("已注册开机启动与每日换图任务。", "Startup entry and daily task registered."); } }
        public static string FavKeptN(int n) { return _en ? ("· Favorites kept (" + n + ")") : ("· 收藏夹保留（" + n + " 张）"); }
        public static string FavGone         { get { return T("· 收藏夹已移入回收站", "· Favorites moved to Recycle Bin"); } }
        public static string FavEmpty        { get { return T("· 收藏夹为空", "· Favorites empty"); } }
        public static string CacheClearedN(int n) { return _en ? ("· Cache cleared (" + n + " items, current kept)") : ("· 壁纸缓存已清理（" + n + " 张，当前壁纸保留）"); }
        public static string CacheClean      { get { return T("· 壁纸缓存无需清理", "· Cache already clean"); } }
        public static string ArchKeptN(int n) { return _en ? ("· Archived images kept (" + n + ")\r\n") : ("· 归档壁纸保留（" + n + " 张）\r\n"); }
        public static string ArchGoneLine    { get { return T("· 归档壁纸已移入回收站\r\n", "· Archived images moved to Recycle Bin\r\n"); } }
        public static string ArchNoneLine    { get { return T("· 归档里没有图片\r\n", "· No archived images\r\n"); } }
        public static string WarnTaskFail    { get { return T("注册计划任务失败（代码 ", "Failed to register scheduled task (code "); } }
        public static string WarnTaskFail2   { get { return T("）。\r\n每日自动换图将不可用，但手动换图仍可正常工作。", ").\r\nDaily auto-change will not work, but manual change still does."); } }
        public static string WarnTaskEx      { get { return T("注册计划任务异常：", "Scheduled task error: "); } }
        public static string WarnStartupFail { get { return T("设置开机启动失败：", "Failed to set startup: "); } }
        public static string TaskDesc        { get { return T(" 每日必应壁纸", " daily wallpaper"); } }
        public static string TooltipPaused   { get { return T(" · 自动换图已暂停", " · auto-change paused"); } }
        public static string TooltipReady    { get { return T("就绪", "ready"); } }

        public static string SetupNote1   { get { return T("程序会把自己和配套文件释放到该目录，并注册开机启动、每日换图任务，\r\n", "The program will copy itself and its support files here, register startup and a daily task,\r\n"); } }
        public static string SetupNote2   { get { return T("同时在「设置 → 应用」里登记卸载条目。\r\n\r\n", "and add an uninstall entry under Settings → Apps.\r\n\r\n"); } }
        public static string SetupNote3   { get { return T("壁纸缓存固定放在安装目录下的 cache 子文件夹，日志在 %TEMP%。\r\n\r\n", "Wallpaper cache lives in the cache subfolder; logs go to %TEMP%.\r\n\r\n"); } }
        public static string SetupNote4   { get { return T("「绿色运行」= 不安装，直接在当前文件夹展开使用。", "Run portable = no install; unpack and use in place."); } }
        public static string WarnNoPath   { get { return T("请填写安装路径。", "Please enter an install path."); } }
        public static string WarnBadDir   { get { return T("目录不可用：", "Folder unavailable: "); } }
        public static string BtnInstallTx { get { return T("安装", "Install"); } }
        public static string BtnPortable  { get { return T("绿色运行", "Run portable"); } }
        public static string ArchNoteAlt  { get { return T("归档图是主动从历史里挑的，保留后可用「选日期」重新设为壁纸。", "Archived images were hand-picked; keep them to reuse via Pick a date."); } }

        public static string AboutArchLine(bool on, int months)
        {
            return _en
                ? ("Archive   Zhu-junwei since 2016, niumoo fallback (" + (on ? "on" : "off") + ", " + months + " months indexed)")
                : ("历史归档  Zhu-junwei（2016 起）+ niumoo 兜底（" + (on ? "已启用" : "已关闭") + "，索引 " + months + " 个月份）");
        }
        public static string AboutOrigLine   { get { return T("原图      www.bing.com/th?id=…_UHD.jpg（最高 5445×3063）", "Original  www.bing.com/th?id=…_UHD.jpg (up to 5445×3063)"); } }
        public static string AboutBackupLine { get { return T("兜底      bingwallpaperimages.microsoft.com（3840×2400）", "Fallback  bingwallpaperimages.microsoft.com (3840×2400)"); } }
        public static string AboutCopyLine   { get { return T("版权      © Microsoft Bing，仅供个人使用", "Copyright © Microsoft Bing, personal use only"); } }
        public static string AboutTrayComp   { get { return T("托盘与安装器", "tray & installer"); } }
        public static string AboutScriptComp { get { return T("核心脚本", "core script"); } }
        public static string AboutTaskLbl    { get { return T("· 计划任务  ", "· Task        "); } }
        public static string AboutStartupLbl { get { return T("· 开机自启  ", "· Startup     "); } }
        public static string AboutAutoLbl    { get { return T("· 自动换图  ", "· Auto-change "); } }
        public static string AboutFooter     { get { return T("卸载走菜单「关于与卸载 → 卸载」，可选保留收藏。", "Uninstall via About & uninstall → Uninstall; you may keep favorites."); } }
        public static string AboutProgramDir { get { return T("程序  ", "Program  "); } }
        public static string AboutLogDir     { get { return T("日志  ", "Log      "); } }

        // 信息窗口的组合
        public static string ArchInfoPrefix2 { get { return _en ? "  " : "　"; } }
        public static string CleanArchN2(int n) { return _en ? ("Clear history archive (" + n + ")") : ("清理历史归档（" + n + " 张）"); }

        // ---------------- 参数化组合
        public static string CurrentWithDate(string title, string date)
        {
            return _en ? ("Current: " + title + " (" + date + ")") : ("当前：" + title + "（" + date + "）");
        }
        public static string CurrentReadable(string readable)
        {
            return _en ? ("Wallpaper of " + readable) : ("当前：" + readable + " 的壁纸");
        }
        public static string CurrentNone()
        {
            return _en ? "Current: nothing yet" : "当前：还没有记录";
        }
        public static string LockPrefix()
        {
            return _en ? "🔒 paused · " : "🔒 自动已暂停 · ";
        }
        public static string FavHeaderN(int n)      { return FavoritesHeader + n + CountTail; }
        public static string RecentHeaderN(int n)   { return RecentHeader + n + CountTail; }
        public static string ArchiveHeaderN(int n)  { return ArchiveHeader + n + CountTail; }
        public static string MoreRest(int n)        { return _en ? (n + " more…") : ("还有 " + n + " 张…"); }
        public static string RandomFavN(int n)      { return _en ? ("Random favorite (" + n + ")") : ("随机一张收藏（" + n + " 张）"); }
        public static string RandomCacheN(int n)    { return _en ? ("Random (favorites empty, cache " + n + ")") : ("随机一张（收藏为空，用缓存 " + n + " 张）"); }
        public static string RandomEmpty()          { return _en ? "Random (nothing available)" : "随机一张（没有可选的图）"; }
        public static string TaskNextAt(string next){ return TaskNormal + TaskNext + next; }
        public static string CleanArchN(int n)      { return _en ? ("Clear history archive (" + n + ")") : ("清理历史归档（" + n + " 张）"); }
        public static string AutoCleanDays(int d)   { return (AutoCleanOn) + (_en ? (" (" + d + " days)") : ("（" + d + " 天）")); }
        public static string KeepFavN(int n)        { return DlKeepFav + n + (_en ? ")" : " 张）"); }
        public static string KeepArchN(int n)       { return DlKeepArch + n + (_en ? ")" : " 张）"); }
        public static string CacheLineN(int n, double mb) { return DlCachePrefix + n + (_en ? (" items, " + mb.ToString("0.0") + " MB") : (" 张，" + mb.ToString("0.0") + " MB")) + DlCacheSuffix; }
        public static string BalloonLockedHint()    { return LockedHint; }

        // 格式不正确的日期兜底：中文用「2026年11月18日」，英文用「Nov 18, 2026」
        public static string FormatDate(string yyyymmdd)
        {
            if (string.IsNullOrEmpty(yyyymmdd) || yyyymmdd.Length != 8) { return yyyymmdd; }
            try
            {
                int y = Convert.ToInt32(yyyymmdd.Substring(0, 4));
                int m = Convert.ToInt32(yyyymmdd.Substring(4, 2));
                int d = Convert.ToInt32(yyyymmdd.Substring(6, 2));
                if (_en)
                {
                    DateTime dt = new DateTime(y, m, d);
                    return dt.ToString("MMM d, yyyy", System.Globalization.CultureInfo.InvariantCulture);
                }
                return y.ToString() + "年" + m.ToString() + "月" + d.ToString() + "日";
            }
            catch { return yyyymmdd; }
        }

        // 关于正文用到的固定串
        public static string AboutTitleLine(string name, string ver) { return name + "  v" + ver; }
        public static string AboutArchState(bool on, int months)
        {
            return _en
                ? ("Archive   Zhu-junwei since 2016, niumoo fallback (" + (on ? "enabled" : "disabled") + ", " + months + " months indexed)")
                : ("历史归档  Zhu-junwei（2016 起）+ niumoo 兜底（" + (on ? "已启用" : "已关闭") + "，索引 " + months + " 个月份）");
        }
        public static string AboutStore(int cCount, double cMb, int fCount, double fMb, int aCount, double aMb)
        {
            if (_en)
            {
                return "Cache    " + cCount + " items / " + cMb.ToString("0.0") + " MB (last 30 days, auto-cleaned)\r\n" +
                       "Favs     " + fCount + " items / " + fMb.ToString("0.0") + " MB (kept forever)\r\n" +
                       "Archive  " + aCount + " items / " + aMb.ToString("0.0") + " MB (manually fetched history)";
            }
            return "缓存  " + cCount + " 张 / " + cMb.ToString("0.0") + " MB（最近 30 天，自动清理）\r\n" +
                   "收藏  " + fCount + " 张 / " + fMb.ToString("0.0") + " MB（永久保留）\r\n" +
                   "归档  " + aCount + " 张 / " + aMb.ToString("0.0") + " MB（主动下载的历史图）";
        }
    }

    internal static class Program
    {
        // ---------------------------------------------------------------- 常量

        internal const string AppName     = "必应壁纸编年史";
        internal const string AppVersion  = "3.1.0";
        private const string TaskName    = "BingWallpaperChronicle";
        private const string RunValue    = "BingWallpaperChronicle";
        private const string RunKeyPath  = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string UninstKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\BingWallpaperChronicle";

        // 内嵌资源：首次运行或分发给他人时自举释放
        private static readonly string[] PayloadFiles = { "bing-wallpaper.ps1", "bing.ico", "bing-busy.ico" };

        private static string root;          // 程序所在目录（绿色模式）
        private static string scriptPath;
        private static string statePath;
        private static string indexPath;
        private static string cacheDir;
        private static string lockPath;
        private static string logPath;
        private static string iconPath;
        private static string busyIconPath;
        private static string configPath;
        private static string favDir;
        private static string favIndexPath;
        private static string archiveDir;
        internal static string ArchiveDirPath { get { return archiveDir; } }
        internal static string ScriptPathPublic { get { return scriptPath; } }
        internal static string ArchiveImgPath { get { return Path.Combine(archiveDir, "images"); } }
        internal static string ArchiveDirPath2 { get { return archiveDir; } }
        internal static string CacheDirPath { get { return cacheDir; } }
        internal static string FavDirPath { get { return favDir; } }

        internal static Dictionary<string, object> ReadPinnedPublic()
        {
            try
            {
                string f = Path.Combine(cacheDir, "pinned.json");
                return ReadJson(f);
            }
            catch { return null; }
        }

        internal static string CurrentWallpaperPath()
        {
            try
            {
                object o = Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "Wallpaper", "");
                return o as string ?? "";
            }
            catch { return ""; }
        }
        private static string archiveImgDir;

        private static NotifyIcon tray;
        private static ContextMenuStrip menu;
        private static Icon iconNormal;
        private static Icon iconBusy;
        private static Form anchor;
        private static volatile bool busy;
        private static string lastSyncedActual = "";
        private static bool refreshing = false;
        private static bool keepMenuOpenAfterClick = false;
        private static bool themeApplied = false;
        private static bool lastAppliedDark = false;
        private static System.Windows.Forms.Timer lockWatch;

        private static void ResolvePaths()
        {
            string r = Path.GetDirectoryName(Application.ExecutablePath);
            if (string.IsNullOrEmpty(r)) { r = @"C:\"; }
            ResolvePathsFromRoot(r);
        }

        private static void ResolvePathsFromRoot(string r)
        {
            root = r;
            scriptPath   = Path.Combine(root, "bing-wallpaper.ps1");
            cacheDir     = Path.Combine(root, "cache");
            statePath    = Path.Combine(root, "state.json");
            indexPath    = Path.Combine(cacheDir, "index.json");
            lockPath     = Path.Combine(cacheDir, ".running");
            iconPath     = Path.Combine(root, "bing.ico");
            busyIconPath = Path.Combine(root, "bing-busy.ico");
            configPath   = Path.Combine(root, "config.json");
            favDir       = Path.Combine(root, "favorites");
            favIndexPath = Path.Combine(favDir, "index.json");
            archiveDir   = Path.Combine(root, "archive");
            archiveImgDir = Path.Combine(archiveDir, "images");
            logPath      = Path.Combine(Path.GetTempPath(), "BingWallpaperLite.log");
        }

        // ---------------------------------------------------------------- 配置读写

        private static Dictionary<string, object> ReadConfig()
        {
            Dictionary<string, object> c = ReadJson(configPath);
            if (c == null) { c = new Dictionary<string, object>(); }
            return c;
        }

        private static bool GetCfgBool(Dictionary<string, object> c, string key, bool dflt)
        {
            if (c == null || !c.ContainsKey(key) || c[key] == null) { return dflt; }
            object v = c[key];
            if (v is bool) { return (bool)v; }
            string s = v.ToString().Trim().ToLowerInvariant();
            if (s == "true" || s == "1" || s == "yes") { return true; }
            if (s == "false" || s == "0" || s == "no") { return false; }
            return dflt;
        }

        private static int GetCfgInt(Dictionary<string, object> c, string key, int dflt)
        {
            if (c == null || !c.ContainsKey(key) || c[key] == null) { return dflt; }
            int n;
            if (int.TryParse(c[key].ToString(), out n)) { return n; }
            return dflt;
        }

        private static string GetCfgStr(Dictionary<string, object> c, string key, string dflt)
        {
            if (c == null || !c.ContainsKey(key) || c[key] == null) { return dflt; }
            string s = c[key].ToString().Trim();
            return s.Length == 0 ? dflt : s;
        }

        private static void SetCfg(Dictionary<string, object> c, string key, object value)
        {
            c[key] = value;
        }

        private static void WriteConfig(Dictionary<string, object> c)
        {
            try
            {
                JavaScriptSerializer ser = new JavaScriptSerializer();
                string json = ser.Serialize(c);
                json = UnescapeUnicode(json);
                File.WriteAllText(configPath, json, new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                MessageBox.Show(S.WarnWriteCfg + ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>JavaScriptSerializer 会把中文转成 \uXXXX，写回前还原。</summary>
        private static string UnescapeUnicode(string s)
        {
            if (string.IsNullOrEmpty(s)) { return s; }
            return System.Text.RegularExpressions.Regex.Replace(s, @"\\u([0-9a-fA-F]{4})",
                delegate(System.Text.RegularExpressions.Match m)
                {
                    return ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString();
                });
        }

        /// <summary>把某个布尔配置项改掉并落盘。</summary>
        private static void ToggleConfigBool(string key, bool value)
        {
            Dictionary<string, object> c = ReadConfig();
            SetCfg(c, key, value);
            WriteConfig(c);
        }

        /// <summary>首次运行：按主屏宽度决定分辨率档位，写进 config.json，之后不再改。</summary>
        private static string DetectResolutionOnce()
        {
            Dictionary<string, object> c = ReadConfig();
            string cur = "";
            if (c.ContainsKey("resolution") && c["resolution"] != null) { cur = c["resolution"].ToString().Trim(); }
            if (cur.Length > 0) { return cur; }

            // 壁纸标题语言跟随界面语言（界面英文时标题也用英文）
            if (!c.ContainsKey("titleLanguage"))
            {
                SetCfg(c, "titleLanguage", S.IsEn ? "en" : "zh");
            }

            int w = 1920;
            try
            {
                Rectangle b = Screen.PrimaryScreen.Bounds;
                w = Math.Max(b.Width, b.Height);
            }
            catch { }

            string r = (w >= 2560) ? "UHD" : "1920x1080";
            SetCfg(c, "resolution", r);
            if (!c.ContainsKey("autoClean")) { SetCfg(c, "autoClean", true); }
            if (!c.ContainsKey("locked")) { SetCfg(c, "locked", false); }

            // 超宽屏（比 2:1 更宽）用「适应」保留完整画面，常规屏用「填充」
            if (!c.ContainsKey("wallpaperStyle"))
            {
                string style = "Fill";
                try
                {
                    Rectangle b2 = Screen.PrimaryScreen.Bounds;
                    double ratio = (double)Math.Max(b2.Width, b2.Height) / Math.Min(b2.Width, b2.Height);
                    if (ratio > 2.0) { style = "Fit"; }
                }
                catch { }
                SetCfg(c, "wallpaperStyle", style);
            }

            WriteConfig(c);
            return r;
        }

        private static string InstallTarget
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "BingWallpaperLite");
            }
        }

        /// <summary>目录里已经有脚本 → 绿色版布局，直接跑。</summary>
        private static bool IsPortableLayout()
        {
            return File.Exists(Path.Combine(root, "bing-wallpaper.ps1"));
        }

        /// <summary>
        /// 单文件首次运行时的安装向导。
        /// 返回 true = 在当前目录继续跑；false = 已跳到别处或用户取消。
        /// </summary>
        private static bool RunSetup()
        {
            using (SetupForm f = new SetupForm(InstallTarget))
            {
                if (f.ShowDialog() != DialogResult.OK) { return false; }

                if (f.PortableMode)
                {
                    EnsurePayload(false);
                    return true;
                }

                return InstallTo(f.SelectedPath);
            }
        }

        /// <summary>把自身与内嵌资源释放到指定目录并启动，返回 false 表示本进程应退出。</summary>
        private static bool InstallTo(string target)
        {
            target = target.TrimEnd('\\');
            if (string.Equals(target, root.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            {
                // 选的就是当前目录，等于就地展开
                EnsurePayload(true);
                return true;
            }

            try { Directory.CreateDirectory(target); }
            catch (Exception ex)
            {
                MessageBox.Show(S.WarnMkDir + ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            string destExe = Path.Combine(target, Path.GetFileName(Application.ExecutablePath));
            try
            {
                File.Copy(Application.ExecutablePath, destExe, true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(S.WarnCopyExe + ex.Message + "\r\n\r\n" + S.WarnCopyHint,
                    AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            string saved = root;
            ResolvePathsFromRoot(target);
            bool ok = EnsurePayload(true);
            root = saved;

            if (!ok)
            {
                MessageBox.Show(S.WarnPayload, AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            // 结束可能存在的旧实例，避免单实例互斥把新装的挡在门外
            int me = Process.GetCurrentProcess().Id;
            try
            {
                foreach (Process other in Process.GetProcessesByName("BingWallpaperLite"))
                {
                    if (other.Id == me) { continue; }
                    try { other.Kill(); other.WaitForExit(3000); } catch { }
                }
            }
            catch { }
            Thread.Sleep(800);

            try { Process.Start(destExe); }
            catch (Exception ex)
            {
                MessageBox.Show(S.WarnLaunch + ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            return false;
        }

        private static string PowerShellExe
        {
            get
            {
                string p = @"C:\Program Files\PowerShell\7\pwsh.exe";
                if (File.Exists(p)) { return p; }
                return @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe";
            }
        }

        // ---------------------------------------------------------------- 自举释放

        /// <summary>
        /// 把内嵌的脚本与图标释放到程序目录；已存在则跳过（绿色模式优先用外部文件）。
        /// 返回 false 表示目录不可写且无法释放。
        /// </summary>
        private static bool EnsurePayload(bool force)
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            bool ok = true;

            foreach (string f in PayloadFiles)
            {
                string dest = Path.Combine(root, f);
                if (!force && File.Exists(dest)) { continue; }
                try
                {
                    using (Stream s = asm.GetManifestResourceStream(f))
                    {
                        if (s == null) { continue; }
                        using (FileStream fs = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            byte[] buf = new byte[8192];
                            int n;
                            while ((n = s.Read(buf, 0, buf.Length)) > 0) { fs.Write(buf, 0, n); }
                        }
                    }
                }
                catch
                {
                    ok = false;
                }
            }

            // config.json 只在缺失时释放，绝不覆盖用户配置
            string cfg = Path.Combine(root, "config.json");
            if (!File.Exists(cfg))
            {
                try
                {
                    using (Stream s = asm.GetManifestResourceStream("config.json"))
                    {
                        if (s != null)
                        {
                            using (FileStream fs = new FileStream(cfg, FileMode.Create, FileAccess.Write, FileShare.None))
                            {
                                byte[] buf = new byte[4096];
                                int n;
                                while ((n = s.Read(buf, 0, buf.Length)) > 0) { fs.Write(buf, 0, n); }
                            }
                        }
                    }
                }
                catch { }
            }

            return ok;
        }

        // ---------------------------------------------------------------- 入口

        [STAThread]
        private static void Main(string[] args)
        {
            ResolvePaths();

            // 语言：config 的 uiLanguage 优先；auto 时按系统 UI 语言（中文→中文，其它→英文）
            bool useEn = false;
            try
            {
                Dictionary<string, object> c0 = ReadConfig();
                string ui = (c0 != null && c0.ContainsKey("uiLanguage") && c0["uiLanguage"] != null)
                    ? c0["uiLanguage"].ToString().Trim().ToLowerInvariant() : "auto";
                if (ui == "en") { useEn = true; }
                else if (ui == "zh") { useEn = false; }
                else
                {
                    string lang = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                    useEn = !string.Equals(lang, "zh", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch { useEn = false; }
            S.Init(useEn);

            string arg0 = args.Length > 0 ? args[0].ToLowerInvariant() : "";

            // 先确保配套文件就位（分发包只有一个 exe 时靠这里自举）
            if (arg0 == "--extract" || arg0 == "/extract")
            {
                Application.EnableVisualStyles();
                bool okx = EnsurePayload(true);
                MessageBox.Show(
                    okx ? (S.ExtractedTo + "\r\n" + root) : (S.ExtractFailed + "\r\n" + root),
                    AppName, MessageBoxButtons.OK, okx ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                return;
            }

            // 单文件首次运行：既不是绿色布局、也不在安装目录 → 走安装向导
            if (!IsPortableLayout() &&
                !string.Equals(root.TrimEnd('\\'),
                    InstallTarget.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                if (!RunSetup()) { return; }
            }

            bool payloadOk = EnsurePayload(false);
            if (!payloadOk)
            {
                MessageBox.Show(
                    S.WarnNoFiles + "\r\n" + root +
                    "\r\n\r\n" + S.WarnNoFiles2,
                    AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (arg0 == "--run" || arg0 == "/run")
            {
                // 计划任务入口：无控制台宿主，内部隐藏启动 pwsh，全程不闪窗口
                string tail = "";
                if (args.Length > 1)
                {
                    tail = string.Join(" ", args, 1, args.Length - 1);
                }
                int rc = RunScriptBlocking(tail);
                Environment.Exit(rc);
            }

            if (arg0 == "--about" || arg0 == "/about")
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                ShowAbout();
                return;
            }

            if (arg0 == "--install" || arg0 == "/install")
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                DoInstall(true);
                return;
            }
            if (arg0 == "--uninstall" || arg0 == "/uninstall")
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                DoUninstall();
                return;
            }

            bool created = false;
            using (Mutex mtx = new Mutex(true, "BingWallpaperLite_SingleInstance", out created))
            {
                if (!created) { return; }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                try
                {
                    anchor = new Form();
                    anchor.ShowInTaskbar = false;
                    anchor.WindowState = FormWindowState.Minimized;
                    anchor.FormBorderStyle = FormBorderStyle.None;
                    anchor.Opacity = 0;
                    IntPtr forceHandle = anchor.Handle;

                    Thread.Sleep(3000);

                    Size sz = SystemInformation.SmallIconSize;
                    iconNormal = LoadIcon(iconPath, sz);
                    iconBusy = LoadIcon(busyIconPath, sz);

                    menu = BuildMenu();
                    tray = new NotifyIcon();
                    tray.Icon = iconNormal;
                    tray.Text = AppName;
                    tray.Visible = true;
                    tray.MouseUp += TrayMouseUp;           // 左键右键都弹菜单
                    tray.MouseDown += delegate { };        // 占位（保留绑定避免后续空引用）

                    bool firstRun = !File.Exists(statePath);

                    try { RefreshAll(); } catch { }
                    try { DetectResolutionOnce(); } catch { }
                    // 注册延后到消息循环启动后执行，避免阻塞启动
                    try { RefreshAll(); } catch { }

                    // 每秒校正一次忙碌图标：脚本结束、锁文件消失后自动恢复，不依赖回调
                    lockWatch = new System.Windows.Forms.Timer();
                    lockWatch.Interval = 1000;
                    lockWatch.Tick += delegate
                    {
                        try
                        {
                bool running = File.Exists(lockPath);
                            if (busy != running) { SetBusy(running); }
                        }
                        catch { }
                    };
                    lockWatch.Start();

                    if (firstRun)
                    {
                        // 全新安装：立刻换一张，别让人对着没反应的托盘发呆
                        anchor.BeginInvoke((MethodInvoker)delegate { RunScriptEx("-Index 0", "firstrun"); });
                    }

                    System.Windows.Forms.Timer regTimer = new System.Windows.Forms.Timer();
                    regTimer.Interval = 8000;
                    regTimer.Tick += delegate
                    {
                        regTimer.Stop();
                        regTimer.Dispose();
                        try { EnsureRegistered(false); } catch { }
                    };
                    regTimer.Start();

                    Application.Run();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(S.WarnLaunch + ex.Message, AppName,
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    if (tray != null) { tray.Visible = false; tray.Dispose(); }
                }
            }
        }

        // ---------------------------------------------------------------- 菜单

        private static ContextMenuStrip BuildMenu()
        {
            ContextMenuStrip m = new ContextMenuStrip();
            m.ShowCheckMargin = false;
            m.ShowImageMargin = false;

            ToolStripMenuItem header = new ToolStripMenuItem(S.Loading);
            header.Name = "header";
            header.Enabled = false;
            m.Items.Add(header);
            ToolStripMenuItem taskState = new ToolStripMenuItem(S.TaskChecking);
            taskState.Name = "taskstate";
            taskState.Click += delegate
            {
                keepMenuOpenAfterClick = true;
                anchor.BeginInvoke((MethodInvoker)delegate { DoRepairTask(); });
            };
            m.Items.Add(taskState);

            m.Items.Add(MakeKeepOpenItem(S.PrevOlder, "-Step 1", "prev"));
            m.Items.Add(MakeKeepOpenItem(S.NextNewer, "-Step -1", "next"));

            m.Items.Add(MakeItem(S.WhereIsThis, delegate { ShowInfo(); }));

            ToolStripMenuItem favToggle = new ToolStripMenuItem(S.FavOff);
            favToggle.Name = "favtoggle";
            favToggle.CheckOnClick = false;
            favToggle.Click += delegate
            {
                keepMenuOpenAfterClick = true;
                bool isFav = IsCurrentFavorited();
                string args = isFav ? "-Unfavorite" : "-Favorite";
                string mode = isFav ? "unfav" : "fav";
                anchor.BeginInvoke((MethodInvoker)delegate { RunScriptEx(args, mode); });
            };
            m.Items.Add(favToggle);
            m.Items.Add(new ToolStripSeparator());

            m.Items.Add(MakeKeepOpenItem(S.ChangeToday, "-Index 0"));
            ToolStripMenuItem yearsMenu = new ToolStripMenuItem(S.YearsAgo);
            yearsMenu.Name = "years";
            m.Items.Add(yearsMenu);


            ToolStripMenuItem cacheMenu = new ToolStripMenuItem(S.Library);
            cacheMenu.Name = "library";
            m.Items.Add(cacheMenu);


            ToolStripMenuItem randItem = new ToolStripMenuItem(S.RandomFav);
            randItem.Name = "randompick";
            randItem.Click += delegate
            {
                keepMenuOpenAfterClick = true;
                anchor.BeginInvoke((MethodInvoker)delegate
                {
                    RunScriptEx("-Random", "random");
                // [排查] 子调用整块注释
                {
                    RefreshArchiveCheck();
                }
                });
            };
            m.Items.Add(randItem);

            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(MakeItem(S.PickDate, delegate { DoPickDate(); }));




            m.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem lockItem = new ToolStripMenuItem(S.LockOff);
            lockItem.Name = "lock";
            lockItem.CheckOnClick = false;
            lockItem.Click += delegate
            {
                keepMenuOpenAfterClick = true;
                Dictionary<string, object> c = ReadConfig();
                bool next = !GetCfgBool(c, "locked", false);
                SetCfg(c, "locked", next);
                WriteConfig(c);

                if (next)
                {
                    Balloon(S.BLocked, S.LockedHint);
                }
                else
                {
                    anchor.BeginInvoke((MethodInvoker)delegate { RunScript("-Index 0"); });
                }
            };
            m.Items.Add(lockItem);


            ToolStripMenuItem notifyItem = new ToolStripMenuItem(S.NotifyOn);
            notifyItem.Name = "notify";
            notifyItem.CheckOnClick = false;
            notifyItem.Click += delegate
            {
                keepMenuOpenAfterClick = true;
                Dictionary<string, object> c = ReadConfig();
                SetCfg(c, "notify", !GetCfgBool(c, "notify", false));
                WriteConfig(c);
            };
            m.Items.Add(notifyItem);


            ToolStripMenuItem startup = new ToolStripMenuItem(S.StartupOff);
            startup.Name = "startup";
            startup.CheckOnClick = false;
            startup.Click += delegate
            {
                keepMenuOpenAfterClick = true;
                SetStartup(!IsStartupEnabled());
            };
            m.Items.Add(startup);

            ToolStripMenuItem timeMenu = new ToolStripMenuItem(S.UpdateTimeMenu);
            timeMenu.Name = "updatetime";
            foreach (string preset in new string[] { "00:10", "06:00", "08:00", "12:00", "18:00" })
            {
                ToolStripMenuItem it = new ToolStripMenuItem(preset);
                it.Tag = preset;
                it.Click += delegate
                {
                    keepMenuOpenAfterClick = true;
                    Dictionary<string, object> c = ReadConfig();
                    SetCfg(c, "updateTime", preset);
                    WriteConfig(c);
                    EnsureRegistered(false);
                    Balloon(S.BRepaired, S.IsEn
                        ? ("Daily update is now " + preset)
                        : ("每日换图时间已改为 " + preset));
                };
                timeMenu.DropDownItems.Add(it);
            }
            m.Items.Add(timeMenu);

            m.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem cleanMenu = new ToolStripMenuItem(S.CleanMenu);
            cleanMenu.Name = "clean";

            ToolStripMenuItem autoCleanItem = new ToolStripMenuItem(S.AutoCleanOn);
            autoCleanItem.Name = "autoclean";
            autoCleanItem.CheckOnClick = false;
            autoCleanItem.Click += delegate
            {
                keepMenuOpenAfterClick = true;
                Dictionary<string, object> c = ReadConfig();
                SetCfg(c, "autoClean", !GetCfgBool(c, "autoClean", true));
                WriteConfig(c);
            };
            cleanMenu.DropDownItems.Add(autoCleanItem);
            cleanMenu.DropDownItems.Add(new ToolStripSeparator());
            cleanMenu.DropDownItems.Add(MakeItem(S.Clean7, delegate { RunScript("-Clean -Keep 7"); }));
            cleanMenu.DropDownItems.Add(MakeItem(S.Clean30, delegate { RunScript("-Clean -Keep 30"); }));
            cleanMenu.DropDownItems.Add(new ToolStripSeparator());
            ToolStripMenuItem cleanArch = new ToolStripMenuItem(S.CleanArchive);
            cleanArch.Name = "cleanarch";
            cleanArch.Click += delegate
            {
                keepMenuOpenAfterClick = true;
                anchor.BeginInvoke((MethodInvoker)delegate { ConfirmCleanArchive(); });
            };
            cleanMenu.DropDownItems.Add(cleanArch);

            ToolStripMenuItem cleanArchIdx = new ToolStripMenuItem(S.CleanArchiveIndexMenu);
            cleanArchIdx.Name = "cleanarchidx";
            cleanArchIdx.Click += delegate
            {
                keepMenuOpenAfterClick = true;
                anchor.BeginInvoke((MethodInvoker)delegate { ConfirmCleanArchiveIndex(); });
            };
            cleanMenu.DropDownItems.Add(cleanArchIdx);

            cleanMenu.DropDownItems.Add(MakeItem(S.CleanAll, delegate { ConfirmCleanAll(); }));
            m.Items.Add(cleanMenu);

            ToolStripMenuItem archiveMenu = new ToolStripMenuItem(S.ArchiveMenu);
            archiveMenu.Name = "archive";

            ToolStripMenuItem archToggle = new ToolStripMenuItem(S.ArchiveOn);
            archToggle.Name = "archtoggle";
            archToggle.CheckOnClick = false;
            archToggle.Click += delegate
            {
                keepMenuOpenAfterClick = true;
                Dictionary<string, object> c = ReadConfig();
                SetCfg(c, "archive", !GetCfgBool(c, "archive", true));
                WriteConfig(c);
            };
            archiveMenu.DropDownItems.Add(archToggle);

            archiveMenu.DropDownItems.Add(new ToolStripSeparator());

            ToolStripMenuItem archSrc1 = new ToolStripMenuItem(S.ArchSrcLine1);
            archSrc1.Name = "archsrc1";
            archSrc1.Enabled = false;
            archiveMenu.DropDownItems.Add(archSrc1);

            ToolStripMenuItem archSrc2 = new ToolStripMenuItem(S.ArchSrcLine2);
            archSrc2.Name = "archsrc2";
            archSrc2.Enabled = false;
            archiveMenu.DropDownItems.Add(archSrc2);

            ToolStripMenuItem archInfo = new ToolStripMenuItem("…");
            archInfo.Name = "archinfo";
            archInfo.Enabled = false;
            archiveMenu.DropDownItems.Add(archInfo);

            archiveMenu.DropDownItems.Add(new ToolStripSeparator());

            ToolStripMenuItem archOpen = new ToolStripMenuItem(S.OpenArchiveDir);
            archOpen.Click += delegate { OpenTarget(archiveDir); };
            archiveMenu.DropDownItems.Add(archOpen);

            ToolStripMenuItem archClear = new ToolStripMenuItem(S.ClearArchiveIdx);
            archClear.Click += delegate
            {
                keepMenuOpenAfterClick = true;
                try
                {
                    if (Directory.Exists(archiveDir))
                    {
                        foreach (string f in Directory.GetFiles(archiveDir))
                        {
                            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                                f,
                                Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                                Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                        }
                    }
                }
                catch { }
                anchor.BeginInvoke((MethodInvoker)delegate { RefreshArchiveCheck(); });
            };
            archiveMenu.DropDownItems.Add(archClear);

            m.Items.Add(archiveMenu);

            ToolStripMenuItem themeMenu = new ToolStripMenuItem(S.ThemeMenu);
            themeMenu.Name = "theme";
            themeMenu.DropDownItems.Add(MakeThemeItem(S.ThemeAuto, "auto"));
            themeMenu.DropDownItems.Add(MakeThemeItem(S.ThemeLight, "light"));
            themeMenu.DropDownItems.Add(MakeThemeItem(S.ThemeDark, "dark"));
            m.Items.Add(themeMenu);

            ToolStripMenuItem sysMenu = new ToolStripMenuItem(S.SysMenu);

            sysMenu.DropDownItems.Add(MakeItem(S.AboutItem + AppName, delegate { ShowAbout(); }));
            sysMenu.DropDownItems.Add(MakeItem(S.OpenLog, delegate { OpenTarget(logPath); }));
            sysMenu.DropDownItems.Add(new ToolStripSeparator());
            sysMenu.DropDownItems.Add(MakeItem(S.UninstallItem, delegate { DoUninstall(); }));
            m.Items.Add(sysMenu);

            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(MakeItem(S.ExitItem, delegate
            {
                if (tray != null) { tray.Visible = false; }
                Application.Exit();
            }));

            // 每次打开菜单都刷新：外部（脚本/AI）改过壁纸后能立刻看到最新状态
            m.Opening += delegate { try { RefreshHeaderOnly(); } catch { } };

            // 点击换图类菜单项后保持菜单打开，方便连续切换；点外面由系统关闭
            m.Closing += delegate(object s, ToolStripDropDownClosingEventArgs e)
            {
                if (e.CloseReason == ToolStripDropDownCloseReason.ItemClicked && keepMenuOpenAfterClick)
                {
                    e.Cancel = true;
                    keepMenuOpenAfterClick = false;
                    anchor.BeginInvoke((MethodInvoker)delegate
                    {
                    try { RefreshAll(); } catch { }
                    });
                    return;
                }
            };

            ApplyDarkTheme(m);
            return m;
        }

        // ---------------------------------------------------------------- 暗色主题

        private static bool IsSystemDark()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (k == null) { return false; }
                    object v = k.GetValue("AppsUseLightTheme");
                    if (v == null) { return false; }
                    return Convert.ToInt32(v) == 0;
                }
            }
            catch { return false; }
        }

        private static void ApplyDarkTheme(ContextMenuStrip m)
        {
            SyncTheme();
        }

        /// <summary>按配置决定当前该用深色还是浅色；auto 时看系统。</summary>
        private static bool ResolveDark()
        {
            string t = GetCfgStr(ReadConfig(), "theme", "auto").ToLowerInvariant();
            if (t == "dark") { return true; }
            if (t == "light") { return false; }
            return IsSystemDark();
        }

        /// <summary>与配置/系统主题对齐；每次打开菜单前调用，变了才重设。</summary>
        private static void SyncTheme()
        {
            if (menu == null) { return; }
            bool dark = ResolveDark();
            if (themeApplied && dark == lastAppliedDark) { return; }

            lastAppliedDark = dark;
            themeApplied = true;

            try
            {
                if (dark)
                {
                    ToolStripProfessionalRenderer r = new ToolStripProfessionalRenderer(new DarkColorTable());
                    menu.Renderer = r;
                    menu.BackColor = DarkColorTable.Bg;
                    menu.ForeColor = DarkColorTable.Text;
                    SetRendererDeep(menu.Items, r);
                    PaintItemsDark(menu.Items);
                }
                else
                {
                    menu.Renderer = null;
                    menu.BackColor = SystemColors.Control;
                    menu.ForeColor = SystemColors.ControlText;
                    SetRendererDeep(menu.Items, null);
                    PaintItemsLight(menu.Items);
                }
            }
            catch { }
        }

        private static void SetRendererDeep(ToolStripItemCollection items, ToolStripRenderer r)
        {
            foreach (ToolStripItem it in items)
            {
                ToolStripMenuItem mi = it as ToolStripMenuItem;
                if (mi == null) { continue; }
                try
                {
                    if (mi.DropDown != null) { mi.DropDown.Renderer = r; }
                }
                catch { }
                if (mi.DropDownItems.Count > 0) { SetRendererDeep(mi.DropDownItems, r); }
            }
        }

        private static void PaintItemsLight(ToolStripItemCollection items)
        {
            foreach (ToolStripItem it in items)
            {
                it.ForeColor = SystemColors.ControlText;
                ToolStripMenuItem mi = it as ToolStripMenuItem;
                if (mi != null && mi.DropDownItems.Count > 0) { PaintItemsLight(mi.DropDownItems); }
            }
        }

        private static void PaintItemsDark(ToolStripItemCollection items)
        {
            foreach (ToolStripItem it in items)
            {
                it.ForeColor = DarkColorTable.Text;
                ToolStripMenuItem mi = it as ToolStripMenuItem;
                if (mi != null && mi.DropDownItems.Count > 0) { PaintItemsDark(mi.DropDownItems); }
            }
        }

        private static ToolStripMenuItem MakeKeepOpenItem(string text, string arguments)
        {
            return MakeKeepOpenItem(text, arguments, "");
        }

        private static ToolStripMenuItem MakeKeepOpenItem(string text, string arguments, string opMode)
        {
            ToolStripMenuItem it = new ToolStripMenuItem(text);
            it.Click += delegate
            {
                keepMenuOpenAfterClick = true;
                RunScriptEx(arguments, opMode);
            };
            return it;
        }

        // ---------------------------------------------------------------- 托盘点击

        /// <summary>
        /// 鼠标按下时刷新菜单内容。此时 ContextMenuStrip 还没弹出，
        /// 修改 Items 不会干扰系统的弹出流程，也不会影响"点外部关闭"。
        /// </summary>
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private static System.Windows.Forms.Timer menuWatch;

        /// <summary>左键、右键都弹菜单；内容在弹出前刷新。</summary>
        private static void TrayMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left && e.Button != MouseButtons.Right) { return; }
            try
            {
                SyncTheme();
                RefreshAll();
                if (menu.Visible) { menu.Close(); return; }
                menu.Show(Cursor.Position);
                StartMenuWatch();
            }
            catch { }
        }

        /// <summary>兜底：左键按在任何已展开菜单之外时把菜单关掉。</summary>
        private static void StartMenuWatch()
        {
            if (menuWatch == null)
            {
                menuWatch = new System.Windows.Forms.Timer();
                menuWatch.Interval = 120;
                menuWatch.Tick += MenuWatchTick;
            }
            menuWatch.Start();
        }

        private static void MenuWatchTick(object sender, EventArgs e)
        {
            try
            {
                if (!menu.Visible) { menuWatch.Stop(); return; }
                if ((GetAsyncKeyState(0x01) & 0x8000) == 0) { return; }
                if (IsInsideAnyOpenMenu(Cursor.Position)) { return; }
                menu.Close();
            }
            catch { }
        }

        private static bool IsInsideAnyOpenMenu(Point p)
        {
            try
            {
                if (menu.Visible && menu.Bounds.Contains(p)) { return true; }
                return IsInsideSubMenu(menu.Items, p);
            }
            catch { return false; }
        }

        private static bool IsInsideSubMenu(ToolStripItemCollection items, Point p)
        {
            foreach (ToolStripItem it in items)
            {
                ToolStripMenuItem mi = it as ToolStripMenuItem;
                if (mi == null) { continue; }
                try
                {
                    if (mi.DropDown != null && mi.DropDown.Visible && mi.DropDown.Bounds.Contains(p)) { return true; }
                }
                catch { }
                if (mi.DropDownItems.Count > 0 && IsInsideSubMenu(mi.DropDownItems, p)) { return true; }
            }
            return false;
        }

        private static ToolStripMenuItem MakeItem(string text, EventHandler handler)
        {
            ToolStripMenuItem it = new ToolStripMenuItem(text);
            it.Click += handler;
            return it;
        }

        private static void ConfirmCleanAll()
        {
            if (busy) { return; }
            using (CleanForm f = new CleanForm(CleanForm.Target.Cache))
            {
                if (f.ShowDialog(anchor) != DialogResult.OK) { return; }
                string mode = f.Permanent ? "permanent" : "recycle";
                RunScript("-CleanAll -CleanMode " + mode);
            }
        }

        private static void ConfirmCleanArchive()
        {
            if (busy) { return; }
            using (CleanForm f = new CleanForm(CleanForm.Target.Archive))
            {
                if (f.ShowDialog(anchor) != DialogResult.OK) { return; }
                string mode = f.Permanent ? "permanent" : "recycle";
                RunScript("-CleanArchive -CleanMode " + mode);
            }
        }

        private static void ConfirmCleanArchiveIndex()
        {
            if (busy) { return; }
            using (CleanForm f = new CleanForm(CleanForm.Target.ArchiveIndex))
            {
                if (f.ShowDialog(anchor) != DialogResult.OK) { return; }
                RunScript("-CleanArchiveIndex");
            }
        }

        private static void ConfirmCleanFavorites()
        {
            if (busy) { return; }
            using (CleanForm f = new CleanForm(CleanForm.Target.Favorites))
            {
                if (f.ShowDialog(anchor) != DialogResult.OK) { return; }
                string mode = f.Permanent ? "permanent" : "recycle";
                RunScript("-CleanFavorites -CleanMode " + mode);
            }
        }


        /// <summary>菜单打开时的轻量刷新：只更新顶部文字，不重建菜单项（重建会触发递归导致栈溢出）。</summary>
        private static void RefreshHeaderOnly()
        {
            try
            {
                Dictionary<string, object> st = ReadJson(statePath);
                string date = GetStr(st, "date");
                string title = GetStr(st, "title");
                if (string.IsNullOrEmpty(title) && date.Length > 0) { title = LookupTitle(date); }

                ToolStripMenuItem header = menu.Items["header"] as ToolStripMenuItem;
                if (header != null)
                {
                    if (string.IsNullOrEmpty(title)) { header.Text = AppName; }
                    else if (string.IsNullOrEmpty(date)) { header.Text = title; }
                    else { header.Text = title + "（" + date + "）"; }
                }

                ToolStripMenuItem favToggle = menu.Items["favtoggle"] as ToolStripMenuItem;
                if (favToggle != null)
                {
                    favToggle.Text = IsCurrentFavorited() ? S.FavOn : S.FavOff;
                }
            }
            catch { }
        }

        // ---------------------------------------------------------------- 刷新编排
        //
        // 分三层，每层可单独调用：
        //   入口 RefreshAll      只做防重入与调度
        //   第 2 层              读状态，产出 showDate/showTitle/isLocked
        //   第 3 层              各部件独立更新，互不依赖
        //
        // 这样某层出问题时能立刻定位，不用像之前那样在一个 60 行函数里猜。

        /// <summary>刷新编排入口：只负责防重入与调度，不做具体工作。</summary>
        private static void RefreshAll()
        {
            if (refreshing) { return; }
            refreshing = true;
            try
            {
                bool running = File.Exists(lockPath);
                SetBusy(running);

                Dictionary<string, object> cfg = ReadConfig();
                Dictionary<string, object> state = ReadJson(statePath);
                bool isLocked = GetCfgBool(cfg, "locked", false);

                string showDate;
                string showTitle;
                ResolveCurrent(state, isLocked, out showDate, out showTitle);

                RefreshTrayTip(running, isLocked, showDate, showTitle, state);
                RefreshHeader(showDate, showTitle, isLocked);
                RefreshMenuItems(state, showDate);
                SyncExternalWallpaper();
            }
            catch { }
            finally { refreshing = false; }
        }

        /// <summary>从 state 与锁定状态算出「要显示的日期」和「要显示的标题」。</summary>
        private static void ResolveCurrent(Dictionary<string, object> state, bool isLocked,
                                           out string showDate, out string showTitle)
        {
            string date = GetStr(state, "date");
            string title = GetStr(state, "title");
            string appliedDate = GetStr(state, "appliedDate");
            if (appliedDate.Length == 0) { appliedDate = date; }

            showDate = isLocked ? appliedDate : date;
            showTitle = title;
            if (isLocked && appliedDate != date) { showTitle = LookupTitle(appliedDate); }
            if (string.IsNullOrEmpty(showTitle) && showDate.Length > 0) { showTitle = LookupTitle(showDate); }
        }

        /// <summary>更新托盘图标的悬停提示。</summary>
        private static void RefreshTrayTip(bool running, bool isLocked, string showDate, string showTitle,
                                           Dictionary<string, object> state)
        {
            if (tray == null || running) { return; }

            if (isLocked)
            {
                tray.Text = Clip(AppName + S.TooltipPaused, 60);
                return;
            }

            // 标题为空时显示日期，日期也没有才显示「就绪」
            string body = showTitle;
            if (string.IsNullOrEmpty(body) && showDate.Length > 0) { body = FormatDateCn(showDate); }
            tray.Text = Clip(AppName + " · " + (body.Length > 0 ? body : S.TooltipReady), 60);
        }

        /// <summary>更新菜单顶部那行「当前：xxx」。</summary>
        private static void RefreshHeader(string showDate, string showTitle, bool isLocked)
        {
            ToolStripMenuItem header = menu.Items["header"] as ToolStripMenuItem;
            if (header == null) { return; }

            string mark = isLocked ? S.LockPrefix() : "";

            if (string.IsNullOrEmpty(showTitle))
            {
                string readable = FormatDateCn(showDate);
                header.Text = mark + (readable.Length > 0 ? S.CurrentReadable(readable) : S.CurrentNone());
            }
            else
            {
                header.Text = mark + (showDate.Length > 0 ? S.CurrentWithDate(showTitle, showDate) : showTitle);
            }
        }

        /// <summary>更新所有子菜单的勾选状态与文字。每个 RefreshXxx 只碰自己的那一项。</summary>
        private static void RefreshMenuItems(Dictionary<string, object> state, string showDate)
        {
            RefreshLibraryMenu(state, showDate);
            RefreshFavoriteCheck();
            RefreshTaskState();
            RefreshYearsMenu();
            RefreshRandomCheck();
            RefreshArchiveCheck();
            RefreshStartupCheck();
            RefreshLockCheck();
            RefreshCleanCheck();
            RefreshNotifyCheck();
            RefreshThemeCheck();
        }

        private static void DoPickDate()
        {
            if (busy) { return; }
            using (CalendarForm f = new CalendarForm())
            {
                if (f.ShowDialog() != DialogResult.OK) { return; }
                string d = f.SelectedDate;
                if (string.IsNullOrEmpty(d)) { return; }
                anchor.BeginInvoke((MethodInvoker)delegate { RunScriptEx("-Date " + d, "date"); });
            }
        }

        private static Dictionary<string, object> ReadPinnedSet()
        {
            Dictionary<string, object> d = ReadJson(Path.Combine(cacheDir, "pinned.json"));
            if (d == null) { d = new Dictionary<string, object>(); }
            return d;
        }

        /// <summary>20261118 → 2026年11月18日；格式不对就原样返回。</summary>
        private static string FormatDateCn(string yyyymmdd)
        {
            if (string.IsNullOrEmpty(yyyymmdd) || yyyymmdd.Length != 8) { return yyyymmdd; }
            try
            {
                int y = Convert.ToInt32(yyyymmdd.Substring(0, 4));
                int m = Convert.ToInt32(yyyymmdd.Substring(4, 2));
                int d = Convert.ToInt32(yyyymmdd.Substring(6, 2));
                return S.FormatDate(yyyymmdd);
            }
            catch { return yyyymmdd; }
        }

        private static string LookupTitle(string date)
        {
            if (string.IsNullOrEmpty(date)) { return ""; }
            Dictionary<string, object> index = ReadJson(indexPath);
            if (index != null && index.ContainsKey(date))
            {
                Dictionary<string, object> e = index[date] as Dictionary<string, object>;
                return GetStr(e, "title");
            }
            return "";
        }

        private static string[] SafeGetFiles(string dir)
        {
            try
            {
                if (!Directory.Exists(dir)) { return new string[0]; }
                string[] f = Directory.GetFiles(dir, "*.jpg");
                Array.Sort(f, delegate(string a, string b)
                {
                    return string.Compare(Path.GetFileName(b), Path.GetFileName(a), StringComparison.Ordinal);
                });
                return f;
            }
            catch { return new string[0]; }
        }

        private static bool IsFavoritedName(string fileName)
        {
            try { return File.Exists(Path.Combine(favDir, fileName)); }
            catch { return false; }
        }

        private static void RefreshLibraryMenu(Dictionary<string, object> state, string currentDate)
        {
            ToolStripMenuItem lib = menu.Items["library"] as ToolStripMenuItem;
            if (lib == null) { return; }
            lib.DropDownItems.Clear();

            string[] favFiles = SafeGetFiles(favDir);
            string[] recentFiles = SafeGetFiles(cacheDir);
            string[] archFiles = SafeGetFiles(archiveImgDir);
            Dictionary<string, object> favIdx = ReadJson(favIndexPath);
            Dictionary<string, object> cacheIdx = ReadJson(indexPath);

            // ---- 收藏
            ToolStripMenuItem favHead = new ToolStripMenuItem(S.FavHeaderN(favFiles.Length));
            favHead.Enabled = false;
            lib.DropDownItems.Add(favHead);
            if (favFiles.Length == 0)
            {
                ToolStripMenuItem e = new ToolStripMenuItem(S.NoFavorites);
                e.Enabled = false;
                lib.DropDownItems.Add(e);
            }
            else
            {
                BuildLibPage(lib, favFiles, favIdx, currentDate, true, 0, 0);
            }

            lib.DropDownItems.Add(new ToolStripSeparator());

            // ---- 最近 30 天
            ToolStripMenuItem recHead = new ToolStripMenuItem(S.RecentHeaderN(recentFiles.Length));
            recHead.Enabled = false;
            lib.DropDownItems.Add(recHead);
            if (recentFiles.Length == 0)
            {
                ToolStripMenuItem e = new ToolStripMenuItem(S.NoRecent);
                e.Enabled = false;
                lib.DropDownItems.Add(e);
            }
            else
            {
                BuildLibPage(lib, recentFiles, cacheIdx, currentDate, false, 0, 0);
            }

            // ---- 历史归档（折叠成子菜单，避免主菜单过长）
            if (archFiles.Length > 0)
            {
                lib.DropDownItems.Add(new ToolStripSeparator());
                ToolStripMenuItem archSub = new ToolStripMenuItem(S.ArchiveHeaderN(archFiles.Length));
                BuildLibPage(archSub, archFiles, LoadArchiveIndex(), currentDate, false, 0, 0);
                archSub.DropDownItems.Add(new ToolStripSeparator());
                archSub.DropDownItems.Add(MakeItem(S.OpenArchiveImg, delegate { OpenTarget(archiveImgDir); }));
                lib.DropDownItems.Add(archSub);
            }

            lib.DropDownItems.Add(new ToolStripSeparator());
            lib.DropDownItems.Add(MakeItem(S.OpenCacheDir, delegate { OpenTarget(cacheDir); }));
            lib.DropDownItems.Add(MakeItem(S.OpenFavDir, delegate { OpenTarget(favDir); }));

            // 清空收藏：走 CleanForm 二次确认，默认进回收站
            lib.DropDownItems.Add(new ToolStripSeparator());
            lib.DropDownItems.Add(MakeItem(S.CleanFavMenu, delegate { ConfirmCleanFavorites(); }));
        }

        /// <summary>把所有 archive\*.json 的标题合并成 {date: {title}}，供壁纸库显示。</summary>
        private static Dictionary<string, object> LoadArchiveIndex()
        {
            Dictionary<string, object> all = new Dictionary<string, object>();
            try
            {
                if (!Directory.Exists(archiveDir)) { return all; }
                foreach (string f in Directory.GetFiles(archiveDir, "*.json"))
                {
                    try
                    {
                        Dictionary<string, object> m = ReadJson(f);
                        if (m == null) { continue; }
                        foreach (KeyValuePair<string, object> kv in m)
                        {
                            Dictionary<string, object> e = kv.Value as Dictionary<string, object>;
                            if (e == null) { continue; }
                            string tt = GetStr(e, "t");
                            if (tt.Length == 0) { continue; }
                            Dictionary<string, object> wrap = new Dictionary<string, object>();
                            wrap["title"] = tt;
                            all[kv.Key] = wrap;
                        }
                    }
                    catch { }
                }
            }
            catch { }
            return all;
        }

        private static void BuildLibPage(ToolStripMenuItem parent, string[] files, Dictionary<string, object> idx,
                                         string currentDate, bool isFavSection, int offset, int depth)
        {
            const int PageSize = 8;
            const int MaxDepth = 3;

            int added = 0;
            for (int i = offset; i < files.Length && added < PageSize; i++, added++)
            {
                string full = files[i];
                string fileName = Path.GetFileName(full);
                string name = Path.GetFileNameWithoutExtension(full);
                int us = name.IndexOf('_');
                string fileDate = us > 0 ? name.Substring(0, us) : name;

                string label = name;
                if (idx != null && idx.ContainsKey(fileDate))
                {
                    Dictionary<string, object> entry = idx[fileDate] as Dictionary<string, object>;
                    string t = GetStr(entry, "title");
                    if (t.Length > 0) { label = fileDate + "  " + t; }
                }

                label = (fileDate == currentDate) ? ("● " + label) : ("　 " + label);

                ToolStripMenuItem it = new ToolStripMenuItem(label);
                string captured = full;
                it.Click += delegate
                {
                    keepMenuOpenAfterClick = true;
                    RunScriptEx("-Use \"" + captured + "\"", "");
                };
                parent.DropDownItems.Add(it);
            }

            int next = offset + added;
            if (next < files.Length)
            {
                if (depth + 1 >= MaxDepth)
                {
                    ToolStripMenuItem rest = new ToolStripMenuItem(S.MoreRest(files.Length - next));
                    rest.Click += delegate { OpenTarget(isFavSection ? favDir : cacheDir); };
                    parent.DropDownItems.Add(rest);
                }
                else
                {
                    ToolStripMenuItem more = new ToolStripMenuItem(S.More);
                    BuildLibPage(more, files, idx, currentDate, isFavSection, next, depth + 1);
                    parent.DropDownItems.Add(more);
                }
            }
        }

        private static bool IsCurrentFavorited()
        {
            try
            {
                string cur = (GetItemPropertyWallpaper());
                if (string.IsNullOrEmpty(cur)) { return false; }
                string name = Path.GetFileName(cur);
                return File.Exists(Path.Combine(favDir, name));
            }
            catch { return false; }
        }

        private static string GetItemPropertyWallpaper()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", false))
                {
                    if (k == null) { return ""; }
                    object v = k.GetValue("Wallpaper");
                    return v == null ? "" : v.ToString();
                }
            }
            catch { return ""; }
        }

        /// <summary>点计划任务那一行时调用：重新写自启 + 重新注册任务，幂等。</summary>
        private static void DoRepairTask()
        {
            SetBusy(true);
            try
            {
                try { SetStartup(true); } catch { }
                try { RunSchtasks("/delete /tn \"" + TaskName + "\" /f"); } catch { }
                try { RegisterTask(); } catch { }

                anchor.BeginInvoke((MethodInvoker)delegate
                {
                    SetBusy(false);
                // [排查] 子调用整块注释
                {
                    RefreshYearsMenu();
                }
                    ToolStripMenuItem it = menu.Items["taskstate"] as ToolStripMenuItem;
                    string now = it == null ? "" : it.Text;
                    if (now.IndexOf("⚠", StringComparison.Ordinal) >= 0)
                    {
                        Balloon(S.BRepairKO, S.BRepairKOHint);
                    }
                    else
                    {
                        Balloon(S.BRepaired, S.BRepairedHint);
                    }
                });
            }
            catch
            {
                SetBusy(false);
            }
        }

        private static void RefreshTaskState()
        {
            ToolStripMenuItem it = menu.Items["taskstate"] as ToolStripMenuItem;
            if (it == null) { return; }
            try
            {
                int rc = 0;
                string xml = RunSchtasksOutput("/query /tn \"" + TaskName + "\" /xml ONE", out rc);
                if (rc != 0 || string.IsNullOrEmpty(xml) || xml.IndexOf("<Task", StringComparison.Ordinal) < 0)
                {
                    it.Text = S.TaskMissing;
                    return;
                }
                string next = "";
                int rc2 = 0;
                string info = RunSchtasksOutput("/query /tn \"" + TaskName + "\" /fo LIST /v", out rc2);
                foreach (string line in info.Split('\n'))
                {
                    if (line.IndexOf("下次运行时间", StringComparison.Ordinal) >= 0 ||
                        line.IndexOf("Next Run Time", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        int colon = line.IndexOf(':');
                        if (colon > 0)
                        {
                            string v = line.Substring(colon + 1).Trim();
                            if (v.Length > 0 && v != "N/A") { next = v; }
                        }
                    }
                }
                it.Text = next.Length > 0
                    ? (S.TaskNextAt(next))
                    : S.TaskNormal;
                it.Enabled = false;
            }
            catch
            {
                it.Text = S.TaskUnknown;
            }
        }

        private static void RefreshFavoriteCheck()
        {
            ToolStripMenuItem it = menu.Items["favtoggle"] as ToolStripMenuItem;
            if (it == null) { return; }
            it.Text = IsCurrentFavorited() ? S.FavOn : S.FavOff;
        }

        private static void RefreshStartupCheck()
        {
            ToolStripMenuItem startup = menu.Items["startup"] as ToolStripMenuItem;
            if (startup == null) { return; }
            startup.Text = IsStartupEnabled() ? S.StartupOn : S.StartupOff;
        }

        private static void RefreshLockCheck()
        {
            ToolStripMenuItem it = menu.Items["lock"] as ToolStripMenuItem;
            if (it == null) { return; }
            bool on = GetCfgBool(ReadConfig(), "locked", false);
            it.Text = on ? S.LockOn : S.LockOff;
        }

        private static void RefreshRandomCheck()
        {
            ToolStripMenuItem it = menu.Items["randompick"] as ToolStripMenuItem;
            if (it == null) { return; }
            int favN = SafeGetFiles(favDir).Length;
            int cacheN = SafeGetFiles(cacheDir).Length;

            if (favN > 0) { it.Text = S.RandomFavN(favN); }
            else if (cacheN > 0) { it.Text = S.RandomCacheN(cacheN); }
            else { it.Text = S.RandomEmpty(); }

            it.Enabled = (favN + cacheN) > 0;
        }

        private static void RefreshCleanCheck()
        {
            ToolStripMenuItem cm = menu.Items["clean"] as ToolStripMenuItem;
            if (cm == null) { return; }
            ToolStripMenuItem it = null;
            foreach (ToolStripItem x in cm.DropDownItems)
            {
                if (x.Name == "autoclean") { it = x as ToolStripMenuItem; break; }
            }
            if (it == null) { return; }
            Dictionary<string, object> c = ReadConfig();
            bool on = GetCfgBool(c, "autoClean", true);
            int days = GetCfgInt(c, "keepDays", 30);
            it.Text = (on ? S.AutoCleanOn : S.AutoCleanOff) + S.DaysPrefix + days + S.DaysSuffix;

            ToolStripMenuItem ca = null;
            foreach (ToolStripItem x in cm.DropDownItems)
            {
                if (x.Name == "cleanarch") { ca = x as ToolStripMenuItem; break; }
            }
            if (ca != null)
            {
                int n = SafeGetFiles(archiveImgDir).Length;
                ca.Text = n > 0 ? S.CleanArchN2(n) : S.CleanArchive;
                ca.Enabled = n > 0;
            }
        }

        private static void RefreshYearsMenu()
        {
            ToolStripMenuItem ym = menu.Items["years"] as ToolStripMenuItem;
            if (ym == null) { return; }
            ym.DropDownItems.Clear();

            DateTime today = DateTime.Today;
            const int earliestYear = 2016;
            int lastYear = today.Year - 1;
            if (lastYear < earliestYear)
            {
                ym.Visible = false;
                return;
            }
            ym.Visible = true;

            bool archiveOn = GetCfgBool(ReadConfig(), "archive", true);
            Dictionary<string, object> favIdx = ReadJson(favIndexPath);
            Dictionary<string, object> cacheIdx = ReadJson(indexPath);

            int shown = 0;
            for (int y = lastYear; y >= earliestYear; y--)
            {
                DateTime dt;
                try { dt = new DateTime(y, today.Month, today.Day); }
                catch { continue; }        // 2/29 这类不存在的日期
                if (dt < new DateTime(2016, 3, 5)) { continue; }

                string key = dt.ToString("yyyyMMdd");
                string label = dt.ToString("yyyy-MM-dd");

                // 本地有？
                string localFile = null;
                try
                {
                    string[] hit = Directory.GetFiles(favDir, key + "_*.jpg");
                    if (hit.Length == 0) { hit = Directory.GetFiles(cacheDir, key + "_*.jpg"); }
                    if (hit.Length == 0 && Directory.Exists(archiveImgDir))
                    {
                        hit = Directory.GetFiles(archiveImgDir, key + "_*.jpg");
                    }
                    if (hit.Length > 0) { localFile = hit[0]; }
                }
                catch { }

                if (localFile != null)
                {
                    string ttl = "";
                    if (favIdx != null && favIdx.ContainsKey(key))
                    {
                        ttl = GetStr(favIdx[key] as Dictionary<string, object>, "title");
                    }
                    if (ttl.Length == 0 && cacheIdx != null && cacheIdx.ContainsKey(key))
                    {
                        ttl = GetStr(cacheIdx[key] as Dictionary<string, object>, "title");
                    }
                    label = label + "  " + (ttl.Length > 0 ? ttl : Path.GetFileNameWithoutExtension(localFile));
                }
                else if (!archiveOn)
                {
                    label = label + S.NeedArchiveSfx;
                }
                else
                {
                    label = label + S.ClickFetchSfx;
                }

                ToolStripMenuItem it = new ToolStripMenuItem(label);
                it.Enabled = localFile != null || archiveOn;
                if (it.Enabled)
                {
                    string captured = key;
                    it.Click += delegate
                    {
                        keepMenuOpenAfterClick = true;
                        anchor.BeginInvoke((MethodInvoker)delegate { RunScriptEx("-Date " + captured, "history"); });
                    };
                }
                ym.DropDownItems.Add(it);
                shown++;
            }

            if (shown == 0)
            {
                ToolStripMenuItem e = new ToolStripMenuItem(S.NoPastYears);
                e.Enabled = false;
                ym.DropDownItems.Add(e);
                return;
            }

            ym.DropDownItems.Add(new ToolStripSeparator());
            ym.DropDownItems.Add(MakeItem(S.OpenArchiveImg, delegate { OpenTarget(archiveImgDir); }));
        }

        private static void RefreshArchiveCheck()
        {
            ToolStripMenuItem am = menu.Items["archive"] as ToolStripMenuItem;
            if (am == null) { return; }

            Dictionary<string, object> c = ReadConfig();
            bool on = GetCfgBool(c, "archive", true);

            foreach (ToolStripItem x in am.DropDownItems)
            {
                if (x.Name == "archtoggle")
                {
                    x.Text = on ? S.ArchiveOn : S.ArchiveOff;
                }
                else if (x.Name == "archinfo")
                {
                    int months = 0;
                    long bytes = 0;
                    try
                    {
                        if (Directory.Exists(archiveDir))
                        {
                            string[] fs = Directory.GetFiles(archiveDir, "*.json");
                            months = fs.Length;
                            foreach (string f in fs) { bytes += new FileInfo(f).Length; }
                        }
                    }
                    catch { }
                    x.Text = S.ArchInfoPrefix + S.ArchCacheInfo(months, Math.Round(bytes / 1024.0, 0)).TrimStart();
                }
                else if (x.Name == "updatetime")
                {
                    int mins = GetUpdateMinutes();
                    string cur = (mins / 60).ToString("00") + ":" + (mins % 60).ToString("00");
                    x.Text = S.UpdateTimeMenu + "  " + cur;
                    ToolStripMenuItem tm = x as ToolStripMenuItem;
                    if (tm != null && tm.DropDownItems.Count > 0)
                    {
                        foreach (ToolStripItem sub in tm.DropDownItems)
                        {
                            ToolStripMenuItem mi = sub as ToolStripMenuItem;
                            if (mi == null || mi.Tag == null) { continue; }
                            mi.Checked = ((string)mi.Tag == cur);
                        }
                    }
                }
                else if (x.Name == "archsrc1" || x.Name == "archsrc2")
                {
                    // 关掉增强数据源时，把两行来源灰掉但仍保留信息
                    x.Enabled = false;
                    x.ForeColor = on ? SystemColors.GrayText : SystemColors.ControlDarkDark;
                    if (!on && x.Name == "archsrc2") { x.Text = S.ArchInfoPrefix + S.ArchOffLine; }
                }
            }
        }

        private static void RefreshNotifyCheck()
        {
            ToolStripMenuItem it = menu.Items["notify"] as ToolStripMenuItem;
            if (it == null) { return; }
            bool on = GetCfgBool(ReadConfig(), "notify", false);
            it.Text = on ? S.NotifyOn : S.NotifyOff;
        }

        private static ToolStripMenuItem MakeThemeItem(string label, string value)
        {
            ToolStripMenuItem it = new ToolStripMenuItem(label);
            it.Tag = value;
            it.Click += delegate
            {
                keepMenuOpenAfterClick = true;
                Dictionary<string, object> c = ReadConfig();
                SetCfg(c, "theme", value);
                WriteConfig(c);
                themeApplied = false;          // 强制重新应用
                SyncTheme();
            };
            return it;
        }

        private static void RefreshThemeCheck()
        {
            ToolStripMenuItem themeMenu = menu.Items["theme"] as ToolStripMenuItem;
            if (themeMenu == null) { return; }
            string cur = GetCfgStr(ReadConfig(), "theme", "auto").ToLowerInvariant();
            foreach (ToolStripItem raw in themeMenu.DropDownItems)
            {
                ToolStripMenuItem it = raw as ToolStripMenuItem;
                if (it == null || it.Tag == null) { continue; }
                string v = it.Tag.ToString();
                it.Text = (v == cur ? "● " : "　 ") + LabelOfTheme(v);
            }
        }

        private static string LabelOfTheme(string v)
        {
            if (v == "light") { return S.ThemeLight; }
            if (v == "dark") { return S.ThemeDark; }
            return S.ThemeAuto;
        }

        private static void SetBusy(bool on)
        {
            bool changed = (busy != on);
            busy = on;
            if (tray == null) { return; }

            if (changed)
            {
                try
                {
                    tray.Icon = on ? iconBusy : iconNormal;
                }
                catch { }
            }

            foreach (ToolStripItem raw in menu.Items)
            {
                ToolStripMenuItem item = raw as ToolStripMenuItem;
                if (item == null) { continue; }
                if (item.Name == "header" || item.Name == "startup") { continue; }
                // 菜单正开着时不禁用，允许连续切换；锁由脚本侧保证
                item.Enabled = menu.Visible ? true : !on;
            }

            if (on) { tray.Text = Clip(AppName + S.WorkingDots, 60); }
        }

        // ---------------------------------------------------------------- 执行脚本

        /// <summary>同步执行脚本并返回退出码，供 --run 模式使用。</summary>
        /// <summary>跑脚本并返回其标准输出（用于 -Query 这类只读取数据的调用）。</summary>
        internal static string RunScriptCapture(string arguments)
        {
            string outFile = Path.Combine(Path.GetTempPath(), "bwl-output.json");
            try { File.WriteAllText(outFile, "", new UTF8Encoding(false)); } catch { }

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = PowerShellExe;
                psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + scriptPath + "\" " + arguments;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                try { psi.StandardOutputEncoding = Encoding.UTF8; psi.StandardErrorEncoding = Encoding.UTF8; } catch { }
                using (Process p = Process.Start(psi))
                {
                    string outText = p.StandardOutput.ReadToEnd();
                    string errText = p.StandardError.ReadToEnd();
                    if (!p.WaitForExit(120000)) { try { p.Kill(); } catch { } }

                    if (!string.IsNullOrEmpty(outText)) { return outText; }
                    try
                    {
                        if (File.Exists(outFile))
                        {
                            string backup = File.ReadAllText(outFile, Encoding.UTF8);
                            if (!string.IsNullOrEmpty(backup)) { return backup; }
                        }
                    }
                    catch { }

                    try
                    {
                        File.AppendAllText(Path.Combine(Path.GetTempPath(), "bwl-search-debug.log"),
                            DateTime.Now.ToString("HH:mm:ss") + "  CAPTURE-EMPTY  stderr=" +
                            (errText ?? "").Replace(Environment.NewLine, " ") + Environment.NewLine);
                    }
                    catch { }
                    return "";
                }
            }
            catch (Exception ex)
            {
                try
                {
                    File.AppendAllText(Path.Combine(Path.GetTempPath(), "bwl-search-debug.log"),
                        DateTime.Now.ToString("HH:mm:ss") + "  CAPTURE-EX  " + ex.Message + Environment.NewLine);
                }
                catch { }
                return "";
            }
        }

        private static int RunScriptBlocking(string arguments)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = PowerShellExe;
                psi.Arguments = "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File \"" + scriptPath + "\" " + arguments;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                using (Process p = Process.Start(psi))
                {
                    p.StandardOutput.ReadToEnd();
                    if (!p.WaitForExit(240000)) { try { p.Kill(); } catch { } return -1; }
                    return p.ExitCode;
                }
            }
            catch { return -1; }
        }

        private static void RunScript(string arguments)
        {
            RunScriptEx(arguments, "");
        }

        private static void RunScriptEx(string arguments, string opMode)
        {
            if (busy) { return; }
            if (!File.Exists(scriptPath))
            {
                MessageBox.Show(S.WarnNoScript + "\r\n" + scriptPath + "\r\n\r\n" + S.WarnNoScript2,
                    AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            SetBusy(true);
            ThreadPool.QueueUserWorkItem(delegate
            {
                int code = -1;
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo();
                    psi.FileName = PowerShellExe;
                    psi.Arguments = "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File \"" + scriptPath + "\" " + arguments;
                    psi.UseShellExecute = false;
                    psi.CreateNoWindow = true;
                    psi.RedirectStandardOutput = true;
                    psi.RedirectStandardError = true;

                    using (Process p = Process.Start(psi))
                    {
                        try { p.StandardOutput.ReadToEnd(); }
                        catch { }
                        if (!p.WaitForExit(240000))
                        {
                            try { p.Kill(); } catch { }
                            code = -1;
                        }
                        else
                        {
                            code = p.ExitCode;
                        }
                    }
                }
                catch
                {
                    code = -1;
                }

                anchor.BeginInvoke((MethodInvoker)delegate
                {
                    SetBusy(false);
                    RefreshAll();

                    if (code == 0 || code == 8)
                    {
                        if (opMode == "firstrun")
                        {
                            Balloon(S.BReady,
                                S.BReadyHint);
                        }
                        else if (opMode == "cleanarch")
                        {
                            RefreshAll();
                            Balloon(S.BCleared, S.CleanedHint);
                        }
                        else if (opMode == "fav")
                        {
                            Balloon(S.BFavDone, S.FavDoneHint);
                            RefreshAll();
                        }
                        else if (opMode == "unfav")
                        {
                            Balloon(S.BFavUndo, S.FavUndoHint);
                            RefreshAll();
                        }
                        else if (GetCfgBool(ReadConfig(), "notify", false))
                        {
                            Dictionary<string, object> st = ReadJson(statePath);
                            string ttl = GetStr(st, "title");
                            if (ttl.Length == 0) { ttl = S.FallbackTitle; }
                            if (GetCfgBool(ReadConfig(), "locked", false))
                            {
                                Balloon(S.BSwitched, ttl + S.SwitchedPaused);
                            }
                            else
                            {
                                Balloon(S.BUpdated, ttl);
                            }
                        }

                        // 降级提示不受 notify 控制：质量异常必须告知
                        if (code == 8)
                        {
                            Balloon(S.BDegraded, S.DegradedHint);
                        }
                    }
                    else if (code == 4)
                    {
                        if (opMode == "next") { Balloon(S.BLatest, S.LatestHint); }
                        else if (opMode == "prev") { Balloon(S.BOldest, S.OldestHint); }
                        else { Balloon(S.BEdge, S.EdgeHint); }
                    }
                    else if (code == 6)
                    {
                        Balloon(S.BNeedArch, S.NeedArchHint);
                    }
                    else if (code == 7)
                    {
                        Balloon(S.BArchDown, S.ArchDownHint);
                    }
                    else if (code == 5)
                    {
                        Balloon(S.BNoImage, S.NoImageHint);
                    }
                    else if (code == 3)
                    {
                        Balloon(S.BNoFav, S.NoFavHint);
                    }
                    else if (code == 9)
                    {
                        // 今天的图还没发布，静默等补跑，不当作错误
                        if (!menu.Visible) { Balloon(S.BWaiting, S.BWaitingHint); }
                    }
                    else if (code == 2)
                    {
                        // 撞锁：菜单多半还开着，图标也会变橙点，不再弹气泡打扰
                        if (!menu.Visible) { Balloon(S.BBusy, S.IsEn ? "Another task is running, please wait" : "另一个任务正在进行，请稍候"); }
                    }
                    else
                    {
                        Balloon(S.BFailed, S.IsEn ? "Right-click → Open log for details" : "右键 → 打开日志 查看原因");
                    }
                });
            });
        }

        // ---------------------------------------------------------------- 信息

        internal static void OpenUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) { return; }
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch { }
        }

        /// <summary>把版权描述压成搜索关键词：去掉 (© …)，取第一个逗号前。</summary>
        private static string BuildSearchUrl(Dictionary<string, object> state)
        {
            string direct = GetStr(state, "searchUrl");
            if (direct.Length > 0) { return direct; }

            string cpr = GetStr(state, "copyright");
            if (cpr.Length == 0) { return ""; }

            int cut = cpr.IndexOf('(');
            if (cut < 0) { cut = cpr.IndexOf('（'); }
            string kw = cut > 0 ? cpr.Substring(0, cut) : cpr;

            cut = kw.IndexOf('，');
            if (cut < 0) { cut = kw.IndexOf(','); }
            if (cut > 0) { kw = kw.Substring(0, cut); }

            kw = kw.Trim();
            if (kw.Length == 0) { return ""; }
            return "https://www.bing.com/search?q=" + Uri.EscapeDataString(kw) + "&form=hpcapt&mkt=zh-cn";
        }

        private static void ShowInfo()
        {
            Dictionary<string, object> state = ReadJson(statePath);
            if (state == null)
            {
                MessageBox.Show(S.InfoNone, AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string date = GetStr(state, "date");
            string title = GetStr(state, "title");
            string copyright = GetStr(state, "copyright");
            string file = GetStr(state, "file");

            // 索引里有更完整的标题/版权时优先用
            Dictionary<string, object> index = ReadJson(indexPath);
            if (index != null && index.ContainsKey(date))
            {
                Dictionary<string, object> entry = index[date] as Dictionary<string, object>;
                if (entry != null)
                {
                    string t2 = GetStr(entry, "title");
                    if (t2.Length > 0) { title = t2; }
                    string c2 = GetStr(entry, "copyright");
                    if (c2.Length > 0) { copyright = c2; }
                }
            }

            string sizeText = "";
            try
            {
                if (file.Length > 0 && File.Exists(file))
                {
                    sizeText = ((new FileInfo(file).Length) / 1024.0 / 1024.0).ToString("0.00") + " MB";
                }
            }
            catch { }

            using (InfoForm f = new InfoForm(date, title, copyright, file, sizeText, BuildSearchUrl(state)))
            {
                f.ShowDialog(anchor);
            }
        }
        private static void StatDir(string dir, out int count, out double mb)
        {
            count = 0;
            mb = 0;
            try
            {
                if (!Directory.Exists(dir)) { return; }
                string[] fs = Directory.GetFiles(dir, "*.jpg");
                count = fs.Length;
                long sz = 0;
                foreach (string f in fs) { try { sz += new FileInfo(f).Length; } catch { } }
                mb = sz / 1024.0 / 1024.0;
            }
            catch { }
        }

        private static void ShowAbout()
        {
            int cCount, fCount, aCount;
            double cMb, fMb, aMb;
            StatDir(cacheDir, out cCount, out cMb);
            StatDir(favDir, out fCount, out fMb);
            StatDir(archiveImgDir, out aCount, out aMb);

            bool taskOn = IsTaskRegistered();
            bool runOn = IsStartupEnabled();
            bool locked = GetCfgBool(ReadConfig(), "locked", false);
            bool archiveOn = GetCfgBool(ReadConfig(), "archive", true);
            int months = 0;
            try
            {
                if (Directory.Exists(archiveDir)) { months = Directory.GetFiles(archiveDir, "*.json").Length; }
            }
            catch { }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine(AppName + "  v" + AppVersion);
            sb.AppendLine();
            sb.AppendLine(S.AboutTagline);
            sb.AppendLine();
            sb.AppendLine(S.AboutCanDo);
            sb.AppendLine(S.AboutFeat1);
            sb.AppendLine(S.AboutFeat2);
            sb.AppendLine(S.AboutFeat3);
            sb.AppendLine(S.AboutFeat4);
            sb.AppendLine();
            sb.AppendLine(S.AboutSources);
            sb.AppendLine(S.AboutDailyLine + "bingwallpaper.microsoft.com/api/BWC/getHPImages");
            sb.AppendLine(S.AboutOrigLine);
            sb.AppendLine(S.AboutArchLine(archiveOn, months));
            sb.AppendLine(S.AboutBackupLine);
            sb.AppendLine(S.AboutCopyLine);
            sb.AppendLine();
            sb.AppendLine(S.AboutComponents);
            sb.AppendLine("· " + Path.GetFileName(Application.ExecutablePath) + "    " + S.AboutTrayComp);
            sb.AppendLine("· " + Path.GetFileName(scriptPath) + "       " + S.AboutScriptComp);
            sb.AppendLine(S.AboutTaskLbl + (taskOn ? S.StateReg : S.StateUnreg));
            sb.AppendLine(S.AboutStartupLbl + (runOn ? S.StateOn : S.StateOff));
            sb.AppendLine(S.AboutAutoLbl + (locked ? S.StatePaused : S.StateRunning));
            sb.AppendLine();
            sb.AppendLine(S.AboutStorage);
            sb.AppendLine(S.AboutProgramDir + root);
            sb.AppendLine(S.AboutStore(cCount, cMb, fCount, fMb, aCount, aMb));
            sb.AppendLine(S.AboutLogDir + logPath);
            sb.AppendLine();
            sb.AppendLine(S.AboutEngineLbl + PowerShellExe);
            sb.AppendLine();
            sb.AppendLine(S.AboutFooter);
            DialogResult r = MessageBox.Show(sb.ToString(), S.AboutItem.Trim() + " " + AppName,
                MessageBoxButtons.OK, MessageBoxIcon.Information);

            if (r == DialogResult.OK) { }
        }

        private static void OpenTarget(string target)
        {
            try
            {
                if (Directory.Exists(target))
                {
                    Process.Start("explorer.exe", "\"" + target + "\"");
                }
                else if (File.Exists(target))
                {
                    if (target.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
                        Process.Start("notepad.exe", "\"" + target + "\"");
                    else
                        Process.Start("explorer.exe", "/select,\"" + target + "\"");
                }
                else
                {
                    MessageBox.Show(S.WarnPathMissing + "\r\n" + target, AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(S.WarnOpenFailed + ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static void Balloon(string title, string text)
        {
            try
            {
                if (tray == null) { return; }
                tray.BalloonTipTitle = title;
                tray.BalloonTipText = Clip(text, 200);
                tray.BalloonTipIcon = ToolTipIcon.Info;
                tray.ShowBalloonTip(4000);
            }
            catch { }
        }

        /// <summary>检测桌面壁纸是否被外部改动；若是，把原先管理的那张备份到桌面并提示。</summary>
        private static void SyncExternalWallpaper()
        {
            try
            {
                string actual = Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "Wallpaper", "") as string;
                if (string.IsNullOrEmpty(actual) || !File.Exists(actual)) { return; }

                // 防重入：同一张外部壁纸只处理一次，避免反复复制与弹泡
                if (string.Equals(actual, lastSyncedActual, StringComparison.OrdinalIgnoreCase)) { return; }
                lastSyncedActual = actual;

                Dictionary<string, object> st = ReadJson(statePath);
                string expected = GetStr(st, "file");
                if (string.IsNullOrEmpty(expected)) { return; }
                if (string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase)) { return; }

                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string name = Path.GetFileName(actual);
                if (string.IsNullOrEmpty(name)) { return; }
                string dest = Path.Combine(desktop, "壁纸备份_" + name);
                bool copied = false;
                if (!File.Exists(dest))
                {
                    try { File.Copy(actual, dest, false); copied = true; }
                    catch { }
                }

                System.Text.RegularExpressions.Match mm =
                    System.Text.RegularExpressions.Regex.Match(name, @"^(\d{8})");
                if (mm.Success)
                {
                    string d8 = mm.Groups[1].Value;
                    Dictionary<string, object> ns = new Dictionary<string, object>();
                    ns["date"] = d8;
                    ns["file"] = actual;
                    ns["title"] = LookupTitle(d8);
                    ns["copyright"] = "";
                    ns["searchUrl"] = "";
                    ns["imageId"] = "";
                    ns["appliedDate"] = d8;
                    ns["updatedAt"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    string json = new JavaScriptSerializer().Serialize(ns);
                    if (!string.IsNullOrEmpty(json)) { File.WriteAllText(statePath, json, new UTF8Encoding(false)); }
                }

                if (copied) { Balloon(S.ExtChangedTtl, S.ExtChangedMsg + " " + Path.GetFileName(dest)); }
            }
            catch { }
        }

        // ---------------------------------------------------------------- 安装 / 卸载

        private static void EnsureRegistered(bool verbose)
        {
            bool changed = false;
            if (!IsStartupPointingHere()) { SetStartup(true); changed = true; }
            if (!IsTaskPointingHere()) { RegisterTask(); changed = true; }
            SetUninstallEntry();
            if (changed && verbose)
            {
                MessageBox.Show(S.MsgRegistered, AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        /// <summary>自启项存在且指向当前 exe。</summary>
        private static bool IsStartupPointingHere()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    if (k == null) { return false; }
                    object v = k.GetValue(RunValue);
                    if (v == null) { return false; }
                    string s = v.ToString().Trim().Trim('"');
                    return string.Equals(s, Application.ExecutablePath, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch { return false; }
        }

        /// <summary>计划任务存在且动作指向当前脚本。</summary>
        private static bool IsTaskPointingHere()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "schtasks.exe";
                psi.Arguments = "/query /tn \"" + TaskName + "\" /xml ONE";
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                using (Process p = Process.Start(psi))
                {
                    string xml = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(10000);
                    if (p.ExitCode != 0) { return false; }
                    return xml.IndexOf(scriptPath, StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
            catch { return false; }
        }

        private static void SetUninstallEntry()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(UninstKeyPath))
                {
                    if (k == null) { return; }
                    k.SetValue("DisplayName", AppName);
                    k.SetValue("DisplayVersion", AppVersion);
                    k.SetValue("Publisher", "BingWallpaperChronicle");
                    k.SetValue("DisplayIcon", Application.ExecutablePath + ",0");
                    k.SetValue("UninstallString", "\"" + Application.ExecutablePath + "\" --uninstall");
                    k.SetValue("InstallLocation", root);
                    k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                    long size = 0;
                    try
                    {
                        foreach (string f in Directory.GetFiles(root)) { size += new FileInfo(f).Length; }
                    }
                    catch { }
                    if (size > 0) { k.SetValue("EstimatedSize", (int)(size / 1024), RegistryValueKind.DWord); }
                }
            }
            catch { }
        }

        private static void RemoveUninstallEntry()
        {
            try { Registry.CurrentUser.DeleteSubKeyTree(UninstKeyPath, false); } catch { }
        }

        private static void DoInstall(bool interactive)
        {
            EnsureRegistered(true);
            if (interactive)
            {
                try { Process.Start(Application.ExecutablePath); } catch { }
            }
        }

        private static void DoUninstall()
        {
            string[] favFiles = SafeGetFiles(favDir);
            string[] cacheFiles = SafeGetFiles(cacheDir);
            string[] archFiles = SafeGetFiles(archiveImgDir);
            int favCount = favFiles.Length;
            int archCount = archFiles.Length;

            double cacheMB = 0;
            try
            {
                foreach (string cf in cacheFiles)
                {
                    cacheMB += new FileInfo(cf).Length / 1024.0 / 1024.0;
                }
            }
            catch { }

            using (UninstallForm f = new UninstallForm(favCount, archCount, cacheFiles.Length, cacheMB, root))
            {
                if (f.ShowDialog() != DialogResult.OK) { return; }
                bool keepFav = f.KeepFavorites;

                try { SetStartup(false); } catch { }
                try { RunSchtasks("/delete /tn \"" + TaskName + "\" /f"); } catch { }
                try { RemoveUninstallEntry(); } catch { }

                // 归档处理：月份索引一律清（纯缓存）；图片按勾选
                bool keepArch = f.KeepArchive;
                try
                {
                    if (Directory.Exists(archiveDir))
                    {
                        foreach (string jf in Directory.GetFiles(archiveDir, "*.json"))
                        {
                            try
                            {
                                Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                                    jf,
                                    Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                                    Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                            }
                            catch { }
                        }
                    }
                    if (!keepArch && archCount > 0 && Directory.Exists(archiveImgDir))
                    {
                        Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(
                            archiveImgDir,
                            Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                            Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                    }
                }
                catch { }

                // 清收藏（用户明确取消勾选时才做）
                if (!keepFav && favCount > 0)
                {
                    try
                    {
                        Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(
                            favDir,
                            Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                            Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                    }
                    catch { }
                }

                // 清缓存：保留当前正在显示的那张，否则桌面会变黑
                string curWp = GetItemPropertyWallpaper();
                int cleared = 0;
                try
                {
                    foreach (string cf in SafeGetFiles(cacheDir))
                    {
                        if (string.Equals(cf, curWp, StringComparison.OrdinalIgnoreCase)) { continue; }
                        try
                        {
                            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                                cf,
                                Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                                Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                            cleared++;
                        }
                        catch { }
                    }
                }
                catch { }

                string favMsg = keepFav
                    ? S.FavKeptN(favCount)
                    : (favCount > 0 ? S.FavGone : S.FavEmpty);
                string cacheMsg = cleared > 0
                    ? S.CacheClearedN(cleared)
                    : S.CacheClean;

                MessageBox.Show(S.DlDone + "\r\n" +
                    S.DlStartup + "\r\n" +
                    S.DlTask + "\r\n" +
                    S.DlUninstEntry + "\r\n" +
                    cacheMsg + "\r\n" +
                    S.DlIdxCleared + "\r\n" +
                    (keepArch
                        ? S.ArchKeptN(archCount)
                        : (archCount > 0 ? S.ArchGoneLine : S.ArchNoneLine)) +
                    favMsg + "\r\n\r\n" +
                    S.DlKeepFiles + "\r\n" + root,
                    AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);

                if (tray != null) { tray.Visible = false; }
                Application.Exit();
            }
        }

        // ---------------------------------------------------------------- 计划任务

        private static bool IsTaskRegistered()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "schtasks.exe";
                psi.Arguments = "/query /tn \"" + TaskName + "\"";
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                using (Process p = Process.Start(psi))
                {
                    p.StandardOutput.ReadToEnd();
                    p.WaitForExit(8000);
                    return p.ExitCode == 0;
                }
            }
            catch { return false; }
        }

        private static string RunSchtasksOutput(string arguments, out int exitCode)
        {
            exitCode = -1;
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "schtasks.exe";
                psi.Arguments = arguments;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                // schtasks 用系统 ANSI 代码页输出（中文系统是 GBK），按 UTF-8 读会乱码
                try { psi.StandardOutputEncoding = Encoding.GetEncoding(0); } catch { }
                using (Process p = Process.Start(psi))
                {
                    string outp = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(10000);
                    exitCode = p.ExitCode;
                    return outp;
                }
            }
            catch { return ""; }
        }

        private static int RunSchtasks(string arguments)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "schtasks.exe";
                psi.Arguments = arguments;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                using (Process p = Process.Start(psi))
                {
                    p.StandardOutput.ReadToEnd();
                    p.WaitForExit(20000);
                    return p.ExitCode;
                }
            }
            catch { return -1; }
        }

        private static void RegisterTask()
        {
            try
            {
                string xml = BuildTaskXml();
                string tmp = Path.Combine(Path.GetTempPath(), "BingWallpaperLite.task.xml");
                File.WriteAllText(tmp, xml, new UnicodeEncoding(false, true));

                int code = RunSchtasks("/create /tn \"" + TaskName + "\" /xml \"" + tmp + "\" /f");
                try { File.Delete(tmp); } catch { }

                if (code != 0)
                {
                    MessageBox.Show(S.WarnTaskFail + code + S.WarnTaskFail2,
                        AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(S.WarnTaskEx + ex.Message, AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static int GetUpdateMinutes()
        {
            try
            {
                string s = GetStr(ReadConfig(), "updateTime");
                if (s.Length >= 4 && s.Contains(":"))
                {
                    string[] parts = s.Split(':');
                    int hh = Convert.ToInt32(parts[0]);
                    int mm = Convert.ToInt32(parts[1]);
                    if (hh >= 0 && hh < 24 && mm >= 0 && mm < 60) { return hh * 60 + mm; }
                }
            }
            catch { }
            return 10;
        }

        private static string BuildTaskXml()
        {
            string user = Environment.UserDomainName + "\\" + Environment.UserName;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-16\"?>");
            sb.AppendLine("<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">");
            sb.AppendLine("  <RegistrationInfo>");
            sb.AppendLine("    <Author>" + XmlEscape(AppName) + "</Author>");
            sb.AppendLine("    <Description>" + XmlEscape(AppName + S.TaskDesc) + "</Description>");
            sb.AppendLine("  </RegistrationInfo>");
            sb.AppendLine("  <Triggers>");
            // 主时间由配置决定；两个补查点自动 +8h / +12h
            int baseMin = GetUpdateMinutes();
            foreach (int off in new int[] { 0, 8 * 60, 12 * 60 })
            {
                int total = (baseMin + off) % (24 * 60);
                string hhmm = (total / 60).ToString("00") + ":" + (total % 60).ToString("00") + ":00";
                sb.AppendLine("    <CalendarTrigger>");
                sb.AppendLine("      <StartBoundary>2020-01-01T" + hhmm + "</StartBoundary>");
                sb.AppendLine("      <Enabled>true</Enabled>");
                sb.AppendLine("      <ScheduleByDay><DaysInterval>1</DaysInterval></ScheduleByDay>");
                sb.AppendLine("    </CalendarTrigger>");
            }
            sb.AppendLine("    <LogonTrigger>");
            sb.AppendLine("      <Enabled>true</Enabled>");
            sb.AppendLine("      <UserId>" + XmlEscape(user) + "</UserId>");
            sb.AppendLine("    </LogonTrigger>");
            sb.AppendLine("  </Triggers>");
            sb.AppendLine("  <Principals>");
            sb.AppendLine("    <Principal id=\"Author\">");
            sb.AppendLine("      <UserId>" + XmlEscape(user) + "</UserId>");
            sb.AppendLine("      <LogonType>InteractiveToken</LogonType>");
            sb.AppendLine("      <RunLevel>LeastPrivilege</RunLevel>");
            sb.AppendLine("    </Principal>");
            sb.AppendLine("  </Principals>");
            sb.AppendLine("  <Settings>");
            sb.AppendLine("    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>");
            sb.AppendLine("    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>");
            sb.AppendLine("    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>");
            sb.AppendLine("    <AllowHardTerminate>true</AllowHardTerminate>");
            sb.AppendLine("    <StartWhenAvailable>true</StartWhenAvailable>");
            sb.AppendLine("    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>");
            sb.AppendLine("    <IdleSettings><StopOnIdleEnd>false</StopOnIdleEnd><RestartOnIdle>false</RestartOnIdle></IdleSettings>");
            sb.AppendLine("    <AllowStartOnDemand>true</AllowStartOnDemand>");
            sb.AppendLine("    <Enabled>true</Enabled>");
            sb.AppendLine("    <Hidden>false</Hidden>");
            sb.AppendLine("    <RunOnlyIfIdle>false</RunOnlyIfIdle>");
            sb.AppendLine("    <ExecutionTimeLimit>PT10M</ExecutionTimeLimit>");
            sb.AppendLine("    <RestartOnFailure><Interval>PT15M</Interval><Count>3</Count></RestartOnFailure>");
            sb.AppendLine("  </Settings>");
            sb.AppendLine("  <Actions Context=\"Author\">");
            sb.AppendLine("    <Exec>");
            sb.AppendLine("      <Command>" + XmlEscape(Application.ExecutablePath) + "</Command>");
            sb.AppendLine("      <Arguments>--run</Arguments>");
            sb.AppendLine("    </Exec>");
            sb.AppendLine("  </Actions>");
            sb.AppendLine("</Task>");
            return sb.ToString();
        }

        private static string XmlEscape(string s)
        {
            if (s == null) { return ""; }
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
                    .Replace("\"", "&quot;").Replace("'", "&apos;");
        }

        // ---------------------------------------------------------------- 开机启动

        private static bool IsStartupEnabled()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    if (k == null) { return false; }
                    object v = k.GetValue(RunValue);
                    return v != null && v.ToString().Length > 0;
                }
            }
            catch { return false; }
        }

        private static void SetStartup(bool on)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (k == null) { return; }
                    if (on) { k.SetValue(RunValue, "\"" + Application.ExecutablePath + "\""); }
                    else { k.DeleteValue(RunValue, false); }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(S.WarnStartupFail + ex.Message, AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // ---------------------------------------------------------------- 工具

        private static Icon LoadIcon(string path, Size size)
        {
            if (File.Exists(path))
            {
                try { return new Icon(path, size); }
                catch { }
            }
            return SystemIcons.Application;
        }

        private static Dictionary<string, object> ReadJson(string path)
        {
            try
            {
                if (!File.Exists(path)) { return null; }
                string json = File.ReadAllText(path, Encoding.UTF8);
                JavaScriptSerializer ser = new JavaScriptSerializer();
                return ser.Deserialize<Dictionary<string, object>>(json);
            }
            catch { return null; }
        }

        private static string GetStr(Dictionary<string, object> dict, string key)
        {
            if (dict == null || !dict.ContainsKey(key) || dict[key] == null) { return ""; }
            return dict[key].ToString();
        }

        private static string Clip(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) { return ""; }
            return s.Length <= max ? s : s.Substring(0, max - 1) + "…";
        }
    }

    // -------------------------------------------------------------------- 暗色配色

    internal sealed class DarkColorTable : ProfessionalColorTable
    {
        public static readonly Color Bg        = Color.FromArgb(32, 32, 32);
        public static readonly Color BgHover   = Color.FromArgb(58, 58, 58);
        public static readonly Color Border    = Color.FromArgb(70, 70, 70);
        public static readonly Color Separator = Color.FromArgb(70, 70, 70);
        public static readonly Color Text      = Color.FromArgb(235, 235, 235);

        public override Color ToolStripDropDownBackground { get { return Bg; } }
        public override Color ImageMarginGradientBegin    { get { return Bg; } }
        public override Color ImageMarginGradientMiddle   { get { return Bg; } }
        public override Color ImageMarginGradientEnd      { get { return Bg; } }
        public override Color MenuBorder                  { get { return Border; } }
        public override Color MenuItemBorder              { get { return BgHover; } }
        public override Color MenuItemSelected            { get { return BgHover; } }
        public override Color MenuItemSelectedGradientBegin { get { return BgHover; } }
        public override Color MenuItemSelectedGradientEnd   { get { return BgHover; } }
        public override Color MenuItemPressedGradientBegin  { get { return BgHover; } }
        public override Color MenuItemPressedGradientEnd    { get { return BgHover; } }
        public override Color SeparatorDark               { get { return Separator; } }
        public override Color SeparatorLight              { get { return Separator; } }
    }

    // -------------------------------------------------------------------- 选日期

    internal sealed class CalendarForm : Form
    {
        private readonly MonthCalendar cal;
        private readonly Label hint;
        private readonly Button ok;
        private readonly Button cancel;

        public string SelectedDate { get; private set; }

        public CalendarForm()
        {
            Text = Program.AppName + (S.IsEn ? " · Search by date" : " · 按日期搜索");
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            ClientSize = new Size(300, 380);
            try { Font = new Font("Microsoft YaHei UI", 9F); } catch { }

            cal = new MonthCalendar();
            cal.MaxSelectionCount = 1;
            try
            {
                cal.MinDate = new DateTime(2016, 3, 5);
                cal.MaxDate = DateTime.Today;
                cal.SetDate(DateTime.Today);
            }
            catch { }
            cal.SetBounds(12, 12, 240, 240);
            Controls.Add(cal);

            hint = new Label();
            hint.AutoSize = false;
            hint.Height = 44;
            hint.ForeColor = SystemColors.GrayText;
            hint.Text = S.IsEn ? "Last 8 days: official original\r\nSince Mar 2016: archive original (online)" : "最近 8 天：官方原图\r\n2016-03 起：归档原图（需联网）";
            Controls.Add(hint);

            ok = new Button();
            ok.Text = S.BtnApply;
            ok.Size = new Size(88, 30);
            ok.DialogResult = DialogResult.OK;
            ok.Click += delegate
            {
                SelectedDate = cal.SelectionStart.ToString("yyyyMMdd");
            };
            Controls.Add(ok);

            cancel = new Button();
            cancel.Text = S.BtnCancel;
            cancel.Size = new Size(88, 30);
            cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Relayout();
        }

        /// <summary>DPI 缩放完成后按日历实际高度重排下面三个控件，避免被盖住。</summary>
        private void Relayout()
        {
            try
            {
                int left = 14;
                int width = cal.Right - left;
                if (width < 200) { width = 200; }

                hint.Left = left;
                hint.Width = width;
                hint.Top = cal.Bottom + 10;

                int btnY = hint.Bottom + 12;
                ok.Top = btnY;
                cancel.Top = btnY;
                ok.Left = (ClientSize.Width - ok.Width - cancel.Width - 12) / 2;
                cancel.Left = ok.Right + 12;

                ClientSize = new Size(ClientSize.Width, btnY + ok.Height + 14);
            }
            catch { }
        }
    }
    // -------------------------------------------------------------------- 卸载确认

    /// <summary>AI 调用说明：展示调用方式，三个复制按钮。</summary>
    internal sealed class AiForm : Form
    {
        public AiForm()
        {
            Text = Program.AppName + S.AiTitle;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            ClientSize = new Size(660, 540);
            try { Font = new Font("Microsoft YaHei UI", 9F); } catch { }

            Label intro = new Label();
            intro.AutoSize = false;
            intro.SetBounds(20, 16, 620, 76);
            intro.Text = S.AiIntro;
            Controls.Add(intro);
            int boxTop = intro.Bottom + 10;

            TextBox box = new TextBox();
            box.Multiline = true;
            box.ScrollBars = ScrollBars.Vertical;
            box.ReadOnly = true;
            box.SetBounds(20, boxTop, 620, 340);
            box.Font = new Font("Consolas", 9F);
            box.Text = BuildPrompt();
            box.Select(0, 0);
            Controls.Add(box);

            int btnTop = box.Bottom + 16;

            Button btnPath = new Button();
            btnPath.Text = S.AiCopyPath;
            btnPath.SetBounds(20, btnTop, 130, 32);
            btnPath.Click += delegate { CopyTo(Program.ScriptPathPublic, btnPath); };
            Controls.Add(btnPath);

            Button btnCmds = new Button();
            btnCmds.Text = S.AiCopyCmds;
            btnCmds.SetBounds(160, btnTop, 140, 32);
            btnCmds.Click += delegate { CopyTo(BuildCommands(), btnCmds); };
            Controls.Add(btnCmds);

            Button btnPrompt = new Button();
            btnPrompt.Text = S.AiCopyPrompt;
            btnPrompt.SetBounds(310, btnTop, 170, 32);
            btnPrompt.Click += delegate { CopyTo(BuildPrompt(), btnPrompt); };
            Controls.Add(btnPrompt);

            Button btnClose = new Button();
            btnClose.Text = S.BtnOK;
            btnClose.SetBounds(554, btnTop, 86, 32);
            btnClose.DialogResult = DialogResult.OK;
            Controls.Add(btnClose);

            AcceptButton = btnClose;
            CancelButton = btnClose;

            ClientSize = new Size(660, btnTop + 32 + 20);
        }

        private static string BuildCommands()
        {
            string exe = Program.ScriptPathPublic;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(@"命令行入口：");
            sb.AppendLine(exe);
            sb.AppendLine();
            sb.AppendLine(@"可用参数：");
            sb.AppendLine(@"  -Index 0            换今天的图");
            sb.AppendLine(@"  -Index 1..7         往前翻 1-7 天");
            sb.AppendLine(@"  -Step 1 / -Step -1  相对前后翻一天");
            sb.AppendLine(@"  -Date YYYYMMDD      换指定日期（2016-03-05 至今）");
            sb.AppendLine(@"  -Random             从收藏里随机");
            sb.AppendLine(@"  -Use ""路径""         用本地图片");
            sb.AppendLine(@"  -Favorite           收藏当前这张");
            sb.AppendLine(@"  -Unfavorite         取消收藏");
            sb.AppendLine(@"  -Query ""雪 山""      搜索并列出匹配项（不改图）");
            sb.AppendLine(@"  -Status             查当前状态（壁纸、各段计数、配置）");
            sb.AppendLine(@"  -Json               让任何操作返回结构化 JSON 结果");
            sb.AppendLine(@"  -Search ""雪 山""     搜索并换第一张匹配的");
            sb.AppendLine(@"  -Clean -Keep 7      清理 7 天前的缓存");
            sb.AppendLine(@"  -CleanAll           清空缓存（保留当前壁纸）");
            sb.AppendLine(@"  -DryRun             只演示不执行");
            return sb.ToString();
        }

        private static string BuildPrompt()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(@"我有一个必应壁纸工具，命令行入口是：");
            sb.AppendLine(Program.ScriptPathPublic);
            sb.AppendLine();
            sb.AppendLine(@"用 pwsh 调用，例如：");
            sb.AppendLine(@"  pwsh -File """ + Program.ScriptPathPublic + @""" -Date 20230615");
            sb.AppendLine();
            sb.AppendLine(@"支持的参数：");
            sb.AppendLine(@"  -Index 0            换今天的");
            sb.AppendLine(@"  -Index 1..7         往前翻");
            sb.AppendLine(@"  -Step 1 / -Step -1  前后翻一天");
            sb.AppendLine(@"  -Date YYYYMMDD      指定日期（2016-03-05 至今）");
            sb.AppendLine(@"  -Random             从收藏随机");
            sb.AppendLine(@"  -Favorite           收藏当前");
            sb.AppendLine(@"  -Query ""关键词""     搜索并列出匹配项");
            sb.AppendLine(@"  -Status             查当前状态（返回 JSON）");
            sb.AppendLine(@"  -Json               加在任何命令后，返回结构化结果");
            sb.AppendLine();
            sb.AppendLine(@"换图后加 -Json 就知道换成了哪张。");
            sb.AppendLine(@"  -Search ""关键词""    搜索并换第一张匹配的");
            sb.AppendLine();
            sb.AppendLine(@"搜索说明：");
            sb.AppendLine(@"  · 关键词用空格分隔，脚本做字面匹配（标题 + 版权）");
            sb.AppendLine(@"  · 中文分词要靠你自己扩词，比如「雪山」要拆成 ""雪 山 冰川 雪峰""");
            sb.AppendLine(@"  · 先用 -Query 看结果，挑好了再用 -Date 换");
            sb.AppendLine();
            sb.AppendLine(@"我的需求：在这里写你要什么。");
            return sb.ToString();
        }

        private static void CopyTo(string text, Button b)
        {
            try
            {
                Clipboard.SetText(text);
                string old = b.Text;
                b.Text = S.AiCopied;
                System.Windows.Forms.Timer tm = new System.Windows.Forms.Timer();
                tm.Interval = 1200;
                tm.Tick += delegate { b.Text = old; tm.Stop(); tm.Dispose(); };
                tm.Start();
            }
            catch { }
        }
    }
    /// <summary>清理前弹窗：显示数量与体积，选择删除方式。</summary>
    internal sealed class CleanForm : Form
    {
        public enum Target { Archive, Cache, ArchiveIndex, Favorites }

        private readonly RadioButton rbRecycle;
        private readonly RadioButton rbPermanent;
        private readonly Button btnStart;
        private readonly Label lblNothing;

        public bool Permanent { get; private set; }

        public CleanForm(Target target)
        {
            string title = S.CleanTtlArchive;
            string dir = Program.ArchiveImgPath;
            string filter = "*.jpg";
            bool hasMode = true;

            if (target == Target.Cache) { title = S.CleanTtlCache; dir = Program.CacheDirPath; }
            else if (target == Target.ArchiveIndex) { title = S.CleanTtlIndex; dir = Program.ArchiveDirPath; filter = "*.json"; hasMode = false; }
            else if (target == Target.Favorites) { title = S.CleanTtlFav; dir = Program.FavDirPath; }

            Text = Program.AppName + title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            ClientSize = new Size(520, hasMode ? 400 : 300);
            try { Font = new Font("Microsoft YaHei UI", 9F); } catch { }

            // 统计
            int totalN = 0, favN = 0, pinN = 0, curN = 0;
            long totalB = 0;
            HashSet<string> favDates = new HashSet<string>();
            Dictionary<string, object> pinned = null;
            string curWp = "";
            try
            {
                foreach (string f in Directory.GetFiles(dir, filter))
                {
                    totalN++;
                    long sz = 0;
                    try { sz = new FileInfo(f).Length; } catch { }
                    totalB += sz;
                }
                if (target != Target.ArchiveIndex)
                {
                    foreach (string f in Directory.GetFiles(Program.FavDirPath, "*.jpg"))
                    {
                        string fn = Path.GetFileName(f);
                        if (fn.Length >= 8) { favDates.Add(fn.Substring(0, 8)); }
                    }
                    pinned = Program.ReadPinnedPublic();
                    curWp = Program.CurrentWallpaperPath();
                }
            }
            catch { }

            long actualB = 0;
            int actualN = 0;
            if (target != Target.ArchiveIndex)
            {
                try
                {
                    foreach (string f in Directory.GetFiles(dir, filter))
                    {
                        string fn = Path.GetFileName(f);
                        string d8 = fn.Length >= 8 ? fn.Substring(0, 8) : "";
                        bool prot = false;
                        if (d8.Length == 8)
                        {
                            if (favDates.Contains(d8)) { favN++; prot = true; }
                            else if (pinned != null && pinned.ContainsKey(d8)) { pinN++; prot = true; }
                        }
                        if (!prot && !string.IsNullOrEmpty(curWp) &&
                            string.Equals(curWp, f, StringComparison.OrdinalIgnoreCase)) { curN++; prot = true; }
                        if (!prot)
                        {
                            actualN++;
                            try { actualB += new FileInfo(f).Length; } catch { }
                        }
                    }
                }
                catch { }
            }
            else { actualN = totalN; actualB = totalB; }

            // 统计区
            Panel box = new Panel();
            box.BorderStyle = BorderStyle.FixedSingle;
            box.SetBounds(18, 16, 484, hasMode ? 176 : 96);
            Controls.Add(box);

            int yy = 10;
            box.Controls.Add(MkLabel(S.CleanTotal, 12, yy));
            box.Controls.Add(MkValue(totalN + (target == Target.ArchiveIndex ? " 个" : " 张"), 320, yy));
            yy += 24;
            box.Controls.Add(MkLabel(S.CleanSize, 12, yy));
            box.Controls.Add(MkValue(FmtSize(totalB), 320, yy));

            if (hasMode && target != Target.ArchiveIndex)
            {
                yy += 30;
                Label sep = new Label();
                sep.BorderStyle = BorderStyle.Fixed3D;
                sep.SetBounds(12, yy, 460, 2);
                box.Controls.Add(sep);
                yy += 8;
                if (favN > 0) { box.Controls.Add(MkLabel(S.CleanFavProtected, 12, yy)); box.Controls.Add(MkValue(favN + " 张", 320, yy)); yy += 22; }
                if (pinN > 0) { box.Controls.Add(MkLabel(S.CleanPinProtected, 12, yy)); box.Controls.Add(MkValue(pinN + " 张", 320, yy)); yy += 22; }
                if (curN > 0) { box.Controls.Add(MkLabel(S.CleanCurProtected, 12, yy)); box.Controls.Add(MkValue(curN + " 张", 320, yy)); yy += 22; }
                yy += 6;
                Label l2 = MkLabel(S.CleanActual, 12, yy);
                l2.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
                box.Controls.Add(l2);
                Label v2 = MkValue(actualN + " 张，" + FmtSize(actualB), 320, yy);
                v2.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
                box.Controls.Add(v2);
            }

            // 删除方式
            int ny = box.Bottom + 14;
            if (hasMode)
            {
                Label ml = MkLabel(S.CleanModeTitle, 18, ny);
                ml.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
                Controls.Add(ml);
                ny += 24;

                rbRecycle = new RadioButton();
                rbRecycle.Text = S.CleanToRecycle + "    " + S.CleanToRecycle2;
                rbRecycle.SetBounds(30, ny, 460, 22);
                Controls.Add(rbRecycle);
                ny += 24;

                rbPermanent = new RadioButton();
                rbPermanent.Text = S.CleanToDelete + "    " + S.CleanToDelete2;
                rbPermanent.SetBounds(30, ny, 460, 22);
                Controls.Add(rbPermanent);
                ny += 26;

                if (target == Target.Cache) { rbPermanent.Checked = true; }
                else { rbRecycle.Checked = true; }
            }

            // 提示
            lblNothing = new Label();
            lblNothing.SetBounds(18, ny, 484, 20);
            lblNothing.ForeColor = Color.Firebrick;
            if (actualN == 0) { lblNothing.Text = S.CleanNothing; }
            else if (target == Target.ArchiveIndex) { lblNothing.Text = S.CleanIndexHint; lblNothing.ForeColor = SystemColors.GrayText; }
            Controls.Add(lblNothing);

            // 按钮
            int by = ClientSize.Height - 52;
            btnStart = new Button();
            btnStart.Text = S.CleanStart;
            btnStart.SetBounds(18, by, 120, 34);
            btnStart.Enabled = actualN > 0;
            btnStart.Click += delegate { Accept(); };
            Controls.Add(btnStart);

            Button btnCancel = new Button();
            btnCancel.Text = S.BtnCancel;
            btnCancel.SetBounds(416, by, 86, 34);
            btnCancel.DialogResult = DialogResult.Cancel;
            Controls.Add(btnCancel);

            AcceptButton = btnStart;
            CancelButton = btnCancel;
        }

        private static Label MkLabel(string text, int x, int y)
        {
            Label l = new Label();
            l.Text = text;
            l.SetBounds(x, y, 300, 20);
            return l;
        }

        private static Label MkValue(string text, int x, int y)
        {
            Label l = new Label();
            l.Text = text;
            l.SetBounds(x, y, 150, 20);
            l.TextAlign = ContentAlignment.MiddleRight;
            return l;
        }

        private static string FmtSize(long b)
        {
            if (b >= 1073741824L) { return (b / 1073741824.0).ToString("0.0") + " GB"; }
            if (b >= 1048576L) { return (b / 1048576.0).ToString("0.0") + " MB"; }
            if (b >= 1024L) { return (b / 1024.0).ToString("0.0") + " KB"; }
            return b + " B";
        }

        private void Accept()
        {
            Permanent = (rbPermanent != null && rbPermanent.Checked);
            DialogResult = DialogResult.OK;
            Close();
        }
    }
    /// <summary>按关键词找壁纸：输入 → 列表 → 确认后换图。</summary>
    internal sealed class SearchForm : Form
    {
        private readonly TextBox txtKw;
        private readonly ListBox lst;
        private readonly Label lblStatus;
        private readonly Button btnSearch;
        private readonly Button btnApply;

        public string PickedDate { get; private set; }

        private readonly List<string[]> results = new List<string[]>();

        public SearchForm()
        {
            Text = Program.AppName + S.SrchTitle;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            ClientSize = new Size(620, 470);
            try { Font = new Font("Microsoft YaHei UI", 9F); } catch { }

            Label lbl = new Label();
            lbl.Text = S.SrchLabel;
            lbl.SetBounds(16, 14, 400, 20);
            Controls.Add(lbl);

            txtKw = new TextBox();
            txtKw.SetBounds(16, 38, 430, 24);
            Controls.Add(txtKw);

            btnSearch = new Button();
            btnSearch.Text = S.SrchBtn;
            btnSearch.SetBounds(456, 37, 78, 26);
            btnSearch.Click += delegate { DoSearch(); };
            Controls.Add(btnSearch);

            lst = new ListBox();
            lst.SetBounds(16, 74, 584, 316);
            lst.DoubleClick += delegate { Accept(); };
            lst.SelectedIndexChanged += delegate { btnApply.Enabled = (lst.SelectedIndex >= 0); };
            Controls.Add(lst);

            lblStatus = new Label();
            lblStatus.SetBounds(16, 396, 584, 18);
            lblStatus.ForeColor = SystemColors.GrayText;
            lblStatus.Text = S.SrchHint;
            Controls.Add(lblStatus);

            btnApply = new Button();
            btnApply.Text = S.SrchApply;
            btnApply.SetBounds(16, 422, 110, 32);
            btnApply.Enabled = false;
            btnApply.Click += delegate { Accept(); };
            Controls.Add(btnApply);

            Button btnCancel = new Button();
            btnCancel.Text = S.BtnCancel;
            btnCancel.SetBounds(514, 422, 86, 32);
            btnCancel.DialogResult = DialogResult.Cancel;
            Controls.Add(btnCancel);

            AcceptButton = btnSearch;
            CancelButton = btnCancel;
        }

        private void DoSearch()
        {
            string kw = txtKw.Text.Trim();
            if (kw.Length == 0) { return; }

            btnSearch.Enabled = false;
            lblStatus.Text = S.SrchSearching;
            lst.Items.Clear();
            results.Clear();
            Application.DoEvents();

            try
            {
                string outText = Program.RunScriptCapture("-Query \"" + kw.Replace("\"", "") + "\"");
                try
                {
                    string dbg = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "bwl-search-debug.log");
                    System.IO.File.AppendAllText(dbg, DateTime.Now.ToString("HH:mm:ss") + "  kw=" + kw + "  len=" + (outText == null ? -1 : outText.Length) + Environment.NewLine);
                    if (!string.IsNullOrEmpty(outText))
                    {
                        string head = outText.Length > 300 ? outText.Substring(0, 300) : outText;
                        System.IO.File.AppendAllText(dbg, "  head=" + head.Replace(Environment.NewLine, "|") + Environment.NewLine);
                    }
                }
                catch { }
                ParseResults(outText);
            }
            catch (Exception ex)
            {
                lblStatus.Text = ex.Message;
            }

            btnSearch.Enabled = true;

            if (lst.Items.Count == 0)
            {
                lblStatus.Text = S.SrchNone;
                btnApply.Enabled = false;
                return;
            }
            lblStatus.Text = S.SrchCount + lst.Items.Count + S.SrchCountTail;
            lst.SelectedIndex = 0;
        }

        private void ParseResults(string json)
        {
            if (string.IsNullOrEmpty(json)) { return; }
            int a = json.IndexOf('[');
            int b = json.LastIndexOf(']');
            if (a < 0 || b <= a) { return; }
            json = json.Substring(a, b - a + 1);

            JavaScriptSerializer js = new JavaScriptSerializer();
            List<Dictionary<string, object>> arr = null;
            try { arr = js.Deserialize<List<Dictionary<string, object>>>(json); } catch { return; }
            if (arr == null) { return; }

            foreach (Dictionary<string, object> d in arr)
            {
                if (d == null) { continue; }
                string date = DictStr(d, "date");
                string title = DictStr(d, "title");
                string cpr = DictStr(d, "copyright");
                if (date.Length == 0) { continue; }

                string shortCpr = cpr;
                int cut = shortCpr.IndexOf('(');
                if (cut > 0) { shortCpr = shortCpr.Substring(0, cut).Trim(); }
                if (shortCpr.Length > 36) { shortCpr = shortCpr.Substring(0, 36) + "…"; }

                string label = date + "  " + (title.Length > 0 ? title : "(无标题)") + "    " + shortCpr;
                lst.Items.Add(label);
                results.Add(new string[] { date, title });
            }
        }

        private static string DictStr(Dictionary<string, object> d, string k)
        {
            if (d == null || !d.ContainsKey(k) || d[k] == null) { return ""; }
            return d[k].ToString();
        }

        private void Accept()
        {
            if (lst.SelectedIndex < 0 || lst.SelectedIndex >= results.Count) { return; }
            string date = results[lst.SelectedIndex][0];
            string title = results[lst.SelectedIndex][1];

            string q = S.SrchConfirm + "\r\n\r\n" + date + "  " + title;
            if (MessageBox.Show(q, Program.AppName + S.SrchConfirmTtl,
                    MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
            {
                return;
            }

            PickedDate = date;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
    /// <summary>「这张图是哪儿」窗口：缩略图 + 信息 + 两个外链按钮。</summary>
    internal sealed class InfoForm : Form
    {
        private const string MoreUrl = "https://bing.wdbyte.com/zh-cn/";
        private const int ThumbW = 240;
        private const int ThumbH = 135;

        private readonly Bitmap thumb;

        /// <summary>读入内存再解码，避免 Image.FromFile 锁定壁纸文件。</summary>
        private static Bitmap MakeThumb(string file, int w, int h)
        {
            try
            {
                if (string.IsNullOrEmpty(file) || !File.Exists(file)) { return null; }
                byte[] bytes = File.ReadAllBytes(file);
                using (MemoryStream ms = new MemoryStream(bytes))
                using (Image src = Image.FromStream(ms))
                {
                    Bitmap bmp = new Bitmap(w, h);
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                        g.DrawImage(src, 0, 0, w, h);
                    }
                    return bmp;
                }
            }
            catch { return null; }
        }

        public InfoForm(string date, string title, string copyright, string file, string sizeText, string searchUrl)
        {
            Text = Program.AppName + S.InfoTitle;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            ClientSize = new Size(620, 350);
            try { Font = new Font("Microsoft YaHei UI", 9F); } catch { }

            // 左侧缩略图
            thumb = MakeThumb(file, ThumbW, ThumbH);
            int textLeft = 20;
            int textWidth = 580;
            if (thumb != null)
            {
                PictureBox pic = new PictureBox();
                pic.Image = thumb;
                pic.SizeMode = PictureBoxSizeMode.Zoom;
                pic.SetBounds(20, 20, ThumbW, ThumbH);
                pic.BorderStyle = BorderStyle.FixedSingle;
                Controls.Add(pic);
                textLeft = 20 + ThumbW + 18;
                textWidth = 580 - ThumbW - 18;
            }

            int y = thumb != null ? 20 : 16;

            Label lbl = new Label();
            lbl.AutoSize = false;
            lbl.SetBounds(textLeft, y, textWidth, thumb != null ? ThumbH : 220);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine(S.InfoDate + date);
            sb.AppendLine();
            if (title.Length > 0) { sb.AppendLine(S.InfoTitleLine + title); sb.AppendLine(); }
            if (copyright.Length > 0) { sb.AppendLine(S.InfoCopy + copyright); sb.AppendLine(); }
            if (file.Length > 0) { sb.AppendLine(S.InfoFile + file); }
            if (sizeText.Length > 0) { sb.AppendLine(S.InfoSize + sizeText); }

            lbl.Text = sb.ToString().Trim();
            Controls.Add(lbl);

            int by = 20 + ThumbH + 26;
            if (thumb == null) { by = 268; }

            Button btnSearch = new Button();
            btnSearch.Text = S.InfoBtnSearch;
            btnSearch.SetBounds(20, by, 130, 32);
            btnSearch.Enabled = searchUrl.Length > 0;
            btnSearch.Click += delegate { Program.OpenUrl(searchUrl); };
            Controls.Add(btnSearch);

            Button btnMore = new Button();
            btnMore.Text = S.InfoBtnMore;
            btnMore.SetBounds(162, by, 130, 32);
            btnMore.Click += delegate { Program.OpenUrl(MoreUrl); };
            Controls.Add(btnMore);

            Button btnClose = new Button();
            btnClose.Text = S.BtnOK;
            btnClose.SetBounds(514, by, 86, 32);
            btnClose.DialogResult = DialogResult.OK;
            Controls.Add(btnClose);

            AcceptButton = btnClose;
            CancelButton = btnClose;

            // 窗口高度跟着内容走
            ClientSize = new Size(620, by + 32 + 20);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && thumb != null) { thumb.Dispose(); }
            base.Dispose(disposing);
        }
    }
    internal sealed class UninstallForm : Form
    {
        private readonly CheckBox chkFav;
        private readonly CheckBox chkArch;
        private readonly Label lbl;

        public bool KeepFavorites { get { return chkFav.Checked; } }
        public bool KeepArchive { get { return chkArch.Checked; } }

        public UninstallForm(int favCount, int archCount, int cacheCount, double cacheMB, string installPath)
        {
            Text = Program.AppName + (S.IsEn ? " · Uninstall" : " · 卸载");
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            ClientSize = new Size(470, 396);
            try { Font = new Font("Microsoft YaHei UI", 9F); } catch { }

            lbl = new Label();
            lbl.AutoSize = false;
            lbl.SetBounds(16, 16, 438, 150);
            lbl.Text =
                S.DlHead + "\r\n" +
                S.DlStartup + "\r\n" +
                S.DlTask + "\r\n" +
                S.DlUninstEntry + "\r\n" +
                S.CacheLineN(cacheCount, cacheMB) + "\r\n" +
                S.DlArchiveIdx + "\r\n\r\n" +
                S.DlKeepProgram;
            Controls.Add(lbl);

            chkFav = new CheckBox();
            chkFav.SetBounds(16, 176, 438, 24);
            chkFav.Text = S.KeepFavN(favCount);
            chkFav.Checked = true;
            chkFav.Enabled = favCount > 0;
            Controls.Add(chkFav);

            chkArch = new CheckBox();
            chkArch.SetBounds(16, 206, 438, 24);
            chkArch.Text = S.KeepArchN(archCount);
            chkArch.Checked = true;
            chkArch.Enabled = archCount > 0;
            Controls.Add(chkArch);

            Label hint = new Label();
            hint.SetBounds(36, 236, 418, 54);
            hint.ForeColor = SystemColors.GrayText;
            hint.Text = S.DlUninstHint +
                        (archCount > 0
                            ? S.ArchNoteAlt
                            : S.DlNoArch);
            Controls.Add(hint);

            Label pathLabel = new Label();
            pathLabel.SetBounds(16, 300, 438, 20);
            pathLabel.ForeColor = SystemColors.GrayText;
            pathLabel.Text = S.DlProgramDir + installPath;
            Controls.Add(pathLabel);

            Button ok = new Button();
            ok.Text = S.BtnUninstall;
            ok.SetBounds(270, 336, 88, 30);
            ok.DialogResult = DialogResult.OK;
            Controls.Add(ok);

            Button cancel = new Button();
            cancel.Text = S.BtnCancel;
            cancel.SetBounds(366, 336, 88, 30);
            cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
        }
    }
    // -------------------------------------------------------------------- 安装向导

    internal sealed class SetupForm : Form
    {
        private readonly TextBox txtPath;

        public string SelectedPath { get { return txtPath.Text.Trim(); } }
        public bool PortableMode { get; private set; }

        public SetupForm(string defaultPath)
        {
            Text = Program.AppName + (S.IsEn ? " · Setup" : " · 安装");
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            ClientSize = new Size(540, 296);
            try { Font = new Font("Microsoft YaHei UI", 9F); }
            catch { }

            Label pre = new Label();
            pre.SetBounds(16, 76, 500, 40);
            pre.AutoSize = false;
            pre.ForeColor = Color.FromArgb(198, 92, 0);
            pre.Text = S.IsEn
                ? "Your desktop wallpaper will change to today's Bing image right after install."
                : "安装后会立即把桌面壁纸换成今天的必应图。";
            Controls.Add(pre);

            Label lbl = new Label();
            lbl.Text = S.IsEn ? "Install location:" : "选择安装位置：";
            lbl.SetBounds(16, 16, 200, 20);
            Controls.Add(lbl);

            txtPath = new TextBox();
            txtPath.Text = defaultPath;
            txtPath.SetBounds(16, 40, 424, 23);
            Controls.Add(txtPath);

            Button btnBrowse = new Button();
            btnBrowse.Text = S.BtnBrowse;
            btnBrowse.SetBounds(448, 39, 76, 25);
            btnBrowse.Click += delegate
            {
                using (FolderBrowserDialog d = new FolderBrowserDialog())
                {
                    d.Description = S.IsEn ? ("Choose a folder for " + Program.AppName) : ("选择 " + Program.AppName + " 的安装文件夹");
                    d.SelectedPath = Directory.Exists(txtPath.Text) ? txtPath.Text : Path.GetDirectoryName(txtPath.Text);
                    d.ShowNewFolderButton = true;
                    if (d.ShowDialog(this) == DialogResult.OK) { txtPath.Text = d.SelectedPath; }
                }
            };
            Controls.Add(btnBrowse);

            Label hint = new Label();
            hint.SetBounds(16, 74, 508, 108);
            hint.Text =
                S.SetupNote1 + S.SetupNote2 + S.SetupNote3 + S.SetupNote4;
            Controls.Add(hint);

            Button btnInstall = new Button();
            btnInstall.Text = S.BtnInstallTx;
            btnInstall.SetBounds(288, 214, 88, 30);
            btnInstall.Click += delegate
            {
                string p = SelectedPath;
                if (p.Length == 0) { MessageBox.Show(S.WarnNoPath, Program.AppName); return; }
                try { Directory.CreateDirectory(p); }
                catch (Exception ex)
                {
                    MessageBox.Show(S.WarnBadDir + ex.Message, Program.AppName);
                    return;
                }
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(btnInstall);

            Button btnPortable = new Button();
            btnPortable.Text = S.BtnPortable;
            btnPortable.SetBounds(184, 214, 96, 30);
            btnPortable.Click += delegate
            {
                PortableMode = true;
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(btnPortable);

            Button btnCancel = new Button();
            btnCancel.Text = S.BtnCancel;
            btnCancel.SetBounds(384, 214, 88, 30);
            btnCancel.DialogResult = DialogResult.Cancel;
            Controls.Add(btnCancel);

            AcceptButton = btnInstall;
            CancelButton = btnCancel;
        }
    }
}
