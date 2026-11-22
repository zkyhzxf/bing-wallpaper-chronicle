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

            // 模式标牌：只有接管时出现，直接告诉用户图片是从哪个目录读的
            ToolStripMenuItem modeState = new ToolStripMenuItem(S.ModeBadge(""));
            modeState.Name = "modestate";
            modeState.Enabled = false;
            modeState.Visible = false;
            m.Items.Add(modeState);

            // 顺序：状态 -> 模式 -> 查看 -> 移动
            ToolStripMenuItem galleryToggle = new ToolStripMenuItem(S.GalleryMenu);
            galleryToggle.Name = "gallerytoggle";
            galleryToggle.CheckOnClick = false;
            galleryToggle.ToolTipText = S.GalleryTipOn;
            galleryToggle.Click += delegate
            {
                keepMenuOpenAfterClick = true;
                bool turnOn = !GalleryMode;
                GalleryMode = turnOn;
                // 开、关都清一次：开时清上次的残留，关时清本次看过的
                // 走独立通道：下载中点关闭也要生效（RunScriptEx 会被 busy 挡掉）
                // 手动切换才发气泡；程序启动与退出时的清理传 null，保持安静
                anchor.BeginInvoke((MethodInvoker)delegate
                {
                    RunGalleryClear(delegate(int n, double mb)
                    {
                        try
                        {
                            if (turnOn) { Balloon(S.GalleryBalloonOn, S.GalleryBalloonOnHint); }
                            else { Balloon(S.GalleryBalloonOff(n, mb), S.GalleryBalloonOffHint); }
                        }
                        catch { }
                    });
                });
            };
            m.Items.Add(galleryToggle);

            m.Items.Add(MakeItem(S.WhereIsThis, delegate { ShowInfo(); }));

            m.Items.Add(MakeKeepOpenItem(S.PrevOlder, "-Step 1", "prev"));
            m.Items.Add(MakeKeepOpenItem(S.NextNewer, "-Step -1", "next"));

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

            ToolStripMenuItem favMgr = new ToolStripMenuItem(S.FavManager);
            favMgr.Name = "favmgr";
            favMgr.Click += delegate
            {
                keepMenuOpenAfterClick = true;
                anchor.BeginInvoke((MethodInvoker)delegate { DoFavManager(); });
            };
            m.Items.Add(favMgr);

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
            m.Items.Add(MakeItem(S.SearchMenu, delegate { DoSearch(); }));
            m.Items.Add(MakeItem(S.AiMenu, delegate { DoAi(); }));




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
            ToolStripMenuItem pinMgrItem = MakeItem(S.PinMenu, delegate { DoPinnedList(); });
            pinMgrItem.Name = "pinmgr";
            cleanMenu.DropDownItems.Add(pinMgrItem);
            cleanMenu.DropDownItems.Add(new ToolStripSeparator());

            // 缓存
            cleanMenu.DropDownItems.Add(MakeItem(S.Clean7, delegate { RunScriptEx("-Clean -Keep 7", "clean"); }));
            cleanMenu.DropDownItems.Add(MakeItem(S.Clean30, delegate { RunScriptEx("-Clean -Keep 30", "clean"); }));
            cleanMenu.DropDownItems.Add(MakeItem(S.CleanAll, delegate { ConfirmCleanAll(); }));

            // 归档
            cleanMenu.DropDownItems.Add(new ToolStripSeparator());
            ToolStripMenuItem cleanArch = new ToolStripMenuItem(S.CleanArchive);
            cleanArch.Name = "cleanarch";
            cleanArch.Click += delegate
            {
                keepMenuOpenAfterClick = true;
                anchor.BeginInvoke((MethodInvoker)delegate { ConfirmCleanArchive(); });
            };
            cleanMenu.DropDownItems.Add(cleanArch);


            // 收藏
            ToolStripMenuItem cleanStories = new ToolStripMenuItem(S.CleanStories);
            cleanStories.Name = "cleanstories";
            cleanStories.Click += delegate
            {
                keepMenuOpenAfterClick = true;
                anchor.BeginInvoke((MethodInvoker)delegate { ConfirmCleanStories(); });
            };
            cleanMenu.DropDownItems.Add(cleanStories);

            cleanMenu.DropDownItems.Add(new ToolStripSeparator());
            cleanMenu.DropDownItems.Add(MakeItem(S.CleanFavMenu, delegate { ConfirmCleanFavorites(); }));

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

            ToolStripMenuItem archIdxInfo = new ToolStripMenuItem("…");
            archIdxInfo.Name = "archidxinfo";
            archIdxInfo.Enabled = false;
            archiveMenu.DropDownItems.Add(archIdxInfo);


            archiveMenu.DropDownItems.Add(new ToolStripSeparator());

            ToolStripMenuItem archOpen = new ToolStripMenuItem(S.OpenArchiveDir);
            archOpen.Click += delegate { OpenTarget(archiveDir); };
            archiveMenu.DropDownItems.Add(archOpen);

            m.Items.Add(archiveMenu);

            ToolStripMenuItem themeMenu = new ToolStripMenuItem(S.ThemeMenu);
            themeMenu.Name = "theme";
            themeMenu.DropDownItems.Add(MakeThemeItem(S.ThemeAuto, "auto"));
            themeMenu.DropDownItems.Add(MakeThemeItem(S.ThemeLight, "light"));
            themeMenu.DropDownItems.Add(MakeThemeItem(S.ThemeDark, "dark"));
            m.Items.Add(themeMenu);

            ToolStripMenuItem sysMenu = new ToolStripMenuItem(S.SysMenu);

            sysMenu.DropDownItems.Add(MakeItem(S.HelpItem, delegate { ShowHelp(); }));
            sysMenu.DropDownItems.Add(new ToolStripSeparator());
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
    }
}
