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
    internal static partial class Program
    {
        // ---------------------------------------------------------------- 刷新编排
        //
        // 分三层，每层可单独调用：
        //   入口 RefreshAll      只做防重入与调度
        //   第 2 层              读状态，产出 showDate/showTitle/isLocked
        //   第 3 层              各部件独立更新，互不依赖
        //
        // 这样某层出问题时能立刻定位，不用像之前那样在一个 60 行函数里猜。

        /// <summary>刷新编排入口：只负责防重入与调度，不做具体工作。</summary>
        private static SearchForm searchForm;
        private static PhotoWallForm wallForm;

        private static TakeoverForm takeoverForm;

        /// <summary>接管目录窗口。</summary>
        private static void DoTakeover()
        {
            if (takeoverForm != null && !takeoverForm.IsDisposed) { takeoverForm.Activate(); return; }
            takeoverForm = new TakeoverForm();
            takeoverForm.FormClosed += delegate { takeoverForm = null; RefreshAll(); };
            takeoverForm.Show();
        }

        /// <summary>照片墙：把历年同一天的图一次摆出来。</summary>
        private static PhotoWallForm festForm;

        /// <summary>按节日筛选：脚本算日期，照片墙只负责画。</summary>
        private static void DoFestival(string name)
        {
            if (festForm != null && !festForm.IsDisposed) { festForm.Activate(); return; }
            string outText = "";
            try { outText = RunScriptCapture("-Festival " + name); } catch { }
            List<string> ds = new List<string>();
            try
            {
                System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(
                    outText, "\"dates\"\\s*:\\s*\\[([^\\]]*)\\]");
                if (m.Success)
                {
                    foreach (System.Text.RegularExpressions.Match mm in System.Text.RegularExpressions.Regex.Matches(m.Groups[1].Value, "\\d{8}"))
                    {
                        ds.Add(mm.Value);
                    }
                }
            }
            catch { }
            if (ds.Count == 0)
            {
                MessageBox.Show(S.FestivalEmpty(name), Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            ds.Sort();
            festForm = new PhotoWallForm(ds.ToArray(), S.FestivalWallTitle(S.FestivalName(name), ds.Count));
            festForm.FormClosed += delegate { festForm = null; };
            festForm.Show();
        }

        private static void DoPhotoWall()
        {
            if (wallForm != null && !wallForm.IsDisposed) { wallForm.Activate(); return; }
            wallForm = new PhotoWallForm();
            wallForm.FormClosed += delegate { wallForm = null; };
            wallForm.Show();
        }
        private static CalendarForm calForm;

        internal static void RefreshAllPublic() { RefreshAll(); }

        private static bool takeoverChecked = false;

        /// <summary>接管目录消失时自动回退：删掉 config.json，回到安装目录的样子。</summary>
        private static void EnsureTakeoverValid()
        {
            if (takeoverChecked) { return; }
            takeoverChecked = true;
            try
            {
                string r = CurrentTakeoverRoot();
                if (!string.IsNullOrEmpty(r) && !Directory.Exists(r))
                {
                    if (File.Exists(TakeoverCfgPath)) { File.Delete(TakeoverCfgPath); }
                    Notify(S.TkBrokenTitle, S.TkBrokenBody(r));
                }
            }
            catch { }
        }

        /// <summary>
        /// 兜底自愈：注册表里的当前壁纸路径如果已经不存在（被哪条清理路径误删、
        /// 或者用户手动挪走了），直接退回今天。
        /// 比「逐个清理函数记得保护当前壁纸」更彻底 —— 它不关心是谁删的。
        /// 节流 10 分钟，避免今天取不到图时反复重试。
        /// </summary>
        private static DateTime lastHealAt = DateTime.MinValue;

        internal static void EnsureWallpaperAlive()
        {
            try
            {
                if ((DateTime.Now - lastHealAt).TotalMinutes < 10) { return; }
                string wp = Program.CurrentWallpaperPath();
                if (string.IsNullOrEmpty(wp)) { return; }          // 压根没设壁纸，不管
                if (File.Exists(wp)) { return; }                   // 正常
                // 路径已失效
                lastHealAt = DateTime.Now;
                try
                {
                    string p = Path.Combine(Path.GetTempPath(), "BingWallpaperLite.log");
                    File.AppendAllText(p,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  [HEAL]  当前壁纸文件已不存在，退回今天：" + wp + "\r\n",
                        new UTF8Encoding(false));
                }
                catch { }
                Balloon(S.HealTitle, S.HealHint);
                anchor.BeginInvoke((MethodInvoker)delegate { RunScriptEx("-Index 0", "heal"); });
            }
            catch { }
        }

        private static void RefreshAll()
        {
            if (refreshing) { return; }
            refreshing = true;
            try
            {
                EnsureTakeoverValid();
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
                // 常驻的信息窗口要跟着换图走：菜单、收藏管理、照片墙、自动换图都经过这里
                try { ReloadInfoIfOpen(); } catch { }
                // 壁纸路径失效就退回今天：不管是被哪条清理路径删的
                try { EnsureWallpaperAlive(); } catch { }
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
            // 先算可用性 gate（一次目录扫描），再刷新各自文字；
            // 所有顶级项的 Enabled 统一由 ApplyMenuState 落，各 RefreshXxx 不再各写各的
            MenuGate g = MenuGate.Ready;
            g.Busy = busy;
            g.Takeover = TakeoverActive();
            g.LocalN = LocalPoolCount();
            g.FavN = FavDateCount();
            SetGate(g);
            try { ApplyMenuState(); } catch { }

            RefreshLibraryMenu(state, showDate);
            RefreshFavoriteCheck();
            RefreshTaskState();
            RefreshTaskTimeChecks();
            RefreshYearsMenu();
            RefreshRandomCheck(g);
            RefreshArchiveCheck();
            RefreshEntryCheck();
            RefreshStartupCheck();
            RefreshLockCheck();
            RefreshCleanCheck();
            RefreshTakeoverState();
            RefreshNotifyCheck();
            RefreshThemeCheck();
        }

        /// <summary>按日期查标题：只读索引，不下载不换图。查不到返回空串。</summary>
        internal static string LookupDateTitle(string date)
        {
            try
            {
                string outText = RunScriptCapture("-DateInfo " + date);
                if (string.IsNullOrEmpty(outText)) { return ""; }
                System.Text.RegularExpressions.Match m =
                    System.Text.RegularExpressions.Regex.Match(outText, "\"title\"\\s*:\\s*\"([^\"]*)\"");
                if (m.Success) { return m.Groups[1].Value; }
            }
            catch { }
            return "";
        }

        /// <summary>导出已下载壁纸：按用户勾选的目录复制到目标文件夹，同名跳过。</summary>
        private static void DoExportDownloaded()
        {
            if (busy) { return; }
            using (ExportForm f = new ExportForm())
            {
                if (f.ShowDialog() != DialogResult.OK) { return; }
                List<string> srcDirs = f.SelectedDirs;
                string dest = f.DestPath;
                if (srcDirs == null || srcDirs.Count == 0 || string.IsNullOrEmpty(dest)) { return; }

                SetBusy(true);
                anchor.BeginInvoke((MethodInvoker)delegate
                {
                    int okCount = 0;
                    int skip = 0;
                    try
                    {
                        foreach (string dir in srcDirs)
                        {
                            string[] files;
                            try { files = Directory.GetFiles(dir, "*.jpg"); }
                            catch { continue; }
                            foreach (string file in files)
                            {
                                string target = Path.Combine(dest, Path.GetFileName(file));
                                if (File.Exists(target)) { skip++; continue; }
                                try { File.Copy(file, target, false); okCount++; } catch { }
                            }
                        }
                    }
                    finally
                    {
                        SetBusy(false);
                        if (!menu.Visible)
                        {
                            Balloon(S.IsEn ? "Export done" : "导出完成", S.ExpDone(okCount, skip));
                        }
                    }
                });
            }
        }

        /// <summary>批量下载壁纸：按日期范围调脚本，完成弹结果。</summary>
        private static void DoDownloadRange()
        {
            if (busy) { return; }
            using (DownloadForm f = new DownloadForm())
            {
                if (f.ShowDialog() != DialogResult.OK) { return; }
                string from = f.FromDate;
                string to = f.ToDate;
                string dest = f.DestPath;
                string res = f.MaxRes;
                if (string.IsNullOrEmpty(dest)) { return; }

                SetBusy(true);
                anchor.BeginInvoke((MethodInvoker)delegate
                {
                    string outText = "";
                    try
                    {
                        outText = RunScriptCapture("-ExportRange -RangeFrom " + from +
                            " -RangeTo " + to + " -DestDir \"" + dest + "\" -MaxRes " + res);
                    }
                    catch { }
                    SetBusy(false);

                    int okCount = 0;
                    int skip = 0;
                    int fail = 0;
                    System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(
                        outText ?? "", "\"ok\"\\s*:\\s*(\\d+)[^}]*\"skip\"\\s*:\\s*(\\d+)[^}]*\"fail\"\\s*:\\s*(\\d+)");
                    if (m.Success)
                    {
                        int.TryParse(m.Groups[1].Value, out okCount);
                        int.TryParse(m.Groups[2].Value, out skip);
                        int.TryParse(m.Groups[3].Value, out fail);
                    }
                    MessageBox.Show(S.DlRangeDone(okCount, skip, fail), AppName,
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    try { OpenTarget(dest); } catch { }
                });
            }
        }

        private static void DoPickDate()
        {
            if (busy) { return; }
            // 非模态常驻：选中日期直接换图，窗口留着，方便连着挑几天
            if (calForm != null && !calForm.IsDisposed) { calForm.Activate(); return; }
            calForm = new CalendarForm();
            calForm.FormClosed += delegate { calForm = null; RefreshAll(); };
            calForm.Show();
        }

        private static void DoSearch()
        {
            if (busy) { return; }
            // 非模态常驻：选中结果直接换图，窗口留着，可以接着搜下一组
            if (searchForm != null && !searchForm.IsDisposed) { searchForm.Activate(); return; }
            searchForm = new SearchForm();
            searchForm.FormClosed += delegate { searchForm = null; RefreshAll(); };
            searchForm.Show();
        }

        private static void DoAi()
        {
            if (busy) { return; }
            using (AiForm f = new AiForm()) { f.ShowDialog(); }
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

        private static readonly Dictionary<string, string[]> scanCache = new Dictionary<string, string[]>();
        private static readonly Dictionary<string, long> scanStamp = new Dictionary<string, long>();

        /// <summary>接管模式下 clean 整个子菜单置灰：只读，点下去也只会被脚本拒绝。</summary>
        private static void RefreshTakeoverState()
        {
            try
            {
                bool tk = TakeoverActive();
                ToolStripMenuItem cm = menu.Items["clean"] as ToolStripMenuItem;
                if (cm != null)
                {
                    // Enabled 交给 ApplyMenuState（按 gate.Takeover 推导），这里只管说明文字
                    cm.ToolTipText = tk ? S.TkReadOnlyHint : "";
                }
                ToolStripMenuItem ms = menu.Items["modestate"] as ToolStripMenuItem;
                if (ms != null)
                {
                    if (tk)
                    {
                        string root = CurrentTakeoverRoot();
                        string name = "";
                        try { name = new DirectoryInfo(root).Name; } catch { }
                        if (string.IsNullOrEmpty(name)) { name = root; }
                        ms.Text = S.ModeBadge(name);
                        ms.ToolTipText = S.ModeBadgeHint(root);
                        ms.Visible = true;
                    }
                    else
                    {
                        ms.Visible = false;
                    }
                }
            }
            catch { }
        }

        private static string[] SafeGetFiles(string dir)
        {
            try
            {
                if (!Directory.Exists(dir)) { return new string[0]; }
                // 接管模式下 root 里可能有几万张：按目录修改时间缓存一次扫描结果
                long stamp = 0;
                try { stamp = Directory.GetLastWriteTimeUtc(dir).Ticks; } catch { }
                string[] cached;
                if (stamp != 0 && scanCache.TryGetValue(dir, out cached))
                {
                    long old;
                    if (scanStamp.TryGetValue(dir, out old) && old == stamp) { return cached; }
                }
                string[] f = Directory.GetFiles(dir, "*.jpg");
                Array.Sort(f, delegate(string a, string b)
                {
                    return string.Compare(Path.GetFileName(b), Path.GetFileName(a), StringComparison.Ordinal);
                });
                if (stamp != 0) { scanCache[dir] = f; scanStamp[dir] = stamp; }
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
            lib.DropDownItems.Add(MakeItem(S.OpenArchiveDir, delegate { OpenTarget(archiveImgDir); }));
            lib.DropDownItems.Add(MakeItem(S.ExportDownloaded, delegate { DoExportDownloaded(); }));
            lib.DropDownItems.Add(MakeItem(S.DownloadRange, delegate { DoDownloadRange(); }));
        }

        /// <summary>归档容量上限（MB），读配置，取不到按 500。</summary>
        internal static int ArchiveCapMB()
        {
            try { return GetCfgInt(ReadConfig(), "archiveMaxMB", 500); }
            catch { return 500; }
        }

        /// <summary>state.json 里记录的当前壁纸日期；取不到返回空串。</summary>
        internal static string CurrentStateDate()
        {
            try
            {
                Dictionary<string, object> st = ReadJson(statePath);
                string d = GetStr(st, "date");
                return d.Length == 8 ? d : "";
            }
            catch { return ""; }
        }

        /// <summary>名单文件路径：接管时读 takeover\pinned.json，与脚本写的同一份。</summary>
        private static string PinnedFilePath
        {
            get
            {
                try
                {
                    if (TakeoverActive()) { return Path.Combine(TakeoverDirPath, "pinned.json"); }
                }
                catch { }
                return Path.Combine(root, "pinned.json");
            }
        }
        private static string PinnedLegacyFilePath
        {
            get
            {
                try
                {
                    if (TakeoverActive()) { return Path.Combine(ActiveCacheDir, "pinned.json"); }
                }
                catch { }
                return Path.Combine(cacheDir, "pinned.json");
            }
        }

        /// <summary>读保护名单（新位置优先，旧版在 cache\ 下的继续兼容）。</summary>
        internal static Dictionary<string, object> ReadPinnedMap()
        {
            string src = PinnedFilePath;
            try
            {
                if (!File.Exists(src) && File.Exists(PinnedLegacyFilePath)) { src = PinnedLegacyFilePath; }
            }
            catch { }
            Dictionary<string, object> d = ReadJson(src);
            if (d == null) { d = new Dictionary<string, object>(); }
            return d;
        }

        /// <summary>写保护名单到程序根目录。</summary>
        internal static void WritePinnedMap(Dictionary<string, object> map)
        {
            try
            {
                if (map == null) { map = new Dictionary<string, object>(); }
                string json = new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(map);
                File.WriteAllText(PinnedFilePath, json, new UTF8Encoding(false));
            }
            catch { }
        }

        /// <summary>找某个日期的图，返回路径；三处都没有返回 null。</summary>
        internal static string FindImageFilePath(string date, out int where)
        {
            where = 0;   // 0=没有 1=收藏 2=缓存 3=归档 4=老存档
            string[] dirs = new string[] { ActiveFavDirPath, ActiveCacheDir, ActiveArchiveImgPath };
            for (int i = 0; i < dirs.Length; i++)
            {
                try
                {
                    if (string.IsNullOrEmpty(dirs[i]) || !Directory.Exists(dirs[i])) { continue; }
                    string[] hit = Directory.GetFiles(dirs[i], date + "_*.jpg");
                    if (hit.Length > 0)
                    {
                        where = i + 1;
                        return hit[0];
                    }
                }
                catch { }
            }
            // 照片墙临时目录：照片墙刚取到的图在这里，排在老存档之前
            try
            {
                if (!string.IsNullOrEmpty(WallDirPath) && Directory.Exists(WallDirPath))
                {
                    string[] hitWall = Directory.GetFiles(WallDirPath, date + "_*.jpg");
                    if (hitWall.Length > 0) { where = 5; return hitWall[0]; }
                }
            }
            catch { }

            // 老存档（2009-08 ~ 2016-02）按月分目录，单独一查
            try
            {
                string om = OldMonthDir(date);
                if (om != null && Directory.Exists(om))
                {
                    string[] hitOld = Directory.GetFiles(om, date + "_*.jpg");
                    if (hitOld.Length > 0) { where = 4; return hitOld[0]; }
                }
            }
            catch { }
            return null;
        }

        internal static string FindImageFilePath(string date)
        {
            int w;
            return FindImageFilePath(date, out w);
        }

        /// <summary>换过的图：名单条目数（含文件已不在的）与仍在磁盘上的合计体积（MB）。
        /// 条目数要和「换过的图」窗口底部的「共 N 项」同口径，否则菜单与窗口数字对不上。</summary>
        internal static void PinnedStat(out int count, out double mb)
        {
            count = 0;
            mb = 0;
            try
            {
                Dictionary<string, object> pins = ReadPinnedMap();
                count = pins.Keys.Count;
                foreach (string d in pins.Keys)
                {
                    int where;
                    string f = FindImageFilePath(d, out where);
                    if (f != null)
                    {
                        try { mb += new FileInfo(f).Length / 1024.0 / 1024.0; }
                        catch { }
                    }
                }
            }
            catch { }
        }

        /// <summary>收藏夹：张数与合计体积（MB）。</summary>
        internal static void FavStat(out int count, out double mb)
        {
            count = 0;
            mb = 0;
            string[] fs = SafeGetFiles(favDir);
            count = fs.Length;
            foreach (string f in fs)
            {
                try { mb += new FileInfo(f).Length / 1024.0 / 1024.0; }
                catch { }
            }
        }

        /// <summary>查某个日期的图在哪：收藏 / 缓存 / 归档 / 没有。回填体积（字节）。</summary>
        internal static string FindImageLocation(string date, out long bytes)
        {
            bytes = 0;
            int where;
            string file = FindImageFilePath(date, out where);
            if (file == null) { return S.CalLocNone; }
            try { bytes = new FileInfo(file).Length; } catch { }
            if (where == 1) { return S.CalLocFav; }
            if (where == 2) { return S.CalLocCache; }
            if (where == 4) { return S.CalLocOld; }
            return S.CalLocArch;
        }

        /// <summary>打开下载链接窗口（日期范围 yyyyMMdd）。</summary>
        internal static void DoExportLinks(string from, string to)
        {
            try
            {
                using (LinkForm f = new LinkForm(from, to))
                {
                    f.ShowDialog(anchor);
                }
            }
            catch { }
        }

        /// <summary>打开收藏管理窗口。</summary>
        private static void DoFavManager()
        {
            if (busy) { return; }
            using (FavForm f = new FavForm())
            {
                f.ShowDialog(anchor);
            }
            RefreshAll();
        }

        /// <summary>打开「换过的图」窗口。</summary>
        private static void DoPinnedList()
        {
            if (busy) { return; }
            using (PinnedForm f = new PinnedForm())
            {
                f.ShowDialog(anchor);
            }
            RefreshAll();
        }

        /// <summary>把所有 archive\*.json 的标题合并成 {date: {title}}，供壁纸库显示。</summary>
        // ---------------------------------------------------------------- 已发布日期索引
        //
        // all-index.json 覆盖 2016-03-05 起，old-index.json 覆盖 2009-07-13 ~ 2016-03-04。
        // 两个都是顶层数组，ReadJson 只认字典，所以单独写一个数组解析。
        // 用途：判断「官方这天到底有没有图」，别拿网络去试（实测试一轮要 20 秒）。

        private static HashSet<string> publishedSet;
        private static string[] publishedSorted;
        private static string publishedStamp;

        private static List<Dictionary<string, object>> ReadJsonArrayFile(string path)
        {
            List<Dictionary<string, object>> list = new List<Dictionary<string, object>>();
            try
            {
                if (!File.Exists(path)) { return list; }
                string txt = File.ReadAllText(path, Encoding.UTF8);
                JavaScriptSerializer js = new JavaScriptSerializer();
                js.MaxJsonLength = int.MaxValue;
                object[] arr = js.DeserializeObject(txt) as object[];
                if (arr == null) { return list; }
                foreach (object o in arr)
                {
                    Dictionary<string, object> d = o as Dictionary<string, object>;
                    if (d != null) { list.Add(d); }
                }
            }
            catch { }
            return list;
        }

        private static string PublishedStamp()
        {
            StringBuilder sb = new StringBuilder();
            string[] files = new string[] {
                Path.Combine(archiveDir, "all-index.json"),
                Path.Combine(archiveDir, "old-index.json") };
            foreach (string f in files)
            {
                try
                {
                    FileInfo fi = new FileInfo(f);
                    sb.Append(fi.Exists ? (fi.Length.ToString() + ":" + fi.LastWriteTimeUtc.Ticks.ToString()) : "-");
                }
                catch { sb.Append("-"); }
                sb.Append('|');
            }
            return sb.ToString();
        }

        private static void EnsurePublishedIndex()
        {
            string stamp = PublishedStamp();
            if (publishedSet != null && stamp == publishedStamp) { return; }

            HashSet<string> set = new HashSet<string>(StringComparer.Ordinal);
            string[] files = new string[] {
                Path.Combine(archiveDir, "all-index.json"),
                Path.Combine(archiveDir, "old-index.json") };
            foreach (string f in files)
            {
                foreach (Dictionary<string, object> d in ReadJsonArrayFile(f))
                {
                    string k = GetStr(d, "d").Replace("-", "").Trim();
                    if (k.Length == 8) { set.Add(k); }
                }
            }
            string[] sorted = new string[set.Count];
            set.CopyTo(sorted);
            Array.Sort(sorted, StringComparer.Ordinal);

            publishedSet = set;
            publishedSorted = sorted;
            publishedStamp = stamp;
        }

        /// <summary>官方索引里有没有这一天。索引读不到时返回 true（不拦截用户）。</summary>
        internal static bool IsDatePublished(string d8)
        {
            try
            {
                EnsurePublishedIndex();
                if (publishedSet == null || publishedSet.Count == 0) { return true; }
                string k = (d8 ?? "").Replace("-", "").Trim();
                if (k.Length != 8) { return true; }
                return publishedSet.Contains(k);
            }
            catch { return true; }
        }

        internal static string PublishedMinDate
        {
            get
            {
                try
                {
                    EnsurePublishedIndex();
                    if (publishedSorted != null && publishedSorted.Length > 0) { return publishedSorted[0]; }
                }
                catch { }
                return "";
            }
        }

        internal static string PublishedMaxDate
        {
            get
            {
                try
                {
                    EnsurePublishedIndex();
                    if (publishedSorted != null && publishedSorted.Length > 0)
                    {
                        return publishedSorted[publishedSorted.Length - 1];
                    }
                }
                catch { }
                return "";
            }
        }

        /// <summary>
        /// 索引里紧邻的一天：step&gt;0 取更早的里最新的，step&lt;0 取更新的里最旧的。
        /// 缺失日期不在集合里，所以天然被跳过（2010-04-15 → 更新 → 2010-04-18）。
        /// </summary>
        internal static string NeighborDate(string from8, int step)
        {
            try
            {
                EnsurePublishedIndex();
                if (publishedSorted == null || publishedSorted.Length == 0) { return ""; }
                string k = (from8 ?? "").Replace("-", "").Trim();
                if (k.Length != 8) { return ""; }

                int hit = Array.BinarySearch(publishedSorted, k, StringComparer.Ordinal);
                int lo = (hit >= 0) ? hit : ~hit;      // 第一个 >= k 的下标
                if (step > 0)
                {
                    int p = lo - 1;                    // 最后一个 < k
                    if (p >= 0) { return publishedSorted[p]; }
                    return "";
                }
                int q = (hit >= 0) ? hit + 1 : lo;     // 第一个 > k
                if (q >= 0 && q < publishedSorted.Length) { return publishedSorted[q]; }
                return "";
            }
            catch { return ""; }
        }

        /// <summary>四个索引文件的 大小+修改时间 指纹，任一变化就让缓存失效。</summary>
        private static string ArchiveIdxStamp()
        {
            StringBuilder sb = new StringBuilder();
            string[] files = new string[] {
                Path.Combine(archiveDir, "all-index.json"),
                Path.Combine(archiveDir, "old-index.json"),
                Path.Combine(cacheDir, "index.json"),
                Path.Combine(favDir, "index.json") };
            foreach (string f in files)
            {
                try
                {
                    FileInfo fi = new FileInfo(f);
                    sb.Append(fi.Exists ? (fi.Length.ToString() + ":" + fi.LastWriteTimeUtc.Ticks.ToString()) : "-");
                }
                catch { sb.Append("-"); }
                sb.Append('|');
            }
            return sb.ToString();
        }

        internal static Dictionary<string, object> LoadArchiveIndex()
        {
            // 菜单每打开一次就会问一次；四个 JSON 合起来约 1 MB，不缓存的话每次都要重新解析
            string stamp = ArchiveIdxStamp();
            if (archIdxCache != null && stamp == archIdxStamp) { return archIdxCache; }
            Dictionary<string, object> all = new Dictionary<string, object>();
            try
            {
                if (!Directory.Exists(archiveDir))
                {
                    archIdxCache = all; archIdxStamp = stamp;
                    return all;
                }
                string allIdxFile = Path.Combine(archiveDir, "all-index.json");
                foreach (string f in Directory.GetFiles(archiveDir, "*.json"))
                {
                    if (string.Equals(f, allIdxFile, StringComparison.OrdinalIgnoreCase)) { continue; }
                    try
                    {
                        Dictionary<string, object> m = ReadJson(f);
                        if (m == null) { continue; }
                        foreach (KeyValuePair<string, object> kv in m)
                        {
                            Dictionary<string, object> e = kv.Value as Dictionary<string, object>;
                            if (e == null) { continue; }
                            string tt = GetStr(e, "t");
                            string cc = GetStr(e, "c");
                            if (tt.Length == 0 && cc.Length == 0) { continue; }
                            Dictionary<string, object> wrap = new Dictionary<string, object>();
                            wrap["title"] = tt;
                            wrap["copyright"] = cc;
                            wrap["link"] = "";
                            all[kv.Key] = wrap;
                        }
                    }
                    catch { }
                }

                // 缓存与收藏的索引里也带 copyright，补空缺
                foreach (string extra in new string[] {
                             Path.Combine(cacheDir, "index.json"),
                             Path.Combine(favDir, "index.json") })
                {
                    try
                    {
                        Dictionary<string, object> mx = ReadJson(extra);
                        if (mx == null) { continue; }
                        foreach (KeyValuePair<string, object> kv in mx)
                        {
                            Dictionary<string, object> e = kv.Value as Dictionary<string, object>;
                            if (e == null) { continue; }
                            string tt = GetStr(e, "title");
                            string cc = GetStr(e, "copyright");
                            if (tt.Length == 0 && cc.Length == 0) { continue; }
                            Dictionary<string, object> wrap;
                            if (all.ContainsKey(kv.Key))
                            {
                                wrap = all[kv.Key] as Dictionary<string, object>;
                                if (wrap == null) { continue; }
                            }
                            else
                            {
                                wrap = new Dictionary<string, object>();
                                wrap["title"] = "";
                                wrap["copyright"] = "";
                                all[kv.Key] = wrap;
                            }
                            if (GetStr(wrap, "title").Length == 0 && tt.Length > 0) { wrap["title"] = tt; }
                            if (GetStr(wrap, "copyright").Length == 0 && cc.Length > 0) { wrap["copyright"] = cc; }
                        }
                    }
                    catch { }
                }

                // 老索引（2009-08 ~ 2016-02，来自第三方存档），补空白日期
                try
                {
                    string oldFile = Path.Combine(archiveDir, "old-index.json");
                    if (File.Exists(oldFile))
                    {
                        object rawOld = new System.Web.Script.Serialization.JavaScriptSerializer()
                            .DeserializeObject(File.ReadAllText(oldFile, Encoding.UTF8));
                        object[] arrOld = rawOld as object[];
                        if (arrOld != null)
                        {
                            foreach (object o in arrOld)
                            {
                                Dictionary<string, object> e = o as Dictionary<string, object>;
                                if (e == null) { continue; }
                                string d8 = e.ContainsKey("d") ? Convert.ToString(e["d"]) : "";
                                string t8 = e.ContainsKey("t") ? Convert.ToString(e["t"]) : "";
                                if (d8.Length != 8 || t8.Length == 0) { continue; }
                                if (all.ContainsKey(d8)) { continue; }
                                Dictionary<string, object> wrap = new Dictionary<string, object>();
                                wrap["title"] = t8;
                                wrap["copyright"] = "";
                                wrap["link"] = "";
                                all[d8] = wrap;
                            }
                        }
                    }
                }
                catch { }

                // 全量索引兜底：数组格式（d/t），只补月份索引里没有的日期
                try
                {
                    if (File.Exists(allIdxFile))
                    {
                        object raw = new System.Web.Script.Serialization.JavaScriptSerializer()
                            .DeserializeObject(File.ReadAllText(allIdxFile, Encoding.UTF8));
                        object[] arr = raw as object[];
                        if (arr != null)
                        {
                            foreach (object o in arr)
                            {
                                Dictionary<string, object> e = o as Dictionary<string, object>;
                                if (e == null) { continue; }
                                string d8 = e.ContainsKey("d") ? Convert.ToString(e["d"]) : "";
                                string t8 = e.ContainsKey("t") ? Convert.ToString(e["t"]) : "";
                                string c8 = e.ContainsKey("c") ? Convert.ToString(e["c"]) : "";
                                string l8 = e.ContainsKey("l") ? Convert.ToString(e["l"]) : "";
                                if (d8.Length != 8 || (t8.Length == 0 && c8.Length == 0)) { continue; }
                                if (all.ContainsKey(d8)) { continue; }
                                Dictionary<string, object> wrap = new Dictionary<string, object>();
                                wrap["title"] = t8;
                                wrap["copyright"] = c8;
                                wrap["link"] = l8;
                                all[d8] = wrap;
                            }
                        }
                    }
                }
                catch { }
            }
            catch { }
            archIdxCache = all;
            archIdxStamp = stamp;
            return all;
        }

        private static void BuildLibPage(ToolStripMenuItem parent, string[] files, Dictionary<string, object> idx,
                                         string currentDate, bool isFavSection, int offset, int depth)
        {
            const int PageSize = 12;
            const int MaxDepth = 4;

            int added = 0;
            for (int i = offset; i < files.Length && added < PageSize; i++, added++)
            {
                string full = files[i];
                string fileName = Path.GetFileName(full);
                string name = Path.GetFileNameWithoutExtension(full);
                int us = name.IndexOf('_');
                string fileDate = us > 0 ? name.Substring(0, us) : name;

                // 标题查不到时只显示日期 +（无标题），不再回落到 20261105_OHR.StileCrossing 这种英文文件名
                string t = "";
                if (idx != null && idx.ContainsKey(fileDate))
                {
                    t = GetStr(idx[fileDate] as Dictionary<string, object>, "title");
                }
                // 收藏/缓存索引里 title 为空时，回落到全量索引与老索引（20100720 就是这种情况）
                if (t.Length == 0) { t = ArchiveTitleOf(LoadArchiveIndex(), fileDate); }
                string label = fileDate + "  " + (t.Length > 0 ? t : S.NoTitle);

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

        internal static bool IsCurrentFavorited()
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

        /// <summary>
        /// 重注册计划任务。改时间必须重建 —— EnsureRegistered 只在「指向不对」时才动，
        /// 光改 config 它不会刷新触发时刻（旧版点「更新时间」看着成功、实际没生效就栽在这）。
        /// </summary>
        private static bool ReregisterTask()
        {
            try { RunSchtasks("/delete /tn \"" + TaskName + "\" /f"); } catch { }
            try { RegisterTask(); } catch { return false; }
            try { return IsTaskPointingHere(); } catch { return false; }
        }

        /// <summary>读 schtasks 的「下次运行时间」；读不到返回空串。</summary>
        private static string NextRunTimeText()
        {
            try
            {
                int rc = 0;
                string info = RunSchtasksOutput("/query /tn \"" + TaskName + "\" /fo LIST /v", out rc);
                foreach (string line in info.Split('\n'))
                {
                    if (line.IndexOf("下次运行时间", StringComparison.Ordinal) >= 0 ||
                        line.IndexOf("Next Run Time", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        int colon = line.IndexOf(':');
                        if (colon > 0)
                        {
                            string v = line.Substring(colon + 1).Trim();
                            if (v.Length > 0 && v != "N/A") { return v; }
                        }
                    }
                }
            }
            catch { }
            return "";
        }

        private static int HmToMinutes(string hm)
        {
            try
            {
                if (string.IsNullOrEmpty(hm)) { return -1; }
                string[] p = hm.Trim().Split(':');
                if (p.Length < 2) { return -1; }
                int hh = Convert.ToInt32(p[0]);
                int mm = Convert.ToInt32(p[1]);
                if (hh < 0 || hh > 23 || mm < 0 || mm > 59) { return -1; }
                return hh * 60 + mm;
            }
            catch { return -1; }
        }

        /// <summary>改每日换图时间：落盘 → 强制重注册 → 报下次时刻。</summary>
        internal static void ApplyUpdateTime(string hm)
        {
            if (HmToMinutes(hm) < 0) { return; }
            try
            {
                Dictionary<string, object> c = ReadConfig();
                SetCfg(c, "updateTime", hm);
                WriteConfig(c);
            }
            catch { }

            bool ok = ReregisterTask();
            try { RefreshAll(); } catch { }

            if (!ok)
            {
                Balloon(S.BRepairKO, S.BRepairKOHint);
                return;
            }
            string next = NextRunTimeText();
            Balloon(S.TaskTimeChanged(hm), next.Length > 0 ? S.TaskNextShort(next) : S.TaskDoneShort);
        }

        /// <summary>点「立即重新注册任务」：只重注册，绝不替用户解除暂停、也不擅改开机自启。</summary>
        private static void DoRepairTask()
        {
            SetBusy(true);
            try
            {
                bool ok = ReregisterTask();
                bool locked = false;
                try { locked = GetCfgBool(ReadConfig(), "locked", false); } catch { }

                anchor.BeginInvoke((MethodInvoker)delegate
                {
                    try { SetBusy(false); } catch { }
                    try { RefreshAll(); } catch { }
                    if (!ok) { Balloon(S.BRepairKO, S.BRepairKOHint); return; }
                    Balloon(S.TaskDoneShort, locked ? S.TaskDoneLocked : S.BRepairedHint);
                });
            }
            catch
            {
                SetBusy(false);
            }
        }

        private static void RefreshTaskState()
        {
            ToolStripMenuItem top = menu.Items["taskstate"] as ToolStripMenuItem;
            if (top == null) { return; }

            ToolStripMenuItem info = top.DropDownItems["taskinfo"] as ToolStripMenuItem;
            ToolStripMenuItem auto = top.DropDownItems["taskauto"] as ToolStripMenuItem;

            bool locked = false;
            try { locked = GetCfgBool(ReadConfig(), "locked", false); } catch { }

            try
            {
                int rc = 0;
                string xml = RunSchtasksOutput("/query /tn \"" + TaskName + "\" /xml ONE", out rc);
                if (rc != 0 || string.IsNullOrEmpty(xml) || xml.IndexOf("<Task", StringComparison.Ordinal) < 0)
                {
                    top.Text = S.TaskMissing;
                    if (info != null) { info.Text = S.TaskMissing; }
                }
                else
                {
                    string next = NextRunTimeText();
                    string head = next.Length > 0 ? S.TaskNextAt(next) : S.TaskNormal;
                    if (info != null) { info.Text = head; }
                    top.Text = locked ? (head + S.TaskPausedSuffix) : head;
                }
            }
            catch
            {
                top.Text = S.TaskUnknown;
                if (info != null) { info.Text = S.TaskUnknown; }
            }

            if (auto != null) { auto.Text = locked ? S.TaskAutoOff : S.TaskAutoOn; }
        }

        /// <summary>勾出当前生效的每日时间（文本前缀风格，与「外观」一致）。</summary>
        private static void RefreshTaskTimeChecks()
        {
            ToolStripMenuItem top = menu.Items["taskstate"] as ToolStripMenuItem;
            if (top == null) { return; }
            int cur = GetUpdateMinutes();
            foreach (ToolStripItem raw in top.DropDownItems)
            {
                ToolStripMenuItem it = raw as ToolStripMenuItem;
                if (it == null || it.Tag == null) { continue; }
                string tag = it.Tag.ToString();
                it.Text = (HmToMinutes(tag) == cur ? "● " : "　 ") + S.TaskDaily(tag);
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

        /// <summary>「收藏管理」「换过的图」两项后面带上张数与体积，让用户一眼看到规模。</summary>
        private static void RefreshEntryCheck()
        {
            try
            {
                ToolStripMenuItem fm = menu.Items["favmgr"] as ToolStripMenuItem;
                if (fm != null)
                {
                    int fn; double fmb;
                    FavStat(out fn, out fmb);
                    fm.Text = S.FavManagerN(fn, fmb);
                }
            }
            catch { }

            try
            {
                ToolStripMenuItem pm = menu.Items["pinmgr"] as ToolStripMenuItem;
                if (pm != null)
                {
                    int pn; double pmb;
                    PinnedStat(out pn, out pmb);
                    pm.Text = S.PinMenuN(pn, pmb);
                }
            }
            catch { }
        }

        /// <summary>随机范围配置值；非法值回落 local。</summary>
        internal static string RandomScopeValue()
        {
            try
            {
                string v = GetCfgStr(ReadConfig(), "randomScope", "local");
                if (v == "fav" || v == "local" || v == "any") { return v; }
            }
            catch { }
            return "local";
        }

        private static void CollectDates(HashSet<string> set, string dir, bool recurse)
        {
            try
            {
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) { return; }
                string[] fs = Directory.GetFiles(dir, "*.jpg", recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
                foreach (string f in fs)
                {
                    string n = Path.GetFileName(f);
                    if (n.Length >= 8) { set.Add(n.Substring(0, 8)); }
                }
            }
            catch { }
        }

        /// <summary>本地随机池的日期数：收藏 + 缓存 + 归档 + 老存档，按日期去重（与脚本口径一致）。</summary>
        internal static int LocalPoolCount()
        {
            HashSet<string> set = new HashSet<string>();
            CollectDates(set, favDir, false);
            CollectDates(set, cacheDir, false);
            CollectDates(set, archiveImgDir, false);
            CollectDates(set, OldDirPath, true);
            return set.Count;
        }

        /// <summary>收藏里的日期数（收藏档的候选数）。</summary>
        internal static int FavDateCount()
        {
            HashSet<string> set = new HashSet<string>();
            CollectDates(set, favDir, false);
            return set.Count;
        }

        /// <summary>选中某个随机范围：先落盘，再让脚本按这个范围换一张。</summary>
        internal static void DoRandomPick(string scope)
        {
            try
            {
                Dictionary<string, object> c = ReadConfig();
                SetCfg(c, "randomScope", scope);
                WriteConfig(c);
            }
            catch { }
            string extra = "-Random -RandomScope " + scope;
            RunScriptEx(extra, "random");
            try { RefreshRandomCheck(GateNow); } catch { }
        }

        private static void RefreshRandomCheck(MenuGate g)
        {
            ToolStripMenuItem it = menu.Items["randompick"] as ToolStripMenuItem;
            if (it == null) { return; }

            int favN = g.FavN;
            int localN = g.LocalN;
            string scope = RandomScopeValue();

            // Enabled 由 ApplyMenuState 按 gate.LocalN 推导，这里只写文字
            if (localN <= 0)
            {
                it.Text = S.RandomTitle("", 0);
            }
            else
            {
                if (scope == "any") { it.Text = S.RandomTitleAny; }
                else if (scope == "fav") { it.Text = S.RandomTitle(S.RandomScopeFavLabel, favN); }
                else { it.Text = S.RandomTitle(S.RandomScopeLocalLabel, localN); }
            }

            SetRandSub(it, "randfav", S.RandomScopeFav(favN), scope == "fav", favN > 0);
            SetRandSub(it, "randlocal", S.RandomScopeLocal(localN), scope == "local", localN > 0);
            SetRandSub(it, "randany", S.RandomScopeAny, scope == "any", true);
        }

        private static void SetRandSub(ToolStripMenuItem parent, string name, string text, bool isCurrent, bool enabled)
        {
            if (parent == null) { return; }
            foreach (ToolStripItem x in parent.DropDownItems)
            {
                if (x.Name != name) { continue; }
                x.Text = text;
                x.Enabled = enabled;
                ToolStripMenuItem mi = x as ToolStripMenuItem;
                if (mi != null) { mi.Checked = isCurrent; }
                return;
            }
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
                // 与「实际清理」口径一致：老存档也算归档的一部分
                try
                {
                    string oldR2 = OldDirPath;
                    if (!string.IsNullOrEmpty(oldR2) && Directory.Exists(oldR2))
                    {
                        foreach (string om2 in Directory.GetDirectories(oldR2))
                        {
                            n += Directory.GetFiles(om2, "*.jpg").Length;
                        }
                    }
                }
                catch { }
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
            int earliestYear = Program.ArchiveMinDateValue.Year;
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
            Dictionary<string, object> archiveIdx = null;
            try { archiveIdx = LoadArchiveIndex(); } catch { }

            int shown = 0;
            for (int y = lastYear; y >= earliestYear; y--)
            {
                DateTime dt;
                try { dt = new DateTime(y, today.Month, today.Day); }
                catch { continue; }        // 2/29 这类不存在的日期
                if (dt < Program.ArchiveMinDateValue) { continue; }

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
                    if (hit.Length == 0)
                    {
                        string omDir = OldMonthDir(key);
                        if (omDir != null && Directory.Exists(omDir))
                        {
                            hit = Directory.GetFiles(omDir, key + "_*.jpg");
                        }
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
                    if (ttl.Length == 0) { ttl = ArchiveTitleOf(archiveIdx, key); }
                    label = label + "  " + (ttl.Length > 0 ? ttl : S.NoTitle);
                }
                else
                {
                    // 标题在本地索引里（all-index 覆盖 2016-03 起、old-index 覆盖 2009-07 起），
                    // 没下载过也先把名字显示出来；能不能点由 Enabled 表达，不再拼后缀字
                    string ttl = ArchiveTitleOf(archiveIdx, key);
                    label = label + "  " + (ttl.Length > 0 ? ttl : S.NoTitle);
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

            ToolStripMenuItem wallItem = new ToolStripMenuItem(S.WallMenu(shown));
            wallItem.Click += delegate
            {
                keepMenuOpenAfterClick = true;
                anchor.BeginInvoke((MethodInvoker)delegate { DoPhotoWall(); });
            };
            ym.DropDownItems.Insert(0, wallItem);
            ym.DropDownItems.Add(new ToolStripSeparator());
            ym.DropDownItems.Add(MakeItem(S.OpenArchiveImg, delegate { OpenTarget(archiveImgDir); }));
        }

        private static Dictionary<string, object> archIdxCache;
        private static string archIdxStamp;

        /// <summary>让归档索引缓存立即失效（索引文件被外部改写后手动调用）。</summary>
        internal static void InvalidateArchiveIndex()
        {
            archIdxCache = null;
            archIdxStamp = null;
        }

        /// <summary>归档索引的合并标题，key 为 8 位日期；不带就返回空串。</summary>
        private static string ArchiveTitleOf(Dictionary<string, object> idx, string key)
        {
            try
            {
                if (idx == null || string.IsNullOrEmpty(key) || !idx.ContainsKey(key)) { return ""; }
                string t = GetStr(idx[key] as Dictionary<string, object>, "title");
                if (t.Length > 50) { t = t.Substring(0, 50) + "…"; }
                return t;
            }
            catch { return ""; }
        }

        private static int cachedIdxCount = -1;
        private static long cachedIdxSize = -1;

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
                    // 归档图片总量与其中受保护的数量
                    int imgN = 0;
                    long imgBytes = 0;
                    int protN = 0;
                    try
                    {
                        Dictionary<string, object> pins = ReadPinnedMap();
                        if (Directory.Exists(ArchiveImgPath))
                        {
                            foreach (string img in Directory.GetFiles(ArchiveImgPath, "*.jpg"))
                            {
                                imgN++;
                                imgBytes += new FileInfo(img).Length;
                                string fn = Path.GetFileName(img);
                                if (fn.Length >= 8 && pins.ContainsKey(fn.Substring(0, 8))) { protN++; }
                            }
                        }
                        // 老存档（archive\old\YYYYMM）算作归档的一部分，不单列
                        string oldRoot2 = OldDirPath;
                        if (Directory.Exists(oldRoot2))
                        {
                            foreach (string om in Directory.GetDirectories(oldRoot2))
                            {
                                foreach (string img in Directory.GetFiles(om, "*.jpg"))
                                {
                                    imgN++;
                                    imgBytes += new FileInfo(img).Length;
                                    string fn = Path.GetFileName(img);
                                    if (fn.Length >= 8 && pins.ContainsKey(fn.Substring(0, 8))) { protN++; }
                                }
                            }
                        }
                    }
                    catch { }

                    int capMB = 500;
                    try { capMB = GetCfgInt(c, "archiveMaxMB", 500); } catch { }
                    x.Text = S.ArchRowArchive(imgN, Math.Round(imgBytes / 1024.0 / 1024.0, 1), capMB, protN);
                }
                else if (x.Name == "archidxinfo")
                {
                    int idxMonths = 0;
                    long idxBytes = 0;
                    int totalIdx = 0;
                    try
                    {
                        if (Directory.Exists(archiveDir))
                        {
                            foreach (string f in Directory.GetFiles(archiveDir, "*.json"))
                            {
                                string fn = Path.GetFileName(f);
                                if (System.Text.RegularExpressions.Regex.IsMatch(fn, @"^\d{4}-\d{2}\.json$"))
                                {
                                    idxMonths++;
                                    idxBytes += new FileInfo(f).Length;
                                }
                            }
                        }

                        string fAll = Path.Combine(archiveDir, "all-index.json");
                        if (File.Exists(fAll))
                        {
                            FileInfo fiAll = new FileInfo(fAll);
                            idxBytes += fiAll.Length;
                            if (fiAll.Length == cachedIdxSize && cachedIdxCount >= 0) { totalIdx = cachedIdxCount; }
                            else
                            {
                                object rawIdx = new System.Web.Script.Serialization.JavaScriptSerializer()
                                    .DeserializeObject(File.ReadAllText(fAll, Encoding.UTF8));
                                object[] arrIdx = rawIdx as object[];
                                if (arrIdx != null) { totalIdx = arrIdx.Length; }
                                cachedIdxCount = totalIdx;
                                cachedIdxSize = fiAll.Length;
                            }
                        }
                    }
                    catch { }
                    x.Text = S.ArchRowIndex(idxMonths, Math.Round(idxBytes / 1024.0, 0), totalIdx);
                }
                else if (x.Name == "archsrc1" || x.Name == "archsrc2" || x.Name == "archsrc3")
                {
                    // 关掉增强数据源时，把来源行灰掉但仍保留信息
                    x.Enabled = false;
                    x.ForeColor = on ? SystemColors.GrayText : SystemColors.ControlDarkDark;
                    if (!on && x.Name == "archsrc2") { x.Text = S.ArchInfoPrefix + S.ArchOffLine; }
                }
                else if (x.Name == "archoldinfo")
                {
                    int oldN = 0;
                    long oldBytes = 0;
                    try
                    {
                        string oldRoot = OldDirPath;
                        if (Directory.Exists(oldRoot))
                        {
                            foreach (string om in Directory.GetDirectories(oldRoot))
                            {
                                foreach (string img in Directory.GetFiles(om, "*.jpg"))
                                {
                                    oldN++;
                                    oldBytes += new FileInfo(img).Length;
                                }
                            }
                        }
                    }
                    catch { }
                    x.Text = S.ArchRowOld(oldN, Math.Round(oldBytes / 1024.0 / 1024.0, 1));
                }
            }

            // 画廊模式状态行
            ToolStripMenuItem gt = menu.Items["gallerytoggle"] as ToolStripMenuItem;
            if (gt != null)
            {
                if (GalleryMode)
                {
                    int gn = 0;
                    long gb = 0;
                    try
                    {
                        string gd = GalleryDirPath;
                        if (Directory.Exists(gd))
                        {
                            foreach (string f in Directory.GetFiles(gd, "*.jpg"))
                            {
                                gn++;
                                gb += new FileInfo(f).Length;
                            }
                        }
                    }
                    catch { }
                    gt.Text = S.GalleryOn + S.GalleryCount(gn, Math.Round(gb / 1024.0 / 1024.0, 1));
                    gt.Font = new Font(gt.Font, FontStyle.Bold);
                }
                else
                {
                    gt.Text = S.GalleryMenu;
                    gt.Font = new Font(gt.Font, FontStyle.Regular);
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
            busySince = on ? DateTime.Now : DateTime.MinValue;

            // 唯一的可用性出口：不再逐项遍历 menu.Items。
            // 老写法按「菜单是否可见」逐项判定 Enabled，菜单关闭时会把整张菜单设成禁用，
            // 而 Opening 只刷文字不重算 → 一次竞态就永久灰（关不掉画廊）。
            try { ApplyMenuState(); } catch { }

            if (tray == null) { return; }

            if (changed)
            {
                try
                {
                    tray.Icon = on ? iconBusy : iconNormal;
                }
                catch { }
            }

            if (on) { tray.Text = Clip(AppName + S.WorkingDots, 60); }
        }
    }
}
