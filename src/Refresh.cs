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
