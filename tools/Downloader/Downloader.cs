using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Threading;

namespace BingWallDownloader
{
    /// <summary>一整天要下载的条目。</summary>
    internal sealed class DayEntry
    {
        public string Date;      // yyyyMMdd
        public string Title;
        public string ImageId;   // all-index 的 i 字段（2016-03 起）
        public string OldName;   // old-index 的 n 字段（2009-07 ~ 2016-03）
        public string OldAlt;    // old-index 的 a 字段，镜像用的备名
        public string Slug;      // 最终用于文件名的短 slug
    }

    internal enum Quality
    {
        Best = 0,   // UHD -> 1080p -> 镜像原图
        Fhd = 1,    // 1080p -> 镜像
        Hd = 2      // 只镜像（1366×768 那种）
    }

    internal sealed class ProgressInfo
    {
        public int Done;
        public int Total;
        public int Ok;
        public int Skip;
        public int Fail;
        public int Low;
        public string CurrentFile;
    }

    /// <summary>全量下载器：读索引、按日期遍历、多源回退、断点续跑。</summary>
    internal sealed class Downloader
    {
        static Downloader()
        {
            // .NET Framework 4.x 默认不启用 TLS 1.2，拿图会直接 404 / 握手失败
            try
            {
                ServicePointManager.SecurityProtocol |=
                    SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11;
            }
            catch { }
            try { ServicePointManager.DefaultConnectionLimit = 16; } catch { }
        }
        public delegate void LogHandler(string line);
        public delegate void ProgressHandler(ProgressInfo info);

        private readonly string indexDir;
        private readonly string destDir;
        private readonly Quality quality;
        private readonly LogHandler log;
        private readonly ProgressHandler progress;

        private volatile bool stopFlag;
        private int doneCnt, okCnt, skipCnt, failCnt;
        private readonly object lockObj = new object();
        private readonly List<string> failLines = new List<string>();
        private readonly List<string> lowLines = new List<string>();
        private string failPathCache;
        private int maxAttempts = 3;

        private static DateTime coolUntil = DateTime.MinValue;
        private static readonly object coolLock = new object();

        public Downloader(string indexDir, string destDir, Quality quality, LogHandler log, ProgressHandler progress)
        {
            this.indexDir = indexDir;
            this.destDir = destDir;
            this.quality = quality;
            this.log = log;
            this.progress = progress;
        }

        public void Stop() { stopFlag = true; }

        public List<string> FailLines
        {
            get { lock (lockObj) { return new List<string>(failLines); } }
        }

        /// <summary>索引目录里找 all-index.json 与 old-index.json；先看给定目录，再看 archive 子目录。</summary>
        public static string ResolveIndexDir(string hint)
        {
            List<string> cands = new List<string>();
            if (!string.IsNullOrEmpty(hint))
            {
                cands.Add(hint);
                cands.Add(Path.Combine(hint, "archive"));
                try
                {
                    DirectoryInfo up = new DirectoryInfo(hint).Parent;
                    if (up != null) { cands.Add(Path.Combine(up.FullName, "archive")); }
                }
                catch { }
            }
            string exeDir = Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath);
            if (!string.IsNullOrEmpty(exeDir))
            {
                cands.Add(exeDir);
                cands.Add(Path.Combine(exeDir, "archive"));
            }
            foreach (string d in cands)
            {
                try
                {
                    if (string.IsNullOrEmpty(d) || !Directory.Exists(d)) { continue; }
                    if (File.Exists(Path.Combine(d, "all-index.json")) || File.Exists(Path.Combine(d, "old-index.json")))
                    {
                        return d;
                    }
                }
                catch { }
            }
            return "";
        }

        /// <summary>读两个索引，合成按日期排序的清单。日期范围外的丢掉。</summary>
        public List<DayEntry> LoadEntries(string from, string to)
        {
            Dictionary<string, DayEntry> map = new Dictionary<string, DayEntry>();
            LoadAllIndex(map);
            LoadOldIndex(map);
            List<DayEntry> list = new List<DayEntry>();
            foreach (DayEntry e in map.Values)
            {
                if (string.CompareOrdinal(e.Date, from) < 0) { continue; }
                if (string.CompareOrdinal(e.Date, to) > 0) { continue; }
                list.Add(e);
            }
            list.Sort(delegate(DayEntry a, DayEntry b) { return string.CompareOrdinal(a.Date, b.Date); });
            return list;
        }

        private void LoadAllIndex(Dictionary<string, DayEntry> map)
        {
            string p = Path.Combine(indexDir, "all-index.json");
            if (!File.Exists(p)) { return; }
            string txt;
            try { txt = File.ReadAllText(p, Encoding.UTF8); } catch { return; }
            MatchCollection ms = Regex.Matches(txt, "\\{[^{}]*\\}");
            foreach (Match m in ms)
            {
                string s = m.Value;
                string d = JsonStr(s, "d");
                string i = JsonStr(s, "i");
                if (d == null || d.Length != 8 || i == null || i.Length == 0) { continue; }
                DayEntry e = new DayEntry();
                e.Date = d;
                e.Title = JsonStr(s, "t") ?? "";
                e.ImageId = i;
                e.Slug = ShortFromImageId(i);
                map[d] = e;
            }
        }

        private void LoadOldIndex(Dictionary<string, DayEntry> map)
        {
            string p = Path.Combine(indexDir, "old-index.json");
            if (!File.Exists(p)) { return; }
            string txt;
            try { txt = File.ReadAllText(p, Encoding.UTF8); } catch { return; }
            MatchCollection ms = Regex.Matches(txt, "\\{[^{}]*\\}");
            foreach (Match m in ms)
            {
                string s = m.Value;
                string d = JsonStr(s, "d");
                if (d == null || d.Length != 8) { continue; }
                DayEntry e;
                if (!map.TryGetValue(d, out e))
                {
                    e = new DayEntry();
                    e.Date = d;
                    map[d] = e;
                }
                e.OldName = JsonStr(s, "n");
                e.OldAlt = JsonStr(s, "a");
                if (e.Title == null || e.Title.Length == 0) { e.Title = JsonStr(s, "t") ?? ""; }
                if (string.IsNullOrEmpty(e.Slug)) { e.Slug = ShortFromOldName(e.OldName); }
            }
        }

        private static string JsonStr(string obj, string key)
        {
            Match m = Regex.Match(obj, "\"" + key + "\"\\s*:\\s*\"([^\"]*)\"");
            if (!m.Success) { return null; }
            return m.Groups[1].Value.Replace("\\/", "/");
        }

        private static string ShortFromImageId(string id)
        {
            if (string.IsNullOrEmpty(id)) { return ""; }
            string s = Regex.Replace(id, "_[A-Za-z]{2}-[A-Za-z]{2}\\d+$", "");
            int us = s.IndexOf('_');
            if (us > 0) { s = s.Substring(0, us); }
            return s;
        }

        private static string ShortFromOldName(string n)
        {
            if (string.IsNullOrEmpty(n)) { return ""; }
            string s = Regex.Replace(n, "_\\d+x\\d+\\.jpg$", "");
            s = Regex.Replace(s, "\\.jpg$", "");
            int us = s.IndexOf('_');
            if (us > 0) { s = s.Substring(0, us); }
            return s;
        }

        /// <summary>按画质档位组装候选地址，越靠前越优先。</summary>
        private List<string> BuildCandidates(DayEntry e)
        {
            List<string> list = new List<string>();
            // 一等：Bing 官方（只有 2018-09 之后有效，但先试不吃亏）
            if (!string.IsNullOrEmpty(e.ImageId))
            {
                if (quality == Quality.Best)
                {
                    list.Add("https://www.bing.com/th?id=" + e.ImageId + "_UHD.jpg");
                }
                if (quality != Quality.Hd)
                {
                    list.Add("https://www.bing.com/th?id=" + e.ImageId + "_1920x1080.jpg");
                }
            }
            if (quality != Quality.Hd && !string.IsNullOrEmpty(e.OldName))
            {
                // 2016 起 wilii 带分辨率后缀
                string ym = e.Date.Substring(0, 4) + "/" + e.Date.Substring(4, 2) + "/" + e.Date.Substring(6, 2) + "/";
                list.Add("https://img.wilii.cn/bingimages/" + ym + e.OldName);
            }
            if (!string.IsNullOrEmpty(e.OldName))
            {
                // 2014 前 wilii 不带后缀
                string ym2 = e.Date.Substring(0, 4) + "/" + e.Date.Substring(4, 2) + "/" + e.Date.Substring(6, 2) + "/";
                list.Add("https://img.wilii.cn/bingimages/" + ym2 + Regex.Replace(e.OldName, "_\\d+x\\d+\\.jpg$", ".jpg"));
            }
            // 末等：anerg 镜像
            string basePath = e.Date.Substring(0, 6);
            List<string> names = new List<string>();
            if (!string.IsNullOrEmpty(e.OldAlt)) { names.Add(e.OldAlt); }
            if (!string.IsNullOrEmpty(e.Slug))
            {
                names.Add(e.Slug);
                names.Add(e.Slug + "_1366x768");
                names.Add(e.Slug + "_1920x1080");
            }
            foreach (string nm in names)
            {
                if (string.IsNullOrEmpty(nm)) { continue; }
                string u = "https://img.nanxiongnandi.com/" + basePath + "/" + nm + ".jpg";
                if (!list.Contains(u)) { list.Add(u); }
            }
            return list;
        }

        /// <summary>
        /// 下载单张：每个候选最多试 maxAttempts 次。
        /// 404/410 直接换候选（明确没有，重试无意义）；429/403 让所有线程一起冷却；
        /// 超时与连接重置按 1s/2s/3s 退避重试。
        /// </summary>
        private bool FetchOne(DayEntry e, string destFile)
        {
            List<string> cands = BuildCandidates(e);
            string lastErr = "";
            DateTime started = DateTime.UtcNow;
            foreach (string u in cands)
            {
                if (stopFlag) { return false; }
                // 单日总预算：候选再多也不能让一个日期拖住整批
                if ((DateTime.UtcNow - started).TotalSeconds > 150)
                {
                    lastErr = "单日超时（150s）";
                    break;
                }
                for (int attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    if (stopFlag) { return false; }
                    string err;
                    DlResult r = DownloadTo(u, destFile, out err);
                    if (r == DlResult.Ok)
                    {
                        CheckQuality(e, destFile);
                        return true;
                    }
                    lastErr = err;
                    if (r == DlResult.NotFound) { break; }
                    if (r == DlResult.Throttled) { CoolDown(2000); }
                    Backoff(attempt);
                }
            }
            lock (lockObj)
            {
                failLines.Add(e.Date + "\t" + (string.IsNullOrEmpty(lastErr) ? "全部候选失败" : lastErr));
            }
            PersistFail(e.Date, lastErr);
            return false;
        }

        /// <summary>Best 档 + 2019 起：落盘后量真实宽度，确认不是「名字叫 UHD、内容是 1080p」。</summary>
        private void CheckQuality(DayEntry e, string file)
        {
            if (quality != Quality.Best) { return; }
            if (string.CompareOrdinal(e.Date, "20190101") < 0) { return; }
            int w = PixelWidth(file);
            if (w <= 0 || w >= 3000) { return; }
            lock (lockObj) { lowLines.Add(e.Date + "\t" + w + "px"); }
            log("⚠ " + e.Date + " 只有 " + w + "px，这天没有真 UHD");
        }

        private static int PixelWidth(string p)
        {
            try
            {
                using (Image img = Image.FromFile(p)) { return img.Width; }
            }
            catch { return 0; }
        }

        /// <summary>失败实时追加，中途也能看，不必等整个任务结束。</summary>
        private void PersistFail(string date, string err)
        {
            try
            {
                lock (lockObj)
                {
                    if (string.IsNullOrEmpty(failPathCache))
                    {
                        failPathCache = Path.Combine(destDir, "_download-fail.txt");
                    }
                    File.AppendAllText(failPathCache, date + "\t" + (err ?? "") + "\r\n", new UTF8Encoding(false));
                }
            }
            catch { }
        }

        private static void CoolDown(int ms)
        {
            lock (coolLock)
            {
                DateTime t = DateTime.UtcNow.AddMilliseconds(ms);
                if (t > coolUntil) { coolUntil = t; }
            }
        }

        private static void Backoff(int attempt)
        {
            DateTime until;
            lock (coolLock) { until = coolUntil; }
            double left = (until - DateTime.UtcNow).TotalMilliseconds;
            if (left > 0) { try { Thread.Sleep((int)Math.Min(left, 3000)); } catch { } }
            try { Thread.Sleep(1000 * attempt); } catch { }
        }

        private enum DlResult { Ok, NotFound, Throttled, Transient }

        private static DlResult DownloadTo(string url, string destFile, out string err)
        {
            err = "";
            string tmp = destFile + ".part";
            Timer dog = null;
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) BingWallDownloader/1.1";
                req.Timeout = 30000;
                req.ReadWriteTimeout = 60000;
                req.AllowAutoRedirect = true;
                // 看门狗：HttpWebRequest.Timeout 对「连上了但服务器不回话」有时根本不生效
                // （半开连接 / 代理中断），只能自己 Abort，否则线程会一直挂着
                dog = new Timer(delegate { try { req.Abort(); } catch { } }, null, 25000, Timeout.Infinite);
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                {
                    if (resp.StatusCode != HttpStatusCode.OK)
                    {
                        err = "HTTP " + (int)resp.StatusCode;
                        return DlResult.NotFound;
                    }
                    long len = resp.ContentLength;
                    if (len > 0 && len < 30000) { err = "内容过小"; return DlResult.NotFound; }
                    using (Stream s = resp.GetResponseStream())
                    using (FileStream fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        byte[] buf = new byte[64 * 1024];
                        int n;
                        while ((n = s.Read(buf, 0, buf.Length)) > 0)
                        {
                            fs.Write(buf, 0, n);
                            if (fs.Length > 200L * 1024 * 1024) { err = "超过 200 MB"; return DlResult.NotFound; }
                        }
                    }
                    FileInfo fi = new FileInfo(tmp);
                    if (fi.Length < 30000) { err = "内容过小"; return DlResult.NotFound; }
                    if (File.Exists(destFile)) { TryDelete(destFile); }
                    File.Move(tmp, destFile);
                    return DlResult.Ok;
                }
            }
            catch (WebException we)
            {
                HttpWebResponse hr = we.Response as HttpWebResponse;
                if (hr != null)
                {
                    int code = (int)hr.StatusCode;
                    err = "HTTP " + code;
                    if (code == 404 || code == 410) { return DlResult.NotFound; }
                    if (code == 429 || code == 403) { return DlResult.Throttled; }
                    return DlResult.Transient;
                }
                err = we.Status.ToString();
                return DlResult.Transient;
            }
            catch (Exception ex) { err = ex.Message; return DlResult.Transient; }
            finally
            {
                if (dog != null) { try { dog.Dispose(); } catch { } }
                // 成功时 tmp 已被 Move 走；其余情况一律不留半成品
                try { if (File.Exists(tmp)) { File.Delete(tmp); } } catch { }
            }
        }

        private static void TryDelete(string p)
        {
            try { if (File.Exists(p)) { File.Delete(p); } } catch { }
        }

        /// <summary>主循环：多线程拉取，失败计数，结束写清单。</summary>
        public void Run(string from, string to, int threads)
        {
            List<DayEntry> list = LoadEntries(from, to);
            if (list.Count == 0)
            {
                log("索引里没有这个范围内的日期，检查索引目录：" + indexDir);
                return;
            }
            try { if (!Directory.Exists(destDir)) { Directory.CreateDirectory(destDir); } }
            catch (Exception ex) { log("目标目录建不了：" + ex.Message); return; }

            // 上次中断留下的半成品（超过 10 分钟没动）先清掉，避免 0 字节文件堆着
            CleanStaleParts();
            try
            {
                failPathCache = Path.Combine(destDir, "_download-fail.txt");
                if (File.Exists(failPathCache)) { File.Delete(failPathCache); }
            }
            catch { }

            // 先过滤掉已有的（断点续跑）
            List<DayEntry> todo = new List<DayEntry>();
            foreach (DayEntry e in list)
            {
                if (ExistsForDate(e.Date)) { skipCnt++; doneCnt++; continue; }
                todo.Add(e);
            }
            log(string.Format("共 {0} 天，已有 {1} 天，待下载 {2} 天", list.Count, skipCnt, todo.Count));

            int total = list.Count;
            Report(total, null);

            int idx = -1;
            object idxLock = new object();
            List<Thread> pool = new List<Thread>();
            int nThreads = Math.Max(1, Math.Min(8, threads));
            for (int t = 0; t < nThreads; t++)
            {
                Thread th = new Thread(delegate()
                {
                    while (!stopFlag)
                    {
                        DayEntry e;
                        lock (idxLock)
                        {
                            idx++;
                            if (idx >= todo.Count) { return; }
                            e = todo[idx];
                        }
                        string slug = string.IsNullOrEmpty(e.Slug) ? "img" : e.Slug;
                        string destFile = Path.Combine(destDir, e.Date + "_" + slug + ".jpg");
                        bool ok = false;
                        try { ok = FetchOne(e, destFile); } catch { ok = false; }
                        lock (lockObj)
                        {
                            doneCnt++;
                            if (ok) { okCnt++; } else { failCnt++; }
                        }
                        Report(total, e.Date);
                    }
                });
                th.IsBackground = true;
                th.Start();
                pool.Add(th);
            }
            foreach (Thread th in pool) { th.Join(); }

            // 失败清单：实时追加过，结束再按日期去重排序重写一遍
            List<string> fails = FailLines;
            List<string> failsUniq = new List<string>();
            foreach (string line in fails) { if (!failsUniq.Contains(line)) { failsUniq.Add(line); } }
            string failPath = Path.Combine(destDir, "_download-fail.txt");
            try
            {
                if (failsUniq.Count > 0)
                {
                    failsUniq.Sort();
                    File.WriteAllLines(failPath, failsUniq.ToArray(), new UTF8Encoding(false));
                }
                else if (File.Exists(failPath)) { File.Delete(failPath); }
            }
            catch { }

            // 无 UHD 清单
            List<string> lows;
            lock (lockObj) { lows = new List<string>(lowLines); }
            string lowPath = Path.Combine(destDir, "_lowres.txt");
            try
            {
                if (lows.Count > 0) { lows.Sort(); File.WriteAllLines(lowPath, lows.ToArray(), new UTF8Encoding(false)); }
                else if (File.Exists(lowPath)) { File.Delete(lowPath); }
            }
            catch { }

            Report(total, null);
            log(string.Format("完成：成功 {0}，跳过 {1}，失败 {2}，无 UHD {3}", okCnt, skipCnt, failCnt, lows.Count));
            if (failsUniq.Count > 0)
            {
                log("失败清单：" + failPath);
                log("再点一次「开始下载」即可补：已有文件会跳过，失败的会重新尝试（每个地址 3 次 + 退避）");
            }
            if (lows.Count > 0)
            {
                log("无 UHD 清单：" + lowPath + "（这些天 Bing 的 UHD 地址返回的就是 1080p）");
            }
        }

        /// <summary>清掉上次中断留下的 .part（10 分钟没更新就算过期）。</summary>
        private void CleanStaleParts()
        {
            try
            {
                foreach (string f in Directory.GetFiles(destDir, "*.part"))
                {
                    try
                    {
                        FileInfo fi = new FileInfo(f);
                        if ((DateTime.UtcNow - fi.LastWriteTimeUtc).TotalMinutes > 10) { fi.Delete(); }
                    }
                    catch { }
                }
            }
            catch { }
        }

        /// <summary>某天是否已经有图（靠文件名前缀判断，和主程序一致）。</summary>
        private bool ExistsForDate(string date)
        {
            try
            {
                string[] hit = Directory.GetFiles(destDir, date + "_*.jpg");
                return hit != null && hit.Length > 0;
            }
            catch { return false; }
        }

        private void Report(int total, string cur)
        {
            ProgressInfo p = new ProgressInfo();
            p.Total = total;
            p.Done = doneCnt;
            p.Ok = okCnt;
            p.Skip = skipCnt;
            p.Fail = failCnt;
            lock (lockObj) { p.Low = lowLines.Count; }
            p.CurrentFile = cur;
            if (progress != null) { progress(p); }
        }
    }
}
