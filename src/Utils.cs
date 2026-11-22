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
        // ---------------------------------------------------------------- 工具

        private static Icon LoadIcon(string path, Size size)
        {
            if (File.Exists(path))
            {
                try { return new Icon(path, size); }
                catch { }
            }
            return SystemIcons.Application;
        }

        private static Dictionary<string, object> ReadJson(string path)
        {
            try
            {
                if (!File.Exists(path)) { return null; }
                string json = File.ReadAllText(path, Encoding.UTF8);
                JavaScriptSerializer ser = new JavaScriptSerializer();
                return ser.Deserialize<Dictionary<string, object>>(json);
            }
            catch { return null; }
        }

        private static string GetStr(Dictionary<string, object> dict, string key)
        {
            if (dict == null || !dict.ContainsKey(key) || dict[key] == null) { return ""; }
            return dict[key].ToString();
        }

        private static string Clip(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) { return ""; }
            return s.Length <= max ? s : s.Substring(0, max - 1) + "…";
        }
    }
}
