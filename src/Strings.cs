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

namespace BingWallpaperLite
{
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
        public static string SrchBtnRandom    { get { return T("随机", "Random"); } }
        public static string SrchEmptyAsk     { get { return T("没有填写关键词。\r\n要从图库里随机抽一组搜索结果吗？", "No keyword entered.\r\nPick a random batch of results from the library?"); } }
        public static string SrchEmptyTtl     { get { return T("  没有关键词", "  No keyword"); } }
        public static string SrchLibEmpty     { get { return T("图库里抽不出结果（索引为空）。", "Nothing to pick (index is empty)."); } }
        public static string SrchNeedSearch   { get { return T("还没有搜索结果，先点「搜索」。", "No results yet — click Search first."); } }
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
        public static string SetupBackupWarn { get { return T("安装前会先把你当前的桌面壁纸备份一份到桌面（同名只备一次）。\r\n安装后立即把桌面壁纸换成今天的必应图。", "Your current wallpaper is copied to the Desktop first (once per image),\r\nthen replaced with today's Bing image."); } }
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

        // ---------------- 关于窗口 / 检查更新
        public static string AboutBtnReleases { get { return T("打开项目页", "Open project page"); } }
        public static string AboutBtnRecheck  { get { return T("重新检查", "Check again"); } }
        public static string AboutBtnClose    { get { return T("关闭", "Close"); } }
        public static string UpdChecking      { get { return T("检查更新…", "Checking for updates…"); } }
        public static string UpdFailedPrefix  { get { return T("检查失败：", "Check failed: "); } }
        public static string UpdLatest(string v) { return _en ? ("Already latest v" + v) : ("已是最新 v" + v); }
        public static string UpdNewer(string a, string b) { return _en ? ("New version v" + a + " (current v" + b + ")") : ("发现新版本 v" + a + "（当前 v" + b + "）"); }

        // ---------------- 覆盖安装
        public static string SetupFound(string v) { return _en ? ("Existing install v" + v + " found; will upgrade in place, keeping config and library.") : ("检测到已有安装 v" + v + "，将覆盖升级；配置与图库会保留"); }
        public static string SetupNotFound { get { return T("未检测到已有安装，将全新安装", "No existing install found; a fresh install will be performed"); } }
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

}
