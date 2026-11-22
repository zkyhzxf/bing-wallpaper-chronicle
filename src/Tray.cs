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
        // ---------------------------------------------------------------- 托盘点击

        /// <summary>
        /// 鼠标按下时刷新菜单内容。此时 ContextMenuStrip 还没弹出，
        /// 修改 Items 不会干扰系统的弹出流程，也不会影响"点外部关闭"。
        /// </summary>
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private static System.Windows.Forms.Timer menuWatch;

        // 菜单空闲兜底：鼠标连续 N 分钟不动就自动收起
        private const int MenuIdleMinutes = 3;
        private static DateTime menuIdleSince = DateTime.MinValue;
        private static Point menuIdlePos = Point.Empty;

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
                menuIdleSince = DateTime.Now;
                menuIdlePos = Cursor.Position;
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

                // 空闲兜底：鼠标连续 MenuIdleMinutes 分钟不动就关掉菜单
                Point now = Cursor.Position;
                if (now != menuIdlePos)
                {
                    menuIdlePos = now;
                    menuIdleSince = DateTime.Now;
                }
                else if (menuIdleSince != DateTime.MinValue &&
                         (DateTime.Now - menuIdleSince).TotalMinutes >= MenuIdleMinutes)
                {
                    menu.Close();
                    return;
                }

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
                RunScriptEx("-CleanAll -CleanMode " + mode, "cleanall");
            }
        }

        private static void ConfirmCleanArchive()
        {
            if (busy) { return; }
            using (CleanForm f = new CleanForm(CleanForm.Target.Archive))
            {
                if (f.ShowDialog(anchor) != DialogResult.OK) { return; }
                string mode = f.Permanent ? "permanent" : "recycle";
                string extra = f.WithIndex ? " -WithIndex" : "";
                if (f.WithOld) { extra += " -IncludeOld"; }
                RunScriptEx("-CleanArchive -CleanMode " + mode + extra, "cleanarch");
            }
        }

        private static void ConfirmCleanStories()
        {
            if (busy) { return; }
            try
            {
                string d = StoriesDirPath;
                int n = 0;
                if (Directory.Exists(d))
                {
                    n = Directory.GetFiles(d, "*.txt").Length;
                }
                if (n == 0) { return; }
                if (MessageBox.Show(S.CleanStoriesN.Replace("{0}", n.ToString()) + "\r\n\r\n" + S.CleanStoriesHint,
                        AppName, MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
                {
                    return;
                }
            }
            catch { return; }
            RunScriptEx("-CleanStories", "cleanstories");
        }

        private static void ConfirmCleanArchiveIndex()
        {
            if (busy) { return; }
            using (CleanForm f = new CleanForm(CleanForm.Target.ArchiveIndex))
            {
                if (f.ShowDialog(anchor) != DialogResult.OK) { return; }
                RunScriptEx("-CleanArchiveIndex", "cleanidx");
            }
        }

        private static void ConfirmCleanFavorites()
        {
            if (busy) { return; }
            using (CleanForm f = new CleanForm(CleanForm.Target.Favorites))
            {
                if (f.ShowDialog(anchor) != DialogResult.OK) { return; }
                string mode = f.Permanent ? "permanent" : "recycle";
                RunScriptEx("-CleanFavorites -CleanMode " + mode, "cleanfav");
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
    }
}
