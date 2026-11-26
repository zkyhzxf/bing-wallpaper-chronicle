using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace BingWallpaperLite
{
    /// <summary>导出指定日期范围的下载直链，交给外部下载器。</summary>
    internal sealed class LinkForm : Form
    {
        private readonly string from;
        private readonly string to;

        private readonly Label lblStat;
        private readonly TextBox txt;
        private readonly Button btnCopy;
        private readonly Button btnSave;
        private readonly Timer timer;

        public LinkForm(string fromDate, string toDate)
        {
            from = fromDate;
            to = toDate;

            Text = Program.AppName + S.LinkTitle;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            ClientSize = new Size(560, 380);
            try { Font = new Font("Microsoft YaHei UI", 9F); } catch { }

            lblStat = new Label();
            lblStat.SetBounds(16, 14, 528, 20);
            lblStat.Text = S.LinkGenerating;
            Controls.Add(lblStat);

            txt = new TextBox();
            txt.SetBounds(16, 40, 528, 282);
            txt.Multiline = true;
            txt.ReadOnly = true;
            txt.ScrollBars = ScrollBars.Vertical;
            txt.WordWrap = false;
            try { txt.Font = new Font("Consolas", 9F); } catch { }
            Controls.Add(txt);

            btnCopy = new Button();
            btnCopy.Text = S.LinkCopy;
            btnCopy.SetBounds(16, 334, 120, 30);
            btnCopy.Enabled = false;
            btnCopy.Click += delegate { CopyAll(); };
            Controls.Add(btnCopy);

            btnSave = new Button();
            btnSave.Text = S.LinkSave;
            btnSave.SetBounds(146, 334, 130, 30);
            btnSave.Enabled = false;
            btnSave.Click += delegate { SaveTo(); };
            Controls.Add(btnSave);

            Button btnClose = new Button();
            btnClose.Text = S.PinClose;
            btnClose.SetBounds(464, 334, 80, 30);
            btnClose.DialogResult = DialogResult.Cancel;
            Controls.Add(btnClose);
            CancelButton = btnClose;

            timer = new Timer();
            timer.Interval = 1500;
            timer.Tick += delegate
            {
                timer.Stop();
                btnCopy.Text = S.LinkCopy;
            };
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            try { BeginInvoke((MethodInvoker)delegate { Generate(); }); } catch { }
        }

        private void Generate()
        {
            Cursor = Cursors.WaitCursor;
            btnCopy.Enabled = false;
            btnSave.Enabled = false;
            lblStat.Text = S.LinkGenerating;

            // 导出链接要联网拉索引，原来在 UI 线程同步跑会把窗口冻住直到超时
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                string outText = "";
                try { outText = Program.RunScriptCapture("-ExportLinks -RangeFrom " + from + " -RangeTo " + to); }
                catch { }
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        Cursor = Cursors.Default;
                        try
                        {
                            List<string> links = ParseLinks(outText);
                            int miss = PickInt(outText, "miss");
                            if (links.Count == 0)
                            {
                                txt.Text = "";
                                lblStat.Text = S.LinkNoLinks;
                                return;
                            }
                            txt.Text = string.Join("\r\n", links.ToArray());
                            lblStat.Text = S.LinkStat(links.Count, miss);
                            btnCopy.Enabled = true;
                            btnSave.Enabled = true;
                        }
                        catch
                        {
                            lblStat.Text = S.LinkNoLinks;
                        }
                    });
                }
                catch { }
            });
        }

        private static List<string> ParseLinks(string json)
        {
            List<string> r = new List<string>();
            if (string.IsNullOrEmpty(json)) { return r; }
            try
            {
                Match m = Regex.Match(json, "\"links\"\\s*:\\s*\\[(.*?)\\]", RegexOptions.Singleline);
                if (!m.Success) { return r; }
                foreach (Match mm in Regex.Matches(m.Groups[1].Value, "\"([^\"]+)\""))
                {
                    string u = mm.Groups[1].Value;
                    if (u.StartsWith("http", StringComparison.OrdinalIgnoreCase)) { r.Add(u); }
                }
            }
            catch { }
            return r;
        }

        private static int PickInt(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) { return 0; }
            try
            {
                Match m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*(\\d+)");
                int v = 0;
                if (m.Success) { int.TryParse(m.Groups[1].Value, out v); }
                return v;
            }
            catch { return 0; }
        }

        private void CopyAll()
        {
            try
            {
                if (txt.Text.Length == 0) { return; }
                Clipboard.SetText(txt.Text);
                btnCopy.Text = S.LinkCopied;
                timer.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void SaveTo()
        {
            if (txt.Text.Length == 0) { return; }
            using (SaveFileDialog d = new SaveFileDialog())
            {
                d.Filter = S.IsEn ? "Text file|*.txt|All files|*.*" : "文本文件|*.txt|所有文件|*.*";
                d.FileName = "bing-links-" + from + "-" + to + ".txt";
                if (d.ShowDialog(this) != DialogResult.OK) { return; }
                try
                {
                    File.WriteAllText(d.FileName, txt.Text, new UTF8Encoding(false));
                    lblStat.Text = S.LinkSaved(d.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { if (timer != null) { timer.Stop(); timer.Dispose(); } } catch { }
            }
            base.Dispose(disposing);
        }
    }
}
