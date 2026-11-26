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
        /// <summary>
        /// 画廊模式下给「换图」类指令补上 -Gallery。
        /// 只放行换图指令：搜索、状态、清理带上会误入画廊分支。
        /// </summary>
        private static string WithGallery(string args)
        {
            if (!GalleryMode || string.IsNullOrEmpty(args)) { return args; }
            if (args.IndexOf("-Gallery", StringComparison.OrdinalIgnoreCase) >= 0) { return args; }
            string[] swap = { "-Step", "-Index", "-Date", "-Random", "-Force" };
            foreach (string k in swap)
            {
                if (args.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return args + " -Gallery";
                }
            }
            return args;
        }

        /// <summary>跑脚本并返回其标准输出（用于 -Query 这类只读取数据的调用）。</summary>
        internal static string RunScriptCapture(string arguments)
        {
            arguments = WithGallery(arguments);
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
                    // 同 RunScriptEx：两个流必须并发读，顺序读会在 stderr 写满时死锁
                    System.Threading.Tasks.Task<string> tOut = p.StandardOutput.ReadToEndAsync();
                    System.Threading.Tasks.Task<string> tErr = p.StandardError.ReadToEndAsync();
                    if (!p.WaitForExit(120000)) { try { p.Kill(); } catch { } }

                    string outText = "";
                    string errText = "";
                    try { outText = tOut.Result; } catch { }
                    try { errText = tErr.Result; } catch { }

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
                    // 同 RunScriptEx：两个流必须并发读，否则 stderr 写满时死锁
                    System.Threading.Tasks.Task<string> tOut = p.StandardOutput.ReadToEndAsync();
                    System.Threading.Tasks.Task<string> tErr = p.StandardError.ReadToEndAsync();
                    if (!p.WaitForExit(240000)) { try { p.Kill(); } catch { } return -1; }
                    try { tOut.Wait(3000); } catch { }
                    try { tErr.Wait(3000); } catch { }
                    return p.ExitCode;
                }
            }
            catch { return -1; }
        }

        private static void RunScript(string arguments)
        {
            RunScriptEx(arguments, "");
        }

        /// <summary>从脚本输出的 JSON 里取一个整数字段，取不到返回 0。</summary>
        private static int ExtractInt(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) { return 0; }
            try
            {
                System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(
                    json, "\"" + key + "\"\\s*:\\s*(\\d+)");
                int v = 0;
                if (m.Success) { int.TryParse(m.Groups[1].Value, out v); }
                return v;
            }
            catch { return 0; }
        }

        /// <summary>画廊清理专用：不受 busy 阻断（下载中点关闭也必须生效），也不占用 busy 标志。</summary>
        /// <summary>同步执行一条脚本指令并返回退出码，供信息窗口内的即时操作（翻页/收藏）使用。</summary>
        internal static int RunScriptExit(string rawArgs)
        {
            try
            {
                string args = WithGallery(rawArgs);
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = PowerShellExe;
                psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + scriptPath + "\" " + args;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                using (Process p = Process.Start(psi))
                {
                    System.Threading.Tasks.Task<string> o1 = p.StandardOutput.ReadToEndAsync();
                    System.Threading.Tasks.Task<string> o2 = p.StandardError.ReadToEndAsync();
                    if (!p.WaitForExit(180000)) { try { p.Kill(); } catch { } return -1; }
                    try { o1.Wait(2000); } catch { }
                    try { o2.Wait(2000); } catch { }
                    return p.ExitCode;
                }
            }
            catch { return -1; }
        }

        /// <summary>同步执行 -Step：dir &gt; 0 更早，dir &lt; 0 更新。</summary>
        internal static int RunStep(int dir)
        {
            return RunScriptExit("-Step " + (dir >= 0 ? "1" : "-1"));
        }

        /// <summary>预取某天的图：只下载到归档，不换壁纸、不写 state。</summary>
        internal static int RunPrefetch(string date)
        {
            if (string.IsNullOrEmpty(date) || date.Length != 8) { return -1; }
            return RunScriptExit("-PrefetchDate " + date);
        }

        /// <summary>清画廊但不要提示（程序启动、退出时用）。</summary>
        private static void RunGalleryClear() { RunGalleryClear(null); }

        /// <summary>清画廊；done 不为 null 时在清完后回调（清掉的张数、MB），并全程在后台线程跑，不卡界面。</summary>
        internal static void RunGalleryClear(System.Action<int, double> done)
        {
            int n0 = 0;
            double mb0 = 0;
            try
            {
                string gd = GalleryDirPath;
                if (Directory.Exists(gd))
                {
                    foreach (string f in Directory.GetFiles(gd, "*.jpg"))
                    {
                        try { mb0 += (new FileInfo(f)).Length / 1024.0 / 1024.0; n0++; } catch { }
                    }
                }
            }
            catch { }

            ThreadPool.QueueUserWorkItem(delegate
            {
                if (File.Exists(scriptPath))
                {
                    try
                    {
                        ProcessStartInfo psi = new ProcessStartInfo();
                        psi.FileName = PowerShellExe;
                        psi.Arguments = "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File \"" + scriptPath + "\" -GalleryClear";
                        psi.UseShellExecute = false;
                        psi.CreateNoWindow = true;
                        psi.RedirectStandardOutput = true;
                        psi.RedirectStandardError = true;
                        using (Process p = Process.Start(psi))
                        {
                            System.Threading.Tasks.Task<string> gOut = p.StandardOutput.ReadToEndAsync();
                            System.Threading.Tasks.Task<string> gErr = p.StandardError.ReadToEndAsync();
                            if (!p.WaitForExit(60000)) { try { p.Kill(); } catch { } }
                            else { try { gOut.Wait(2000); } catch { } try { gErr.Wait(2000); } catch { } }
                        }
                    }
                    catch { }
                }
                if (done != null)
                {
                    try { done(n0, Math.Round(mb0, 1)); } catch { }
                }
            });
        }

        private static void RunScriptEx(string arguments, string opMode)
        {
            if (busy) { NotifyBusy(); return; }
            arguments = WithGallery(arguments);
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
                string outText = "";
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
                        // 两个流必须同时读：只读 stdout 时，若脚本往 stderr 写满管道缓冲区（默认 4 KB），
                        // 脚本会阻塞在写 stderr，ReadToEnd 永远等 stdout 关闭 → 死锁 → busy 卡在 true
                        System.Threading.Tasks.Task<string> tOut = p.StandardOutput.ReadToEndAsync();
                        System.Threading.Tasks.Task<string> tErr = p.StandardError.ReadToEndAsync();

                        if (!p.WaitForExit(240000))
                        {
                            try { p.Kill(); } catch { }
                            code = -1;
                        }
                        else
                        {
                            try { tOut.Wait(3000); } catch { }
                            try { tErr.Wait(3000); } catch { }
                            code = p.ExitCode;
                            try { outText = tOut.Result ?? ""; } catch { }
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

                    // code==4 = 「已经在边界」：图没变，但得让人知道点了有效果
                    if (code == 4 && (opMode.Length == 0 || opMode == "today"))
                    {
                        Balloon(S.AlreadyToday, S.AlreadyTodayHint);
                    }
                    if (code == 0 || code == 8)
                    {
                        if (opMode == "firstrun")
                        {
                            Balloon(S.BReady,
                                S.BReadyHint);
                        }
                        else if (opMode == "clean" || opMode == "cleanall" ||
                                 opMode == "cleanarch" || opMode == "cleanidx" ||
                                 opMode == "cleanfav")
                        {
                            RefreshAll();
                            string body;
                            if (opMode == "cleanfav")
                            {
                                body = S.CleanDoneFav(ExtractInt(outText, "removed"), ExtractInt(outText, "moved"));
                            }
                            else if (opMode == "cleanarch")
                            {
                                body = S.CleanDoneArch(ExtractInt(outText, "removed"),
                                                       ExtractInt(outText, "keptFav"),
                                                       ExtractInt(outText, "keptCur"));
                                int idxN = ExtractInt(outText, "idxRemoved");
                                if (idxN > 0) { body = body + S.CleanIdxSuffix(idxN); }
                            }
                            else if (opMode == "cleanidx")
                            {
                                body = S.CleanDoneIdx(ExtractInt(outText, "removed"));
                            }
                            else if (opMode == "cleanall")
                            {
                                body = S.CleanDoneAll(ExtractInt(outText, "removed"), ExtractInt(outText, "protected"));
                            }
                            else
                            {
                                int rmN = ExtractInt(outText, "removed");
                                int ptN = ExtractInt(outText, "protected");
                                body = S.CleanDoneCache(rmN, ptN);
                                if (ptN >= 5 && ptN >= rmN) { body = body + " " + S.PinHintMore; }
                            }
                            Balloon(S.IsEn ? "Cleaned" : "清理完成", body);
                        }
                        else if (opMode == "fav")
                        {
                            Balloon(S.BFavDone, S.FavDoneHint);
                            RefreshAll();
                            // 收藏之后直接把「这张图是哪儿」摆到面前：
                            // 翻图、收藏、看详情都在那个窗口里，不主动引导没人会点开
                            try { ShowInfoPublic(); } catch { }
                        }
                        else if (opMode == "unfav")
                        {
                            Balloon(S.BFavUndo, S.FavUndoHint);
                            RefreshAll();
                        }
                        else if (opMode == "index")
                        {
                            Balloon(S.BIndexOk, S.IndexOkHint);
                        }
                        else if (GetCfgBool(ReadConfig(), "notify", false))
                        {
                            Dictionary<string, object> st = ReadJson(StatePathNow);
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
                    // 退出码语义见 bing-wallpaper.ps1 头部的 $EXIT 表；
                    // 11 = 图已就位但桌面未更新，12 = 日期越界（未来日期）
                    else if (code == 11)
                    {
                        Balloon(S.BWallFail, S.WallApplyFail);
                    }
                    else if (code == 12)
                    {
                        Balloon(S.BFutureDate, S.FutureDateHint);
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
