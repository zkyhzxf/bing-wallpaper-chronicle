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
        public static string RandomFav        { get { return T("随机换一张", "Switch to a random favorite"); } }
        public static string LockOff          { get { return T("☐ 暂停自动换图", "☐ Pause auto-change"); } }
        public static string LockOn           { get { return T("☑ 已暂停自动换图", "☑ Auto-change paused"); } }
        public static string NotifyOn         { get { return T("☑ 换图后通知", "☑ Notify on change"); } }
        public static string NotifyOff        { get { return T("☐ 换图后通知", "☐ Notify on change"); } }
        public static string StartupOn        { get { return T("☑ 开机启动", "☑ Run at startup"); } }
        public static string StartupOff       { get { return T("☐ 开机启动", "☐ Run at startup"); } }
        public static string CleanMenu        { get { return T("清理", "Cleanup"); } }
        public static string AutoCleanOn      { get { return T("☑ 自动清理缓存", "☑ Auto-clean cache"); } }
        public static string AutoCleanOff     { get { return T("☐ 自动清理缓存", "☐ Auto-clean cache"); } }
        public static string Clean7           { get { return T("清理缓存（保留 7 天）", "Clean cache (keep 7 days)"); } }
        public static string Clean30          { get { return T("清理缓存（保留 30 天）", "Clean cache (keep 30 days)"); } }
        public static string CleanArchive     { get { return T("清理归档图片…", "Clean archived images…"); } }
        public static string CleanAll         { get { return T("清空缓存（保留当前）…", "Clear cache (keep current)…"); } }
        public static string ArchiveMenu      { get { return T("增强数据源", "Data sources"); } }
        public static string ArchiveOn        { get { return T("☑ 启用第三方归档", "☑ Enable third-party archive"); } }
        public static string ArchiveOff       { get { return T("☐ 启用第三方归档", "☐ Enable third-party archive"); } }
        public static string OpenArchiveDir   { get { return T("打开归档目录", "Open archive folder"); } }
        public static string ClearArchiveIdx  { get { return T("清除归档索引缓存", "Clear archive index cache"); } }
        public static string UpdateIndex      { get { return T("更新索引（联网）", "Update index (online)"); } }
        public static string BIndexOk         { get { return T("索引已更新", "Index updated"); } }
        public static string IndexOkHint      { get { return T("关键词搜索会用到最新数据", "Keyword search now uses the latest data"); } }
        public static string UpdateTimeMenu   { get { return T("更新时间", "Update time"); } }
        public static string SearchMenu       { get { return T("按关键词找…", "Search by keyword…"); } }
        public static string AiMenu           { get { return T("AI 调用…", "AI access…"); } }

        public static string SrchTitle        { get { return T(" · 按关键词找壁纸", " · Search wallpapers"); } }
        public static string SrchLabel        { get { return T("关键词（空格分隔，可多个）：", "Keywords (space-separated):"); } }
        public static string SrchBtn          { get { return T("搜索", "Search"); } }
        public static string SrchApply        { get { return T("换这张", "Use this"); } }
        public static string SrchRefresh      { get { return T("刷新索引", "Refresh index"); } }
        public static string SrchHint         { get { return T("索引覆盖 2009-07 至今（6339 天）；最近 8 天实时补齐。", "Index covers Jul 2009 onward (6339 days); the latest 8 days are always fetched live."); } }
        public static string SrchSearching    { get { return T("搜索中…", "Searching…"); } }
        public static string SrchBtnRandom    { get { return T("随机", "Random"); } }
        public static string SrchLibEmpty     { get { return T("图库里抽不出结果（索引为空）。", "Nothing to pick (index is empty)."); } }
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
        public static string CleanArchiveIndexMenu { get { return T("清除归档索引", "Clear archive index"); } }
        public static string CleanResult(int n) { return _en ? ("Cleaned " + n + " items") : ("清理完成：删除 " + n + " 个"); }
        public static string CleanDoneCache(int rm, int pt) { return _en ? ("Cleaned " + rm + ", protected " + pt) : ("清理完成：删除 " + rm + " 张，保护 " + pt + " 张"); }
        public static string CleanDoneAll(int rm, int kept) { return _en ? ("Cache cleared — " + rm + " item(s) removed (incl. photo-wall thumbnails), current wallpaper kept") : ("缓存已清空：删除 " + rm + " 项（含照片墙缩略图），当前壁纸保留"); }
        public static string CleanDoneFav(int rm, int moved) { return _en ? ("Favorites cleared — " + rm + " removed, " + moved + " moved back to cache") : ("收藏夹已清空：删除 " + rm + " 张，当前壁纸 " + moved + " 张已移回缓存"); }
        public static string CleanDoneIdx(int rm) { return _en ? ("Removed " + rm + " index files") : ("删除 " + rm + " 个索引文件"); }
        public static string CleanWithIndex { get { return T("同时清理索引（15 个月份 + 全量索引）", "Also clear indexes (monthly + full)"); } }
        public static string CleanWithIndexHint { get { return T("删掉的是目录册，不是图片：之后取历史图要联网查，搜索会重下索引", "Removes the catalogue, not the images: later lookups go online and search re-downloads the index"); } }
        public static string CleanWithStories(int n, string size)
        {
            return _en
                ? ("Also clear the daily-story cache (" + n + " · " + size + ")")
                : ("同时清理每日故事缓存（" + n + " 条 · " + size + "）");
        }
        public static string CleanWithStoriesHint { get { return T("故事是本地缓存，删了下次查看时按需重抓；不影响壁纸与索引", "Story text is a local cache — re-fetched on demand, wallpaper and indexes untouched"); } }
        public static string CleanWithOld(int n, double mb)
        {
            return _en
                ? ("Also clear legacy archive (" + n + " images / " + mb.ToString("0.0") + " MB, 2009-08 ~ 2016-02)")
                : ("同时清理老存档（" + n + " 张 / " + mb.ToString("0.0") + " MB，2009-08 ~ 2016-02）");
        }
        public static string CleanWithOldHint { get { return T("老存档来自第三方镜像，删了要重新联网取；分辨率 1366×768", "Legacy archive comes from a third-party mirror; deleting it means downloading again"); } }
        public static string CleanOldInline(int n, double mb)
        {
            return _en ? (" · incl. " + n + " legacy / " + mb.ToString("0.0") + " MB") : ("（含老存档 " + n + " 张 · " + mb.ToString("0.0") + " MB）");
        }
        public static string CleanIdxSuffix(int n) { return _en ? ("; " + n + " index files removed") : ("；另删 " + n + " 个索引文件"); }
        public static string CleanDoneArch(int rm, int fav, int cur)
        {
            int kept = fav + cur;
            return _en
                ? ("Removed " + rm + "; kept " + kept + ": favorites " + fav + ", current " + cur)
                : ("删除 " + rm + " 张；未删 " + kept + " 张：收藏 " + fav + "、当前壁纸 " + cur);
        }
        public static string CleanResult2(int rm, int pt)
        {
            return _en
                ? ("Cleaned " + rm + " items, " + pt + " protected")
                : ("清理完成：删除 " + rm + " 个，保护 " + pt + " 个");
        }

        // ---------------- 换过的图
        public static string PinMenu      { get { return T("换过的图…", "Switched-to images…"); } }
        public static string PinMenuN(int n, double mb) { return _en ? ("Switched-to images… (" + n + " items · " + mb.ToString("0.0") + " MB)") : ("换过的图…（" + n + " 项 · " + mb.ToString("0.0") + " MB）"); }
        public static string PinTitle     { get { return T(" · 换过的图", " · Switched-to images"); } }
        public static string PinIntro1    { get { return T("你换过壁纸的历史图都记在这里。收藏过的也在，位置列会标出来。", "Historical images you switched to as wallpaper. Favorites are here too — the Location column marks them."); } }
        public static string PinIntro2    { get { return T("自动清理不会删这些图。想永久留着某张，点「加入收藏」；不想让它占着名单，点「移出名单」—— 移出只影响自动清理，不删文件。", "Auto-clean never removes these. Favorite one to keep it for good, or remove it from the list — that only affects auto-clean, it does not delete files."); } }
        public static string PinColDate   { get { return T("日期", "Date"); } }
        public static string PinColTitle  { get { return T("标题", "Title"); } }
        public static string PinColSize   { get { return T("体积", "Size"); } }
        public static string PinColWhere  { get { return T("位置", "Location"); } }
        public static string PinLocFav    { get { return T("收藏", "Favorite"); } }
        public static string PinLocCache  { get { return T("缓存", "Cache"); } }
        public static string PinLocArch   { get { return T("归档", "Archive"); } }
        public static string PinLocOld    { get { return T("老存档", "Legacy"); } }
        public static string PinGone      { get { return T("文件已不在", "file missing"); } }
        public static string PinNoTitle   { get { return T("(无标题)", "(no title)"); } }
        public static string PinRemove    { get { return T("移出名单", "Remove"); } }
        public static string PinOpenFile  { get { return T("打开文件", "Open file"); } }
        public static string PinClose     { get { return T("关闭", "Close"); } }
        public static string PinHintMore  { get { return T("这些日期在自动清理时被跳过，可在「换过的图」里移出。", "Skipped during auto-clean; manage them under Switched-to images."); } }
        public static string PinNote      { get { return T("自动清理缓存（30 天规则）会跳过这些日期。手动的「清空缓存」「清理归档图片」不看这份清单，只保留收藏和当前壁纸。归档满了会分两轮清：先删名单外的，空间还不够才动名单里的。", "The 30-day auto-clean skips these dates. Manual \"clear cache\" and \"clean archived images\" ignore this list and keep only favorites and the current wallpaper. When the archive fills up it clears in two passes: unlisted images first, only then the ones on this list."); } }
        public static string PinSum(int n, double mb) { return _en ? (n + " entries · " + mb.ToString("0.0") + " MB") : ("共 " + n + " 项 · " + mb.ToString("0.0") + " MB"); }
        public static string FavSumFiltered(int hit, int total, double mb) { return _en ? (hit + " of " + total + " · " + mb.ToString("0.0") + " MB") : ("匹配 " + hit + " / 共 " + total + " 项 · " + mb.ToString("0.0") + " MB"); }
        public static string FavSearchHint { get { return T("搜索标题/版权/日期", "search title/copyright/date"); } }
        public static string PinConfirm(int n) { return _en ? ("These " + n + " dates will no longer be skipped by auto-clean and may be removed next time it runs. Nothing is moved out now. Continue?") : ("这 " + n + " 天将不再被自动清理跳过，下次清理可能删掉它们。本次操作不移出任何文件。确定吗？"); }
        /// <summary>按位置分类说明移出名单的实际效果 —— 老存档和收藏本来就不受自动清理，移出对它们没有作用。</summary>
        public static string PinConfirm2(int total, int affected, int legacy, int fav)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            if (_en)
            {
                sb.AppendLine("Remove " + total + " date(s) from the list:");
                if (affected > 0) { sb.AppendLine("· " + affected + " in cache or archive — the next clean-up may delete them"); }
                if (legacy > 0) { sb.AppendLine("· " + legacy + " in the legacy archive — never auto-cleaned, will be skipped. To delete: Clean → Clean archive"); }
                if (fav > 0) { sb.AppendLine("· " + fav + " favorited — favorites are unaffected, will be skipped"); }
                sb.Append("No files are deleted. Continue?");
                return sb.ToString();
            }
            sb.AppendLine("将从名单移出 " + total + " 天：");
            if (affected > 0) { sb.AppendLine("· " + affected + " 天在缓存或归档里 —— 移出后下次清理可能删掉它们"); }
            if (legacy > 0) { sb.AppendLine("· " + legacy + " 天是老存档 —— 本来就不受自动清理，会自动跳过；要删请走 清理 → 清理归档"); }
            if (fav > 0) { sb.AppendLine("· " + fav + " 天已收藏 —— 收藏不受影响，会自动跳过"); }
            sb.Append("本次操作不删除任何文件。确定吗？");
            return sb.ToString();
        }
        public static string PinNoEffectTakeover
        {
            get
            {
                return T("当前是接管模式：这个目录是只读的，软件不会清理它里面的任何图。\r\n移出名单在这里不改变任何东西，所以没有执行。\r\n\r\n要处理这些图，请直接在资源管理器里操作那个目录。",
                         "Takeover mode: this folder is read-only for the app and is never auto-cleaned.\r\nRemoving entries changes nothing here, so nothing was done.\r\n\r\nManage those images directly in Explorer.");
            }
        }
        public static string PinNoEffect
        {
            get
            {
                return T("选中的都是老存档或已收藏的图。它们本来就不受自动清理，移出名单没有实际作用，也会继续留在列表里。\r\n\r\n老存档要删：清理 → 清理归档\r\n收藏要删：在「收藏管理」里取消收藏",
                         "Everything you selected is either in the legacy archive or favorited. Those are never auto-cleaned, so removing them from the list has no effect — they stay listed.\r\n\r\nTo delete legacy images: Clean → Clean archive\r\nTo delete a favorite: un-favorite it in Manage favorites");
            }
        }
        public static string PinPurgeDead(int n) { return _en ? ("Purge missing (" + n + ")") : ("清理失效项（" + n + "）"); }
        public static string PinFav        { get { return T("加入收藏", "Add to favorites"); } }
        public static string PinFavDone    { get { return T("已加入收藏", "Added to favorites"); } }
        public static string PinFavBatch(int ok, int skip, int fail)
        {
            if (fail == 0 && skip == 0) { return _en ? (ok + " added to favorites") : ("已加入收藏：" + ok + " 张"); }
            return _en
                ? ("Done — added " + ok + ", already saved " + skip + ", failed " + fail)
                : ("完成：成功 " + ok + " 张，已在收藏 " + skip + " 张，失败 " + fail + " 张");
        }
        public static string FavManager    { get { return T("收藏管理…", "Manage favorites…"); } }
        public static string FavManagerN(int n, double mb) { return _en ? ("Manage favorites… (" + n + " · " + mb.ToString("0.0") + " MB)") : ("收藏管理…（" + n + " 张 · " + mb.ToString("0.0") + " MB）"); }
        public static string FavTitle      { get { return T(" · 收藏管理", " · Manage favorites"); } }
        public static string FavIntro      { get { return T("双击任意一张可直接设为桌面壁纸。勾选多张后点「移出收藏」，可以一次移出；文件只是退回缓存，本窗口不删除任何文件，之后进入 30 天清理规则。", "Double-click any row to set it as the desktop wallpaper. Tick several rows, then click Remove to move them all at once — files only go back to cache, nothing is deleted here, and they fall under the 30-day rule afterwards."); } }
        public static string FavSetWall    { get { return T("设置壁纸", "Set as wallpaper"); } }
        public static string FavOpenFolder { get { return T("打开文件夹", "Show in folder"); } }
        public static string FavInvert     { get { return T("反选", "Invert"); } }
        public static string FavSum(int n, double mb) { return _en ? (n + " items · " + mb.ToString("0.0") + " MB") : ("收藏 " + n + " 张 · " + mb.ToString("0.0") + " MB"); }
        public static string FavRemove     { get { return T("移出收藏", "Remove"); } }
        public static string FavConfirm(int n) { return _en ? ("Move these " + n + " images back to cache? Nothing is deleted now, but they lose the favorites exemption and may be auto-cleaned after 30 days.") : ("把这 " + n + " 张移出收藏？文件会退回缓存，本次不删除；之后不再享受收藏的免清理，30 天后可能被自动清掉。"); }
        public static string FavDone(int n) { return _en ? ("Moved " + n + " files back to cache") : ("已移出 " + n + " 张，文件已退回缓存"); }
        public static string PinFavAlready { get { return T("这张已经在收藏里了", "Already in favorites"); } }
        public static string PinFavNoFile  { get { return T("文件已不在，无法收藏", "File is gone, cannot favorite"); } }
        public static string PinNoSelection { get { return T("选中一行查看预览", "Select a row to preview"); } }
        public static string PinListEmpty  { get { return T("名单是空的", "List is empty"); } }
        public static string CleanTotal       { get { return T("图片总数", "Total files"); } }
        public static string CleanSize        { get { return T("占用空间", "Total size"); } }
        public static string CleanFavProtected{ get { return T("已被收藏（保留）", "Favorited (kept)"); } }
        public static string CleanPinProtected{ get { return T("换过的图（不会自动清，可在此调整）", "Switched-to (never auto-cleaned, adjustable here)"); } }
        public static string CleanCurProtected{ get { return T("当前壁纸（保留，删了桌面会黑）", "Current (kept — deleting it would blank the desktop)"); } }
        public static string CleanCurMoved  { get { return T("当前壁纸（将移回缓存）", "Current (will return to cache)"); } }
        public static string CleanActual      { get { return T("实际可清理", "Will remove"); } }
        public static string CleanArchRow     { get { return T("归档图片", "Archive images"); } }
        public static string CleanOldRow      { get { return T("老存档", "Legacy archive"); } }
        public static string CleanOldNote     { get { return T("第三方镜像，不受任何自动清理", "Third-party mirror, never auto-cleaned"); } }
        public static string CleanModeTitle   { get { return T("删除方式", "Delete method"); } }
        public static string CleanToRecycle   { get { return T("进回收站", "Move to Recycle Bin"); } }
        public static string CleanToRecycle2  { get { return T("可恢复，但磁盘空间不释放", "Recoverable; frees no disk space"); } }
        public static string CleanToDelete    { get { return T("彻底删除", "Delete permanently"); } }
        public static string CleanToDelete2   { get { return T("立即释放空间，不可恢复", "Frees space now; not recoverable"); } }
        public static string CleanStart       { get { return T("开始清理", "Start"); } }
        public static string CleanNothing     { get { return T("全部受保护，没有可清理的内容。", "Nothing to clean — everything is protected."); } }
        public static string CleanIndexHint   { get { return T("索引是目录册，删掉后取历史图要联网查；搜索会重新下载索引。", "Indexes are just catalogues: after removal, history lookups go online and search re-downloads them."); } }
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
        public static string BOldest          { get { return T("没有更早的照片了", "No older images"); } }
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
        public static string BWaitingHint     { get { return T("Bing 还没发布今天的图，8:10 和 12:10 会自动再查一次", "Bing hasn't published today's image yet; it will retry at 08:10 and 12:10"); } }

        // ---------------- 计划任务状态
        public static string TaskMissing      { get { return T("计划任务：未注册 ⚠ 点这里修复", "Scheduled task: not registered ⚠ click to fix"); } }
        public static string TaskNormal       { get { return T("计划任务：正常", "Scheduled task: OK"); } }
        public static string TaskUnknown      { get { return T("计划任务：状态未知", "Scheduled task: unknown"); } }
        public static string TaskNext         { get { return T(" · 下次 ", " · next "); } }

        // ---------------- 按钮
        public static string BtnUninstall     { get { return T("卸载", "Uninstall"); } }
        public static string BtnCancel        { get { return T("取消", "Cancel"); } }
        public static string BtnInstall       { get { return T("安装", "Install"); } }

        public static string BtnBrowse        { get { return T("浏览…", "Browse…"); } }
        public static string BtnApply         { get { return T("换这张", "Use this"); } }
        public static string BtnToday         { get { return T("今天", "Today"); } }
        public static string LinkTitle        { get { return T(" · 下载链接", " · Download links"); } }
        public static string LinkGenerating   { get { return T("正在生成链接…", "Generating links…"); } }
        public static string LinkCopy         { get { return T("复制全部", "Copy all"); } }
        public static string LinkCopied       { get { return T("已复制 ✓", "Copied ✓"); } }
        public static string LinkSave         { get { return T("保存为 txt…", "Save as txt…"); } }
        public static string LinkNoLinks      { get { return T("这个范围里没有可用链接", "No links in this range"); } }
        public static string BtnExportLinks   { get { return T("只导出链接", "Links only"); } }
        public static string LinkStat(int ok, int miss)
        {
            if (_en) { return (ok + " links" + (miss > 0 ? (", " + miss + " days skipped") : "")); }
            return ("共 " + ok + " 条" + (miss > 0 ? ("，跳过 " + miss + " 天无记录") : ""));
        }
        public static string LinkSaved(string path) { return _en ? ("Saved to " + path) : ("已保存到 " + path); }
        public static string CalNoTitle       { get { return T("索引里没有这一天的标题", "No title for this date"); } }
        public static string CalLocFav        { get { return T("● 已在本地 · 收藏", "● Local · favorites"); } }
        public static string CalLocCache      { get { return T("● 已在本地 · 缓存", "● Local · cache"); } }
        public static string CalLocArch       { get { return T("● 已在本地 · 归档", "● Local · archive"); } }
        public static string CalLocOld        { get { return T("● 已在本地 · 老存档", "● Local · old archive"); } }
        public static string CalLocNone       { get { return T("○ 本地没有 · 需联网", "○ Not local · needs network"); } }
        public static string CalBoldHint      { get { return T("粗体：本地已有这张图", "Bold: already local"); } }
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
        public static string DlArchNote        { get { return T("归档里的图是你从历史里挑出来的，可用「选日期」重新设为壁纸。归档有 500 MB 上限，满了会从最旧的开始自动清。", "Images you picked from history; reuse them via Pick a date. The archive is capped at 500 MB and clears oldest-first when full."); } }
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
        public static string ConfirmCleanAll   { get { return T("将删除缓存目录里的壁纸（当前正在用的那张保留），并清空照片墙缩略图。\r\n\r\n照片墙的临时原图不在此列 —— 关闭照片墙时会单独问你保留还是丢弃。确定吗？", "Delete cached wallpapers (the current one is kept) and clear photo-wall thumbnails.\r\n\r\nPhoto-wall temporary images are handled separately when you close that window. Continue?"); } }
        public static string ConfirmCleanTtl   { get { return T(" · 清理缓存", " · Clear cache"); } }
        public static string ConfirmArchTtl    { get { return T(" · 清理历史归档", " · Clear history archive"); } }
        public static string ConfirmArchBody   { get { return T(" 张图移入回收站？\r\n\r\n当前正在显示的壁纸不动。之后可从回收站找回，\r\n也可以通过「选日期」重新下载。", " images will be moved to the Recycle Bin.\r\n\r\nThe current wallpaper is left untouched. You can restore from the Recycle Bin\r\nor re-download later via Pick a date."); } }
        public static string BRepairKOHint     { get { return T("计划任务仍不可用，请用管理员身份运行一次或查看日志", "Task still unavailable; run as administrator once, or check the log"); } }
        public static string BRepairedHint     { get { return T("计划任务已按当前设置重新注册", "Task re-registered with the current settings"); } }
        public static string TaskAutoOn        { get { return T("自动换图：运行中", "Auto-change: running"); } }
        public static string TaskAutoOff       { get { return T("自动换图：已暂停", "Auto-change: paused"); } }
        public static string TaskPausedSuffix  { get { return T(" · 已暂停", " · paused"); } }
        public static string TaskDaily(string hm)   { return T("每天 ", "Daily ") + hm; }
        public static string TaskTimeCustom    { get { return T("自定义时间…", "Custom time…"); } }
        public static string TaskTimePickHint  { get { return T("每天几点自动换图？", "What time should the wallpaper change?"); } }
        public static string TaskRereg         { get { return T("立即重新注册任务", "Re-register task now"); } }
        public static string TaskDoneShort     { get { return T("计划任务已重新注册", "Task re-registered"); } }
        public static string TaskDoneLocked    { get { return T("计划任务已重新注册 · 自动换图仍处暂停状态", "Task re-registered; auto-change is still paused"); } }
        public static string TaskTimeChanged(string hm) { return T("已改为 ", "Now at ") + hm; }
        public static string TaskNextShort(string next) { return T("下次 ", "next ") + next; }
        public static string BReadyHint        { get { return T("壁纸每天 0:10 自动更新，8:10 与 12:10 会补查一次。右键托盘图标可换图、收藏、暂停。", "Wallpaper updates daily at 00:10, with retries at 08:10 and 12:10. Right-click the tray icon to change, favorite, or pause."); } }
        public static string NoImageHint       { get { return T("换一天试试", "Try another date"); } }
        public static string NeedArchHint      { get { return T("2025-03 之前的日期要开启「增强数据源 ▸ 启用第三方归档」才能取到", "Dates before Mar 2025 need Data sources → Enable third-party archive"); } }
        public static string ArchDownHint      { get { return T("检查网络或代理，或在「增强数据源」里临时关闭后重试", "Check network or proxy, or disable Data sources and retry"); } }
        public static string LatestHint        { get { return T("当前就是最新的一张；更早的可用「选日期」", "This is already the newest; use Pick a date for older ones"); } }
        public static string OldestHint        { get { return T("已到能取到的最早一天（2009-07-13），可在「按日期搜索」里跳转", "Reached the earliest available day (2009-07-13); use Search by date to jump"); } }
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
        public static string NoTitle        { get { return T("（无标题）", "(no title)"); } }

        // ---------------- 照片墙
        public static string WallTitle(string mmdd)
        {
            string md = mmdd.Length == 4 ? (mmdd.Substring(0, 2) + "-" + mmdd.Substring(2)) : mmdd;
            return _en ? ("Photo wall · " + md + " across years") : ("照片墙 · 历年 " + md);
        }
        public static string WallMenu(int n)
        {
            return _en ? string.Format("Photo wall · {0} images from past years", n)
                       : string.Format("照片墙 · 历年同日 {0} 张", n);
        }
        public static string WallFetchMissing { get { return T("把缺的取回", "Fetch missing"); } }
        public static string WallToArchive   { get { return T("直接存进归档", "Save to archive"); } }
        public static string WallFetching     { get { return T("正在取这张…", "Fetching…"); } }
        public static string WallFetchingN    { get { return T("正在取第 {0}/{1} 张…", "Fetching {0}/{1}…"); } }
        public static string WallFetchingPreview { get { return T("正在取第 {0}/{1} 张…（预览档 1080p）", "Fetching {0}/{1}… (1080p preview)"); } }
        public static string WallUpgrading   { get { return T("正在取高清原图…（UHD，约 5 MB）", "Fetching the full-resolution original… (UHD, about 5 MB)"); } }
        public static string WallPreviewHint { get { return T("（预览为 1080p，收藏或设为壁纸时自动换原图）", "(preview is 1080p; favoriting or setting as wallpaper upgrades to the original)"); } }
        public static string WallFetchDone(int ok, int total)
        {
            string tpl = _en ? "Fetched {0} of {1}" : "已取回 {0}/{1} 张";
            return string.Format(tpl, ok, total);
        }
        /// <summary>取图结果分三类报：成功（含降级）、无图、总数。不把「这一天没有图」算成成功。</summary>
        public static string WallFetchDone2(int ok, int degraded, int none, int total)
        {
            if (_en)
            {
                string e = string.Format("Fetched {0} of {1}", ok, total);
                if (degraded > 0) { e += string.Format(" ({0} stayed at 1080p)", degraded); }
                if (none > 0) { e += string.Format(", {0} day(s) have no image", none); }
                return e;
            }
            string c = string.Format("已取回 {0}/{1} 张", ok, total);
            if (degraded > 0) { c += string.Format("（其中 {0} 张只拿到 1080p）", degraded); }
            if (none > 0) { c += string.Format("，{0} 天本来就没有图", none); }
            return c;
        }
        public static string WallFetchAborted(int ok, int total)
        {
            return _en
                ? string.Format("Stopped after {0} of {1} (window closed)", ok, total)
                : string.Format("已中止：取回 {0}/{1} 张（窗口已关闭）", ok, total);
        }
        public static string WallApplyFail { get { return T("图已经拿到，但桌面壁纸没换上。可能是系统策略或权限限制，也可能这张图正被别的程序占用；右键 →「系统」→「打开日志」能看到具体原因。", "The image is ready but the desktop did not update. The system policy, permissions, or another program holding the file may be the cause; right-click → System → Open log for details."); } }
        public static string BWallFail    { get { return T("图取到了，桌面没换上", "Image ready, desktop not updated"); } }
        public static string BFutureDate  { get { return T("不能选未来日期", "Future dates are not allowed"); } }
        public static string FutureDateHint { get { return T("换成今天或更早的日期", "Pick today or an earlier date"); } }
        public static string WallKeeping  { get { return T("正在把这批图补成高清原图，稍等…", "Upgrading these images to the original quality…"); } }
        public static string WallStatusFmt
        {
            get { return T("本地已有 {0} 张 / 共 {1} 个年份，还缺 {2} 张（双击格子设为壁纸）",
                           "{0} of {1} years available, {2} missing (double-click a cell to set as wallpaper)"); }
        }
        public static string WallApplied      { get { return T("已设为桌面壁纸", "Set as desktop wallpaper"); } }
        public static string WallCardFetch    { get { return T("取回", "Fetch"); } }
        public static string WallSortLabel    { get { return T("排序", "Sort"); } }
        public static string WallSortNew      { get { return T("新 → 旧", "New → Old"); } }
        public static string WallSortOld      { get { return T("旧 → 新", "Old → New"); } }
        public static string WallFilterLocal  { get { return T("只看本地有", "Local only"); } }

        // ---------------- 接管目录
        public static string TkMenu       { get { return T("接管目录…", "Takeover folder…"); } }
        public static string TkTitle      { get { return T("接管目录", "Takeover folder"); } }
        public static string TkHeader     { get { return T("接管 = 换一个图片仓库", "Takeover = switch the image store"); } }
        public static string TkTip
        {
            get { return T("接管后只读这个目录：不清理、不移动、不改名。新下载的图也存进去。\r\n唯一的例外是画质升级：2019 年后的图若不足 UHD，取到原图后会原地覆盖同名文件。\r\n退出接管会删掉 takeover 目录，软件回到接管前的样子，原来的配置一点都没动。", "While taken over, this folder is read-only for the app: nothing is cleaned, moved or renamed. New downloads go here too.\r\nThe one exception is a quality upgrade: a post-2019 image below UHD gets overwritten in place when the original is fetched.\r\nLeaving takeover deletes the takeover folder and returns to the previous state."); }
        }
        public static string TkOff        { get { return T("当前：未接管（用安装目录里的缓存与归档）", "Current: not taken over (using the install folder)"); } }
        public static string TkOn         { get { return T("当前：接管中（只读这个目录）", "Current: taken over (read-only)"); } }
        public static string TkOnBody(string root, int n, int days)
        {
            string tpl = _en ? "Folder: {0}\r\n{1} images · {2} days covered; new downloads go here; cleaning and moving are disabled."
                             : "目录：{0}\r\n{1} 张图 · 覆盖 {2} 天；新下载的图存这里；清理与移动已禁用。";
            return string.Format(tpl, root, n, days);
        }
        public static string TkFillBtn  { get { return T("补齐缺失日期（最近 3 年）…", "Fill missing days (last 3 years)…"); } }
        public static string TkFilling  { get { return T("正在补齐，进度见日志…", "Filling — see the log for progress…"); } }
        public static string TkFillAsk(DateTime from, DateTime to)
        {
            string a = from.ToString("yyyy-MM-dd");
            string b = to.ToString("yyyy-MM-dd");
            return _en
                ? ("Add every day between " + a + " and " + b + " that the library does not have yet.\r\nExisting days are skipped, nothing is overwritten. Roughly 2 seconds per image; close this window to stop.\r\n\r\nStart?")
                : ("把 " + a + " ~ " + b + " 之间图库还没有的日期逐天补进来。\r\n已有的一张都不动、不覆盖；每张约 2 秒，关掉这个窗口就停。\r\n\r\n开始吗？");
        }
        /// <summary>菜单里的常驻模式标牌：接管时显示「收藏家模式 · 目录名」。</summary>
        public static string ModeBadge(string name)
        {
            string tpl = _en ? "Collector mode · {0}" : "收藏家模式 · {0}";
            return string.Format(tpl, name);
        }
        public static string ModeBadgeHint(string root)
        {
            string tpl = _en ? "Read-only mount: images are read from {0}\nLeaving takeover deletes the sandbox folder"
                             : "只读挂载：图片从 {0} 读取\n退出接管时沙盒目录整个删除";
            return string.Format(tpl, root);
        }
        public static string TkBrokenState { get { return T("当前：接管目录不见了（已自动回到原模式）", "Current: takeover folder missing (reverted)"); } }
        /// <summary>按节日筛选：菜单名、空态、照片墙标题、节日英文名。</summary>
        public static string FestivalMenu { get { return T("按节日筛选", "Filter by festival"); } }
        public static string FestivalEmpty(string name)
        {
            string tpl = _en ? "No image available for {0} yet." : "「{0}」还没有可用的图";
            return string.Format(tpl, name);
        }
        public static string FestivalWallTitle(string name, int n)
        {
            string tpl = _en ? "{0} · {1} years" : "{0} · {1} 张";
            return string.Format(tpl, name, n);
        }
        public static string FestivalName(string cn)
        {
            if (!_en) { return cn; }
            switch (cn)
            {
                case "春节": return "Spring Festival";
                case "元宵节": return "Lantern Festival";
                case "清明节": return "Qingming";
                case "端午节": return "Dragon Boat";
                case "中秋节": return "Mid-Autumn";
                case "重阳节": return "Double Ninth";
                case "国庆节": return "National Day";
                case "元旦": return "New Year's Day";
                case "除夕": return "New Year's Eve";
            }
            return cn;
        }
        public static string TkBrokenBody(string root)
        {
            string tpl = _en ? "Folder not found: {0}" : "找不到目录：{0}";
            return string.Format(tpl, root);
        }
        public static string TkBrokenTitle { get { return T("接管目录不可用", "Takeover folder unavailable"); } }
        public static string TkBrokenBody2 { get { return T("已自动回到安装目录的模式", "Reverted to the install folder"); } }
        public static string TkPick       { get { return T("选择目录并接管", "Choose folder"); } }
        public static string TkChange     { get { return T("换一个目录", "Change folder"); } }
        public static string TkExit       { get { return T("退出接管", "Leave takeover"); } }
        public static string TkImport     { get { return T("从备份接管…", "Take over from backup…"); } }
        public static string TkBackupAsk
        {
            get { return T("退出接管前要不要先备份？\r\n\r\n是 = 备份到 backup 目录再退出\r\n否 = 直接退出，沙盒删掉\r\n取消 = 留在接管模式", "Back up before leaving?\r\n\r\nYes = copy to backup\\ then leave\r\nNo = leave and delete the sandbox\r\nCancel = stay"); }
        }
        public static string TkBackedUp(string dir)
        {
            string tpl = _en ? "Backed up to {0}" : "已备份到 {0}";
            return string.Format(tpl, dir);
        }
        public static string TkImportPick  { get { return T("选一个备份目录（里面要有 config.json）", "Pick a backup folder that contains config.json"); } }
        public static string TkImported    { get { return T("已从备份恢复接管状态", "Takeover state restored from backup"); } }
        public static string TkImportFail  { get { return T("这个目录不是有效的接管备份", "That folder is not a takeover backup"); } }
        public static string TkReadOnlyHint { get { return T("接管模式只读：清理会改动接管目录，已禁用", "Read-only takeover: cleanup would modify the takeover folder"); } }
        public static string TkChoose     { get { return T("选一个放着壁纸的目录", "Pick a folder that already holds wallpapers"); } }
        public static string TkEmptyWarn  { get { return T("这个目录里没有 .jpg 文件，确定要用它吗？", "This folder has no .jpg files. Use it anyway?"); } }
        public static string TkEntered(string root)
        {
            string tpl = _en ? "Taken over: {0}" : "已接管：{0}";
            return string.Format(tpl, root);
        }
        public static string TkExitConfirm
        {
            get { return T("退出接管会删掉 takeover 目录（进回收站），软件回到接管前的样子。继续吗？", "Leaving takeover deletes the takeover folder (to the Recycle Bin) and returns to the previous state. Continue?"); }
        }
        public static string TkExitConfirmFav(int n)
        {
            string tpl = _en ? "Leaving takeover deletes the takeover folder, including {0} favorited images. Continue?"
                             : "退出接管会删掉 takeover 目录，里面 {0} 张收藏的图也会一起没掉。继续吗？";
            return string.Format(tpl, n);
        }
        public static string TkExited     { get { return T("已退出接管，回到安装目录", "Left takeover; back to the install folder"); } }
        public static string TkFail       { get { return T("操作失败：", "Operation failed:"); } }
        public static string WallZoomHint     { get { return T("Ctrl + 滚轮 缩放", "Ctrl + wheel to zoom"); } }
        public static string WallSetAs        { get { return T("设为桌面壁纸", "Set as wallpaper"); } }
        public static string WallBtnSet       { get { return T("换这张", "Use this"); } }
        public static string WallBtnFav       { get { return T("收藏这张", "Favorite"); } }
        public static string WallBtnUnfav     { get { return T("取消收藏", "Unfavorite"); } }
        public static string WallUnfavDone    { get { return T("已取消收藏", "Removed from favorites"); } }
        public static string WallBtnOpen      { get { return T("打开位置", "Open folder"); } }
        public static string WallFavDone      { get { return T("已收藏", "Favorited"); } }
        public static string WallFavFail      { get { return T("收藏失败，这张可能不在缓存里", "Favorite failed; the file may not be in the cache"); } }
        public static string WallShowInFolder { get { return T("在资源管理器中显示", "Show in folder"); } }
        public static string WallCopyTitle    { get { return T("复制标题", "Copy title"); } }
        public static string WallCloseIdleBody { get { return T("本次只看了本地已有的图，没有临时文件。\r\n要关闭照片墙吗？", "Only local images were viewed; nothing temporary was downloaded.\r\nClose the photo wall?"); } }
        public static string WallResultKeep(int n)
        {
            string tpl = _en ? "Kept {0} images in the history archive" : "已暂存 {0} 张到历史归档";
            return string.Format(tpl, n);
        }
        public static string WallResultDrop(int n)
        {
            string tpl = _en ? "Dropped {0} temporary images" : "已丢弃 {0} 张临时图";
            return string.Format(tpl, n);
        }
        public static string WallIdleHint     { get { return T("（本地都有，没有临时文件，关窗不会问）", "(all local; nothing temporary to ask about)"); } }
        public static string WallClosedIdle   { get { return T("照片墙已关闭，本次只看了本地已有的图", "Photo wall closed; only local images were viewed"); } }
        public static string WallClosedKeep   { get { return T("已暂存 {0} 张到历史归档", "Kept {0} images in the archive"); } }
        public static string WallClosedDrop   { get { return T("已丢弃 {0} 张临时图", "Dropped {0} temporary images"); } }
        public static string WallCloseTitle   { get { return T("这张照片墙怎么处理？", "What should happen to this photo wall?"); } }
        public static string WallCloseBody(int n)
        {
            string tpl = _en
                ? "You fetched {0} images into the temporary folder; keep them in the archive?"
                : "本次取回了 {0} 张图，放在临时目录里；要暂存到历史归档吗？";
            return string.Format(tpl, n);
        }
        public static string WallCloseRemember { get { return T("记住这次选择，以后不再问", "Remember this choice"); } }
        public static string WallCloseKeep     { get { return T("暂存到归档", "Keep in archive"); } }
        public static string WallCloseDrop     { get { return T("不保留", "Drop"); } }
        public static string WallCloseBack     { get { return T("返回照片墙", "Back"); } }
        public static string ReloadIndex    { get { return T("重新载入索引", "Reload index"); } }
        public static string SrchWorking    { get { return T("正在换图…", "Switching…"); } }
        public static string SrchSwitchFail { get { return T("换图失败，可以再点一次", "Switch failed; try again"); } }
        public static string SrchNoSource   { get { return T("这一天的原图取不到（源不可达），稍后再试试", "Couldn't reach a source for that day — try again later"); } }
        public static string SrchNoImage    { get { return T("这一天必应没有发布壁纸", "Bing published no wallpaper that day"); } }
        public static string DeadDateSuffix { get { return T("（原图已失效）", "(source image gone)"); } }
        public static string BtnClose       { get { return T("关闭", "Close"); } }

        public static string SrchSwitched(string date)
        {
            return _en ? ("Switched to " + date) : ("已切换到 " + date);
        }

        public static string GalleryBalloonOn
        {
            get { return T("画廊模式已开启", "Gallery mode on"); }
        }
        public static string GalleryBalloonOnHint
        {
            get { return T("看的图存在画廊目录，不登记保护名单；关掉模式会清空", "Images go to the gallery folder and are not protected; turning it off clears them"); }
        }
        public static string GalleryBalloonOff(int n, double mb)
        {
            string tpl = _en ? "Gallery mode off, cleared {0} images / {1} MB" : "画廊模式已关闭，已清空 {0} 张 · {1} MB";
            return string.Format(tpl, n, mb.ToString("0.0"));
        }
        public static string GalleryBalloonOffHint
        {
            get { return T("桌面壁纸保持不变，缓存与归档不受影响", "Desktop wallpaper is unchanged; cache and archive are untouched"); }
        }
        public static string ArchSrcOn      { get { return T("来源 Zhu-junwei（2016 起）+ niumoo 兜底", "Source: Zhu-junwei (since 2016) + niumoo fallback"); } }
        public static string ArchSrcOff     { get { return T("已关闭，8 天以外的日期只能走压缩版", "Disabled; dates older than 8 days fall back to compressed"); } }
        public static string ArchCacheInfo(int months, double kb)
        {
            return _en
                ? ("  " + "｜cached " + months + " months (" + kb.ToString("0") + " KB)")
                : ("　｜已缓存 " + months + " 个月（" + kb.ToString("0") + " KB）");
        }
        public static string ArchInfoPrefix { get { return _en ? "  " : "　"; } }
        public static string ArchImgInfo(int n, double mb, int prot) { return _en ? (" · " + n + " images / " + mb + " MB (" + prot + " protected)") : (" · 图片 " + n + " 张 / " + mb + " MB（" + prot + " 张受保护）"); }
        public static string ArchSrcLine1   { get { return T("主源 Zhu-junwei  ·  2016-03 起  ·  带中文标题", "Primary  Zhu-junwei  ·  since Mar 2016  ·  with titles"); } }
        public static string ArchSrcLine2   { get { return T("兜底 niumoo      ·  2021-02 起  ·  仅 ID", "Fallback niumoo    ·  since Feb 2021  ·  IDs only"); } }
        public static string ArchSrcLine3   { get { return T("老档 wilii+anerg ·  2009-07 起  ·  老图 958×512 / 1366×768", "Old wilii+anerg  ·  since Jul 2009  ·  958x512 / 1366x768"); } }
        public static string ArchRowArchive(int n, double mb, int cap, int prot)
        {
            return _en
                ? ("Archive  " + n + " images / " + mb.ToString("0.0") + " MB (cap " + cap + " MB, " + prot + " protected)")
                : ("归档  " + n + " 张 / " + mb.ToString("0.0") + " MB（上限 " + cap + " MB，" + prot + " 张受保护）");
        }
        public static string ArchRowOld(int n, double mb)
        {
            return _en
                ? ("Legacy  " + n + " images / " + mb.ToString("0.0") + " MB (2009-08 ~ 2016-02)")
                : ("老存档  " + n + " 张 / " + mb.ToString("0.0") + " MB（2009-08 ~ 2016-02）");
        }
        public static string GalleryMenu  { get { return T("画廊模式（随手看图，退出即清）", "Gallery mode (browse freely, cleared on exit)"); } }
        public static string GalleryOn    { get { return T("● 画廊模式 · 关掉就清空画廊", "● Gallery on · cleared when you turn it off"); } }
        public static string GalleryCount(int n, double mb)
        {
            return _en ? (" · " + n + " images / " + mb.ToString("0.0") + " MB") : (" · 画廊 " + n + " 张 / " + mb.ToString("0.0") + " MB");
        }
        public static string GalleryTipOn  { get { return T("画廊模式下看的图不进保护名单，关掉这个模式就清空", "Images viewed in Gallery mode are not protected and are cleared when you turn it off"); } }
        public static string ArchRowIndex(int months, double kb, int total)
        {
            return _en
                ? ("Index  " + months + " months + full index " + kb.ToString("0") + " KB (" + total + " dates available)")
                : ("索引  " + months + " 个月份 + 全量索引 " + kb.ToString("0") + " KB（共 " + total + " 条可下载）");
        }
        public static string CleanCapHint(int mb)
        {
            return _en
                ? ("The archive is capped at " + mb + " MB; when full, the oldest is cleared automatically." + Environment.NewLine +
                   "To keep one image: open \"Keep during auto-clean\" and click \"Add to favorites\"." + Environment.NewLine +
                   "To move a batch out: use \"Library → Download wallpapers…\".")
                : ("归档有 " + mb + " MB 上限，满了会从最旧的开始自动清理。" + Environment.NewLine +
                   "单张留存：在「换过的图」里点「加入收藏」。" + Environment.NewLine +
                   "批量搬走：用「壁纸库 → 导出已下载壁纸…」，勾上「归档」，复制到指定文件夹。");
        }
        public static string ArchOffLine    { get { return T("已关闭，8 天以外的日期只能走压缩版", "Disabled — dates older than 8 days fall back to compressed"); } }

        // ---------------- 信息窗口
        public static string InfoTitle       { get { return T(" · 这张图是哪儿", " · Where is this?"); } }
        public static string InfoNone        { get { return T("还没有换图记录。", "No wallpaper record yet."); } }
        public static string InfoDate        { get { return T("日期：", "Date: "); } }
        public static string InfoTitleLine   { get { return T("标题：", "Title: "); } }
        public static string InfoCopy        { get { return T("版权：", "Copyright: "); } }
        public static string InfoFile        { get { return T("文件：", "File: "); } }
        public static string InfoSize        { get { return T("大小：", "Size: "); } }
        public static string InfoBtnSearch   { get { return T("了解更多", "Learn more"); } }
        public static string InfoBtnFolder   { get { return T("文件夹中打开", "Show in folder"); } }
        public static string InfoBtnMore     { get { return T("往年的今天", "Same day, other years"); } }
        public static string InfoBtnFav      { get { return T("☆ 收藏", "☆ Favorite"); } }
        public static string HelpItem       { get { return T("帮助…", "Help…"); } }
        public static string HelpTitle      { get { return T(" · 帮助", " · Help"); } }
        public static string HelpBody
        {
            get
            {
                return _en
                    ? @"1. Daily rotation
    The wallpaper switches to today''s Bing image at 00:10, with retries at 08:10 and 12:10.
    To jump back to today: tray menu -> ""Change to today"".

2. Browsing backwards
    ""Previous / Next"" moves one day at a time. The last 8 days come from the live API;
    older ones continue from the archive, back to 13 July 2009.
    With ""What is this image"" open, the left and right arrow keys work too, and the
    window and desktop wallpaper change together.
    If one image cannot be fetched, the window retries up to 3 times on its own.
    Tray menu -> ""This day in past years"" lists the same date across years and shows
    the title even before the image is downloaded; click an entry to fetch it.

3. Finding by date
    Tray menu -> ""Search by date"" opens a calendar (top level, no need to open the library first).
    Dates shown in bold already have their image stored locally.

4. Favorites, cache and archive
    Cache keeps 30 days; the archive is capped at 500 MB and clears oldest first.
    Images in favorites are never removed automatically.
    ""Clean cache"" and ""Clean archive"" free space by those rules and always keep
    favorites and the current wallpaper.
    Legacy archive images (before Mar 2016) sit outside that 500 MB quota and are never
    touched by any automatic clean-up — they cost too much to re-download from the
    mirrors, and clearing them requires ticking the box manually.

5. Gallery mode
    Turn it on to browse freely without piling up files.
    Images viewed in gallery mode land in cache\gallery and are not registered for
    protection; turning the mode off or restarting clears them.
    Click ""Favorite"" on anything you like - it moves to favorites and survives the cleanup.
    In the photo wall (first item under ""This day in past years""), ""Use this"" moves the image into the archive.

6. Why some images look soft
    UHD only from 2019 onward; 2016-03 to 2018 tops out at 1080p. Before that,
    Bing's original uploads were purged, so only third-party mirrors remain at
    958x512 or 1366x768. That was Bing's publishing standard at the time, not a
    download error.

7. Data sources and troubleshooting
    Primary index Zhu-junwei, fallback bing.wilii.cn, legacy archive anerg,
    daily stories from December 2013.
    Start with the log: tray menu -> ""System"" -> ""Open log"".
    If automatic change stops working, the top line of the menu shows the scheduled
    task status; click it to attempt a repair.

8. Filter by festival
    Tray menu -> ""Filter by festival"" -> pick one. The photo wall shows that
    festival across the years. Nine festivals: Spring Festival, Lantern, Qingming,
    Dragon Boat, Mid-Autumn, Double Ninth, National Day, New Years Day, New Years Eve.
    Dates are computed from the lunar calendar on the spot: no network, never stale.

9. Bulk download and takeover folder
    ""Library -> Download wallpapers..."" saves a date range to a folder you pick,
    with three quality levels: highest available (UHD), 1080p, mirror only.
    Existing files are skipped, so an interrupted run can be resumed.
    The result can be mounted as a takeover folder: tray menu -> ""Takeover folder...""
    It is read-only; leaving takeover sends the sandbox to the Recycle Bin,
    with an optional backup first.

10. Photo wall
    Tray menu -> ""Days in past years"" or ""Filter by festival"" opens it.
    ""Fetch missing"" pulls 1080p first as a preview (about 0.3 MB each, ten times
    faster than the original), and each image appears as soon as it lands, instead
    of waiting for the whole wall. Choosing ""save to archive"" on close upgrades
    every image to UHD again, so the archive still keeps the original.
    Favoriting or setting a wallpaper also upgrades to the original first, so a
    1080p preview never ends up in your favorites or on your desktop.
    Set ""wallPreview"": ""uhd"" in config.json (or pass -WallFull) to take UHD directly.

11. Where the image comes from
    Five steps, each falling back to the next automatically:
      1. Bing original  th?id={ID}_UHD.jpg          3840x2160  about 3.5 MB
      2. Bing scaled    th?id={ID}&w=3840&h=2160    3840x2160  about 0.8 MB
      3. Microsoft CDN  Latest/3840x2400/{date}.jpg
      4. Bing 1080p     th?id={ID}_1920x1080.jpg    1920x1080  about 0.33 MB
      5. Third-party mirror from 2009-08, 1366x768 or 958x512
    For 2009-2016 images two more steps exist: the wilii filename table and the
    anerg monthly mirror. The index ships inside the exe, so titles and paging
    work offline; only the image itself needs the network.
    If the Microsoft list API is down, paging falls back to the local index
    instead of failing.

12. Why storage stays put
    Three caps, none of which can grow without bound:
      - Cache       30 days, cleaned on a rolling basis
      - Archive     capped at 500 MB; clears in two rounds (unlisted first,
                    then listed entries if still over)
      - Thumbnails  at most 800 files and 100 MB; orphans go first
    Favorites, hand-picked dates and the current wallpaper are skipped by all
    three rules. Gallery, photo wall and prefetch folders are wiped when you
    close them or switch modes.
    A typical steady state: 20-30 MB of cache, an archive near its cap, and
    under 0.3 MB of thumbnails.
    Daily stories are cached as plain text under archive\stories and are
    re-fetched on demand. Clear them from Clean -> Clean history archive
    (tick the story-cache option)."
                    : @"一、每天怎么换图
    默认在 00:10 自动换成当天的必应图，8:10 / 12:10 各补查一次。
    想立刻回到今天：托盘菜单 →「换成今天」。

二、往回翻
    「上一张 / 下一张」按天往前翻。最近 8 天走实时接口，更早的自动接续归档，
    最远可以翻到 2009 年 7 月 13 日。
    打开「这张图是哪儿」之后，按键盘 ← → 也能翻，窗口内容和桌面壁纸会一起变。
    某一张一时取不到时，窗口里会自动再试 3 次，不用手动重开。
    托盘菜单 →「往年的今天」列出历年同日的图；还没下载过的也会先显示标题，点一下才去取。

三、按日期找
    托盘菜单 →「按日期搜索…」打开日历（在主菜单里，不用先进壁纸库）。
    日历上显示为粗体的日期，表示本地已经有这一天的图。

四、收藏、缓存与归档
    缓存保留 30 天，归档上限 500 MB，超过就从最旧的开始清。
    收藏夹里的图永远不会被自动清理。
    「清理缓存」「清理归档」会按上述规则腾空间，收藏与当前壁纸始终保留。
    老存档（2016 年 3 月之前的镜像图）在归档额度之外单独保管，任何自动清理都不会动它 ——
    这些图从第三方站重新下载的成本太高，而且要清得在「清理归档」里显式勾选。

五、画廊模式
    想随手翻着看、又不想攒下一堆文件时打开它。
    画廊里看的图落在 cache\gallery，不登记保护名单；关掉模式或重启软件就清空。
    看到喜欢的随手点「收藏」，它会移进收藏夹，退出画廊也不会被清。
    照片墙（往年的今天 → 第一项）里点「换这张」，图会自动存进历史归档，之后归归档上限管。

六、为什么有些图比较糊
    2019 年起才是 3840×2160；2016 年 3 月到 2018 年是 1080p；
    再往前（2009 到 2016 初）Bing 的原始上传已被清理，只能从第三方镜像拿到
    958×512 或 1366×768。这是当年必应的发布标准，不是下载出错。

七、数据来源与出问题怎么办
    主索引 Zhu-junwei、兜底 bing.wilii.cn、老存档 anerg，每日故事自 2013 年 12 月起。
    出问题先看日志：托盘菜单 →「系统」→「打开日志」。
    自动换图不生效时，菜单顶部那行会显示计划任务状态，点它可以尝试修复。

八、按节日筛选
    托盘菜单 →「按节日筛选」→ 选一个节日，照片墙会把历年这一天的图铺开。
    九个节日：春节 / 元宵 / 清明 / 端午 / 中秋 / 重阳 / 国庆 / 元旦 / 除夕。
    日期由农历算法现场算出，不需要联网，也不会随年份过时。

九、批量下载与接管目录
    「壁纸库 → 批量下载壁纸…」可以按日期范围把图下到指定目录，三档画质可选：
    最高可用（UHD）/ 1080p / 只用镜像。已有同名文件会跳过，中断后接着跑。
    下好的目录可以直接拿去做「接管目录」：托盘菜单 →「接管目录…」→ 选它。
    接管是只读挂载，退出时沙盒整体进回收站，可选先备份。

十、照片墙
    托盘菜单 →「往年的今天」或「按节日筛选」都会打开照片墙。
    缺图点「把缺的取回」：先取 1080p 当预览（一张约 0.3 MB，比原图快十倍），
    下完一张就显示一张，不用等整墙下完。
    关窗选「暂存到归档」时会自动补一次 UHD，所以归档里仍然是原图。
    收藏或设为壁纸前也会先补原图，不会把 1080p 存进收藏夹、也不会拿它当桌面。
    想整墙直接拿 UHD：config.json 里把 wallPreview 设成 ""uhd""，或用脚本参数 -WallFull。

十一、这张图是从哪来的
    取图有固定顺序，前一级失败自动退到下一级，不用你换源：
    1. Bing 原图    th?id={ID}_UHD.jpg          3840×2160  约 3.5 MB
    2. Bing 缩放    th?id={ID}&w=3840&h=2160    3840×2160  约 0.8 MB
    3. 微软 CDN     Latest/3840x2400/{date}.jpg
    4. Bing 1080p   th?id={ID}_1920x1080.jpg    1920×1080  约 0.33 MB
    5. 第三方镜像   2009-08 起的老图，1366×768 或 958×512
    翻 2009 到 2016 的图时还有两级：wilii 文件名表和 anerg 月度镜像。
    索引内嵌在 exe 里，断网也能查标题、能翻页，只是图本身要联网。
    微软的列表接口不通时，翻页会改按本地索引相对移动，不会卡在今天。

十二、占用为什么不会一直涨
    三层封顶，任何一层都不会无限增长：
    ・日常缓存  保留 30 天，滚动清理
    ・历史归档  上限 500 MB，满了分两轮清：先清没进保留名单的，还不够再动名单里的
    ・缩略图    最多 800 张且不超过 100 MB，源图已经不在的先清
    收藏的、手动挑过的、当前正在用的，这三条规则都跳过。
    画廊、照片墙、预取这三个临时目录，关窗或切模式就清空。
    稳态大致是：缓存 20-30 MB、归档接近上限、缩略图 0.3 MB 以下。
    每日故事以纯文本缓存在 archive\stories，删了按需重抓。清它走
    清理 →「清理历史归档」，勾上故事缓存那一项。";
            }
        }
        public static string InfoBtnFaved    { get { return T("★ 已收藏", "★ Favorited"); } }
        public static string InfoFavFail     { get { return T("这张图不在缓存或画廊里，无法收藏", "This image is not in the cache, so it cannot be favorited"); } }
        public static string InfoBtnPrev     { get { return T("◀ 上一张", "◀ Older"); } }
        public static string InfoBtnNext     { get { return T("下一张 ▶", "Newer ▶"); } }
        public static string InfoSwitching   { get { return T("正在切换…", "Switching…"); } }
        public static string InfoSwitchFail  { get { return T("试了 3 次仍取不到这张图：可能确实没有这一张，也可能网络暂时不通", "Still unavailable after 3 attempts: the image may not exist, or the network is down"); } }
        public static string InfoRetrying    { get { return T("正在取图…（第 {0}/{1} 次）", "Fetching… (attempt {0}/{1})"); } }
        public static string StoryTitle      { get { return T("每日故事", "Daily story"); } }
        public static string StoryExpand     { get { return T("展开全文", "Read more"); } }
        public static string StoryCollapse   { get { return T("收起", "Collapse"); } }
        public static string StoryLoading    { get { return T("正在获取故事…", "Loading story…"); } }
        public static string CrashTitle      { get { return T("遇到一个问题", "Something went wrong"); } }
        public static string HealTitle       { get { return T("壁纸文件不在了，已退回今天", "Wallpaper file is gone — switched back to today"); } }
        public static string HealHint        { get { return T("原来的文件可能被清理掉了，这里直接换回今天的图", "The previous file was cleaned up; switched to today's image"); } }
        public static string GalleryTakeoverBlocked { get { return T("接管模式下不能用画廊模式。\r\n画廊是「临时试看、退出即清」，而接管是换一个长期图库，两者语义冲突。\r\n\r\n要试看临时图片，先退出接管。", "Gallery mode is unavailable while a takeover folder is active.\r\nGallery is a throwaway preview area, takeover is a long-term library.\r\n\r\nLeave takeover first if you want to browse temporary images."); } }
        public static string CrashHint       { get { return T("已经记下现场，程序继续运行。日志：%TEMP%\\BingWallpaperLite.log", "Details were logged and the app keeps running. Log: %TEMP%\\BingWallpaperLite.log"); } }
        public static string BusyTitle       { get { return T("正在处理", "Working"); } }
        public static string BusyHint        { get { return T("上一次换图或清理还没跑完，等它结束后再点；菜单会自己恢复。", "The previous run is still going. Try again when it finishes; the menu recovers on its own."); } }
        /// <summary>数量 + 单位。中文用「12 张」，英文用「12 images」，别在窗体里直接拼中文单位。</summary>
        public static string UnitImg(int n)  { return _en ? (n + " images") : (n + " 张"); }
        public static string UnitFile(int n) { return _en ? (n + " files")  : (n + " 个"); }

        /// <summary>「复制给 AI 的说明」正文：一段能直接粘给模型的任务描述。</summary>
        public static string AiPromptBody(string exe)
        {
            StringBuilder sb = new StringBuilder();
            if (_en)
            {
                sb.AppendLine("I have a Bing wallpaper tool. Its command line entry is:");
                sb.AppendLine(exe);
                sb.AppendLine();
                sb.AppendLine("Call it with pwsh, for example:");
                sb.AppendLine("  pwsh -File \"" + exe + "\" -Date 20230615");
                sb.AppendLine();
                sb.AppendLine("Supported options:");
                sb.AppendLine("  -Index 0            switch to today's image");
                sb.AppendLine("  -Index 1..7         go back 1-7 days");
                sb.AppendLine("  -Step 1 / -Step -1  previous / next day");
                sb.AppendLine("  -Date YYYYMMDD      switch to that date (from 2009-07-13)");
                sb.AppendLine("  -Random -RandomScope local  random image (fav / local / any)");
                sb.AppendLine("  -Use \"path\"         use a local file");
                sb.AppendLine("  -Favorite           favorite the current image");
                sb.AppendLine("  -Unfavorite         unfavorite it");
                sb.AppendLine("  -Query \"keyword\"    search and list matches");
                sb.AppendLine("  -Status             current state (JSON)");
                sb.AppendLine("  -Json               return structured output for any command");
                sb.AppendLine("  -DryRun             show what would happen");
                sb.AppendLine();
                sb.AppendLine("Add -Json after a switch to see which image was applied.");
                sb.AppendLine("  -Search \"keyword\"   search and switch to the first match");
                sb.AppendLine();
                sb.AppendLine("About search:");
                sb.AppendLine("  · Keywords are space-separated and matched literally against title + copyright");
                sb.AppendLine("  · Chinese word segmentation is your job — expand 「雪山」 into \"雪 山 冰川 雪峰\"");
                sb.AppendLine("  · Use -Query first to inspect results, then -Date to switch");
                sb.AppendLine("  · Dates before 2019 have no UHD; 1080p or a mirror copy is expected, not an error");
                sb.AppendLine();
                sb.AppendLine("My request: write what you want here.");
                return sb.ToString();
            }
            sb.AppendLine(@"我有一个必应壁纸工具，命令行入口是：");
            sb.AppendLine(exe);
            sb.AppendLine();
            sb.AppendLine(@"用 pwsh 调用，例如：");
            sb.AppendLine(@"  pwsh -File """ + exe + @""" -Date 20230615");
            sb.AppendLine();
            sb.AppendLine(@"支持的参数：");
            sb.AppendLine(@"  -Index 0            换今天的");
            sb.AppendLine(@"  -Index 1..7         往前翻");
            sb.AppendLine(@"  -Step 1 / -Step -1  前后翻一天");
            sb.AppendLine(@"  -Date YYYYMMDD      指定日期（2009-07-13 至今）");
            sb.AppendLine(@"  -Random -RandomScope local  随机一张（fav 收藏 / local 本地全部 / any 任意一天）");
            sb.AppendLine(@"  -Use ""路径""         用本地图片");
            sb.AppendLine(@"  -Favorite           收藏当前");
            sb.AppendLine(@"  -Unfavorite         取消收藏");
            sb.AppendLine(@"  -Query ""关键词""     搜索并列出匹配项");
            sb.AppendLine(@"  -Status             查当前状态（返回 JSON）");
            sb.AppendLine(@"  -Json               加在任何命令后，返回结构化结果");
            sb.AppendLine(@"  -DryRun             只演示不执行");
            sb.AppendLine();
            sb.AppendLine(@"换图后加 -Json 就知道换成了哪张。");
            sb.AppendLine(@"  -Search ""关键词""    搜索并换第一张匹配的");
            sb.AppendLine();
            sb.AppendLine(@"搜索说明：");
            sb.AppendLine(@"  · 关键词用空格分隔，脚本做字面匹配（标题 + 版权）");
            sb.AppendLine(@"  · 中文分词要靠你自己扩词，比如「雪山」要拆成 ""雪 山 冰川 雪峰""");
            sb.AppendLine(@"  · 先用 -Query 看结果，挑好了再用 -Date 换");
            sb.AppendLine(@"  · 2019 年前的日期没有 UHD，拿到的是 1080p 或镜像档，不是出错");
            sb.AppendLine();
            sb.AppendLine(@"我的需求：在这里写你要什么。");
            return sb.ToString();
        }

        /// <summary>AI 窗口里的命令行速查表。内容与脚本 param 块保持一致，改参数时记得同步这里。</summary>
        public static string AiCommands(string exe)
        {
            StringBuilder sb = new StringBuilder();
            if (_en)
            {
                sb.AppendLine("Command line entry:");
                sb.AppendLine(exe);
                sb.AppendLine();
                sb.AppendLine("Options:");
                sb.AppendLine("  -Index 0            switch to today's image");
                sb.AppendLine("  -Index 1..7         go back 1-7 days");
                sb.AppendLine("  -Step 1 / -Step -1  previous / next day");
                sb.AppendLine("  -Date YYYYMMDD      switch to that date (from 2009-07-13; pre-2016-03 comes from mirrors, lower resolution)");
                sb.AppendLine("  -Random -RandomScope local   random image (fav / local / any)");
                sb.AppendLine("  -Use \"path\"         use a local file as wallpaper");
                sb.AppendLine("  -Favorite           favorite the current image");
                sb.AppendLine("  -Unfavorite         unfavorite it");
                sb.AppendLine("  -Query \"snow peak\"  search and list matches (does not switch)");
                sb.AppendLine("  -Status             current state (wallpaper, counts, config)");
                sb.AppendLine("  -Json               return structured JSON for any operation");
                sb.AppendLine("  -Search \"snow peak\" search and switch to the first match");
                sb.AppendLine("  -Clean -Keep 7      clean cache older than 7 days");
                sb.AppendLine("  -CleanAll           clear cache (keeps the current wallpaper)");
                sb.AppendLine("  -CleanArchive       clean archive images (-WithIndex adds indexes; the legacy archive goes too)");
                sb.AppendLine("  -CleanFavorites     clear favorites (current wallpaper moves back to cache)");
                sb.AppendLine("  -Gallery            gallery mode: files go to cache\\gallery, not pinned");
                sb.AppendLine("  -GalleryClear       leave gallery mode and clear cache\\gallery");
                sb.AppendLine("  -FavDate 20261120   add that date to favorites");
                sb.AppendLine("  -UnfavDate 20261120 move it back to cache");
                sb.AppendLine("  -ExportLinks -RangeFrom 20261101 -RangeTo 20261121");
                sb.AppendLine("                      export download links only (JSON)");
                sb.AppendLine("  -ExportRange -RangeFrom 20261101 -RangeTo 20261121 -DestDir <dir>");
                sb.AppendLine("                      batch download a date range");
                sb.AppendLine("  -KeepExisting       skip files that already exist (default checks quality and upgrades)");
                sb.AppendLine();
                sb.AppendLine("  [Photo wall and prefetch]");
                sb.AppendLine("  -WallDate YYYYMMDD  fetch that date into cache\\wall (no wallpaper change)");
                sb.AppendLine("  -WallFull           with -WallDate, ask for UHD instead of the 1080p preview");
                sb.AppendLine("  -WallKeep           promote cache\\wall into archive\\images (UHD added)");
                sb.AppendLine("  -WallDrop           discard cache\\wall");
                sb.AppendLine("  -PrefetchDate YYYYMMDD  prefetch into cache\\prefetch");
                sb.AppendLine();
                sb.AppendLine("  [Lookup and maintenance]");
                sb.AppendLine("  -KwFile kw.txt      read keywords from a UTF-8 file (safer for non-ASCII)");
                sb.AppendLine("  -Festivals          how many days each of the nine festivals has");
                sb.AppendLine("  -Festival <name>    all dates of that festival (JSON, lunar computed on the fly)");
                sb.AppendLine("  -AuditDates         verify index date coverage");
                sb.AppendLine("  -AuditOld           verify the legacy index");
                sb.AppendLine("  -MigrateOld         move misplaced legacy images back into archive\\old\\<month>\\");
                sb.AppendLine("  -FillLibrary -RangeFrom -RangeTo    fill missing days into the takeover library (takeover only)");
                sb.AppendLine();
                sb.AppendLine("  -UpdateIndex        force-refresh the full index");
                sb.AppendLine("  -DryRun             show what would happen");
                return sb.ToString();
            }
            sb.AppendLine("命令行入口：");
            sb.AppendLine(exe);
            sb.AppendLine();
            sb.AppendLine("可用参数：");
            sb.AppendLine("  -Index 0            换今天的图");
            sb.AppendLine("  -Index 1..7         往前翻 1-7 天");
            sb.AppendLine("  -Step 1 / -Step -1  相对前后翻一天");
            sb.AppendLine("  -Date YYYYMMDD      换指定日期（2009-07-13 至今；2016-03 前是镜像档，分辨率较低）");
            sb.AppendLine("  -Random -RandomScope local   随机一张（fav 收藏 / local 本地全部 / any 任意一天）");
            sb.AppendLine("  -Use \"路径\"         用本地图片");
            sb.AppendLine("  -Favorite           收藏当前这张");
            sb.AppendLine("  -Unfavorite         取消收藏");
            sb.AppendLine("  -Query \"雪 山\"      搜索并列出匹配项（不改图）");
            sb.AppendLine("  -Status             查当前状态（壁纸、各段计数、配置）");
            sb.AppendLine("  -Json               让任何操作返回结构化 JSON 结果");
            sb.AppendLine("  -Search \"雪 山\"     搜索并换第一张匹配的");
            sb.AppendLine("  -Clean -Keep 7      清理 7 天前的缓存");
            sb.AppendLine("  -CleanAll           清空缓存（保留当前壁纸）");
            sb.AppendLine("  -CleanArchive       清理归档图片（可加 -WithIndex 连索引；老存档会一并清）");
            sb.AppendLine("  -CleanFavorites     清空收藏（当前壁纸移回缓存）");
            sb.AppendLine("  -Gallery            画廊模式：图落 cache\\gallery，不登记保护名单");
            sb.AppendLine("  -GalleryClear       退出画廊模式，清空 cache\\gallery");
            sb.AppendLine("  -FavDate 20261120   把该日期的图加入收藏");
            sb.AppendLine("  -UnfavDate 20261120 把收藏里该日期移回缓存");
            sb.AppendLine("  -ExportLinks -RangeFrom 20261101 -RangeTo 20261121");
            sb.AppendLine("                      只导出下载直链（JSON），不下载图片");
            sb.AppendLine("  -ExportRange -RangeFrom 20261101 -RangeTo 20261121 -DestDir 目录");
            sb.AppendLine("                      按日期范围批量下载到指定目录");
            sb.AppendLine("  -KeepExisting       批量下载时目标已有同名文件就直接跳过（默认会检查画质并升级）");
            sb.AppendLine();
            sb.AppendLine("  【照片墙与预取】");
            sb.AppendLine("  -WallDate YYYYMMDD  取该日期的图到 cache\\wall（不设壁纸、不写状态）");
            sb.AppendLine("  -WallFull           配合 -WallDate，直接要 UHD 而非 1080p 预览");
            sb.AppendLine("  -WallKeep           把 cache\\wall 的图转正进 archive\\images（自动补 UHD）");
            sb.AppendLine("  -WallDrop           丢弃 cache\\wall 的临时图");
            sb.AppendLine("  -PrefetchDate YYYYMMDD  预取到 cache\\prefetch（不设壁纸）");
            sb.AppendLine();
            sb.AppendLine("  【查找与维护】");
            sb.AppendLine("  -KwFile kw.txt      关键词从 UTF-8 文件读（PS 5.1 传中文更稳）");
            sb.AppendLine("  -Festivals          九个节日各有多少天");
            sb.AppendLine("  -Festival 中秋节    该节日的全部日期（JSON，农历现场算）");
            sb.AppendLine("  -AuditDates         核对索引日期完整性");
            sb.AppendLine("  -AuditOld           核对老索引");
            sb.AppendLine("  -MigrateOld         把错位的老存档挪回 archive\\old\\<月份>\\（3.6.48 及更早的遗留）");
            sb.AppendLine("  -FillLibrary -RangeFrom -RangeTo   补齐接管图库里缺的日期（仅接管模式）");
            sb.AppendLine();
            sb.AppendLine("  -UpdateIndex        强制刷新全量索引");
            sb.AppendLine("  -DryRun             只演示不执行");
            return sb.ToString();
        }
        public static string ShowStoryOn     { get { return T("● 显示每日故事", "● Show daily story"); } }
        public static string ShowStoryOff    { get { return T("显示每日故事（关）", "Daily story (off)"); } }

        // ---------------- 关于正文
        public static string AboutFeat1      { get { return T("· 覆盖 6339 天：2009-07-13 至今，一天不缺（空缺 3 天，来源本身就缺）", "· 6339 days covered: 2009-07-13 to today, one per day (3 days missing at the source)"); } }
        public static string AboutFeat2      { get { return T("· 往回翻到 2009 年 7 月：最近 8 天走实时接口，更早自动接续归档；日历可跳任意一天", "· Browse back to Jul 2009: last 8 days from the API, older from the archive; the calendar jumps anywhere"); } }
        public static string AboutFeat2b     { get { return T("· 取图 5 级自动回退：UHD → 缩放 → 微软 CDN → 1080p → 第三方镜像", "· 5-step fallback: UHD → scaled → MS CDN → 1080p → third-party mirror"); } }
        public static string AboutFeat3      { get { return T("· 三层封顶不攒垃圾：缓存 30 天 / 归档 500 MB / 缩略图 800 张", "· Three caps keep storage flat: 30-day cache / 500 MB archive / 800 thumbnails"); } }
        public static string AboutFeat4      { get { return T("· 关掉托盘也照常换图；索引内嵌在 exe 里，断网也能查标题", "· Works with the tray closed; the index is embedded, so titles stay searchable offline"); } }
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

        // ---------------- 导出与批量下载
        public static string ExportDownloaded { get { return T("导出已下载壁纸…", "Export downloaded…"); } }
        public static string DownloadRange    { get { return T("批量下载壁纸…", "Batch download…"); } }
        public static string ExpTitle         { get { return T(" · 导出已下载壁纸", " · Export downloaded"); } }
        public static string ExpWhich         { get { return T("导出哪些", "Include"); } }
        public static string ExpTo            { get { return T("导出到", "Export to"); } }
        public static string ExpBtn            { get { return T("导出", "Export"); } }
        public static string ExpEmpty         { get { return T("这个目录里没有图片。", "No images in this folder."); } }
        public static string ExpSum(int n, double mb) { return _en ? (n + " selected, " + mb.ToString("0.0") + " MB") : ("已选 " + n + " 张，共 " + mb.ToString("0.0") + " MB"); }
        public static string ExpDone(int ok, int skip) { return _en ? ("Export done — " + ok + " new, " + skip + " skipped") : ("导出完成：新存 " + ok + " 张，跳过 " + skip + " 张"); }

        public static string DlTitle           { get { return T(" · 批量下载壁纸", " · Batch download"); } }
        public static string DlRange           { get { return T("时间范围", "Date range"); } }
        public static string DlFrom            { get { return T("从", "From"); } }
        public static string DlTo              { get { return T("到", "To"); } }
        public static string DlDest            { get { return T("下载到", "Save to"); } }
        public static string DlQuality { get { return T("画质", "Quality"); } }
        public static string DlQualityUhd { get { return T("最高可用（UHD → 1080p）", "Highest (UHD → 1080p)"); } }
        public static string DlQualityFhd { get { return T("1080p（体积约 1/15）", "1080p (about 1/15 the size)"); } }
        public static string DlQualityHd { get { return T("只用镜像（1366×768）", "Mirror only (1366×768)"); } }
        public static string DlNote            { get { return T("本地已有同一张会直接跳过，不会重复下载", "Existing files are skipped, nothing is downloaded twice"); } }
        public static string DlBtn             { get { return T("开始", "Start"); } }
        public static string DlWorking         { get { return T("正在下载…", "Downloading…"); } }
        public static string DlEst(int n, double mb) { return _en ? (n + " days, about " + mb.ToString("0") + " MB") : ("共 " + n + " 天，约 " + mb.ToString("0") + " MB"); }
        public static string DlEstWarn(int n, double mb, int mins) { return _en ? (n + " days, about " + mb.ToString("0") + " MB · large batch, roughly " + mins + " min") : ("共 " + n + " 天，约 " + mb.ToString("0") + " MB · 数量较大，预计 " + mins + " 分钟"); }
        public static string DlRangeDone(int ok, int skip, int fail) { return _en ? ("Done — " + ok + " downloaded, " + skip + " skipped, " + fail + " failed") : ("完成：下载 " + ok + " 张，跳过 " + skip + " 张，失败 " + fail + " 张"); }
        public static string DlRangeDone2(int ok, int skip, int fail, int low)
        {
            if (_en)
            {
                return "Done — " + ok + " downloaded (" + low + " without UHD), " + skip + " skipped, " + fail + " failed"
                     + (low > 0 ? "\r\n\r\nSome days have no UHD on Bing's side (_UHD returns 1080p). They are recorded in no-uhd.json and will not be retried." : "");
            }
            return "完成：下载 " + ok + " 张（其中无 UHD " + low + " 张），跳过 " + skip + " 张，失败 " + fail + " 张"
                 + (low > 0 ? "\r\n\r\n有几天 Bing 本身就只给 1080p（_UHD 地址返回的是 1080p 内容）。已记入 no-uhd.json，之后不再重复尝试。" : "");
        }
        public static string DlUhdNote { get { return T("UHD 仅 2019 年起提供；2019 年前最高 1920×1080（部分镜像仅 958px）。每张都会校验真实宽度。", "UHD exists only from 2019 on; earlier days cap at 1920×1080 (some mirrors 958px). Every file is verified."); } }
        public static string DlOnlyLowRes { get { return T("只补非 UHD（本地已有的低画质才重下，其余跳过）", "Only fill missing UHD (re-download low-res files only)"); } }
        public static string WallCardGap { get { return T("Bing 官方未发布", "Not published"); } }
        public static string WallOfficialGap { get { return T("这一天 Bing 官方没有发布壁纸，没有图可拉。", "Bing published no wallpaper that day; there is nothing to fetch."); } }
        public static string WallGapHint(int n) { return _en ? ("· " + n + " day(s) never published") : ("· 其中 " + n + " 天 Bing 官方没发图"); }
        public static string WarnDateOrder     { get { return T("起始日期不能晚于结束日期。", "Start date is after end date."); } }
        public static string WarnInsideApp     { get { return T("不能选程序自己的目录或它的子目录——那里会被自动清理误伤，程序文件也可能被覆盖。请换一个位置。", "Cannot use the program's own folder or its subfolders — auto-cleanup would delete them and program files could be overwritten. Please pick another location."); } }
        public static string WarnNoPath   { get { return T("请填写安装路径。", "Please enter an install path."); } }
        public static string WarnBadDir   { get { return T("目录不可用：", "Folder unavailable: "); } }
        public static string BtnInstallTx { get { return T("安装", "Install"); } }
        public static string BtnPortable  { get { return T("绿色运行", "Run portable"); } }
        public static string SetupDeskLink { get { return T("在桌面创建快捷方式", "Create a desktop shortcut"); } }
        public static string ArchNoteAlt  { get { return T("归档里的图是你从历史里挑出来的，可用「选日期」重新设为壁纸。归档有 500 MB 上限，满了会从最旧的开始自动清。", "Images you picked from history; reuse them via Pick a date. The archive is capped at 500 MB and clears oldest-first when full."); } }

        public static string AboutArchLine(bool on, int months)
        {
            return _en
                ? ("Archive   Zhu-junwei since 2016, niumoo fallback (" + (on ? "on" : "off") + ", " + months + " months indexed, size-capped)")
                : ("历史归档  Zhu-junwei（2016 起）+ niumoo 兜底（" + (on ? "已启用" : "已关闭") + "，索引 " + months + " 个月份，有容量上限）");
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
        // 随机范围三档：按「要付什么代价」分，不按文件在哪个目录分
        public static string RandomScopeFav(int n)   { return _en ? ("Favorites (" + n + ")") : ("收藏（" + n + " 张）"); }
        public static string RandomScopeLocal(int n) { return _en ? ("All local (" + n + ")") : ("本地全部（" + n + " 张）"); }
        public static string RandomScopeAny         { get { return T("任意一天（没下载过的会先下载）", "Any day (downloads if missing)"); } }
        public static string RandomScopeFavLabel    { get { return T("收藏", "favorites"); } }
        public static string RandomScopeLocalLabel  { get { return T("本地全部", "local"); } }
        public static string RandomScopeAnyLabel    { get { return T("任意一天", "any day"); } }
        public static string RandomTitle(string scopeLabel, int n)
        {
            if (n <= 0) { return _en ? "Random image (nothing available)" : "随机换一张（没有可选的图）"; }
            return _en ? ("Random " + scopeLabel + " (" + n + ")") : ("随机换一张（" + scopeLabel + " " + n + " 张）");
        }
        public static string RandomTitleAny { get { return _en ? "Random image (any day)" : "随机换一张（任意一天）"; } }
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
                ? ("Archive   Zhu-junwei since 2016, niumoo fallback (" + (on ? "enabled" : "disabled") + ", " + months + " months indexed, size-capped)")
                : ("历史归档  Zhu-junwei（2016 起）+ niumoo 兜底（" + (on ? "已启用" : "已关闭") + "，索引 " + months + " 个月份，有容量上限）");
        }
        public static string AboutStore(int cCount, double cMb, int fCount, double fMb, int aCount, double aMb)
        {
            if (_en)
            {
                return "Cache    " + cCount + " items / " + cMb.ToString("0.0") + " MB (last 30 days, auto-cleaned)\r\n" +
                       "Favs     " + fCount + " items / " + fMb.ToString("0.0") + " MB (kept forever)\r\n" +
                       "Archive  " + aCount + " items / " + aMb.ToString("0.0") + " MB (capped, oldest cleared first)";
            }
            return "缓存  " + cCount + " 张 / " + cMb.ToString("0.0") + " MB（最近 30 天，自动清理）\r\n" +
                   "收藏  " + fCount + " 张 / " + fMb.ToString("0.0") + " MB（永久保留）\r\n" +
                   "归档  " + aCount + " 张 / " + aMb.ToString("0.0") + " MB（有容量上限，满了从旧到新清）";
        }
    }

}
