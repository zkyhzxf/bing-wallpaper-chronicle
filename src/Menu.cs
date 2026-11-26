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
            // 计划任务：容器，不是动作。状态行一点就删任务重注册、还强制开自启，
            // 这是误触陷阱；「修复」与「改更新时间」都做成明确的子项。
            ToolStripMenuItem taskState = new ToolStripMenuItem(S.TaskChecking);
            taskState.Name = "taskstate";

            ToolStripMenuItem taskInfo = new ToolStripMenuItem(S.TaskNormal);
            taskInfo.Name = "taskinfo";
            taskInfo.Enabled = false;
            taskState.DropDownItems.Add(taskInfo);

            ToolStripMenuItem taskAuto = new ToolStripMenuItem(S.TaskAutoOn);
            taskAuto.Name = "taskauto";
            taskAuto.Enabled = false;
            taskState.DropDownItems.Add(taskAuto);

            taskState.DropDownItems.Add(new ToolStripSeparator());

            int timeIdx = 0;
            foreach (string preset in new string[] { "00:10", "06:00", "08:00", "12:00", "18:00" })
            {
                ToolStripMenuItem it = new ToolStripMenuItem("　" + S.TaskDaily(preset));
                it.Name = "tt" + timeIdx;
                it.Tag = preset;
                it.Click += delegate
                {
                    keepMenuOpenAfterClick = true;
                    ApplyUpdateTime(preset);
                };
                taskState.DropDownItems.Add(it);
                timeIdx++;
            }

            ToolStripMenuItem customTime = new ToolStripMenuItem(S.TaskTimeCustom);
            customTime.Name = "ttcustom";
            customTime.Click += delegate
            {
                keepMenuOpenAfterClick = true;
                string hm;
                using (TimeForm f = new TimeForm(GetUpdateMinutes()))
                {
                    if (f.ShowDialog(anchor) != DialogResult.OK) { return; }
                    hm = f.SelectedHm;
                }
                if (!string.IsNullOrEmpty(hm)) { ApplyUpdateTime(hm); }
            };
            taskState.DropDownItems.Add(customTime);

            taskState.DropDownItems.Add(new ToolStripSeparator());

            ToolStripMenuItem reregTask = new ToolStripMenuItem(S.TaskRereg);
            reregTask.Name = "taskrereg";
            reregTask.Click += delegate
            {
                keepMenuOpenAfterClick = true;
                anchor.BeginInvoke((MethodInvoker)delegate { DoRepairTask(); });
            };
            taskState.DropDownItems.Add(reregTask);

            m.Items.Add(taskState);

            // 模式标牌：只有接管时出现。它不只是一行字——点一下直接打开那个图库目录
            ToolStripMenuItem modeState = new ToolStripMenuItem(S.ModeBadge(""));
            modeState.Name = "modestate";
            modeState.Visible = false;
            modeState.Click += delegate
            {
                keepMenuOpenAfterClick = false;
                Program.OpenTakeoverRoot();
            };
            m.Items.Add(modeState);

            // 顺序：状态 -> 模式 -> 查看 -> 移动
            ToolStripMenuItem galleryToggle = new ToolStripMenuItem(S.GalleryMenu);
            galleryToggle.Name = "gallerytoggle";
            galleryToggle.CheckOnClick = false;
            galleryToggle.ToolTipText = S.GalleryTipOn;
            galleryToggle.Click += delegate
            {
                keepMenuOpenAfterClick = true;
                // 接管模式下画廊没有意义：接管是「用我自己的图库」，
                // 画廊是「临时试看、退出即清」，两套语义混在一起会让人分不清
                if (Program.TakeoverActive())
                {
                    anchor.BeginInvoke((MethodInvoker)delegate
                    {
                        MessageBox.Show(S.GalleryTakeoverBlocked, Program.AppName,
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    });
                    return;
                }
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

            ToolStripMenuItem whereItem = MakeItem(S.WhereIsThis, delegate { ShowInfo(); });
            whereItem.Name = "wherethis";
            m.Items.Add(whereItem);

            ToolStripMenuItem prevItem = MakeKeepOpenItem(S.PrevOlder, "-Step 1", "prev");
            prevItem.Name = "prev";
            m.Items.Add(prevItem);
            ToolStripMenuItem nextItem = MakeKeepOpenItem(S.NextNewer, "-Step -1", "next");
            nextItem.Name = "next";
            m.Items.Add(nextItem);

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

            // 换过的图：和收藏管理并列 —— 它是「找回我的图」的入口，不是清理工具
            ToolStripMenuItem pinMgrItem = MakeItem(S.PinMenu, delegate { DoPinnedList(); });
            pinMgrItem.Name = "pinmgr";
            m.Items.Add(pinMgrItem);

            m.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem todayItem = MakeKeepOpenItem(S.ChangeToday, "-Index 0");
            todayItem.Name = "today";
            m.Items.Add(todayItem);
            ToolStripMenuItem yearsMenu = new ToolStripMenuItem(S.YearsAgo);
            yearsMenu.Name = "years";
            m.Items.Add(yearsMenu);


            ToolStripMenuItem cacheMenu = new ToolStripMenuItem(S.Library);
            cacheMenu.Name = "library";
            m.Items.Add(cacheMenu);


            // 随机换一张：范围是每次都可能变的意图，所以做成子菜单而不是塞进设置
            ToolStripMenuItem randItem = new ToolStripMenuItem(S.RandomFav);
            randItem.Name = "randompick";

            // 轮播：把「随机换一张」从一次性动作变成持续行为。
            // 与「暂停自动换图」互斥，两边都只调 SetWallMode，互斥规则只有一处实现。
            ToolStripMenuItem rotateItem = new ToolStripMenuItem(S.RotateOff);
            rotateItem.Name = "rotate";
            rotateItem.CheckOnClick = false;
            rotateItem.ToolTipText = S.RotateTip;
            rotateItem.Click += delegate
            {
                keepMenuOpenAfterClick = true;
                if (CurrentWallMode() == WallModeRotate)
                {
                    SetWallMode(WallModeDaily);
                    Balloon(S.RotateOffBalloon, S.RotateOffHint);
                }
                else if (GateNow.LocalN <= 0)
                {
                    // 池子空了开轮播等于开了个空转的定时器，直接拦下比让它静默不干活好
                    Balloon(S.RotateEmptyBalloon, S.RotateEmptyHint);
                }
                else
                {
                    // 进轮播会顺手关掉换图后通知；原本开着的话，得让人知道这事儿发生了
                    bool hadNotify = false;
                    try { hadNotify = GetCfgBool(ReadConfig(), "notify", false); } catch { }
                    SetWallMode(WallModeRotate);
                    Balloon(S.RotateOnBalloon(RotateMinutes),
                            hadNotify ? S.RotateNotifOffHint : S.RotateOnHint);
                }
                anchor.BeginInvoke((MethodInvoker)delegate { try { RefreshAllPublic(); } catch { } });
            };
            randItem.DropDownItems.Add(rotateItem);

            // 轮播间隔：常用档位点一下就生效，冷门值走自定义输入
            ToolStripMenuItem riMenu = new ToolStripMenuItem(S.RotateIntervalMenu);
            riMenu.Name = "rotateint";
            foreach (int mins in new int[] { 5, 10, 15, 30, 60, 120 })
            {
                int mm = mins;
                ToolStripMenuItem mi = new ToolStripMenuItem(S.RotateEvery(mm));
                mi.Name = "rotateint" + mm;
                mi.Tag = mm;
                mi.Click += delegate
                {
                    keepMenuOpenAfterClick = true;
                    RotateMinutes = mm;
                    anchor.BeginInvoke((MethodInvoker)delegate { try { RefreshAllPublic(); } catch { } });
                };
                riMenu.DropDownItems.Add(mi);
            }
            riMenu.DropDownItems.Add(new ToolStripSeparator());
            ToolStripMenuItem riCustom = new ToolStripMenuItem(S.RotateCustom);
            riCustom.Name = "rotateintcustom";
            riCustom.Click += delegate { DoRotateCustom(); };
            riMenu.DropDownItems.Add(riCustom);
            randItem.DropDownItems.Add(riMenu);
            randItem.DropDownItems.Add(new ToolStripSeparator());

            foreach (string[] sc in new string[][] {
                new string[] { "fav",   "randfav"   },
                new string[] { "local", "randlocal" },
                new string[] { "any",   "randany"   } })
            {
                string scopeVal = sc[0];
                if (scopeVal == "any") { randItem.DropDownItems.Add(new ToolStripSeparator()); }
                ToolStripMenuItem si = new ToolStripMenuItem(S.RandomScopeFav(0));
                si.Name = sc[1];
                si.Tag = scopeVal;
                si.CheckOnClick = false;
                si.Click += delegate
                {
                    keepMenuOpenAfterClick = true;
                    anchor.BeginInvoke((MethodInvoker)delegate { DoRandomPick(scopeVal); });
                };
                randItem.DropDownItems.Add(si);
            }
            m.Items.Add(randItem);

            m.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem pickDateItem = MakeItem(S.PickDate, delegate { DoPickDate(); });
            pickDateItem.Name = "pickdate";
            m.Items.Add(pickDateItem);
            // 按节日筛选：日期由脚本用农历算法算出来，这里只负责传节名
            ToolStripMenuItem festMenu = new ToolStripMenuItem(S.FestivalMenu);
            festMenu.Name = "festival";
            foreach (string fn in new string[] { "春节", "元宵节", "清明节", "端午节", "中秋节", "重阳节", "国庆节", "元旦", "除夕" })
            {
                string capFn = fn;
                ToolStripMenuItem fi = new ToolStripMenuItem(S.FestivalName(capFn));
                fi.Click += delegate { DoFestival(capFn); };
                festMenu.DropDownItems.Add(fi);
            }
            m.Items.Add(festMenu);
            ToolStripMenuItem searchItem = MakeItem(S.SearchMenu, delegate { DoSearch(); });
            searchItem.Name = "search";
            m.Items.Add(searchItem);
            ToolStripMenuItem aiItem = MakeItem(S.AiMenu, delegate { DoAi(); });
            aiItem.Name = "ai";
            m.Items.Add(aiItem);




            m.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem lockItem = new ToolStripMenuItem(S.LockOff);
            lockItem.Name = "lock";
            lockItem.CheckOnClick = false;
            lockItem.Click += delegate
            {
                keepMenuOpenAfterClick = true;
                // 三态互斥统一走 SetWallMode：点这一下要么进暂停，要么回每日一换。
                // 回每日一换时轮播一并关掉 —— 「打开自动换图」就该让轮播让位。
                bool wasPaused = CurrentWallMode() == WallModePaused;
                if (wasPaused)
                {
                    SetWallMode(WallModeDaily);
                    anchor.BeginInvoke((MethodInvoker)delegate { RunScript("-Index 0"); });
                }
                else
                {
                    SetWallMode(WallModePaused);
                    Balloon(S.BLocked, S.LockedHint);
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


            ToolStripMenuItem fastItem = new ToolStripMenuItem(S.FastSwitchOff);
            fastItem.Name = "fastswitch";
            fastItem.CheckOnClick = false;
            fastItem.ToolTipText = S.FastSwitchTip;
            fastItem.Click += delegate
            {
                keepMenuOpenAfterClick = true;
                Dictionary<string, object> c = ReadConfig();
                SetCfg(c, "fastSwitch", !GetCfgBool(c, "fastSwitch", true));
                WriteConfig(c);
            };
            m.Items.Add(fastItem);

            ToolStripMenuItem startup = new ToolStripMenuItem(S.StartupOff);
            startup.Name = "startup";
            startup.CheckOnClick = false;
            startup.Click += delegate
            {
                keepMenuOpenAfterClick = true;
                SetStartup(!IsStartupEnabled());
            };
            m.Items.Add(startup);

            // 更新时间已并入上面的「计划任务」子菜单：什么时候跑、跑不跑，同一个入口说完
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

            ToolStripMenuItem acSet = new ToolStripMenuItem(S.AutoCleanSetMenu);
            acSet.Name = "autocleanset";
            acSet.Click += delegate
            {
                keepMenuOpenAfterClick = true;
                int curDays = GetCfgInt(ReadConfig(), "keepDays", 30);
                string curItems = Program.CfgString("autoCleanItems", "cache,archive");
                string acInfo = "";
                try
                {
                    string[] af2 = SafeGetFiles(archiveImgDir);
                    long ab2 = 0;
                    foreach (string one in af2) { try { ab2 += new FileInfo(one).Length; } catch { } }
                    acInfo = af2.Length + " 张 · " + (ab2 / 1024.0 / 1024.0).ToString("0.0") + " MB";
                }
                catch { }
                int curCap = GetCfgInt(ReadConfig(), "archiveMaxMB", 500);
                using (AutoCleanForm f = new AutoCleanForm(curDays, curItems, acInfo, curCap))
                {
                    if (f.ShowDialog(anchor) != DialogResult.OK) { return; }
                    Dictionary<string, object> c2 = ReadConfig();
                    SetCfg(c2, "keepDays", f.KeepDays);
                    SetCfg(c2, "autoCleanItems", f.Items);
                    SetCfg(c2, "archiveMaxMB", f.CapMB);
                    WriteConfig(c2);
                }
            };
            cleanMenu.DropDownItems.Add(acSet);

            cleanMenu.DropDownItems.Add(new ToolStripSeparator());

            // 缓存：原来「保留 7 天 / 保留 30 天 / 清空」三项合成一个，点了弹窗自己填天数
            ToolStripMenuItem cleanNow = new ToolStripMenuItem(S.CleanNow);
            cleanNow.Name = "cleannow";
            cleanNow.Click += delegate
            {
                keepMenuOpenAfterClick = true;
                int curKeep = GetCfgInt(ReadConfig(), "keepDays", 30);
                string keepArchInfo = "";
                string keepOldInfo = "";
                try
                {
                    string[] af = SafeGetFiles(archiveImgDir);
                    long ab = 0;
                    foreach (string one in af) { try { ab += new FileInfo(one).Length; } catch { } }
                    keepArchInfo = af.Length + " 张 · " + (ab / 1024.0 / 1024.0).ToString("0.0") + " MB";

                    int on = 0;
                    long ob = 0;
                    string oldR = OldDirPath;
                    if (!string.IsNullOrEmpty(oldR) && Directory.Exists(oldR))
                    {
                        foreach (string om in Directory.GetDirectories(oldR))
                        {
                            string[] of = SafeGetFiles(om);
                            on += of.Length;
                            foreach (string one in of) { try { ob += new FileInfo(one).Length; } catch { } }
                        }
                    }
                    keepOldInfo = on + " 张 · " + (ob / 1024.0 / 1024.0).ToString("0.0") + " MB";
                }
                catch { }
                int favCount = 0;
                try { favCount = SafeGetFiles(favDir).Length; } catch { }
                using (CleanCacheForm cf = new CleanCacheForm(curKeep, keepArchInfo, keepOldInfo, favCount))
                {
                    if (cf.ShowDialog(anchor) != DialogResult.OK) { return; }

                    // 前四项按天数清，交给 -Clean；后两项是整目录清空，交给 -CleanArchive
                    string picked = cf.Items == null ? "" : cf.Items;
                    string dayItems = "";
                    foreach (string one in picked.Split(','))
                    {
                        string k = one.Trim();
                        if (k == "cache" || k == "thumbs" || k == "wall" || k == "stories")
                        {
                            if (dayItems.Length > 0) { dayItems += ","; }
                            dayItems += k;
                        }
                    }
                    if (dayItems.Length > 0)
                    {
                        RunScriptEx("-Clean -Keep " + cf.KeepDays + " -CleanItems " + dayItems, "clean");
                    }
                    bool wantArch = picked.IndexOf("archive", StringComparison.Ordinal) >= 0;
                    bool wantOld = picked.IndexOf("old", StringComparison.Ordinal) >= 0;
                    if (wantArch || wantOld)
                    {
                        string extra = wantOld ? " -IncludeOld" : "";
                        RunScriptEx("-CleanArchive -CleanMode recycle" + extra, "cleanarch");
                    }
                    // 收藏是用户特意留的图，单独一次确认，确认了才动
                    if (picked.IndexOf("fav", StringComparison.Ordinal) >= 0 && favCount > 0)
                    {
                        DialogResult dr = MessageBox.Show(S.CnFavConfirm(favCount), AppName,
                            MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
                        if (dr == DialogResult.OK)
                        {
                            RunScriptEx("-CleanFavorites -CleanMode recycle", "cleanfav");
                        }
                    }
                }
            };
            cleanMenu.DropDownItems.Add(cleanNow);

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

            // 接管目录属于「换一个图库」—— 和上面的数据源开关同族，不放在「关于」那种只读区
            archiveMenu.DropDownItems.Add(new ToolStripSeparator());
            archiveMenu.DropDownItems.Add(MakeItem(S.TkMenu, delegate { DoTakeover(); }));

            m.Items.Add(archiveMenu);

            ToolStripMenuItem themeMenu = new ToolStripMenuItem(S.ThemeMenu);
            themeMenu.Name = "theme";
            themeMenu.DropDownItems.Add(MakeThemeItem(S.ThemeAuto, "auto"));
            themeMenu.DropDownItems.Add(MakeThemeItem(S.ThemeLight, "light"));
            themeMenu.DropDownItems.Add(MakeThemeItem(S.ThemeDark, "dark"));
            m.Items.Add(themeMenu);

            ToolStripMenuItem sysMenu = new ToolStripMenuItem(S.SysMenu);
            sysMenu.Name = "sys";

            sysMenu.DropDownItems.Add(MakeItem(S.HelpItem, delegate { ShowHelp(); }));
            sysMenu.DropDownItems.Add(new ToolStripSeparator());
            sysMenu.DropDownItems.Add(MakeItem(S.AboutItem + AppName, delegate { ShowAbout(); }));
            sysMenu.DropDownItems.Add(MakeItem(S.OpenLog, delegate { OpenTarget(logPath); }));
            sysMenu.DropDownItems.Add(new ToolStripSeparator());
            sysMenu.DropDownItems.Add(MakeItem(S.UninstallItem, delegate { DoUninstall(); }));
            m.Items.Add(sysMenu);

            m.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem exitItem = MakeItem(S.ExitItem, delegate
            {
                if (tray != null) { tray.Visible = false; }
                Application.Exit();
            });
            exitItem.Name = "exit";
            m.Items.Add(exitItem);

            // 每次打开菜单都刷新：外部（脚本/AI）改过壁纸后能立刻看到最新状态。
            // 顺序有讲究：先自愈（busy 与锁不一致就地复位），再刷文字，最后按 gate 重算
            // 每个项的 Enabled —— 这是"任何脏状态下次打开即恢复"的唯一保证。
            m.Opening += delegate
            {
                keepMenuOpenAfterClick = false;
                try { HealBusyIfStale(); } catch { }
                try { RefreshHeaderOnly(); } catch { }
                try { ApplyMenuState(); } catch { }
            };

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
