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

        /// <summary>照片墙：把历年同一天的图一次摆出来。</summary>
        private static void DoPhotoWall()
        {
            if (wallForm != null && !wallForm.IsDisposed) { wallForm.Activate(); return; }
            wallForm = new PhotoWallForm();
            wallForm.FormClosed += delegate { wallForm = null; };
            wallForm.Show();
        }
        private static CalendarForm calForm;

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
                if (string.IsNullOrEmpty(dest)) { return; }

                SetBusy(true);
                anchor.BeginInvoke((MethodInvoker)delegate
                {
                    string outText = "";
                    try
                    {
                        outText = RunScriptCapture("-ExportRange -RangeFrom " + from +
                            " -RangeTo " + to + " -DestDir \"" + dest + "\"");
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
            lib.DropDownItems.Add(MakeItem(S.OpenArchiveDir, delegate { OpenTarget(archiveImgDir); }));
            lib.DropDownItems.Add(MakeItem(S.ReloadIndex, delegate { InvalidateArchiveIndex(); RefreshAll(); }));
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

        private static string PinnedFilePath { get { return Path.Combine(root, "pinned.json"); } }
        private static string PinnedLegacyFilePath { get { return Path.Combine(cacheDir, "pinned.json"); } }

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
            string[] dirs = new string[] { FavDirPath, CacheDirPath, ArchiveImgPath };
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

        /// <summary>保留清单里那些图合计占多少 MB。</summary>
        internal static double PinnedTotalMB()
        {
            double mb = 0;
            try
            {
                Dictionary<string, object> pins = ReadPinnedMap();
                foreach (string d in pins.Keys)
                {
                    int where;
                    string f = FindImageFilePath(d, out where);
                    if (f != null) { try { mb += new FileInfo(f).Length / 1024.0 / 1024.0; } catch { } }
                }
            }
            catch { }
            return mb;
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

        /// <summary>打开「自动清理保留」窗口。</summary>
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

                // 标题查不到时只显示日期 +（无标题），不再回落到 20261105_OHR.StileCrossing 这种英文文件名
                string t = "";
                if (idx != null && idx.ContainsKey(fileDate))
                {
                    t = GetStr(idx[fileDate] as Dictionary<string, object>, "title");
                }
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
            ToolStripMenuItem cs = null;
            foreach (ToolStripItem x2 in cm.DropDownItems)
            {
                if (x2.Name == "cleanstories") { cs = x2 as ToolStripMenuItem; break; }
            }
            if (cs != null)
            {
                int sn = 0;
                try
                {
                    string sd = StoriesDirPath;
                    if (!string.IsNullOrEmpty(sd) && Directory.Exists(sd)) { sn = Directory.GetFiles(sd, "*.txt").Length; }
                }
                catch { }
                cs.Text = sn > 0 ? string.Format(S.CleanStoriesN, sn) : S.CleanStories;
                cs.Enabled = sn > 0;
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

            // 保留清单那项后面带上占用的体积，让用户知道有多大
            try
            {
                foreach (ToolStripItem x in cm.DropDownItems)
                {
                    if (x.Name == "pinmgr")
                    {
                        x.Text = S.PinMenuMB(PinnedTotalMB());
                        break;
                    }
                }
            }
            catch { }
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
    }
}
