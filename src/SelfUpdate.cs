using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows.Forms;

namespace BingWallpaperLite
{
    /// <summary>
    /// 静默查新版 + 一键升级。
    /// 只对 v* tag 的 Release 生效：CI 会把安装包传成 BingWallpaperChronicle-vX.Y.Z-Setup.exe。
    /// </summary>
    internal static partial class Program
    {
        // 一个进程只查一次：定时器可能被反复启动（托盘隐藏再显示），不能每次都打 GitHub
        private static bool updaterStarted = false;

        /// <summary>
        /// 托盘起来 15 秒后查一次。太早会和首跑换图抢带宽，太晚人已经走开了。
        /// 每天只查一次（lastUpdateCheck 记日期），不是每次启动都查。
        /// </summary>
        internal static void StartUpdateWatch()
        {
            if (updaterStarted) { return; }
            updaterStarted = true;

            System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
            t.Interval = 15000;
            t.Tick += delegate
            {
                t.Stop();
                t.Dispose();
                ThreadPool.QueueUserWorkItem(delegate { CheckUpdateQuiet(); });
            };
            t.Start();
        }

        private static void CheckUpdateQuiet()
        {
            string today = DateTime.Today.ToString("yyyyMMdd");

            // 节流：今天查过了就不再打。失败也算查过 —— 网络不通时不该被反复重试。
            try
            {
                Dictionary<string, object> c = ReadConfig();
                if (string.Equals(GetStr(c, "lastUpdateCheck"), today, StringComparison.Ordinal)) { return; }
                SetCfg(c, "lastUpdateCheck", today);
                WriteConfig(c);
            }
            catch { }

            UpdateProbe p = null;
            try { p = CheckLatestRelease(); } catch { }
            if (p == null || !p.Ok) { return; }

            string latest = p.Tag.Trim().TrimStart('v', 'V');
            if (latest.Length == 0) { return; }
            if (CompareVersion(latest, AppVersion) <= 0) { return; }

            // 用户点过「跳过这个版本」：同版本不再提，更高的版本照常提
            string skip = "";
            try { skip = GetStr(ReadConfig(), "skipVersion"); } catch { }
            if (string.Equals(skip, latest, StringComparison.Ordinal)) { return; }

            try
            {
                anchor.BeginInvoke((MethodInvoker)delegate { ShowUpdateDialog(latest, p); });
            }
            catch { }
        }

        private static void ShowUpdateDialog(string latest, UpdateProbe p)
        {
            string size = "";
            if (p.AssetSize > 0)
            {
                size = (p.AssetSize / 1024.0 / 1024.0).ToString("0.0") + " MB";
            }

            using (UpdateForm f = new UpdateForm(latest, AppVersion, size))
            {
                DialogResult d = f.ShowDialog();
                if (d == DialogResult.Yes)
                {
                    ThreadPool.QueueUserWorkItem(delegate { DownloadAndRun(p); });
                }
                else if (d == DialogResult.Ignore)
                {
                    CfgSetString("skipVersion", latest);
                }
            }
        }

        /// <summary>下载安装包并启动。任何一步失败都退回「打开 Release 页」，不让人卡死在这里。</summary>
        private static void DownloadAndRun(UpdateProbe p)
        {
            if (string.IsNullOrEmpty(p.AssetUrl))
            {
                try { OpenReleasesPage(); } catch { }
                return;
            }

            string dir = Path.Combine(Path.GetTempPath(), "BingWallpaperLite-update");
            try { if (!Directory.Exists(dir)) { Directory.CreateDirectory(dir); } }
            catch { return; }

            string name = string.IsNullOrEmpty(p.AssetName)
                ? ("BingWallpaperLite-" + p.Tag + "-Setup.exe")
                : p.AssetName;
            string dest = Path.Combine(dir, name);

            // 上次下到一半的文件留着续传；已经完整就不再下
            if (!LooksLikeExe(dest) && !DownloadFile(p.AssetUrl, dest))
            {
                try { OpenReleasesPage(); } catch { }
                return;
            }
            if (!LooksLikeExe(dest))
            {
                try { File.Delete(dest); } catch { }
                try { OpenReleasesPage(); } catch { }
                return;
            }

            try { Process.Start(dest); } catch { try { OpenReleasesPage(); } catch { } }

            // 安装程序要覆盖正在运行的 exe，托盘必须先退
            try { anchor.BeginInvoke((MethodInvoker)delegate { Application.Exit(); }); } catch { }
        }

        /// <summary>只认 PE 头。下到一半的 HTML 错误页也是 200，别拿它去 Start。</summary>
        private static bool LooksLikeExe(string path)
        {
            try
            {
                if (!File.Exists(path)) { return false; }
                if (new FileInfo(path).Length < 65536) { return false; }
                using (FileStream fs = File.OpenRead(path))
                {
                    byte[] head = new byte[2];
                    if (fs.Read(head, 0, 2) != 2) { return false; }
                    return head[0] == (byte)'M' && head[1] == (byte)'Z';
                }
            }
            catch { return false; }
        }

        /// <summary>三路回退：gh-proxy 前缀 → 直连 → 配置里的代理。全失败才认输。</summary>
        private static bool DownloadFile(string url, string dest)
        {
            try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; } catch { }

            List<string> urls = new List<string>();
            urls.Add("https://gh-proxy.com/" + url);
            urls.Add(url);

            for (int i = 0; i < urls.Count; i++)
            {
                if (TryDownload(urls[i], dest, null)) { return true; }
            }

            string proxy = "";
            try { proxy = GetStr(ReadConfig(), "proxy"); } catch { }
            if (!string.IsNullOrEmpty(proxy))
            {
                for (int i = 0; i < urls.Count; i++)
                {
                    if (TryDownload(urls[i], dest, proxy)) { return true; }
                }
            }
            return false;
        }

        private static bool TryDownload(string url, string dest, string proxy)
        {
            try
            {
                long have = 0;
                try { if (File.Exists(dest)) { have = new FileInfo(dest).Length; } } catch { }

                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "GET";
                req.UserAgent = "BingWallpaperLite/" + AppVersion;
                req.Timeout = 30000;
                req.ReadWriteTimeout = 120000;
                req.AllowAutoRedirect = true;
                if (!string.IsNullOrEmpty(proxy)) { req.Proxy = new WebProxy(proxy.Trim()); }
                if (have > 0) { req.AddRange(have); }

                using (WebResponse resp = req.GetResponse())
                {
                    // 只有真拿到 206 才续写；服务端忽略 Range 时必须从头写，
                    // 否则会把整个文件追加到半截文件后面，得到一个更大的坏包
                    HttpWebResponse hr = resp as HttpWebResponse;
                    bool append = have > 0 && hr != null && hr.StatusCode == HttpStatusCode.PartialContent;

                    using (Stream s = resp.GetResponseStream())
                    using (FileStream fs = new FileStream(dest,
                               append ? FileMode.Append : FileMode.Create, FileAccess.Write))
                    {
                        byte[] buf = new byte[81920];
                        int read;
                        while ((read = s.Read(buf, 0, buf.Length)) > 0) { fs.Write(buf, 0, read); }
                    }
                }
                return true;
            }
            catch { return false; }
        }
    }
}
