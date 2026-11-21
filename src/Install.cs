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
        // ---------------------------------------------------------------- 安装 / 卸载

        private static void EnsureRegistered(bool verbose)
        {
            bool changed = false;
            if (!IsStartupPointingHere()) { SetStartup(true); changed = true; }
            if (!IsTaskPointingHere()) { RegisterTask(); changed = true; }
            SetUninstallEntry();
            if (changed && verbose)
            {
                MessageBox.Show(S.MsgRegistered, AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        /// <summary>自启项存在且指向当前 exe。</summary>
        private static bool IsStartupPointingHere()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    if (k == null) { return false; }
                    object v = k.GetValue(RunValue);
                    if (v == null) { return false; }
                    string s = v.ToString().Trim().Trim('"');
                    return string.Equals(s, Application.ExecutablePath, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch { return false; }
        }

        /// <summary>计划任务存在且动作指向当前脚本。</summary>
        private static bool IsTaskPointingHere()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "schtasks.exe";
                psi.Arguments = "/query /tn \"" + TaskName + "\" /xml ONE";
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                using (Process p = Process.Start(psi))
                {
                    string xml = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(10000);
                    if (p.ExitCode != 0) { return false; }
                    return xml.IndexOf(scriptPath, StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
            catch { return false; }
        }

        private static void SetUninstallEntry()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(UninstKeyPath))
                {
                    if (k == null) { return; }
                    k.SetValue("DisplayName", AppName);
                    k.SetValue("DisplayVersion", AppVersion);
                    k.SetValue("Publisher", "BingWallpaperChronicle");
                    k.SetValue("DisplayIcon", Application.ExecutablePath + ",0");
                    k.SetValue("UninstallString", "\"" + Application.ExecutablePath + "\" --uninstall");
                    k.SetValue("InstallLocation", root);
                    k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                    long size = 0;
                    try
                    {
                        foreach (string f in Directory.GetFiles(root)) { size += new FileInfo(f).Length; }
                    }
                    catch { }
                    if (size > 0) { k.SetValue("EstimatedSize", (int)(size / 1024), RegistryValueKind.DWord); }
                }
            }
            catch { }
        }

        private static void RemoveUninstallEntry()
        {
            try { Registry.CurrentUser.DeleteSubKeyTree(UninstKeyPath, false); } catch { }
        }

        /// <summary>安装 / 覆盖安装前，无条件把当前桌面壁纸备份一份到桌面；同名已存在则跳过。</summary>
        private static void BackupCurrentWallpaperOnce()
        {
            try
            {
                string actual = Registry.GetValue(
                    @"HKEY_CURRENT_USER\Control Panel\Desktop", "Wallpaper", "") as string;
                if (string.IsNullOrEmpty(actual) || !File.Exists(actual)) { return; }

                string name = Path.GetFileName(actual);
                if (string.IsNullOrEmpty(name)) { return; }

                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string dest = Path.Combine(desktop, "壁纸备份_" + name);
                if (File.Exists(dest)) { return; }

                File.Copy(actual, dest, false);
            }
            catch { }
        }

        private static void DoInstall(bool interactive)
        {
            EnsureRegistered(true);
            if (interactive)
            {
                try { Process.Start(Application.ExecutablePath); } catch { }
            }
        }

        private static void DoUninstall()
        {
            string[] favFiles = SafeGetFiles(favDir);
            string[] cacheFiles = SafeGetFiles(cacheDir);
            string[] archFiles = SafeGetFiles(archiveImgDir);
            int favCount = favFiles.Length;
            int archCount = archFiles.Length;

            double cacheMB = 0;
            try
            {
                foreach (string cf in cacheFiles)
                {
                    cacheMB += new FileInfo(cf).Length / 1024.0 / 1024.0;
                }
            }
            catch { }

            using (UninstallForm f = new UninstallForm(favCount, archCount, cacheFiles.Length, cacheMB, root))
            {
                if (f.ShowDialog() != DialogResult.OK) { return; }
                bool keepFav = f.KeepFavorites;

                try { SetStartup(false); } catch { }
                try { RunSchtasks("/delete /tn \"" + TaskName + "\" /f"); } catch { }
                try { RemoveUninstallEntry(); } catch { }

                // 归档处理：月份索引一律清（纯缓存）；图片按勾选
                bool keepArch = f.KeepArchive;
                try
                {
                    if (Directory.Exists(archiveDir))
                    {
                        foreach (string jf in Directory.GetFiles(archiveDir, "*.json"))
                        {
                            try
                            {
                                Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                                    jf,
                                    Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                                    Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                            }
                            catch { }
                        }
                    }
                    if (!keepArch && archCount > 0 && Directory.Exists(archiveImgDir))
                    {
                        Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(
                            archiveImgDir,
                            Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                            Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                    }
                }
                catch { }

                // 清收藏（用户明确取消勾选时才做）
                if (!keepFav && favCount > 0)
                {
                    try
                    {
                        Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(
                            favDir,
                            Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                            Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                    }
                    catch { }
                }

                // 清缓存：保留当前正在显示的那张，否则桌面会变黑
                string curWp = GetItemPropertyWallpaper();
                int cleared = 0;
                try
                {
                    foreach (string cf in SafeGetFiles(cacheDir))
                    {
                        if (string.Equals(cf, curWp, StringComparison.OrdinalIgnoreCase)) { continue; }
                        try
                        {
                            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                                cf,
                                Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                                Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                            cleared++;
                        }
                        catch { }
                    }
                }
                catch { }

                string favMsg = keepFav
                    ? S.FavKeptN(favCount)
                    : (favCount > 0 ? S.FavGone : S.FavEmpty);
                string cacheMsg = cleared > 0
                    ? S.CacheClearedN(cleared)
                    : S.CacheClean;

                MessageBox.Show(S.DlDone + "\r\n" +
                    S.DlStartup + "\r\n" +
                    S.DlTask + "\r\n" +
                    S.DlUninstEntry + "\r\n" +
                    cacheMsg + "\r\n" +
                    S.DlIdxCleared + "\r\n" +
                    (keepArch
                        ? S.ArchKeptN(archCount)
                        : (archCount > 0 ? S.ArchGoneLine : S.ArchNoneLine)) +
                    favMsg + "\r\n\r\n" +
                    S.DlKeepFiles + "\r\n" + root,
                    AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);

                if (tray != null) { tray.Visible = false; }
                Application.Exit();
            }
        }

        // ---------------------------------------------------------------- 计划任务

        private static bool IsTaskRegistered()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "schtasks.exe";
                psi.Arguments = "/query /tn \"" + TaskName + "\"";
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                using (Process p = Process.Start(psi))
                {
                    p.StandardOutput.ReadToEnd();
                    p.WaitForExit(8000);
                    return p.ExitCode == 0;
                }
            }
            catch { return false; }
        }

        private static string RunSchtasksOutput(string arguments, out int exitCode)
        {
            exitCode = -1;
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "schtasks.exe";
                psi.Arguments = arguments;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                // schtasks 用系统 ANSI 代码页输出（中文系统是 GBK），按 UTF-8 读会乱码
                try { psi.StandardOutputEncoding = Encoding.GetEncoding(0); } catch { }
                using (Process p = Process.Start(psi))
                {
                    string outp = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(10000);
                    exitCode = p.ExitCode;
                    return outp;
                }
            }
            catch { return ""; }
        }

        private static int RunSchtasks(string arguments)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "schtasks.exe";
                psi.Arguments = arguments;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                using (Process p = Process.Start(psi))
                {
                    p.StandardOutput.ReadToEnd();
                    p.WaitForExit(20000);
                    return p.ExitCode;
                }
            }
            catch { return -1; }
        }

        private static void RegisterTask()
        {
            try
            {
                string xml = BuildTaskXml();
                string tmp = Path.Combine(Path.GetTempPath(), "BingWallpaperLite.task.xml");
                File.WriteAllText(tmp, xml, new UnicodeEncoding(false, true));

                int code = RunSchtasks("/create /tn \"" + TaskName + "\" /xml \"" + tmp + "\" /f");
                try { File.Delete(tmp); } catch { }

                if (code != 0)
                {
                    MessageBox.Show(S.WarnTaskFail + code + S.WarnTaskFail2,
                        AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(S.WarnTaskEx + ex.Message, AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static int GetUpdateMinutes()
        {
            try
            {
                string s = GetStr(ReadConfig(), "updateTime");
                if (s.Length >= 4 && s.Contains(":"))
                {
                    string[] parts = s.Split(':');
                    int hh = Convert.ToInt32(parts[0]);
                    int mm = Convert.ToInt32(parts[1]);
                    if (hh >= 0 && hh < 24 && mm >= 0 && mm < 60) { return hh * 60 + mm; }
                }
            }
            catch { }
            return 10;
        }

        private static string BuildTaskXml()
        {
            string user = Environment.UserDomainName + "\\" + Environment.UserName;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-16\"?>");
            sb.AppendLine("<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">");
            sb.AppendLine("  <RegistrationInfo>");
            sb.AppendLine("    <Author>" + XmlEscape(AppName) + "</Author>");
            sb.AppendLine("    <Description>" + XmlEscape(AppName + S.TaskDesc) + "</Description>");
            sb.AppendLine("  </RegistrationInfo>");
            sb.AppendLine("  <Triggers>");
            // 主时间由配置决定；两个补查点自动 +8h / +12h
            int baseMin = GetUpdateMinutes();
            foreach (int off in new int[] { 0, 8 * 60, 12 * 60 })
            {
                int total = (baseMin + off) % (24 * 60);
                string hhmm = (total / 60).ToString("00") + ":" + (total % 60).ToString("00") + ":00";
                sb.AppendLine("    <CalendarTrigger>");
                sb.AppendLine("      <StartBoundary>2020-01-01T" + hhmm + "</StartBoundary>");
                sb.AppendLine("      <Enabled>true</Enabled>");
                sb.AppendLine("      <ScheduleByDay><DaysInterval>1</DaysInterval></ScheduleByDay>");
                sb.AppendLine("    </CalendarTrigger>");
            }
            sb.AppendLine("    <LogonTrigger>");
            sb.AppendLine("      <Enabled>true</Enabled>");
            sb.AppendLine("      <UserId>" + XmlEscape(user) + "</UserId>");
            sb.AppendLine("    </LogonTrigger>");
            sb.AppendLine("  </Triggers>");
            sb.AppendLine("  <Principals>");
            sb.AppendLine("    <Principal id=\"Author\">");
            sb.AppendLine("      <UserId>" + XmlEscape(user) + "</UserId>");
            sb.AppendLine("      <LogonType>InteractiveToken</LogonType>");
            sb.AppendLine("      <RunLevel>LeastPrivilege</RunLevel>");
            sb.AppendLine("    </Principal>");
            sb.AppendLine("  </Principals>");
            sb.AppendLine("  <Settings>");
            sb.AppendLine("    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>");
            sb.AppendLine("    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>");
            sb.AppendLine("    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>");
            sb.AppendLine("    <AllowHardTerminate>true</AllowHardTerminate>");
            sb.AppendLine("    <StartWhenAvailable>true</StartWhenAvailable>");
            sb.AppendLine("    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>");
            sb.AppendLine("    <IdleSettings><StopOnIdleEnd>false</StopOnIdleEnd><RestartOnIdle>false</RestartOnIdle></IdleSettings>");
            sb.AppendLine("    <AllowStartOnDemand>true</AllowStartOnDemand>");
            sb.AppendLine("    <Enabled>true</Enabled>");
            sb.AppendLine("    <Hidden>false</Hidden>");
            sb.AppendLine("    <RunOnlyIfIdle>false</RunOnlyIfIdle>");
            sb.AppendLine("    <ExecutionTimeLimit>PT10M</ExecutionTimeLimit>");
            sb.AppendLine("    <RestartOnFailure><Interval>PT15M</Interval><Count>3</Count></RestartOnFailure>");
            sb.AppendLine("  </Settings>");
            sb.AppendLine("  <Actions Context=\"Author\">");
            sb.AppendLine("    <Exec>");
            sb.AppendLine("      <Command>" + XmlEscape(Application.ExecutablePath) + "</Command>");
            sb.AppendLine("      <Arguments>--run</Arguments>");
            sb.AppendLine("    </Exec>");
            sb.AppendLine("  </Actions>");
            sb.AppendLine("</Task>");
            return sb.ToString();
        }

        private static string XmlEscape(string s)
        {
            if (s == null) { return ""; }
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
                    .Replace("\"", "&quot;").Replace("'", "&apos;");
        }

        // ---------------------------------------------------------------- 开机启动

        private static bool IsStartupEnabled()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    if (k == null) { return false; }
                    object v = k.GetValue(RunValue);
                    return v != null && v.ToString().Length > 0;
                }
            }
            catch { return false; }
        }

        private static void SetStartup(bool on)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (k == null) { return; }
                    if (on) { k.SetValue(RunValue, "\"" + Application.ExecutablePath + "\""); }
                    else { k.DeleteValue(RunValue, false); }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(S.WarnStartupFail + ex.Message, AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
