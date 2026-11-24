using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace BingWallpaperLite
{
    internal sealed class SearchForm : Form
    {
        private readonly TextBox txtKw;
        private readonly ListBox lst;
        private readonly Label lblStatus;
        private readonly Button btnSearch;
        private readonly Button btnApply;
        private readonly Button btnRandom;
        private static readonly Random rnd = new Random();
        private const int RandomGroupSize = 20;
        private bool busy = false;

        public string PickedDate { get; private set; }

        private readonly List<string[]> results = new List<string[]>();

        public SearchForm()
        {
            Text = Program.AppName + S.SrchTitle;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            ClientSize = new Size(620, 470);
            try { Font = new Font("Microsoft YaHei UI", 9F); } catch { }

            Label lbl = new Label();
            lbl.Text = S.SrchLabel;
            lbl.SetBounds(16, 14, 400, 20);
            Controls.Add(lbl);

            txtKw = new TextBox();
            txtKw.SetBounds(16, 38, 430, 24);
            Controls.Add(txtKw);

            btnSearch = new Button();
            btnSearch.Text = S.SrchBtn;
            btnSearch.SetBounds(456, 37, 70, 26);
            btnSearch.Click += delegate { DoSearch(); };
            Controls.Add(btnSearch);

            btnRandom = new Button();
            btnRandom.Text = S.SrchBtnRandom;
            btnRandom.SetBounds(532, 37, 70, 26);
            btnRandom.Click += delegate { DoRandom(); };
            Controls.Add(btnRandom);

            lst = new ListBox();
            lst.SetBounds(16, 74, 584, 316);
            lst.DoubleClick += delegate { Accept(); };
            lst.SelectedIndexChanged += delegate { btnApply.Enabled = (lst.SelectedIndex >= 0); };
            Controls.Add(lst);

            lblStatus = new Label();
            lblStatus.SetBounds(16, 396, 584, 18);
            lblStatus.ForeColor = SystemColors.GrayText;
            lblStatus.Text = S.SrchHint;
            Controls.Add(lblStatus);

            btnApply = new Button();
            btnApply.Text = S.SrchApply;
            btnApply.SetBounds(16, 422, 110, 32);
            btnApply.Enabled = false;
            btnApply.Click += delegate { Accept(); };
            Controls.Add(btnApply);

            Button btnCancel = new Button();
            btnCancel.Text = S.BtnClose;
            btnCancel.SetBounds(514, 422, 86, 32);
            btnCancel.Click += delegate { Close(); };
            Controls.Add(btnCancel);

            AcceptButton = btnSearch;
            CancelButton = btnCancel;
        }

        private void DoRandom()
        {
            // 列表里已经有结果 → 随机挑其中一张，走与手动选择完全相同的 Accept()
            if (results.Count > 0)
            {
                lst.SelectedIndex = rnd.Next(results.Count);
                Accept();
                return;
            }
            PickRandomGroup();
        }

        private void PickRandomGroup()
        {
            if (busy) { return; }
            RunQueryAsync("-PickRandom " + RandomGroupSize, S.SrchLibEmpty);
        }

        private void DoSearch()
        {
            if (busy) { return; }
            string kw = txtKw.Text.Trim();
            // 空关键词 = 从全库随机抽一组，不再弹确认框
            if (kw.Length == 0) { PickRandomGroup(); return; }
            RunQueryAsync("-Query \"" + kw.Replace("\"", "") + "\"", S.SrchNone);
        }

        /// <summary>
        /// 查询放后台线程跑。原来是 UI 线程同步调 RunScriptCapture，
        /// 首次搜索要拉 1.6 MB 索引，窗口会一直冻到 120 秒超时。
        /// </summary>
        private void RunQueryAsync(string args, string emptyText)
        {
            busy = true;
            Cursor = Cursors.WaitCursor;
            lblStatus.Text = S.SrchSearching;
            lst.Items.Clear();
            results.Clear();
            btnApply.Enabled = false;

            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                string outText = "";
                string err = null;
                try { outText = Program.RunScriptCapture(args); }
                catch (Exception ex) { err = ex.Message; }
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        busy = false;
                        Cursor = Cursors.Default;
                        if (err != null) { lblStatus.Text = err; return; }
                        try { ParseResults(outText); } catch { }
                        if (lst.Items.Count == 0)
                        {
                            lblStatus.Text = emptyText;
                            btnApply.Enabled = false;
                            return;
                        }
                        lblStatus.Text = S.SrchCount + lst.Items.Count + S.SrchCountTail;
                        lst.SelectedIndex = 0;
                        lst.TopIndex = 0;
                    });
                }
                catch { }
            });
        }

        private void ParseResults(string json)
        {
            if (string.IsNullOrEmpty(json)) { return; }
            int a = json.IndexOf('[');
            int b = json.LastIndexOf(']');
            if (a < 0 || b <= a) { return; }
            json = json.Substring(a, b - a + 1);

            JavaScriptSerializer js = new JavaScriptSerializer();
            List<Dictionary<string, object>> arr = null;
            try { arr = js.Deserialize<List<Dictionary<string, object>>>(json); } catch { return; }
            if (arr == null) { return; }

            foreach (Dictionary<string, object> d in arr)
            {
                if (d == null) { continue; }
                string date = DictStr(d, "date");
                string title = DictStr(d, "title");
                string cpr = DictStr(d, "copyright");
                if (date.Length == 0) { continue; }

                string shortCpr = cpr;
                int cut = shortCpr.IndexOf('(');
                if (cut > 0) { shortCpr = shortCpr.Substring(0, cut).Trim(); }
                if (shortCpr.Length > 36) { shortCpr = shortCpr.Substring(0, 36) + "…"; }

                // dead = 索引里有、原图已 404。列出来但标清楚，别让人点了才发现取不到
                bool dead = DictBool(d, "dead");
                string label = date + "  " + (title.Length > 0 ? title : "(无标题)") + "    " + shortCpr;
                if (dead) { label = label + "  " + S.DeadDateSuffix; }
                lst.Items.Add(label);
                results.Add(new string[] { date, title, dead ? "1" : "" });
            }
        }

        private static bool DictBool(Dictionary<string, object> d, string k)
        {
            if (d == null || !d.ContainsKey(k) || d[k] == null) { return false; }
            object v = d[k];
            if (v is bool) { return (bool)v; }
            return string.Equals(v.ToString(), "true", StringComparison.OrdinalIgnoreCase) || v.ToString() == "1";
        }

        private static string DictStr(Dictionary<string, object> d, string k)
        {
            if (d == null || !d.ContainsKey(k) || d[k] == null) { return ""; }
            return d[k].ToString();
        }

        /// <summary>选中一条就直接换图，窗口留着 —— 换图不再是「关窗确认」的副作用。</summary>
        private void Accept()
        {
            if (busy || lst.SelectedIndex < 0 || lst.SelectedIndex >= results.Count) { return; }
            string date = results[lst.SelectedIndex][0];
            if (results[lst.SelectedIndex].Length > 2 && results[lst.SelectedIndex][2] == "1")
            {
                lblStatus.Text = S.SrchNoImage;
                return;
            }
            PickedDate = date;

            busy = true;
            Cursor = Cursors.WaitCursor;
            btnApply.Enabled = false;
            lblStatus.Text = S.SrchWorking;
            string d8 = date;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                int code = Program.RunScriptExit("-Date " + d8);
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        busy = false;
                        Cursor = Cursors.Default;
                        btnApply.Enabled = (lst.SelectedIndex >= 0);
                        if (code == 11) { lblStatus.Text = S.WallApplyFail; }
                        else if (code == 12) { lblStatus.Text = S.FutureDateHint; }
                        else if (code == 4) { lblStatus.Text = S.SrchNoSource; }
                        else if (code == 5) { lblStatus.Text = S.SrchNoImage; }
                        else { lblStatus.Text = (code == 0 || code == 8) ? S.SrchSwitched(d8) : S.SrchSwitchFail; }
                    });
                }
                catch { }
            });
        }
    }
    /// <summary>「这张图是哪儿」窗口：缩略图 + 信息 + 两个外链按钮。</summary>
}
