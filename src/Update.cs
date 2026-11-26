using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace BingWallpaperLite
{
    /// <summary>一次检查更新的结果。请求本身失败也算结果，原因写在 Error。</summary>
    internal sealed class UpdateProbe
    {
        public bool Ok;
        public string Tag = "";
        public string Error = "";

        // Release 里的安装包。三个都为空说明这个 tag 只打了名字没传附件，
        // 此时仍可报版本，只是不能一键升级 —— 降级为打开 Release 页。
        public string AssetUrl = "";
        public string AssetName = "";
        public long AssetSize = 0;
    }

    internal static partial class Program
    {
        private const string ReleasesPageUrl = "https://github.com/zkyhzxf/bing-wallpaper-chronicle/releases/latest";

        // 通道顺序照脚本里验证过的 GitHub 访问策略：gh-proxy 前缀优先，直连兜底
        private static readonly string[] ReleaseApiUrls = new string[]
        {
            "https://gh-proxy.com/https://api.github.com/repos/zkyhzxf/bing-wallpaper-chronicle/releases/latest",
            "https://api.github.com/repos/zkyhzxf/bing-wallpaper-chronicle/releases/latest"
        };

        internal static string ReleasesUrl { get { return ReleasesPageUrl; } }

        /// <summary>打开项目 Release 页（系统默认浏览器）。</summary>
        internal static void OpenReleasesPage()
        {
            try { Process.Start(ReleasesPageUrl); } catch { }
        }

        /// <summary>作者主页：B 站空间。和 Release 页放一处，改地址只改这里。</summary>
        private const string AuthorPageUrl = "https://space.bilibili.com/14027543";

        internal static void OpenAuthorPage()
        {
            try { Process.Start(AuthorPageUrl); } catch { }
        }

        /// <summary>探一次最新 Release。任何失败都返回对象，不抛异常。</summary>
        internal static UpdateProbe CheckLatestRelease()
        {
            // .NET Framework 编出的 exe 默认只启用 Ssl3|Tls，GitHub 只收 TLS 1.2 以上
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            UpdateProbe r = new UpdateProbe();
            string proxy = "";
            try { proxy = GetStr(ReadConfig(), "proxy"); } catch { }

            string lastErr = "";

            foreach (string u in ReleaseApiUrls)
            {
                if (TryFetchTag(u, null, r, ref lastErr)) { return r; }
            }
            if (!string.IsNullOrEmpty(proxy))
            {
                foreach (string u in ReleaseApiUrls)
                {
                    if (TryFetchTag(u, proxy, r, ref lastErr)) { return r; }
                }
            }

            r.Ok = false;
            r.Tag = "";
            r.Error = Clip(string.IsNullOrEmpty(lastErr) ? "未知错误" : lastErr, 60);
            return r;
        }

        private static bool TryFetchTag(string url, string proxy, UpdateProbe r, ref string lastErr)
        {
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "GET";
                req.UserAgent = "BingWallpaperLite/" + AppVersion;
                req.Accept = "application/vnd.github+json";
                req.Timeout = 5000;
                req.ReadWriteTimeout = 5000;
                req.AllowAutoRedirect = true;
                req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                if (!string.IsNullOrEmpty(proxy))
                {
                    req.Proxy = new WebProxy(proxy.Trim());
                }

                using (WebResponse resp = req.GetResponse())
                using (Stream s = resp.GetResponseStream())
                using (StreamReader sr = new StreamReader(s, Encoding.UTF8))
                {
                    string body = sr.ReadToEnd();
                    Match m = Regex.Match(body, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
                    if (!m.Success)
                    {
                        lastErr = "响应里没有 tag_name";
                        return false;
                    }
                    r.Ok = true;
                    r.Tag = m.Groups[1].Value.Trim();
                    r.Error = "";
                    ParseAsset(body, r);
                    return true;
                }
            }
            catch (WebException we)
            {
                if (we.Response is HttpWebResponse)
                {
                    lastErr = "HTTP " + (int)((HttpWebResponse)we.Response).StatusCode;
                }
                else
                {
                    lastErr = we.Status == WebExceptionStatus.Timeout ? "请求超时" : we.Message;
                }
                return false;
            }
            catch (Exception ex)
            {
                lastErr = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// 从同一个响应体里捞安装包。只认 .exe，取第一个 ——
        /// CI 每个 Release 只传一个 BingWallpaperChronicle-vX.Y.Z-Setup.exe。
        /// </summary>
        private static void ParseAsset(string body, UpdateProbe r)
        {
            if (string.IsNullOrEmpty(body)) { return; }
            try
            {
                Match mUrl = Regex.Match(body, "\"browser_download_url\"\\s*:\\s*\"([^\"]+\\.exe)\"");
                if (!mUrl.Success) { return; }

                r.AssetUrl = mUrl.Groups[1].Value.Trim();
                // 名字从 URL 末段取，比再匹配一次 name 稳：name 与 url 可能不同步
                int slash = r.AssetUrl.LastIndexOf('/');
                r.AssetName = slash >= 0 ? r.AssetUrl.Substring(slash + 1) : r.AssetUrl;

                // size 只在 assets 里出现，且通常只有一个附件，取第一个就够
                Match mSize = Regex.Match(body, "\"size\"\\s*:\\s*(\\d+)");
                long sz = 0;
                if (mSize.Success && long.TryParse(mSize.Groups[1].Value, out sz)) { r.AssetSize = sz; }
            }
            catch { }
        }

        /// <summary>按点分段比较版本号；"v" 前缀忽略，缺失段补 0。</summary>
        internal static int CompareVersion(string a, string b)
        {
            string[] pa = (a ?? "").Trim().TrimStart('v', 'V').Split('.');
            string[] pb = (b ?? "").Trim().TrimStart('v', 'V').Split('.');
            int n = Math.Max(pa.Length, pb.Length);
            for (int i = 0; i < n; i++)
            {
                int x = 0;
                int y = 0;
                int.TryParse(i < pa.Length ? pa[i] : "0", out x);
                int.TryParse(i < pb.Length ? pb[i] : "0", out y);
                if (x != y) { return x > y ? 1 : -1; }
            }
            return 0;
        }
    }
}
