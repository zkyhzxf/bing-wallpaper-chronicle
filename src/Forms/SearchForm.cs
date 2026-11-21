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
            btnSearch.SetBounds(456, 37, 78, 26);
            btnSearch.Click += delegate { DoSearch(); };
            Controls.Add(btnSearch);

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
            btnCancel.Text = S.BtnCancel;
            btnCancel.SetBounds(514, 422, 86, 32);
            btnCancel.DialogResult = DialogResult.Cancel;
            Controls.Add(btnCancel);

            AcceptButton = btnSearch;
            CancelButton = btnCancel;
        }

        private void DoSearch()
        {
            string kw = txtKw.Text.Trim();
            if (kw.Length == 0) { return; }

            btnSearch.Enabled = false;
            lblStatus.Text = S.SrchSearching;
            lst.Items.Clear();
            results.Clear();
            Application.DoEvents();

            try
            {
                string outText = Program.RunScriptCapture("-Query \"" + kw.Replace("\"", "") + "\"");
                try
                {
                    string dbg = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "bwl-search-debug.log");
                    System.IO.File.AppendAllText(dbg, DateTime.Now.ToString("HH:mm:ss") + "  kw=" + kw + "  len=" + (outText == null ? -1 : outText.Length) + Environment.NewLine);
                    if (!string.IsNullOrEmpty(outText))
                    {
                        string head = outText.Length > 300 ? outText.Substring(0, 300) : outText;
                        System.IO.File.AppendAllText(dbg, "  head=" + head.Replace(Environment.NewLine, "|") + Environment.NewLine);
                    }
                }
                catch { }
                ParseResults(outText);
            }
            catch (Exception ex)
            {
                lblStatus.Text = ex.Message;
            }

            btnSearch.Enabled = true;

            if (lst.Items.Count == 0)
            {
                lblStatus.Text = S.SrchNone;
                btnApply.Enabled = false;
                return;
            }
            lblStatus.Text = S.SrchCount + lst.Items.Count + S.SrchCountTail;
            lst.SelectedIndex = 0;
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

                string label = date + "  " + (title.Length > 0 ? title : "(无标题)") + "    " + shortCpr;
                lst.Items.Add(label);
                results.Add(new string[] { date, title });
            }
        }

        private static string DictStr(Dictionary<string, object> d, string k)
        {
            if (d == null || !d.ContainsKey(k) || d[k] == null) { return ""; }
            return d[k].ToString();
        }

        private void Accept()
        {
            if (lst.SelectedIndex < 0 || lst.SelectedIndex >= results.Count) { return; }
            string date = results[lst.SelectedIndex][0];
            string title = results[lst.SelectedIndex][1];

            string q = S.SrchConfirm + "\r\n\r\n" + date + "  " + title;
            if (MessageBox.Show(q, Program.AppName + S.SrchConfirmTtl,
                    MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
            {
                return;
            }

            PickedDate = date;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
    /// <summary>「这张图是哪儿」窗口：缩略图 + 信息 + 两个外链按钮。</summary>
}
