using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace BingWallpaperLite
{
    internal static partial class Program
    {
        // ================================================================ 菜单可用性
        //
        // 唯一裁决点：菜单项 Enabled 是「派生态」，由 gate 推导，不保存历史。
        //   1. 默认可用；禁用必须有证据（busy / 接管 / 无候选）。
        //   2. 每次打开菜单重算一次 → 任何脏状态在下次打开时自愈。
        //   3. 出口项（关画廊、暂停、改时间、关于、退出）永不禁用。
        //
        // 为什么改成这样：v3.6.61 及以前由 SetBusy 遍历 menu.Items 逐项赋
        //   item.Enabled = menu.Visible ? true : !on;
        // 菜单关闭时一旦 busy 翻转，整张菜单被设成禁用；而 Opening 只刷新文字、
        // 从不重算 Enabled → 一次竞态就把菜单灰到关不掉画廊，且关掉再开也不恢复。

        internal struct MenuGate
        {
            internal bool Busy;
            internal bool Takeover;
            internal int LocalN;
            internal int FavN;

            /// <summary>保守初值：一律可用。初始化顺序再乱也不会把菜单误灰。</summary>
            internal static MenuGate Ready
            {
                get
                {
                    MenuGate g = new MenuGate();
                    g.Busy = false;
                    g.Takeover = false;
                    g.LocalN = 1;
                    g.FavN = 1;
                    return g;
                }
            }
        }

        /// <summary>busy 期间照旧可点的项：查看、配置、自救出口。少一个就可能把用户锁在灰菜单里。</summary>
        private static readonly string[] MenuAlwaysOn = new string[]
        {
            "taskstate", "gallerytoggle", "wherethis",
            "favtoggle", "favmgr", "pinmgr",
            "lock", "notify", "startup", "updatetime", "theme", "sys", "exit"
        };

        /// <summary>纯展示的信息行：永远禁用，点了也不做事。灰着才是正确表达。</summary>
        private static readonly string[] MenuInfoOnly = new string[]
        {
            "header", "modestate"
        };

        /// <summary>busy 期间禁用的项：点一下就会再起一个 powershell 去跑脚本。</summary>
        private static readonly string[] MenuBusyBlocked = new string[]
        {
            "prev", "next", "today", "years", "library", "randompick",
            "pickdate", "festival", "search", "ai", "clean", "archive"
        };

        /// <summary>最近一次 RefreshAll 算出的 gate；Opening 直接用缓存，不做 IO。</summary>
        private static MenuGate menuGate = MenuGate.Ready;

        internal static MenuGate GateNow { get { return menuGate; } }
        internal static void SetGate(MenuGate g) { menuGate = g; }

        private static bool NameIn(string[] list, string name)
        {
            if (string.IsNullOrEmpty(name)) { return false; }
            for (int i = 0; i < list.Length; i++)
            {
                if (string.Equals(list[i], name, StringComparison.OrdinalIgnoreCase)) { return true; }
            }
            return false;
        }

        /// <summary>纯函数：无 IO、无 UI，可被 -MenuSelfTest 直接断言。</summary>
        internal static bool ShouldEnable(string name, MenuGate g)
        {
            // 信息行恒禁用：只用来显示当前状态，点击不做事
            if (NameIn(MenuInfoOnly, name)) { return false; }

            if (NameIn(MenuAlwaysOn, name)) { return true; }
            if (g.Busy) { return !NameIn(MenuBusyBlocked, name); }
            if (string.Equals(name, "clean", StringComparison.OrdinalIgnoreCase)) { return !g.Takeover; }
            if (string.Equals(name, "randompick", StringComparison.OrdinalIgnoreCase)) { return g.LocalN > 0; }
            return true;
        }

        /// <summary>把 gate 落到每个顶级菜单项。用索引遍历，集合变动也不会半途中止。</summary>
        internal static void ApplyMenuState()
        {
            ContextMenuStrip m = menu;
            if (m == null) { return; }

            // busy 必须取实时值（SetBusy 会立刻调本方法，而 gate 是 RefreshAll 时的快照）；
            // 接管状态与候选数沿用快照，避免打开菜单时去扫目录。
            MenuGate g = menuGate;
            g.Busy = busy;

            for (int i = 0; i < m.Items.Count; i++)
            {
                ToolStripMenuItem it = m.Items[i] as ToolStripMenuItem;
                if (it == null) { continue; }
                bool on = true;
                try { on = ShouldEnable(it.Name, g); } catch { on = true; }
                try { if (it.Enabled != on) { it.Enabled = on; } } catch { }
            }
        }

        /// <summary>
        /// 打开菜单前的自愈：busy 还挂着但锁早没了 → 就地复位。
        /// 不依赖 SetBusy 的成对调用，也不依赖 Timer。
        /// </summary>
        internal static void HealBusyIfStale()
        {
            if (!busy) { return; }
            bool locked = false;
            try { locked = File.Exists(lockPath); } catch { }
            if (locked) { return; }
            // 刚起的 3 秒内锁可能还没写出来，别急着复位
            if (busySince == DateTime.MinValue) { SetBusy(false); return; }
            if ((DateTime.Now - busySince).TotalSeconds < 3) { return; }

            SetBusy(false);
            try
            {
                File.AppendAllText(Path.Combine(Path.GetTempPath(), "BingWallpaperLite.log"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  [HEAL]  busy 与锁不一致，已复位" + "\r\n",
                    new UTF8Encoding(false));
            }
            catch { }
        }

        /// <summary>忙时的统一反馈：入口拦截不再静默吞掉点击。</summary>
        internal static void NotifyBusy()
        {
            try { Balloon(S.BusyTitle, S.BusyHint); } catch { }
        }

        /// <summary>
        /// -MenuSelfTest：把 ShouldEnable 的判定表跑一遍。
        /// 结果写 %TEMP%\BingWallpaperLite.menugate.txt（GUI 子系统没有 stdout），并用退出码表达成败。
        /// </summary>
        internal static int RunMenuSelfTest()
        {
            int total = 0;
            List<string> bad = new List<string>();

            MenuGate idle = MenuGate.Ready;
            MenuGate busyG = MenuGate.Ready; busyG.Busy = true;
            MenuGate tkG = MenuGate.Ready; tkG.Takeover = true;
            MenuGate emptyG = MenuGate.Ready; emptyG.LocalN = 0;

            // busy 时：出口与查看类必须活
            string[] mustLive = new string[] {
                "gallerytoggle", "exit", "lock", "startup", "sys", "updatetime",
                "theme", "wherethis", "favtoggle", "taskstate"
            };
            for (int i = 0; i < mustLive.Length; i++)
            {
                total++;
                if (!ShouldEnable(mustLive[i], busyG)) { bad.Add("FAIL busy:" + mustLive[i] + " 应为可用"); }
            }

            // 信息行：空闲时也必须灰着
            total++;
            if (ShouldEnable("header", idle)) { bad.Add("FAIL info:header 应为禁用"); }
            total++;
            if (ShouldEnable("modestate", idle)) { bad.Add("FAIL info:modestate 应为禁用"); }
            total++;
            if (ShouldEnable("header", busyG)) { bad.Add("FAIL info:header@busy 应为禁用"); }

            // busy 时：会起脚本的重活必须挡
            string[] mustBlock = new string[] {
                "prev", "next", "today", "years", "library", "randompick",
                "pickdate", "festival", "search", "ai", "clean", "archive"
            };
            for (int i = 0; i < mustBlock.Length; i++)
            {
                total++;
                if (ShouldEnable(mustBlock[i], busyG)) { bad.Add("FAIL busy:" + mustBlock[i] + " 应为禁用"); }
            }

            // 空闲时：全部可用
            total++;
            if (!ShouldEnable("prev", idle)) { bad.Add("FAIL idle:prev"); }
            total++;
            if (!ShouldEnable("clean", idle)) { bad.Add("FAIL idle:clean"); }
            total++;
            if (!ShouldEnable("randompick", idle)) { bad.Add("FAIL idle:randompick"); }

            // 接管：清理必须挡，其余照常
            total++;
            if (ShouldEnable("clean", tkG)) { bad.Add("FAIL takeover:clean 应为禁用"); }
            total++;
            if (!ShouldEnable("gallerytoggle", tkG)) { bad.Add("FAIL takeover:gallerytoggle 应为可用"); }

            // 无本地候选：随机换一张必须挡
            total++;
            if (ShouldEnable("randompick", emptyG)) { bad.Add("FAIL empty:randompick 应为禁用"); }
            total++;
            if (!ShouldEnable("prev", emptyG)) { bad.Add("FAIL empty:prev 应为可用"); }

            // 未命名的项默认可用（宁可多点一下，也不要锁死）
            total++;
            if (!ShouldEnable("", idle)) { bad.Add("FAIL unnamed:idle"); }

            // ---- 集成段：真建一个菜单对象，走 SetBusy → ApplyMenuState 的完整路径
            ContextMenuStrip savedMenu = menu;
            MenuGate savedGate = menuGate;
            ContextMenuStrip tmp = new ContextMenuStrip();
            string[] probe = new string[] { "header", "gallerytoggle", "wherethis", "prev", "clean", "exit" };
            for (int i = 0; i < probe.Length; i++)
            {
                ToolStripMenuItem mi = new ToolStripMenuItem(probe[i]);
                mi.Name = probe[i];
                tmp.Items.Add(mi);
            }
            menu = tmp;
            try
            {
                SetGate(MenuGate.Ready);
                SetBusy(true);

                total++;
                if (!tmp.Items["gallerytoggle"].Enabled) { bad.Add("FAIL ui:busy 时画廊开关被禁"); }
                total++;
                if (!tmp.Items["wherethis"].Enabled) { bad.Add("FAIL ui:busy 时查看项被禁"); }
                total++;
                if (!tmp.Items["exit"].Enabled) { bad.Add("FAIL ui:busy 时退出被禁"); }
                total++;
                if (tmp.Items["prev"].Enabled) { bad.Add("FAIL ui:busy 时上一张没被禁"); }
                total++;
                if (tmp.Items["header"].Enabled) { bad.Add("FAIL ui:header 被当成可用"); }

                SetBusy(false);
                total++;
                if (!tmp.Items["prev"].Enabled) { bad.Add("FAIL ui:idle 时上一张仍被禁（状态没恢复）"); }
                total++;
                if (tmp.Items["header"].Enabled) { bad.Add("FAIL ui:idle 时 header 被启用"); }
            }
            finally
            {
                try { SetBusy(false); } catch { }
                menu = savedMenu;
                menuGate = savedGate;
                try { tmp.Dispose(); } catch { }
            }

            string verdict = bad.Count == 0
                ? "MENUGATE OK " + total + "/" + total
                : "MENUGATE FAIL " + bad.Count + "/" + total + " :: " + string.Join(" | ", bad.ToArray());

            try
            {
                File.WriteAllText(Path.Combine(Path.GetTempPath(), "BingWallpaperLite.menugate.txt"),
                    verdict + "\r\n", new UTF8Encoding(false));
            }
            catch { }

            return bad.Count == 0 ? 0 : 1;
        }
    }
}
