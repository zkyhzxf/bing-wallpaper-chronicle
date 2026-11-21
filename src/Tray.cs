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
    }
}
