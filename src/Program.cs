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

[assembly: AssemblyTitle("必应壁纸编年史")]
[assembly: AssemblyProduct("必应壁纸编年史")]
[assembly: AssemblyDescription("从必应官方接口获取每日图片并设为桌面壁纸。无水印、无广告、无后台守护进程。")]
[assembly: AssemblyCompany("BingWallpaperChronicle")]
[assembly: AssemblyCopyright("© 2026 BingWallpaperChronicle")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyConfiguration("Release")]
[assembly: AssemblyCulture("")]
[assembly: AssemblyVersion("3.2.0.0")]
[assembly: AssemblyFileVersion("3.2.0.0")]
[assembly: AssemblyInformationalVersion("3.2.0")]
[assembly: ComVisible(false)]

namespace BingWallpaperLite
{
    internal static partial class Program
    {
        // ---------------------------------------------------------------- 常量

        internal const string AppName     = "必应壁纸编年史";
        internal const string AppVersion  = "3.2.0";
        private const string TaskName    = "BingWallpaperChronicle";
        private const string RunValue    = "BingWallpaperChronicle";
        private const string RunKeyPath  = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string UninstKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\BingWallpaperChronicle";

        // 内嵌资源：首次运行或分发给他人时自举释放
        private static readonly string[] PayloadFiles = { "bing-wallpaper.ps1", "bing.ico", "bing-busy.ico" };

        private static string root;          // 程序所在目录（绿色模式）
        private static string scriptPath;
        private static string statePath;
        private static string indexPath;
        private static string cacheDir;
        private static string lockPath;
        private static string logPath;
        private static string iconPath;
        private static string busyIconPath;
        private static string configPath;
        private static string favDir;
        private static string favIndexPath;
        private static string archiveDir;
        internal static string ArchiveDirPath { get { return archiveDir; } }
        internal static string ScriptPathPublic { get { return scriptPath; } }
        internal static string ArchiveImgPath { get { return Path.Combine(archiveDir, "images"); } }
        internal static string ArchiveDirPath2 { get { return archiveDir; } }
        internal static string CacheDirPath { get { return cacheDir; } }
        internal static string FavDirPath { get { return favDir; } }

        internal static Dictionary<string, object> ReadPinnedPublic()
        {
            try
            {
                string f = Path.Combine(cacheDir, "pinned.json");
                return ReadJson(f);
            }
            catch { return null; }
        }

        internal static string CurrentWallpaperPath()
        {
            try
            {
                object o = Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "Wallpaper", "");
                return o as string ?? "";
            }
            catch { return ""; }
        }
        private static string archiveImgDir;

        private static NotifyIcon tray;
        private static ContextMenuStrip menu;
        private static Icon iconNormal;
        private static Icon iconBusy;
        private static Form anchor;
        private static volatile bool busy;
        private static string lastSyncedActual = "";
        private static bool refreshing = false;
        private static bool keepMenuOpenAfterClick = false;
        private static bool themeApplied = false;
        private static bool lastAppliedDark = false;
        private static System.Windows.Forms.Timer lockWatch;

        private static void ResolvePaths()
        {
            string r = Path.GetDirectoryName(Application.ExecutablePath);
            if (string.IsNullOrEmpty(r)) { r = @"C:\"; }
            ResolvePathsFromRoot(r);
        }

        private static void ResolvePathsFromRoot(string r)
        {
            root = r;
            scriptPath   = Path.Combine(root, "bing-wallpaper.ps1");
            cacheDir     = Path.Combine(root, "cache");
            statePath    = Path.Combine(root, "state.json");
            indexPath    = Path.Combine(cacheDir, "index.json");
            lockPath     = Path.Combine(cacheDir, ".running");
            iconPath     = Path.Combine(root, "bing.ico");
            busyIconPath = Path.Combine(root, "bing-busy.ico");
            configPath   = Path.Combine(root, "config.json");
            favDir       = Path.Combine(root, "favorites");
            favIndexPath = Path.Combine(favDir, "index.json");
            archiveDir   = Path.Combine(root, "archive");
            archiveImgDir = Path.Combine(archiveDir, "images");
            logPath      = Path.Combine(Path.GetTempPath(), "BingWallpaperLite.log");
        }

        // ---------------------------------------------------------------- 配置读写

        private static Dictionary<string, object> ReadConfig()
        {
            Dictionary<string, object> c = ReadJson(configPath);
            if (c == null) { c = new Dictionary<string, object>(); }
            return c;
        }

        private static bool GetCfgBool(Dictionary<string, object> c, string key, bool dflt)
        {
            if (c == null || !c.ContainsKey(key) || c[key] == null) { return dflt; }
            object v = c[key];
            if (v is bool) { return (bool)v; }
            string s = v.ToString().Trim().ToLowerInvariant();
            if (s == "true" || s == "1" || s == "yes") { return true; }
            if (s == "false" || s == "0" || s == "no") { return false; }
            return dflt;
        }

        private static int GetCfgInt(Dictionary<string, object> c, string key, int dflt)
        {
            if (c == null || !c.ContainsKey(key) || c[key] == null) { return dflt; }
            int n;
            if (int.TryParse(c[key].ToString(), out n)) { return n; }
            return dflt;
        }

        private static string GetCfgStr(Dictionary<string, object> c, string key, string dflt)
        {
            if (c == null || !c.ContainsKey(key) || c[key] == null) { return dflt; }
            string s = c[key].ToString().Trim();
            return s.Length == 0 ? dflt : s;
        }

        private static void SetCfg(Dictionary<string, object> c, string key, object value)
        {
            c[key] = value;
        }

        private static void WriteConfig(Dictionary<string, object> c)
        {
            try
            {
                JavaScriptSerializer ser = new JavaScriptSerializer();
                string json = ser.Serialize(c);
                json = UnescapeUnicode(json);
                File.WriteAllText(configPath, json, new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                MessageBox.Show(S.WarnWriteCfg + ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>JavaScriptSerializer 会把中文转成 \uXXXX，写回前还原。</summary>
        private static string UnescapeUnicode(string s)
        {
            if (string.IsNullOrEmpty(s)) { return s; }
            return System.Text.RegularExpressions.Regex.Replace(s, @"\\u([0-9a-fA-F]{4})",
                delegate(System.Text.RegularExpressions.Match m)
                {
                    return ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString();
                });
        }

        /// <summary>把某个布尔配置项改掉并落盘。</summary>
        private static void ToggleConfigBool(string key, bool value)
        {
            Dictionary<string, object> c = ReadConfig();
            SetCfg(c, key, value);
            WriteConfig(c);
        }

        /// <summary>首次运行：按主屏宽度决定分辨率档位，写进 config.json，之后不再改。</summary>
        private static string DetectResolutionOnce()
        {
            Dictionary<string, object> c = ReadConfig();
            string cur = "";
            if (c.ContainsKey("resolution") && c["resolution"] != null) { cur = c["resolution"].ToString().Trim(); }
            if (cur.Length > 0) { return cur; }

            // 壁纸标题语言跟随界面语言（界面英文时标题也用英文）
            if (!c.ContainsKey("titleLanguage"))
            {
                SetCfg(c, "titleLanguage", S.IsEn ? "en" : "zh");
            }

            int w = 1920;
            try
            {
                Rectangle b = Screen.PrimaryScreen.Bounds;
                w = Math.Max(b.Width, b.Height);
            }
            catch { }

            string r = (w >= 2560) ? "UHD" : "1920x1080";
            SetCfg(c, "resolution", r);
            if (!c.ContainsKey("autoClean")) { SetCfg(c, "autoClean", true); }
            if (!c.ContainsKey("locked")) { SetCfg(c, "locked", false); }

            // 超宽屏（比 2:1 更宽）用「适应」保留完整画面，常规屏用「填充」
            if (!c.ContainsKey("wallpaperStyle"))
            {
                string style = "Fill";
                try
                {
                    Rectangle b2 = Screen.PrimaryScreen.Bounds;
                    double ratio = (double)Math.Max(b2.Width, b2.Height) / Math.Min(b2.Width, b2.Height);
                    if (ratio > 2.0) { style = "Fit"; }
                }
                catch { }
                SetCfg(c, "wallpaperStyle", style);
            }

            WriteConfig(c);
            return r;
        }

        private static string InstallTarget
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "BingWallpaperLite");
            }
        }

        /// <summary>目录里已经有脚本 → 绿色版布局，直接跑。</summary>
        private static bool IsPortableLayout()
        {
            return File.Exists(Path.Combine(root, "bing-wallpaper.ps1"));
        }

        /// <summary>
        /// 单文件首次运行时的安装向导。
        /// 返回 true = 在当前目录继续跑；false = 已跳到别处或用户取消。
        /// </summary>
        private static bool RunSetup()
        {
            using (SetupForm f = new SetupForm(InstallTarget))
            {
                if (f.ShowDialog() != DialogResult.OK) { return false; }

                if (f.PortableMode)
                {
                    EnsurePayload(false);
                    return true;
                }

                return InstallTo(f.SelectedPath);
            }
        }

        /// <summary>把自身与内嵌资源释放到指定目录并启动，返回 false 表示本进程应退出。</summary>
        private static bool InstallTo(string target)
        {
            target = target.TrimEnd('\\');
            if (string.Equals(target, root.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            {
                // 选的就是当前目录，等于就地展开
                EnsurePayload(true);
                return true;
            }

            try { Directory.CreateDirectory(target); }
            catch (Exception ex)
            {
                MessageBox.Show(S.WarnMkDir + ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            string destExe = Path.Combine(target, Path.GetFileName(Application.ExecutablePath));
            try
            {
                File.Copy(Application.ExecutablePath, destExe, true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(S.WarnCopyExe + ex.Message + "\r\n\r\n" + S.WarnCopyHint,
                    AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            string saved = root;
            ResolvePathsFromRoot(target);
            bool ok = EnsurePayload(true);
            root = saved;

            if (!ok)
            {
                MessageBox.Show(S.WarnPayload, AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            // 结束可能存在的旧实例，避免单实例互斥把新装的挡在门外
            int me = Process.GetCurrentProcess().Id;
            try
            {
                foreach (Process other in Process.GetProcessesByName("BingWallpaperLite"))
                {
                    if (other.Id == me) { continue; }
                    try { other.Kill(); other.WaitForExit(3000); } catch { }
                }
            }
            catch { }
            Thread.Sleep(800);

            try { Process.Start(destExe); }
            catch (Exception ex)
            {
                MessageBox.Show(S.WarnLaunch + ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            return false;
        }

        private static string PowerShellExe
        {
            get
            {
                string p = @"C:\Program Files\PowerShell\7\pwsh.exe";
                if (File.Exists(p)) { return p; }
                return @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe";
            }
        }

        // ---------------------------------------------------------------- 自举释放

        /// <summary>
        /// 把内嵌的脚本与图标释放到程序目录；已存在则跳过（绿色模式优先用外部文件）。
        /// 返回 false 表示目录不可写且无法释放。
        /// </summary>
        private static bool EnsurePayload(bool force)
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            bool ok = true;

            foreach (string f in PayloadFiles)
            {
                string dest = Path.Combine(root, f);
                if (!force && File.Exists(dest)) { continue; }
                try
                {
                    using (Stream s = asm.GetManifestResourceStream(f))
                    {
                        if (s == null) { continue; }
                        using (FileStream fs = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            byte[] buf = new byte[8192];
                            int n;
                            while ((n = s.Read(buf, 0, buf.Length)) > 0) { fs.Write(buf, 0, n); }
                        }
                    }
                }
                catch
                {
                    ok = false;
                }
            }

            // config.json 只在缺失时释放，绝不覆盖用户配置
            string cfg = Path.Combine(root, "config.json");
            if (!File.Exists(cfg))
            {
                try
                {
                    using (Stream s = asm.GetManifestResourceStream("config.json"))
                    {
                        if (s != null)
                        {
                            using (FileStream fs = new FileStream(cfg, FileMode.Create, FileAccess.Write, FileShare.None))
                            {
                                byte[] buf = new byte[4096];
                                int n;
                                while ((n = s.Read(buf, 0, buf.Length)) > 0) { fs.Write(buf, 0, n); }
                            }
                        }
                    }
                }
                catch { }
            }

            return ok;
        }

        // ---------------------------------------------------------------- 入口

        [STAThread]
        private static void Main(string[] args)
        {
            ResolvePaths();

            // 语言：config 的 uiLanguage 优先；auto 时按系统 UI 语言（中文→中文，其它→英文）
            bool useEn = false;
            try
            {
                Dictionary<string, object> c0 = ReadConfig();
                string ui = (c0 != null && c0.ContainsKey("uiLanguage") && c0["uiLanguage"] != null)
                    ? c0["uiLanguage"].ToString().Trim().ToLowerInvariant() : "auto";
                if (ui == "en") { useEn = true; }
                else if (ui == "zh") { useEn = false; }
                else
                {
                    string lang = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                    useEn = !string.Equals(lang, "zh", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch { useEn = false; }
            S.Init(useEn);

            string arg0 = args.Length > 0 ? args[0].ToLowerInvariant() : "";

            // 先确保配套文件就位（分发包只有一个 exe 时靠这里自举）
            if (arg0 == "--extract" || arg0 == "/extract")
            {
                Application.EnableVisualStyles();
                bool okx = EnsurePayload(true);
                MessageBox.Show(
                    okx ? (S.ExtractedTo + "\r\n" + root) : (S.ExtractFailed + "\r\n" + root),
                    AppName, MessageBoxButtons.OK, okx ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                return;
            }

            // 单文件首次运行：既不是绿色布局、也不在安装目录 → 走安装向导
            if (!IsPortableLayout() &&
                !string.Equals(root.TrimEnd('\\'),
                    InstallTarget.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                if (!RunSetup()) { return; }
            }

            bool payloadOk = EnsurePayload(false);
            if (!payloadOk)
            {
                MessageBox.Show(
                    S.WarnNoFiles + "\r\n" + root +
                    "\r\n\r\n" + S.WarnNoFiles2,
                    AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (arg0 == "--run" || arg0 == "/run")
            {
                // 计划任务入口：无控制台宿主，内部隐藏启动 pwsh，全程不闪窗口
                string tail = "";
                if (args.Length > 1)
                {
                    tail = string.Join(" ", args, 1, args.Length - 1);
                }
                int rc = RunScriptBlocking(tail);
                Environment.Exit(rc);
            }

            if (arg0 == "--about" || arg0 == "/about")
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                ShowAbout();
                return;
            }

            if (arg0 == "--install" || arg0 == "/install")
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                DoInstall(true);
                return;
            }
            if (arg0 == "--uninstall" || arg0 == "/uninstall")
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                DoUninstall();
                return;
            }

            bool created = false;
            using (Mutex mtx = new Mutex(true, "BingWallpaperLite_SingleInstance", out created))
            {
                if (!created) { return; }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                try
                {
                    anchor = new Form();
                    anchor.ShowInTaskbar = false;
                    anchor.WindowState = FormWindowState.Minimized;
                    anchor.FormBorderStyle = FormBorderStyle.None;
                    anchor.Opacity = 0;
                    IntPtr forceHandle = anchor.Handle;

                    Thread.Sleep(3000);

                    Size sz = SystemInformation.SmallIconSize;
                    iconNormal = LoadIcon(iconPath, sz);
                    iconBusy = LoadIcon(busyIconPath, sz);

                    menu = BuildMenu();
                    tray = new NotifyIcon();
                    tray.Icon = iconNormal;
                    tray.Text = AppName;
                    tray.Visible = true;
                    tray.MouseUp += TrayMouseUp;           // 左键右键都弹菜单
                    tray.MouseDown += delegate { };        // 占位（保留绑定避免后续空引用）

                    bool firstRun = !File.Exists(statePath);

                    try { RefreshAll(); } catch { }
                    try { DetectResolutionOnce(); } catch { }
                    // 注册延后到消息循环启动后执行，避免阻塞启动
                    try { RefreshAll(); } catch { }

                    // 每秒校正一次忙碌图标：脚本结束、锁文件消失后自动恢复，不依赖回调
                    lockWatch = new System.Windows.Forms.Timer();
                    lockWatch.Interval = 1000;
                    lockWatch.Tick += delegate
                    {
                        try
                        {
                bool running = File.Exists(lockPath);
                            if (busy != running) { SetBusy(running); }
                        }
                        catch { }
                    };
                    lockWatch.Start();

                    if (firstRun)
                    {
                        // 全新安装：立刻换一张，别让人对着没反应的托盘发呆
                        anchor.BeginInvoke((MethodInvoker)delegate { RunScriptEx("-Index 0", "firstrun"); });
                    }

                    System.Windows.Forms.Timer regTimer = new System.Windows.Forms.Timer();
                    regTimer.Interval = 8000;
                    regTimer.Tick += delegate
                    {
                        regTimer.Stop();
                        regTimer.Dispose();
                        try { EnsureRegistered(false); } catch { }
                    };
                    regTimer.Start();

                    Application.Run();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(S.WarnLaunch + ex.Message, AppName,
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    if (tray != null) { tray.Visible = false; tray.Dispose(); }
                }
            }
        }
    }
}

