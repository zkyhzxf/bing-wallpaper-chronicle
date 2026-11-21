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
        // ---------------------------------------------------------------- 执行脚本

        /// <summary>同步执行脚本并返回退出码，供 --run 模式使用。</summary>
        /// <summary>跑脚本并返回其标准输出（用于 -Query 这类只读取数据的调用）。</summary>
        internal static string RunScriptCapture(string arguments)
        {
            string outFile = Path.Combine(Path.GetTempPath(), "bwl-output.json");
            try { File.WriteAllText(outFile, "", new UTF8Encoding(false)); } catch { }

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = PowerShellExe;
                psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + scriptPath + "\" " + arguments;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                try { psi.StandardOutputEncoding = Encoding.UTF8; psi.StandardErrorEncoding = Encoding.UTF8; } catch { }
                using (Process p = Process.Start(psi))
                {
                    string outText = p.StandardOutput.ReadToEnd();
                    string errText = p.StandardError.ReadToEnd();
                    if (!p.WaitForExit(120000)) { try { p.Kill(); } catch { } }

                    if (!string.IsNullOrEmpty(outText)) { return outText; }
                    try
                    {
                        if (File.Exists(outFile))
                        {
                            string backup = File.ReadAllText(outFile, Encoding.UTF8);
                            if (!string.IsNullOrEmpty(backup)) { return backup; }
                        }
                    }
                    catch { }

                    try
                    {
                        File.AppendAllText(Path.Combine(Path.GetTempPath(), "bwl-search-debug.log"),
                            DateTime.Now.ToString("HH:mm:ss") + "  CAPTURE-EMPTY  stderr=" +
                            (errText ?? "").Replace(Environment.NewLine, " ") + Environment.NewLine);
                    }
                    catch { }
                    return "";
                }
            }
            catch (Exception ex)
            {
                try
                {
                    File.AppendAllText(Path.Combine(Path.GetTempPath(), "bwl-search-debug.log"),
                        DateTime.Now.ToString("HH:mm:ss") + "  CAPTURE-EX  " + ex.Message + Environment.NewLine);
                }
                catch { }
                return "";
            }
        }

        private static int RunScriptBlocking(string arguments)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = PowerShellExe;
                psi.Arguments = "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File \"" + scriptPath + "\" " + arguments;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                using (Process p = Process.Start(psi))
                {
                    p.StandardOutput.ReadToEnd();
                    if (!p.WaitForExit(240000)) { try { p.Kill(); } catch { } return -1; }
                    return p.ExitCode;
                }
            }
            catch { return -1; }
        }

        private static void RunScript(string arguments)
        {
            RunScriptEx(arguments, "");
        }

        private static void RunScriptEx(string arguments, string opMode)
        {
            if (busy) { return; }
            if (!File.Exists(scriptPath))
            {
                MessageBox.Show(S.WarnNoScript + "\r\n" + scriptPath + "\r\n\r\n" + S.WarnNoScript2,
                    AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            SetBusy(true);
            ThreadPool.QueueUserWorkItem(delegate
            {
                int code = -1;
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo();
                    psi.FileName = PowerShellExe;
                    psi.Arguments = "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File \"" + scriptPath + "\" " + arguments;
                    psi.UseShellExecute = false;
                    psi.CreateNoWindow = true;
                    psi.RedirectStandardOutput = true;
                    psi.RedirectStandardError = true;

                    using (Process p = Process.Start(psi))
                    {
                        try { p.StandardOutput.ReadToEnd(); }
                        catch { }
                        if (!p.WaitForExit(240000))
                        {
                            try { p.Kill(); } catch { }
                            code = -1;
                        }
                        else
                        {
                            code = p.ExitCode;
                        }
                    }
                }
                catch
                {
                    code = -1;
                }

                anchor.BeginInvoke((MethodInvoker)delegate
                {
                    SetBusy(false);
                    RefreshAll();

                    if (code == 0 || code == 8)
                    {
                        if (opMode == "firstrun")
                        {
                            Balloon(S.BReady,
                                S.BReadyHint);
                        }
                        else if (opMode == "cleanarch")
                        {
                            RefreshAll();
                            Balloon(S.BCleared, S.CleanedHint);
                        }
                        else if (opMode == "fav")
                        {
                            Balloon(S.BFavDone, S.FavDoneHint);
                            RefreshAll();
                        }
                        else if (opMode == "unfav")
                        {
                            Balloon(S.BFavUndo, S.FavUndoHint);
                            RefreshAll();
                        }
                        else if (GetCfgBool(ReadConfig(), "notify", false))
                        {
                            Dictionary<string, object> st = ReadJson(statePath);
                            string ttl = GetStr(st, "title");
                            if (ttl.Length == 0) { ttl = S.FallbackTitle; }
                            if (GetCfgBool(ReadConfig(), "locked", false))
                            {
                                Balloon(S.BSwitched, ttl + S.SwitchedPaused);
                            }
                            else
                            {
                                Balloon(S.BUpdated, ttl);
                            }
                        }

                        // 降级提示不受 notify 控制：质量异常必须告知
                        if (code == 8)
                        {
                            Balloon(S.BDegraded, S.DegradedHint);
                        }
                    }
                    else if (code == 4)
                    {
                        if (opMode == "next") { Balloon(S.BLatest, S.LatestHint); }
                        else if (opMode == "prev") { Balloon(S.BOldest, S.OldestHint); }
                        else { Balloon(S.BEdge, S.EdgeHint); }
                    }
                    else if (code == 6)
                    {
                        Balloon(S.BNeedArch, S.NeedArchHint);
                    }
                    else if (code == 7)
                    {
                        Balloon(S.BArchDown, S.ArchDownHint);
                    }
                    else if (code == 5)
                    {
                        Balloon(S.BNoImage, S.NoImageHint);
                    }
                    else if (code == 3)
                    {
                        Balloon(S.BNoFav, S.NoFavHint);
                    }
                    else if (code == 9)
                    {
                        // 今天的图还没发布，静默等补跑，不当作错误
                        if (!menu.Visible) { Balloon(S.BWaiting, S.BWaitingHint); }
                    }
                    else if (code == 2)
                    {
                        // 撞锁：菜单多半还开着，图标也会变橙点，不再弹气泡打扰
                        if (!menu.Visible) { Balloon(S.BBusy, S.IsEn ? "Another task is running, please wait" : "另一个任务正在进行，请稍候"); }
                    }
                    else
                    {
                        Balloon(S.BFailed, S.IsEn ? "Right-click → Open log for details" : "右键 → 打开日志 查看原因");
                    }
                });
            });
        }
    }
}
