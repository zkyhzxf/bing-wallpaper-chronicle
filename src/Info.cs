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
        // ---------------------------------------------------------------- 信息

        internal static void OpenUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) { return; }
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch { }
        }

        /// <summary>把版权描述压成搜索关键词：去掉 (© …)，取第一个逗号前。</summary>
        private static string BuildSearchUrl(Dictionary<string, object> state)
        {
            string direct = GetStr(state, "searchUrl");
            if (direct.Length > 0) { return direct; }

            string cpr = GetStr(state, "copyright");
            if (cpr.Length == 0) { return ""; }

            int cut = cpr.IndexOf('(');
            if (cut < 0) { cut = cpr.IndexOf('（'); }
            string kw = cut > 0 ? cpr.Substring(0, cut) : cpr;

            cut = kw.IndexOf('，');
            if (cut < 0) { cut = kw.IndexOf(','); }
            if (cut > 0) { kw = kw.Substring(0, cut); }

            kw = kw.Trim();
            if (kw.Length == 0) { return ""; }
            return "https://www.bing.com/search?q=" + Uri.EscapeDataString(kw) + "&form=hpcapt&mkt=zh-cn";
        }

        private static void ShowInfo()
        {
            Dictionary<string, object> state = ReadJson(statePath);
            if (state == null)
            {
                MessageBox.Show(S.InfoNone, AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string date = GetStr(state, "date");
            string title = GetStr(state, "title");
            string copyright = GetStr(state, "copyright");
            string file = GetStr(state, "file");

            // 索引里有更完整的标题/版权时优先用
            Dictionary<string, object> index = ReadJson(indexPath);
            if (index != null && index.ContainsKey(date))
            {
                Dictionary<string, object> entry = index[date] as Dictionary<string, object>;
                if (entry != null)
                {
                    string t2 = GetStr(entry, "title");
                    if (t2.Length > 0) { title = t2; }
                    string c2 = GetStr(entry, "copyright");
                    if (c2.Length > 0) { copyright = c2; }
                }
            }

            string sizeText = "";
            try
            {
                if (file.Length > 0 && File.Exists(file))
                {
                    sizeText = ((new FileInfo(file).Length) / 1024.0 / 1024.0).ToString("0.00") + " MB";
                }
            }
            catch { }

            using (InfoForm f = new InfoForm(date, title, copyright, file, sizeText, BuildSearchUrl(state)))
            {
                f.ShowDialog(anchor);
            }
        }
        private static void StatDir(string dir, out int count, out double mb)
        {
            count = 0;
            mb = 0;
            try
            {
                if (!Directory.Exists(dir)) { return; }
                string[] fs = Directory.GetFiles(dir, "*.jpg");
                count = fs.Length;
                long sz = 0;
                foreach (string f in fs) { try { sz += new FileInfo(f).Length; } catch { } }
                mb = sz / 1024.0 / 1024.0;
            }
            catch { }
        }

        private static void ShowAbout()
        {
            int cCount, fCount, aCount;
            double cMb, fMb, aMb;
            StatDir(cacheDir, out cCount, out cMb);
            StatDir(favDir, out fCount, out fMb);
            StatDir(archiveImgDir, out aCount, out aMb);

            bool taskOn = IsTaskRegistered();
            bool runOn = IsStartupEnabled();
            bool locked = GetCfgBool(ReadConfig(), "locked", false);
            bool archiveOn = GetCfgBool(ReadConfig(), "archive", true);
            int months = 0;
            try
            {
                if (Directory.Exists(archiveDir)) { months = Directory.GetFiles(archiveDir, "*.json").Length; }
            }
            catch { }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine(AppName + "  v" + AppVersion);
            sb.AppendLine();
            sb.AppendLine(S.AboutTagline);
            sb.AppendLine();
            sb.AppendLine(S.AboutCanDo);
            sb.AppendLine(S.AboutFeat1);
            sb.AppendLine(S.AboutFeat2);
            sb.AppendLine(S.AboutFeat3);
            sb.AppendLine(S.AboutFeat4);
            sb.AppendLine();
            sb.AppendLine(S.AboutSources);
            sb.AppendLine(S.AboutDailyLine + "bingwallpaper.microsoft.com/api/BWC/getHPImages");
            sb.AppendLine(S.AboutOrigLine);
            sb.AppendLine(S.AboutArchLine(archiveOn, months));
            sb.AppendLine(S.AboutBackupLine);
            sb.AppendLine(S.AboutCopyLine);
            sb.AppendLine();
            sb.AppendLine(S.AboutComponents);
            sb.AppendLine("· " + Path.GetFileName(Application.ExecutablePath) + "    " + S.AboutTrayComp);
            sb.AppendLine("· " + Path.GetFileName(scriptPath) + "       " + S.AboutScriptComp);
            sb.AppendLine(S.AboutTaskLbl + (taskOn ? S.StateReg : S.StateUnreg));
            sb.AppendLine(S.AboutStartupLbl + (runOn ? S.StateOn : S.StateOff));
            sb.AppendLine(S.AboutAutoLbl + (locked ? S.StatePaused : S.StateRunning));
            sb.AppendLine();
            sb.AppendLine(S.AboutStorage);
            sb.AppendLine(S.AboutProgramDir + root);
            sb.AppendLine(S.AboutStore(cCount, cMb, fCount, fMb, aCount, aMb));
            sb.AppendLine(S.AboutLogDir + logPath);
            sb.AppendLine();
            sb.AppendLine(S.AboutEngineLbl + PowerShellExe);
            sb.AppendLine();
            sb.AppendLine(S.AboutFooter);
            using (AboutForm f = new AboutForm(sb.ToString()))
            {
                f.ShowDialog();
            }
        }

        private static void OpenTarget(string target)
        {
            try
            {
                if (Directory.Exists(target))
                {
                    Process.Start("explorer.exe", "\"" + target + "\"");
                }
                else if (File.Exists(target))
                {
                    if (target.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
                        Process.Start("notepad.exe", "\"" + target + "\"");
                    else
                        Process.Start("explorer.exe", "/select,\"" + target + "\"");
                }
                else
                {
                    MessageBox.Show(S.WarnPathMissing + "\r\n" + target, AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(S.WarnOpenFailed + ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static void Balloon(string title, string text)
        {
            try
            {
                if (tray == null) { return; }
                tray.BalloonTipTitle = title;
                tray.BalloonTipText = Clip(text, 200);
                tray.BalloonTipIcon = ToolTipIcon.Info;
                tray.ShowBalloonTip(4000);
            }
            catch { }
        }

        /// <summary>壁纸是否落在本程序管理的目录里（cache / favorites / archive\images）。</summary>
        private static bool IsManagedWallpaper(string path)
        {
            string p;
            try { p = Path.GetFullPath(path); }
            catch { return false; }

            string[] dirs = new string[] { cacheDir, favDir, archiveImgDir };
            foreach (string d in dirs)
            {
                if (string.IsNullOrEmpty(d)) { continue; }
                string dd;
                try { dd = Path.GetFullPath(d).TrimEnd('\\') + "\\"; }
                catch { continue; }
                if (p.StartsWith(dd, StringComparison.OrdinalIgnoreCase)) { return true; }
            }
            return false;
        }

        /// <summary>检测桌面壁纸是否被外部改动；若是，把原先管理的那张备份到桌面并提示。</summary>
        private static void SyncExternalWallpaper()
        {
            try
            {
                string actual = Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "Wallpaper", "") as string;
                if (string.IsNullOrEmpty(actual) || !File.Exists(actual)) { return; }

                // 防重入：同一张外部壁纸只处理一次，避免反复复制与弹泡
                if (string.Equals(actual, lastSyncedActual, StringComparison.OrdinalIgnoreCase)) { return; }
                lastSyncedActual = actual;

                // 本程序管理的三处目录里的图，一律视为自己换的，不备份
                if (IsManagedWallpaper(actual)) { return; }

                // 系统转码副本（TranscodedWallpaper）没有备份价值
                try
                {
                    string themes = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "Microsoft", "Windows", "Themes");
                    string td = Path.GetFullPath(themes).TrimEnd('\\') + "\\";
                    if (Path.GetFullPath(actual).StartsWith(td, StringComparison.OrdinalIgnoreCase)) { return; }
                }
                catch { }

                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string name = Path.GetFileName(actual);
                if (string.IsNullOrEmpty(name)) { return; }
                string dest = Path.Combine(desktop, "壁纸备份_" + name);
                bool copied = false;
                if (!File.Exists(dest))
                {
                    try { File.Copy(actual, dest, false); copied = true; }
                    catch { }
                }

                System.Text.RegularExpressions.Match mm =
                    System.Text.RegularExpressions.Regex.Match(name, @"^(\d{8})");
                if (mm.Success)
                {
                    string d8 = mm.Groups[1].Value;
                    Dictionary<string, object> ns = new Dictionary<string, object>();
                    ns["date"] = d8;
                    ns["file"] = actual;
                    ns["title"] = LookupTitle(d8);
                    ns["copyright"] = "";
                    ns["searchUrl"] = "";
                    ns["imageId"] = "";
                    ns["appliedDate"] = d8;
                    ns["updatedAt"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    string json = new JavaScriptSerializer().Serialize(ns);
                    if (!string.IsNullOrEmpty(json)) { File.WriteAllText(statePath, json, new UTF8Encoding(false)); }
                }

                if (copied) { Balloon(S.ExtChangedTtl, S.ExtChangedMsg + " " + Path.GetFileName(dest)); }
            }
            catch { }
        }
    }
}
