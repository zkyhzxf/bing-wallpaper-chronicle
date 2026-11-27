﻿﻿﻿﻿﻿using System;
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
[assembly: AssemblyVersion("3.6.98.0")]
[assembly: AssemblyFileVersion("3.6.98.0")]
[assembly: AssemblyInformationalVersion("3.6.98")]
[assembly: ComVisible(false)]

namespace BingWallpaperLite
{
    internal static partial class Program
    {
        // ---------------------------------------------------------------- 常量

        internal const string AppName     = "必应壁纸编年史";
        internal const string AppVersion  = "3.6.98";
        private const string TaskName    = "BingWallpaperChronicle";
        private const string RunValue    = "BingWallpaperChronicle";

        // 归档取数下界：与 bing-wallpaper.ps1 的 $ArchiveMinDate 必须一致（脚本第 223 行）
        internal static readonly DateTime ArchiveMinDateValue = new DateTime(2009, 7, 13);
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
        /// <summary>当前生效的归档图目录：接管时就是接管的那个图库根目录。
        /// 窗口侧的统计、找图、位置列都必须用它，否则会和脚本操作的不是同一个目录。</summary>
        internal static string ActiveArchiveImgPath
        {
            get
            {
                try
                {
                    if (TakeoverActive()) { return CurrentTakeoverRoot(); }
                }
                catch { }
                return ArchiveImgPath;
            }
        }
        internal static string ArchiveDirPath2 { get { return archiveDir; } }
        internal static string CacheDirPath { get { return cacheDir; } }
        internal static string FavDirPath { get { return favDir; } }
        internal static string OldDirPath { get { return Path.Combine(archiveDir, "old"); } }
        /// <summary>当前生效的缓存根：接管时是 takeover\cache，否则是安装目录的 cache。</summary>
        internal static string ActiveCacheDir
        {
            get
            {
                try
                {
                    if (File.Exists(Path.Combine(root, "takeover") + Path.DirectorySeparatorChar + "config.json"))
                    {
                        return Path.Combine(root, "takeover", "cache");
                    }
                }
                catch { }
                return cacheDir;
            }
        }

        /// <summary>当前生效的收藏目录：接管时是 takeover\favorites。</summary>
        internal static string ActiveFavDirPath
        {
            get
            {
                try
                {
                    if (File.Exists(Path.Combine(root, "takeover") + Path.DirectorySeparatorChar + "config.json"))
                    {
                        return Path.Combine(root, "takeover", "favorites");
                    }
                }
                catch { }
                return favDir;
            }
        }

        internal static string GalleryDirPath { get { return Path.Combine(ActiveCacheDir, "gallery"); } }
        internal static string StoriesDirPath { get { return Path.Combine(archiveDir, "stories"); } }
        /// <summary>预取中转目录：只用于翻页加速，退出/启动时清空。</summary>
        internal static string PrefetchDirPath { get { return Path.Combine(ActiveCacheDir, "prefetch"); } }
        internal static string WallDirPath { get { return Path.Combine(ActiveCacheDir, "wall"); } }
        /// <summary>某一天的图是否在收藏里（按文件名前缀判断，不依赖索引）。</summary>
        internal static bool IsDateFavorited(string d8)
        {
            try
            {
                if (string.IsNullOrEmpty(d8) || d8.Length != 8) { return false; }
                string[] dirs = new string[] { ActiveFavDirPath };
                foreach (string dir in dirs)
                {
                    if (!Directory.Exists(dir)) { continue; }
                    if (Directory.GetFiles(dir, d8 + "_*.jpg").Length > 0) { return true; }
                }
            }
            catch { }
            return false;
        }

        internal static string TakeoverDirPath { get { return Path.Combine(root, "takeover"); } }
        internal static string TakeoverCfgPath { get { return Path.Combine(TakeoverDirPath, "config.json"); } }

        /// <summary>接管目录（外部图片仓库）；没接管返回空串。</summary>
        internal static string CurrentTakeoverRoot()
        {
            try
            {
                if (!File.Exists(TakeoverCfgPath)) { return ""; }
                Dictionary<string, object> c = ReadJson(TakeoverCfgPath);
                return GetStr(c, "root");
            }
            catch { return ""; }
        }

        // ---- 接管感知的「当前生效」路径 ------------------------------------
        // 脚本在接管下把这四个文件写在 takeover\ 沙盒里，UI 侧必须跟着走，
        // 否则菜单标题、托盘提示、busy 判断、收藏判定全部读的是安装目录里的过期副本。

        private static bool takeoverCached = false;
        private static DateTime takeoverStampUtc = DateTime.MinValue;

        /// <summary>按 takeover\config.json 的最后修改时间缓存，文件没变就不重复解析。</summary>
        private static bool TakeoverCached()
        {
            try
            {
                string p = TakeoverCfgPath;
                if (!File.Exists(p)) { return false; }
                DateTime st = File.GetLastWriteTimeUtc(p);
                if (st == takeoverStampUtc) { return takeoverCached; }
                takeoverStampUtc = st;
                takeoverCached = TakeoverActive();
                return takeoverCached;
            }
            catch { return false; }
        }

        internal static string StatePathNow
        {
            get { return TakeoverCached() ? Path.Combine(TakeoverDirPath, "state.json") : statePath; }
        }

        internal static string CacheIndexPathNow
        {
            get { return TakeoverCached() ? Path.Combine(TakeoverDirPath, "cache-index.json") : indexPath; }
        }

        internal static string LockPathNow
        {
            get { return TakeoverCached() ? Path.Combine(TakeoverDirPath, @"cache\.running") : lockPath; }
        }

        internal static string FavIndexPathNow
        {
            get { return TakeoverCached() ? Path.Combine(TakeoverDirPath, @"favorites\index.json") : favIndexPath; }
        }

        /// <summary>打开当前接管图库目录（资源管理器）。未接管时什么都不做。</summary>
        internal static void OpenTakeoverRoot()
        {
            try
            {
                string root = CurrentTakeoverRoot();
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) { return; }
                System.Diagnostics.Process.Start("explorer.exe", "\"" + root + "\"");
            }
            catch { }
        }

        /// <summary>计划任务审计日志：不依赖脚本，哪怕 PowerShell 起不来也能留痕。</summary>
        private static string TaskAuditPath
        {
            get { return Path.Combine(Path.GetTempPath(), "BingWallpaperLite.task.log"); }
        }

        /// <summary>自动换图失败标记：托盘下次启动时读它并提示一次。</summary>
        private static string TaskFailPath
        {
            get { return Path.Combine(Path.GetTempPath(), "BingWallpaperLite.taskfail"); }
        }

        private static void TaskAudit(string phase, string detail)
        {
            try
            {
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + phase + "  " + detail + "\r\n";
                File.AppendAllText(TaskAuditPath, line, new UTF8Encoding(false));
                TaskAuditTrim();
            }
            catch { }
        }

        /// <summary>超过 256 KB 就只留最后 64 KB，和脚本日志同一策略。</summary>
        private static void TaskAuditTrim()
        {
            try
            {
                FileInfo fi = new FileInfo(TaskAuditPath);
                if (!fi.Exists || fi.Length <= 256 * 1024) { return; }
                string txt = File.ReadAllText(TaskAuditPath, Encoding.UTF8);
                string tail = txt.Substring(Math.Max(0, txt.Length - 64 * 1024));
                File.WriteAllText(TaskAuditPath, tail, new UTF8Encoding(false));
            }
            catch { }
        }

        /// <summary>上次自动换图失败就报一次，报完删标记。</summary>
        private static void ReportLastTaskFailure()
        {
            try
            {
                string p = TaskFailPath;
                if (!File.Exists(p)) { return; }
                string txt = File.ReadAllText(p, Encoding.UTF8);
                try { File.Delete(p); } catch { }
                string[] parts = txt.Split('|');
                string when = parts.Length > 0 ? parts[0] : "";
                string code = parts.Length > 1 ? parts[1] : "?";
                Balloon(S.TaskFailTitle, S.TaskFailBody(when, code));
            }
            catch { }
        }

        /// <summary>接管生效中：配置存在且目录还在。</summary>
        internal static bool TakeoverActive()
        {
            string r = CurrentTakeoverRoot();
            return !string.IsNullOrEmpty(r) && Directory.Exists(r);
        }

        /// <summary>把 takeover 沙盒整个复制到 backup\takeover-时间戳\，返回目标路径。</summary>
        internal static string BackupTakeover()
        {
            string src = TakeoverDirPath;
            if (!Directory.Exists(src)) { return ""; }
            string dst = Path.Combine(root, "backup", "takeover-" + DateTime.Now.ToString("yyyyMMdd-HHmm"));
            try
            {
                CopyDirTree(src, dst);
                return dst;
            }
            catch { return ""; }
        }

        /// <summary>从备份目录恢复接管沙盒（先清掉现有沙盒，走回收站）。</summary>
        internal static bool ImportTakeover(string backupDir)
        {
            try
            {
                if (string.IsNullOrEmpty(backupDir) || !Directory.Exists(backupDir)) { return false; }
                string cfg = Path.Combine(backupDir, "config.json");
                if (!File.Exists(cfg)) { return false; }
                string dst = TakeoverDirPath;
                if (Directory.Exists(dst))
                {
                    Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(
                        dst,
                        Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                        Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin,
                        Microsoft.VisualBasic.FileIO.UICancelOption.DoNothing);
                }
                CopyDirTree(backupDir, dst);
                return Directory.Exists(dst);
            }
            catch { return false; }
        }

        private static void CopyDirTree(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            foreach (string f in Directory.GetFiles(src))
            {
                try { File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), true); } catch { }
            }
            foreach (string d in Directory.GetDirectories(src))
            {
                CopyDirTree(d, Path.Combine(dst, Path.GetFileName(d)));
            }
        }

        /// <summary>退出接管：删掉整个 takeover 沙盒（走回收站）。keepFav=false 时连收藏一起没。</summary>
        internal static void ExitTakeover(bool askKeepFav)
        {
            string dir = TakeoverDirPath;
            if (!Directory.Exists(dir)) { return; }
            try
            {
                // 整目录一律送回收站：沙盒里的图是用户自己目录的副本，误点也该能反悔。
                // 原来 askKeepFav=false 会 Directory.Delete 永久递归删除，那条分支一直不可达，
                // 留着就是隐患（将来有人传 false 就会连收藏一起永久删掉）。
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(
                    dir,
                    Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                    Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin,
                    Microsoft.VisualBasic.FileIO.UICancelOption.DoNothing);
            }
            catch { }
        }

        /// <summary>接管状态变了之后刷新托盘菜单与状态。</summary>
        internal static void RefreshAfterTakeover()
        {
            try { Program.RefreshAllPublic(); } catch { }
        }
        internal static string ThumbsDirPath { get { return Path.Combine(ActiveCacheDir, "thumbs"); } }

        /// <summary>读一个字符串配置；没有就返回 dflt。</summary>
        internal static string CfgString(string key, string dflt)
        {
            try
            {
                string v = GetStr(ReadConfig(), key);
                return string.IsNullOrEmpty(v) ? dflt : v;
            }
            catch { return dflt; }
        }

        /// <summary>读一个整型配置；没有或解析失败就返回 dflt。</summary>
        internal static int CfgInt(string key, int dflt)
        {
            try { return GetCfgInt(ReadConfig(), key, dflt); }
            catch { return dflt; }
        }

        /// <summary>写一个字符串配置。</summary>
        internal static void CfgSetString(string key, string val)
        {
            try
            {
                Dictionary<string, object> c = ReadConfig();
                SetCfg(c, key, val);
                WriteConfig(c);
            }
            catch { }
        }

        /// <summary>写一个布尔配置。</summary>
        internal static void CfgSetBool(string key, bool val)
        {
            try
            {
                Dictionary<string, object> c = ReadConfig();
                SetCfg(c, key, val);
                WriteConfig(c);
            }
            catch { }
        }

        /// <summary>写一个整型配置。</summary>
        internal static void CfgSetInt(string key, int val)
        {
            try
            {
                Dictionary<string, object> c = ReadConfig();
                SetCfg(c, key, val);
                WriteConfig(c);
            }
            catch { }
        }

        // ================================================================ 换图策略三态
        //
        //   daily  ：每天一次（默认）
        //   paused ：暂停自动换图，照常下载，只是不换桌面
        //   rotate ：随机轮播，每 rotateMinutes 分钟换一张
        //
        // 三态共用 config.json 的两个键（locked / rotate），一次写完。
        // 互斥只在这里实现：散到各个 Click 里去写，迟早出现「暂停」和「轮播」同时亮
        // 这种自相矛盾的状态，而且谁也说不清该以哪个为准。

        internal const string WallModeDaily = "daily";
        internal const string WallModePaused = "paused";
        internal const string WallModeRotate = "rotate";

        /// <summary>当前换图策略。派生值，不单独存盘。</summary>
        internal static string CurrentWallMode()
        {
            Dictionary<string, object> c;
            try { c = ReadConfig(); } catch { return WallModeDaily; }
            try
            {
                if (GetCfgBool(c, "rotate", false)) { return WallModeRotate; }
                if (GetCfgBool(c, "locked", false)) { return WallModePaused; }
            }
            catch { }
            return WallModeDaily;
        }

        /// <summary>
        /// 唯一写入口。定下一态：locked 与 rotate 一次性落盘，互斥由这里保证。
        /// 切进 rotate 时把锚点重设为现在 —— 否则上一次轮播的时间差会让它立刻又换一张。
        /// </summary>
        internal static void SetWallMode(string mode)
        {
            try
            {
                Dictionary<string, object> c = ReadConfig();
                SetCfg(c, "locked", mode == WallModePaused);
                SetCfg(c, "rotate", mode == WallModeRotate);
                if (mode == WallModeRotate)
                {
                    SetCfg(c, "lastRotate", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    // 轮播每半小时换一张，每次都弹「已换成 X」等于骚扰，
                    // 所以进轮播就顺手把换图后通知关掉；切回每日一换不自动打开，
                    // 那是用户自己的选择，程序不替他恢复。
                    SetCfg(c, "notify", false);
                }
                WriteConfig(c);
            }
            catch { }
        }

        /// <summary>
        /// 当前生效的 state.json 的日期与标题。接管时读沙盒里那份，不是安装目录那份 ——
        /// 两处 state 内容不同，读错了会显示成别人家的图。
        /// </summary>
        internal static void CurrentStateDateTitle(out string date, out string title)
        {
            date = "";
            title = "";
            try
            {
                Dictionary<string, object> st = ReadJson(StatePathNow);
                date = GetStr(st, "date");
                title = GetStr(st, "title");
            }
            catch { }
        }

        /// <summary>轮播间隔（分钟），夹在 1–720。越界不报错，直接夹住。</summary>
        internal static int RotateMinutes
        {
            get
            {
                int v = CfgInt("rotateMinutes", 30);
                return v < 1 ? 1 : (v > 720 ? 720 : v);
            }
            set { CfgSetInt("rotateMinutes", value < 1 ? 1 : (value > 720 ? 720 : value)); }
        }

        /// <summary>
        /// 缩略图三重封顶之一：清孤儿（源图已经不在）+ 按上限淘汰最久未写的。
        /// 缩略图是派生数据，重建成本低，直接删不进回收站。
        /// </summary>
        internal static void TrimThumbs()
        {
            try
            {
                string dir = ThumbsDirPath;
                if (!Directory.Exists(dir)) { return; }
                FileInfo[] files = new DirectoryInfo(dir).GetFiles("*.jpg");
                if (files.Length == 0) { return; }

                foreach (FileInfo f in files)
                {
                    string d8 = Path.GetFileNameWithoutExtension(f.Name);
                    if (d8.Length != 8) { continue; }
                    if (!SourceExists(d8)) { try { f.Delete(); } catch { } }
                }

                files = new DirectoryInfo(dir).GetFiles("*.jpg");
                int maxCount = GetCfgInt(ReadConfig(), "thumbsMaxCount", 2000);
                int maxMB = GetCfgInt(ReadConfig(), "thumbsMaxMB", 200);
                long total = 0;
                foreach (FileInfo f in files) { total += f.Length; }
                long cap = maxMB * 1024L * 1024L;
                if (files.Length <= maxCount && total <= cap) { return; }

                Array.Sort(files, delegate(FileInfo a, FileInfo b)
                {
                    return a.LastWriteTimeUtc.CompareTo(b.LastWriteTimeUtc);
                });
                int i = 0;
                while (i < files.Length && ((files.Length - i) > maxCount || total > cap))
                {
                    total -= files[i].Length;
                    try { files[i].Delete(); } catch { }
                    i++;
                }
            }
            catch { }
        }

        /// <summary>某天的原图是否还在（收藏 / 缓存 / 归档 / 老存档 / 照片墙）。</summary>
        private static bool SourceExists(string d8)
        {
            try
            {
                string[] dirs = new string[] { ActiveFavDirPath, ActiveCacheDir, ArchiveImgPath, WallDirPath };
                // 接管目录也要认：用户把整库放在别处时，缩略图不能因为「找不到源图」被反复删掉重建
                try
                {
                    string tkRoot = CurrentTakeoverRoot();
                    if (!string.IsNullOrEmpty(tkRoot)) { dirs = new string[] { ActiveFavDirPath, ActiveCacheDir, ArchiveImgPath, WallDirPath, tkRoot }; }
                }
                catch { }
                foreach (string dir in dirs)
                {
                    if (!Directory.Exists(dir)) { continue; }
                    if (Directory.GetFiles(dir, d8 + "_*.jpg").Length > 0) { return true; }
                }
                string om = OldMonthDir(d8);
                if (om != null && Directory.Exists(om))
                {
                    if (Directory.GetFiles(om, d8 + "_*.jpg").Length > 0) { return true; }
                }
            }
            catch { }
            return false;
        }
        /// <summary>是否在「这张图是哪儿」里显示每日故事，配置项 showStory，默认开。</summary>
        internal static bool ShowStory { get { try { return GetCfgBool(ReadConfig(), "showStory", true); } catch { return true; } } }

        /// <summary>取某天的每日故事：本地缓存命中直接返回，否则由脚本按需抓取并落盘。取不到返回空串。</summary>
        internal static string GetStory(string date)
        {
            try
            {
                string d = date == null ? "" : date.Replace("-", "").Trim();
                if (d.Length != 8) { return ""; }
                string json = RunScriptCapture("-StoryFor " + d);
                if (string.IsNullOrEmpty(json)) { return ""; }
                int i = json.IndexOf('{');
                if (i < 0) { return ""; }
                JavaScriptSerializer ser = new JavaScriptSerializer();
                Dictionary<string, object> o = ser.DeserializeObject(json.Substring(i)) as Dictionary<string, object>;
                if (o == null || !o.ContainsKey("story")) { return ""; }
                return o["story"] == null ? "" : o["story"].ToString();
            }
            catch { return ""; }
        }

        /// <summary>老存档按月分目录：archive\old\YYYYMM，日期不是 8 位时返回 null。</summary>
        internal static string OldMonthDir(string date)
        {
            if (string.IsNullOrEmpty(date) || date.Length != 8) { return null; }
            return Path.Combine(OldDirPath, date.Substring(0, 6));
        }

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
        private static DateTime busySince = DateTime.MinValue;
        private static string lastSyncedActual = "";
        private static bool refreshing = false;
        private static bool keepMenuOpenAfterClick = false;
        /// <summary>画廊模式：只在内存里，重启托盘即失效；开启时图落 cache\gallery，退出时清空。</summary>
        internal static bool GalleryMode = false;
        private static bool themeApplied = false;
        private static bool lastAppliedDark = false;
        private static System.Windows.Forms.Timer lockWatch;
        private static System.Windows.Forms.Timer rotateWatch;
        // 轮播补跑节流：同一自然日最多拉脚本补当天的图 3 次，网络不通时不刷屏
        private static string rotateCatchupDay = "";
        private static int rotateCatchupTries = 0;

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

        /// <summary>探测已有安装；返回安装目录（无安装返回 null），ver 带出版本号。</summary>
        private static string DetectExistingInstall(out string ver)
        {
            ver = "";
            try
            {
                using (Microsoft.Win32.RegistryKey k =
                    Microsoft.Win32.Registry.CurrentUser.OpenSubKey(UninstKeyPath, false))
                {
                    if (k != null)
                    {
                        string loc = k.GetValue("InstallLocation") as string;
                        if (!string.IsNullOrEmpty(loc))
                        {
                            string e = Path.Combine(loc, Path.GetFileName(Application.ExecutablePath));
                            if (File.Exists(e))
                            {
                                ver = (k.GetValue("DisplayVersion") as string) ?? "";
                                return loc;
                            }
                        }
                    }
                }
            }
            catch { }

            string def = InstallTarget;
            string de = Path.Combine(def, Path.GetFileName(Application.ExecutablePath));
            if (File.Exists(de))
            {
                try { ver = System.Diagnostics.FileVersionInfo.GetVersionInfo(de).FileVersion; } catch { }
                return def;
            }
            return null;
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
            string tv = "";
            string tp = DetectExistingInstall(out tv);
            using (SetupForm f = new SetupForm(InstallTarget, tp, tv))
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

            // 安装前先说声再见：把用户此刻正在用的壁纸留一份到桌面
            BackupCurrentWallpaperOnce();

            // 覆盖安装：先把正在运行的旧实例结束，否则 exe 被占用，复制必然失败
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
            Thread.Sleep(500);

            string destExe = Path.Combine(target, Path.GetFileName(Application.ExecutablePath));
            bool copied = false;
            for (int attempt = 0; attempt < 2 && !copied; attempt++)
            {
                try
                {
                    File.Copy(Application.ExecutablePath, destExe, true);
                    copied = true;
                }
                catch (Exception ex)
                {
                    if (attempt == 0) { Thread.Sleep(600); continue; }
                    MessageBox.Show(S.WarnCopyExe + ex.Message + "\r\n\r\n" + S.WarnCopyHint,
                        AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
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
        /// <summary>目标路径是否落在程序目录树内（含程序目录本身）。解析失败按不合法处理。</summary>
        internal static bool IsInsideProgramDir(string path)
        {
            if (string.IsNullOrEmpty(path)) { return true; }
            try
            {
                string p = Path.GetFullPath(path).TrimEnd('\\');
                string r = Path.GetFullPath(root).TrimEnd('\\');
                if (string.Equals(p, r, StringComparison.OrdinalIgnoreCase)) { return true; }
                return (p + "\\").StartsWith(r + "\\", StringComparison.OrdinalIgnoreCase);
            }
            catch { return true; }
        }

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

            // 离线故事包：把打包时已有的每日故事正文解到 archive\stories\，
            // 已存在的不覆盖（用户联网抓到的更新版本优先）
            try
            {
                string stDir = Path.Combine(archiveDir, "stories");
                using (Stream sz = asm.GetManifestResourceStream("stories.zip"))
                {
                    if (sz != null)
                    {
                        if (!Directory.Exists(stDir)) { Directory.CreateDirectory(stDir); }
                        string tmp = Path.Combine(Path.GetTempPath(), "bwl-stories-" + Guid.NewGuid().ToString("N") + ".zip");
                        try
                        {
                            using (FileStream fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                            {
                                byte[] buf = new byte[8192];
                                int n;
                                while ((n = sz.Read(buf, 0, buf.Length)) > 0) { fs.Write(buf, 0, n); }
                            }
                            using (System.IO.Compression.ZipArchive za =
                                   System.IO.Compression.ZipFile.OpenRead(tmp))
                            {
                                foreach (System.IO.Compression.ZipArchiveEntry e in za.Entries)
                                {
                                    string name = Path.GetFileName(e.FullName);
                                    if (name.Length == 0) { continue; }
                                    string dest = Path.Combine(stDir, name);
                                    if (File.Exists(dest)) { continue; }
                                    // 手动读流写文件：ExtractToFile 是扩展方法，这里没引入命名空间
                                    try
                                    {
                                        using (Stream es = e.Open())
                                        using (FileStream os = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None))
                                        {
                                            byte[] eb = new byte[8192];
                                            int en;
                                            while ((en = es.Read(eb, 0, eb.Length)) > 0) { os.Write(eb, 0, en); }
                                        }
                                    }
                                    catch { }
                                }
                            }
                        }
                        finally
                        {
                            try { if (File.Exists(tmp)) { File.Delete(tmp); } } catch { }
                        }
                    }
                }
            }
            catch { }

            // 离线索引快照：archive 下没有索引时才释放，让全新安装立即能搜全部历史
            try
            {
                string snap = Path.Combine(archiveDir, "all-index.json");
                if (!File.Exists(snap))
                {
                    if (!Directory.Exists(archiveDir)) { Directory.CreateDirectory(archiveDir); }
                    using (Stream ss = asm.GetManifestResourceStream("all-index.json"))
                    {
                        if (ss != null)
                        {
                            using (FileStream fs = new FileStream(snap, FileMode.Create, FileAccess.Write, FileShare.None))
                            {
                                byte[] buf = new byte[8192];
                                int n;
                                while ((n = ss.Read(buf, 0, buf.Length)) > 0) { fs.Write(buf, 0, n); }
                            }
                        }
                    }
                }
            }
            catch { }

            // 老存档索引：2009-08 ~ 2016-02 的 slug 表，同样只在缺失时释放
            try
            {
                string olds = Path.Combine(archiveDir, "old-index.json");
                if (!File.Exists(olds))
                {
                    if (!Directory.Exists(archiveDir)) { Directory.CreateDirectory(archiveDir); }
                    using (Stream osr = asm.GetManifestResourceStream("old-index.json"))
                    {
                        if (osr != null)
                        {
                            using (FileStream fs = new FileStream(olds, FileMode.Create, FileAccess.Write, FileShare.None))
                            {
                                byte[] buf = new byte[8192];
                                int n;
                                while ((n = osr.Read(buf, 0, buf.Length)) > 0) { fs.Write(buf, 0, n); }
                            }
                        }
                    }
                }
            }
            catch { }

            // wilii 兜底索引（2016-03 起），同样只在缺失时释放
            try
            {
                string wil = Path.Combine(archiveDir, "wilii-index.json");
                if (!File.Exists(wil))
                {
                    if (!Directory.Exists(archiveDir)) { Directory.CreateDirectory(archiveDir); }
                    using (Stream wsr = asm.GetManifestResourceStream("wilii-index.json"))
                    {
                        if (wsr != null)
                        {
                            using (FileStream fs = new FileStream(wil, FileMode.Create, FileAccess.Write, FileShare.None))
                            {
                                byte[] buf = new byte[8192];
                                int n;
                                while ((n = wsr.Read(buf, 0, buf.Length)) > 0) { fs.Write(buf, 0, n); }
                            }
                        }
                    }
                }
            }
            catch { }

            // 每日故事映射表（日期 -> slug）。正文不打包，按需抓取
            try
            {
                string sm = Path.Combine(archiveDir, "story-map.json");
                if (!File.Exists(sm))
                {
                    if (!Directory.Exists(archiveDir)) { Directory.CreateDirectory(archiveDir); }
                    using (Stream smr = asm.GetManifestResourceStream("story-map.json"))
                    {
                        if (smr != null)
                        {
                            using (FileStream fs = new FileStream(sm, FileMode.Create, FileAccess.Write, FileShare.None))
                            {
                                byte[] buf = new byte[8192];
                                int n;
                                while ((n = smr.Read(buf, 0, buf.Length)) > 0) { fs.Write(buf, 0, n); }
                            }
                        }
                    }
                }
            }
            catch { }

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

        /// <summary>
        /// 全局异常兜底。没有这两道钩子时，UI 线程任何一个漏网的异常都会让进程
        /// 直接消失 —— 用户只看到托盘图标不见了，而计划任务还在跑。
        /// UI 线程：记日志 + 气泡，程序继续；后台线程：拦不住退出，至少留下现场。
        /// </summary>
        private static void InstallCrashGuards()
        {
            try { Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException); } catch { }
            Application.ThreadException += delegate(object sender, System.Threading.ThreadExceptionEventArgs e)
            {
                try
                {
                    WriteCrashLog("UI", e.Exception);
                    Balloon(S.CrashTitle, S.CrashHint);
                }
                catch { }
            };
            try
            {
                AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
                {
                    try { WriteCrashLog("AppDomain", e.ExceptionObject as Exception); } catch { }
                };
            }
            catch { }
        }

        /// <summary>把崩溃现场写进同一个日志文件，格式与脚本日志一致，方便对照时间线。</summary>
        private static void WriteCrashLog(string where, Exception ex)
        {
            try
            {
                string detail = "未知异常";
                if (ex != null)
                {
                    detail = ex.GetType().Name + ": " + ex.Message;
                    string st = ex.StackTrace;
                    if (!string.IsNullOrEmpty(st))
                    {
                        string[] lines = st.Split('\n');
                        int n = lines.Length < 4 ? lines.Length : 4;
                        StringBuilder sb = new StringBuilder();
                        for (int i = 0; i < n; i++) { sb.Append(" | ").Append(lines[i].Trim()); }
                        detail += sb.ToString();
                    }
                }
                detail = detail.Replace("\r", " ").Replace("\n", " ");
                string p = Path.Combine(Path.GetTempPath(), "BingWallpaperLite.log");
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  [CRASH]  " + where + "：" + detail + "\r\n";
                File.AppendAllText(p, line, new UTF8Encoding(false));
            }
            catch { }
        }

        [STAThread]
        private static void Main(string[] args)
        {
            ResolvePaths();
            InstallCrashGuards();

            // 英文版暂时停用（2026-11-25）：界面统一走中文，不再看 config.uiLanguage 与系统语言。
            // 恢复路径两处一起改：把这里换回「读 config.uiLanguage，auto 时按系统语言决定 useEn」，
            // 再把 Strings.Init 里的 _en = false 改回 _en = en。
            S.Init(false);

            string arg0 = args.Length > 0 ? args[0].ToLowerInvariant() : "";

            // 菜单可用性自测：纯判定表，不建托盘、不碰网络。结果写 %TEMP% 并用退出码表达成败
            if (arg0 == "--menuselftest" || arg0 == "/menuselftest")
            {
                Environment.Exit(RunMenuSelfTest());
            }

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
                // 审计日志：这是「到底跑没跑」的唯一证据。不写就永远只能猜。
                Stopwatch swTask = Stopwatch.StartNew();
                TaskAudit("start", "args=[" + (tail.Length > 0 ? tail : "(none)") + "]  pid="
                                   + Process.GetCurrentProcess().Id
                                   + "  exe=" + Application.ExecutablePath);
                int rc = -1;
                try { rc = RunScriptBlocking(tail); }
                catch (Exception exRun) { TaskAudit("error", "抛出异常：" + exRun.Message); }
                swTask.Stop();
                TaskAudit("end", "rc=" + rc + "  took="
                                 + (swTask.ElapsedMilliseconds / 1000.0).ToString("0.0") + "s");
                if (rc != 0)
                {
                    try
                    {
                        File.WriteAllText(TaskFailPath,
                            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "|" + rc,
                            new UTF8Encoding(false));
                    }
                    catch { }
                }
                Environment.Exit(rc);
            }

            if (arg0 == "--takeover" || arg0 == "/takeover")
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                using (TakeoverForm f = new TakeoverForm())
                {
                    f.ShowDialog();
                }
                return;
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

                    bool firstRun = !File.Exists(StatePathNow);

                    // 上次自动换图如果失败过，这里是唯一能报出来的时机
                    try { ReportLastTaskFailure(); } catch { }

                    try { RefreshAll(); } catch { }
                    try { DetectResolutionOnce(); } catch { }
                    // 注册延后到消息循环启动后执行，避免阻塞启动
                    try { RefreshAll(); } catch { }

                    // 预取只是翻页加速用的中转文件，不跨会话；启动先清空
                    try
                    {
                        string pd = PrefetchDirPath;
                        if (Directory.Exists(pd) && Directory.GetFiles(pd, "*.jpg").Length > 0)
                        {
                            System.Threading.ThreadPool.QueueUserWorkItem(delegate
                            {
                                try { RunScriptExit("-PrefetchClear"); } catch { }
                            });
                        }
                    }
                    catch { }

                    // 画廊模式不跨会话：上次异常退出（崩溃/关机）留下的残留在这里清掉
                    try
                    {
                        string gd = GalleryDirPath;
                        if (Directory.Exists(gd) && Directory.GetFiles(gd, "*.jpg").Length > 0)
                        {
                            System.Threading.ThreadPool.QueueUserWorkItem(delegate
                            {
                                RunGalleryClear();
                                try { anchor.BeginInvoke((MethodInvoker)delegate { RefreshAll(); }); } catch { }
                            });
                        }
                    }
                    catch { }

                    // 每秒校正一次忙碌图标：脚本结束、锁文件消失后自动恢复，不依赖回调
                    lockWatch = new System.Windows.Forms.Timer();
                    lockWatch.Interval = 1000;
                    lockWatch.Tick += delegate
                    {
                        try
                        {
                            bool running = File.Exists(LockPathNow);

                            // 复位分两档：
                            //   锁已经没了还挂着 5 秒以上 → 脚本被强杀/被安全软件打断，立刻复位；
                            //   锁还在但超过 6 分钟 → 真卡死（正常脚本最长 4 分钟会被 WaitForExit 杀掉）。
                            bool stuck = false;
                            string why = "";
                            if (busy && busySince != DateTime.MinValue)
                            {
                                double sec = (DateTime.Now - busySince).TotalSeconds;
                                if (!running && sec > 5) { stuck = true; why = "锁已消失但 busy 仍挂着"; }
                                else if (sec > 360) { stuck = true; why = "busy 卡住超过 6 分钟"; }
                            }

                            if (stuck)
                            {
                                try
                                {
                                    System.IO.File.AppendAllText(
                                        Path.Combine(Path.GetTempPath(), "BingWallpaperLite.log"),
                                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") +
                                        "  [WARN]  " + why + "，强制复位" + Environment.NewLine);
                                }
                                catch { }
                                SetBusy(false);
                                running = false;
                            }

                            if (busy != running) { SetBusy(running); }
                        }
                        catch { }
                    };
                    lockWatch.Start();

                    // ---- 随机轮播：60 秒一跳，到点从本地池随机换一张。
                    // 单独一个 Timer 而不是塞进 lockWatch：那个每秒一跳、只干图标校正，
                    // 混进来会让「换图」和「刷图标」互相牵连，出问题不好定位。
                    rotateWatch = new System.Windows.Forms.Timer();
                    rotateWatch.Interval = 60000;
                    rotateWatch.Tick += delegate { try { RotateTick(); } catch { } };
                    rotateWatch.Start();

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

                    // 15 秒后静默查一次新版；每天一次，失败不重试
                    try { StartUpdateWatch(); } catch { }

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

