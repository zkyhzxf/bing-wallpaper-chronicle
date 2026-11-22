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
        public static string SrchHint         { get { return T("索引覆盖 2016-03 至今；最近 8 天实时补齐。", "Index covers Mar 2016 onward; the latest 8 days are always fetched live."); } }
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
        public static string CleanArchiveIndexMenu { get { return T("清除归档索引", "Clear archive index"); } }
        public static string CleanResult(int n) { return _en ? ("Cleaned " + n + " items") : ("清理完成：删除 " + n + " 个"); }
        public static string CleanDoneCache(int rm, int pt) { return _en ? ("Cleaned " + rm + ", protected " + pt) : ("清理完成：删除 " + rm + " 张，保护 " + pt + " 张"); }
        public static string CleanDoneAll(int rm, int kept) { return _en ? ("Cache cleared — " + rm + " removed, " + kept + " kept (current)") : ("缓存已清空：删除 " + rm + " 张，当前壁纸 " + kept + " 张保留"); }
        public static string CleanDoneFav(int rm, int moved) { return _en ? ("Favorites cleared — " + rm + " removed, " + moved + " moved back to cache") : ("收藏夹已清空：删除 " + rm + " 张，当前壁纸 " + moved + " 张已移回缓存"); }
        public static string CleanDoneIdx(int rm) { return _en ? ("Removed " + rm + " index files") : ("删除 " + rm + " 个索引文件"); }
        public static string CleanWithIndex { get { return T("同时清理索引（15 个月份 + 全量索引）", "Also clear indexes (monthly + full)"); } }
        public static string CleanWithIndexHint { get { return T("删掉的是目录册，不是图片：之后取历史图要联网查，搜索会重下索引", "Removes the catalogue, not the images: later lookups go online and search re-downloads the index"); } }
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

        // ---------------- 自动清理保留
        public static string PinMenu      { get { return T("自动清理保留…", "Keep during auto-clean…"); } }
        public static string PinMenuMB(double mb) { return _en ? ("Keep during auto-clean… (" + mb.ToString("0.0") + " MB)") : ("自动清理保留…（" + mb.ToString("0.0") + " MB）"); }
        public static string PinTitle     { get { return T(" · 自动清理保留", " · Kept from auto-clean"); } }
        public static string PinIntro1    { get { return T("这里记的是你用「翻页」「按日期」「搜索」取过的历史图。", "Dates recorded when you browsed, picked by date, or searched."); } }
        public static string PinIntro2    { get { return T("登记过的日期，自动清理和归档超限清理都不会动。", "Those dates are never touched by auto-clean or archive size limits."); } }
        public static string PinColDate   { get { return T("日期", "Date"); } }
        public static string PinColTitle  { get { return T("标题", "Title"); } }
        public static string PinColSize   { get { return T("体积", "Size"); } }
        public static string PinGone      { get { return T("文件已不在", "file missing"); } }
        public static string PinNoTitle   { get { return T("(无标题)", "(no title)"); } }
        public static string PinRemove    { get { return T("移出名单", "Remove"); } }
        public static string PinOpenFile  { get { return T("打开文件", "Open file"); } }
        public static string PinClose     { get { return T("关闭", "Close"); } }
        public static string PinHintMore  { get { return T("这些日期在自动清理时被跳过，可在「清理 → 自动清理保留」里移出。", "Skipped during auto-clean; manage them under Cleanup → Keep during auto-clean."); } }
        public static string PinNote      { get { return T("自动清理缓存（30 天规则）会跳过这些日期。手动的「清空缓存」「清理归档图片」不看这份清单，只保留收藏和当前壁纸。归档满了会分两轮清：先删名单外的，空间还不够才动名单里的。", "The 30-day auto-clean skips these dates. Manual \"clear cache\" and \"clean archived images\" ignore this list and keep only favorites and the current wallpaper. When the archive fills up it clears in two passes: unlisted images first, only then the ones on this list."); } }
        public static string PinSum(int n, double mb) { return _en ? (n + " entries · " + mb.ToString("0.0") + " MB") : ("共 " + n + " 项 · " + mb.ToString("0.0") + " MB"); }
        public static string FavSumFiltered(int hit, int total, double mb) { return _en ? (hit + " of " + total + " · " + mb.ToString("0.0") + " MB") : ("匹配 " + hit + " / 共 " + total + " 项 · " + mb.ToString("0.0") + " MB"); }
        public static string FavSearchHint { get { return T("搜索标题/版权/日期", "search title/copyright/date"); } }
        public static string PinConfirm(int n) { return _en ? ("These " + n + " dates will no longer be skipped by auto-clean and may be removed next time it runs. Nothing is moved out now. Continue?") : ("这 " + n + " 天将不再被自动清理跳过，下次清理可能删掉它们。本次操作不移出任何文件。确定吗？"); }
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
        public static string CleanPinProtected{ get { return T("清单保护（保留，可在「自动清理保留」调整）", "Pinned (kept, adjustable in auto-clean list)"); } }
        public static string CleanCurProtected{ get { return T("当前壁纸（保留，删了桌面会黑）", "Current (kept — deleting it would blank the desktop)"); } }
        public static string CleanCurMoved  { get { return T("当前壁纸（将移回缓存）", "Current (will return to cache)"); } }
        public static string CleanActual      { get { return T("实际可清理", "Will remove"); } }
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
        public static string ConfirmCleanAll   { get { return T("将删除缓存目录里除当前壁纸以外的所有图片。\r\n当前壁纸会保留，其它全部清掉。确定吗？", "Delete every image in the cache except the current wallpaper?\r\nThe current one is kept. Continue?"); } }
        public static string ConfirmCleanTtl   { get { return T(" · 清理缓存", " · Clear cache"); } }
        public static string ConfirmArchTtl    { get { return T(" · 清理历史归档", " · Clear history archive"); } }
        public static string ConfirmArchBody   { get { return T(" 张图移入回收站？\r\n\r\n当前正在显示的壁纸不动。之后可从回收站找回，\r\n也可以通过「选日期」重新下载。", " images will be moved to the Recycle Bin.\r\n\r\nThe current wallpaper is left untouched. You can restore from the Recycle Bin\r\nor re-download later via Pick a date."); } }
        public static string BRepairKOHint     { get { return T("计划任务仍不可用，请用管理员身份运行一次或查看日志", "Task still unavailable; run as administrator once, or check the log"); } }
        public static string BRepairedHint     { get { return T("计划任务已重新注册，自动换图恢复正常", "Task re-registered; daily changes resumed"); } }
        public static string BReadyHint        { get { return T("壁纸每天 0:10 自动更新，8:30 与 12:00 会补查一次。右键托盘图标可换图、收藏、暂停。", "Wallpaper updates daily at 00:10, with retries at 08:30 and 12:00. Right-click the tray icon to change, favorite, or pause."); } }
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
        public static string WallFetching     { get { return T("正在取这张…", "Fetching…"); } }
        public static string WallFetchingN    { get { return T("正在取第 {0}/{1} 张…", "Fetching {0}/{1}…"); } }
        public static string WallFetchDone(int ok, int total)
        {
            string tpl = _en ? "Fetched {0} of {1}" : "已取回 {0}/{1} 张";
            return string.Format(tpl, ok, total);
        }
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
                   "单张留存：在「自动清理保留」里点「加入收藏」。" + Environment.NewLine +
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
        public static string InfoBtnMore     { get { return T("浏览更多壁纸", "Browse more"); } }
        public static string InfoBtnFav      { get { return T("☆ 收藏", "☆ Favorite"); } }
        public static string HelpItem       { get { return T("帮助…", "Help…"); } }
        public static string HelpTitle      { get { return T(" · 帮助", " · Help"); } }
        public static string HelpBody
        {
            get
            {
                return _en
                    ? @"1. Daily rotation
    The wallpaper switches to today''s Bing image at 00:10, with retries at 08:30 and 12:00.
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
    Tray menu -> ""Library"" -> ""Search by date"" opens a calendar.
    Dates shown in bold already have their image stored locally.

4. Favorites, cache and archive
    Cache keeps 30 days; the archive is capped at 500 MB and clears oldest first.
    Images in favorites are never removed automatically.
    ""Clean cache"" and ""Clean archive"" free space by those rules and always keep
    favorites and the current wallpaper.

5. Gallery mode
    Turn it on to browse freely without piling up files.
    Images viewed in gallery mode land in cache\gallery and are not registered for
    protection; turning the mode off or restarting clears them.
    Click ""Favorite"" on anything you like - it moves to favorites and survives the cleanup.

6. Why some images look soft
    Originals from 2009-2013 are 958x512, 2014 to early 2016 are 1366x768 or 1080p,
    and only from March 2016 onwards are they 3840x2160. That was Bing''s publishing
    standard at the time, not a download error.

7. Data sources and troubleshooting
    Primary index Zhu-junwei, fallback bing.wilii.cn, legacy archive anerg,
    daily stories from December 2013.
    Start with the log: tray menu -> ""System"" -> ""Open log"".
    If automatic change stops working, the top line of the menu shows the scheduled
    task status; click it to attempt a repair."
                    : @"一、每天怎么换图
    默认在 00:10 自动换成当天的必应图，8:30 与 12:00 各补查一次。
    想立刻回到今天：托盘菜单 →「换成今天」。

二、往回翻
    「上一张 / 下一张」按天往前翻。最近 8 天走实时接口，更早的自动接续归档，
    最远可以翻到 2009 年 7 月 13 日。
    打开「这张图是哪儿」之后，按键盘 ← → 也能翻，窗口内容和桌面壁纸会一起变。
    某一张一时取不到时，窗口里会自动再试 3 次，不用手动重开。
    托盘菜单 →「往年的今天」列出历年同日的图；还没下载过的也会先显示标题，点一下才去取。

三、按日期找
    托盘菜单 →「壁纸库」→「按日期搜索」打开日历。
    日历上显示为粗体的日期，表示本地已经有这一天的图。

四、收藏、缓存与归档
    缓存保留 30 天，归档上限 500 MB，超过就从最旧的开始清。
    收藏夹里的图永远不会被自动清理。
    「清理缓存」「清理归档」会按上述规则腾空间，收藏与当前壁纸始终保留。

五、画廊模式
    想随手翻着看、又不想攒下一堆文件时打开它。
    画廊里看的图落在 cache\gallery，不登记保护名单；关掉模式或重启软件就清空。
    看到喜欢的随手点「收藏」，它会移进收藏夹，退出画廊也不会被清。

六、为什么有些图比较糊
    2009 至 2013 年的原图就是 958×512，2014 年到 2016 年初是 1366×768 或 1080p，
    2016 年 3 月之后才是 3840×2160。这是当年必应的发布标准，不是下载出错。

七、数据来源与出问题怎么办
    主索引 Zhu-junwei、兜底 bing.wilii.cn、老存档 anerg，每日故事自 2013 年 12 月起。
    出问题先看日志：托盘菜单 →「系统」→「打开日志」。
    自动换图不生效时，菜单顶部那行会显示计划任务状态，点它可以尝试修复。";
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
        public static string CleanStories    { get { return T("清理每日故事缓存", "Clear story cache"); } }
        public static string CleanStoriesN   { get { return T("清理每日故事缓存（{0} 条）", "Clear story cache ({0})"); } }
        public static string ShowStoryOn     { get { return T("● 显示每日故事", "● Show daily story"); } }
        public static string ShowStoryOff    { get { return T("显示每日故事（关）", "Daily story (off)"); } }
        public static string CleanStoriesHint { get { return T("删掉的是本地缓存，下次查看时按需重新抓取；不影响壁纸与索引", "Removes the local cache only; stories are re-fetched on demand"); } }

        // ---------------- 关于正文
        public static string AboutFeat1      { get { return T("· 每天 00:10 自动换当天的图，8:30 / 12:00 补查", "· Switches to today's image at 00:10, with retries at 08:30 / 12:00"); } }
        public static string AboutFeat2      { get { return T("· 往回翻：最近 8 天直接取，更早的接续归档，可一路翻到 2009-07", "· Browse back: last 8 days from the API, older ones continue from the archive, all the way to Jul 2009"); } }
        public static string AboutFeat2b     { get { return T("· 日历跳到 2009-07 以来的任意一天（2016-03 前是存档图，分辨率较低）", "· Jump to any day since Jul 2009 (before Mar 2016 comes from an older archive at lower resolution)"); } }
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
        public static string DlNote            { get { return T("本地已有同一张会直接跳过，不会重复下载", "Existing files are skipped, nothing is downloaded twice"); } }
        public static string DlBtn             { get { return T("开始", "Start"); } }
        public static string DlWorking         { get { return T("正在下载…", "Downloading…"); } }
        public static string DlEst(int n, double mb) { return _en ? (n + " days, about " + mb.ToString("0") + " MB") : ("共 " + n + " 天，约 " + mb.ToString("0") + " MB"); }
        public static string DlEstWarn(int n, double mb, int mins) { return _en ? (n + " days, about " + mb.ToString("0") + " MB · large batch, roughly " + mins + " min") : ("共 " + n + " 天，约 " + mb.ToString("0") + " MB · 数量较大，预计 " + mins + " 分钟"); }
        public static string DlRangeDone(int ok, int skip, int fail) { return _en ? ("Done — " + ok + " downloaded, " + skip + " skipped, " + fail + " failed") : ("完成：下载 " + ok + " 张，跳过 " + skip + " 张，失败 " + fail + " 张"); }
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
