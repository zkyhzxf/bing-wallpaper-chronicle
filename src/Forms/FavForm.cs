using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace BingWallpaperLite
{
    /// <summary>收藏管理：批量移出收藏（文件退回缓存，不删除）。</summary>
    internal sealed class FavForm : Form
    {
        private readonly ListView lst;
        private readonly Label lblSum;
        private readonly Label lblInfo;
        private readonly Label lblCopy;
        private readonly PictureBox pic;
        private Dictionary<string, object> idxCache;
        private readonly Button btnRemove;
        private readonly Button btnOpen;

        private readonly List<string> dates = new List<string>();
        private readonly List<string> files = new List<string>();

        public FavForm()
        {
            Text = Program.AppName + S.FavTitle;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            ClientSize = new Size(620, 492);
            try { Font = new Font("Microsoft YaHei UI", 9F); } catch { }

            Label l1 = new Label();
            l1.SetBounds(16, 10, 588, 40);
            l1.AutoSize = false;
            l1.ForeColor = SystemColors.GrayText;
            l1.Text = S.FavIntro;
            Controls.Add(l1);

            lblSum = new Label();
            lblSum.SetBounds(16, 54, 340, 20);
            lblSum.AutoSize = false;
            Controls.Add(lblSum);

            Button btnAll = new Button();
            btnAll.Text = S.IsEn ? "All" : "全选";
            btnAll.SetBounds(366, 52, 60, 24);
            btnAll.Click += delegate { CheckAll(true); };
            Controls.Add(btnAll);

            Button btnInvert = new Button();
            btnInvert.Text = S.FavInvert;
            btnInvert.SetBounds(432, 52, 60, 24);
            btnInvert.Click += delegate { InvertAll(); };
            Controls.Add(btnInvert);

            Button btnNone = new Button();
            btnNone.Text = S.IsEn ? "None" : "全不选";
            btnNone.SetBounds(498, 52, 74, 24);
            btnNone.Click += delegate { CheckAll(false); };
            Controls.Add(btnNone);

            lst = new ListView();
            lst.SetBounds(16, 80, 588, 190);
            lst.View = View.Details;
            lst.CheckBoxes = true;
            lst.FullRowSelect = true;
            lst.MultiSelect = false;
            lst.HideSelection = false;
            lst.Columns.Add(S.PinColDate, 92);
            lst.Columns.Add(S.PinColTitle, 360);
            lst.Columns.Add(S.PinColSize, 96, HorizontalAlignment.Right);
            lst.ItemChecked += delegate { RefreshSum(); };
            lst.SelectedIndexChanged += delegate { OnSelectionChanged(); };
            lst.DoubleClick += delegate { OpenSelected(); };
            Controls.Add(lst);

            pic = new PictureBox();
            pic.SetBounds(16, 278, 240, 135);
            pic.BorderStyle = BorderStyle.FixedSingle;
            pic.SizeMode = PictureBoxSizeMode.Zoom;
            pic.Visible = false;
            Controls.Add(pic);

            lblInfo = new Label();
            lblInfo.SetBounds(268, 278, 336, 90);
            lblInfo.AutoSize = false;
            lblInfo.ForeColor = SystemColors.GrayText;
            lblInfo.TextAlign = ContentAlignment.MiddleCenter;
            Controls.Add(lblInfo);

            lblCopy = new Label();
            lblCopy.SetBounds(268, 374, 336, 40);
            lblCopy.AutoSize = false;
            lblCopy.ForeColor = SystemColors.GrayText;
            lblCopy.TextAlign = ContentAlignment.TopLeft;
            try { lblCopy.Font = new Font("Microsoft YaHei UI", 8F); } catch { }
            Controls.Add(lblCopy);

            btnRemove = new Button();
            btnRemove.Text = S.FavRemove;
            btnRemove.SetBounds(16, 448, 120, 30);
            btnRemove.Click += delegate { RemoveChecked(); };
            Controls.Add(btnRemove);

            btnOpen = new Button();
            btnOpen.Text = S.PinOpenFile;
            btnOpen.SetBounds(420, 448, 96, 30);
            btnOpen.Click += delegate { OpenSelected(); };
            Controls.Add(btnOpen);

            Button btnClose = new Button();
            btnClose.Text = S.PinClose;
            btnClose.SetBounds(524, 448, 80, 30);
            btnClose.DialogResult = DialogResult.Cancel;
            Controls.Add(btnClose);
            CancelButton = btnClose;

            LoadList();
        }

        private void LoadList()
        {
            lst.Items.Clear();
            dates.Clear();
            files.Clear();

            Dictionary<string, object> titles = null;
            try { titles = Program.LoadArchiveIndex(); } catch { }
            idxCache = titles;

            List<string> found = new List<string>();
            try
            {
                string fav = Program.FavDirPath;
                if (!string.IsNullOrEmpty(fav) && Directory.Exists(fav))
                {
                    foreach (string f in Directory.GetFiles(fav, "*.jpg")) { found.Add(f); }
                }
            }
            catch { }
            found.Sort(StringComparer.OrdinalIgnoreCase);

            foreach (string f in found)
            {
                string fn = Path.GetFileName(f);
                string d = fn.Length >= 8 ? fn.Substring(0, 8) : "";
                if (d.Length != 8) { continue; }

                string title = S.PinNoTitle;
                if (titles != null && titles.ContainsKey(d))
                {
                    Dictionary<string, object> e = titles[d] as Dictionary<string, object>;
                    if (e != null && e.ContainsKey("title"))
                    {
                        string t = e["title"] == null ? "" : e["title"].ToString();
                        if (t.Length > 0) { title = t; }
                    }
                }

                double mb = 0;
                try { mb = new FileInfo(f).Length / 1024.0 / 1024.0; } catch { }

                ListViewItem it = new ListViewItem(d);
                it.SubItems.Add(title);
                it.SubItems.Add(mb.ToString("0.0") + " MB");
                it.Tag = d;
                lst.Items.Add(it);
                dates.Add(d);
                files.Add(f);
            }

            RefreshSum();

            if (lst.Items.Count > 0)
            {
                lst.Items[0].Selected = true;
                lst.Items[0].Focused = true;
            }
            else
            {
                ShowPreview(-1);
            }
            OnSelectionChanged();
        }

        private static Bitmap MakeThumb(string file, int w, int h)
        {
            try
            {
                if (string.IsNullOrEmpty(file) || !File.Exists(file)) { return null; }
                byte[] bytes = File.ReadAllBytes(file);
                using (MemoryStream ms = new MemoryStream(bytes))
                using (Image src = Image.FromStream(ms))
                {
                    Bitmap bmp = new Bitmap(w, h);
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                        g.DrawImage(src, 0, 0, w, h);
                    }
                    return bmp;
                }
            }
            catch { return null; }
        }

        private void SetHint(string text)
        {
            lblInfo.ForeColor = SystemColors.GrayText;
            lblInfo.TextAlign = ContentAlignment.MiddleCenter;
            lblInfo.Text = text;
        }

        private void ShowPreview(int idx)
        {
            try
            {
                if (pic.Image != null)
                {
                    Image old = pic.Image;
                    pic.Image = null;
                    old.Dispose();
                }
            }
            catch { }

            lblCopy.Text = "";

            if (lst.Items.Count == 0)
            {
                pic.Visible = false;
                SetHint(S.FavEmpty);
                return;
            }
            if (idx < 0 || idx >= files.Count)
            {
                pic.Visible = false;
                SetHint(S.PinNoSelection);
                return;
            }

            Bitmap bmp = MakeThumb(files[idx], 240, 135);
            if (bmp != null) { pic.Image = bmp; pic.Visible = true; }
            else { pic.Visible = false; }

            string title = lst.Items[idx].SubItems.Count > 1 ? lst.Items[idx].SubItems[1].Text : "";
            double mb = 0;
            try { mb = new FileInfo(files[idx]).Length / 1024.0 / 1024.0; } catch { }

            lblInfo.ForeColor = SystemColors.ControlText;
            lblInfo.TextAlign = ContentAlignment.TopLeft;
            lblInfo.Text = dates[idx] + Environment.NewLine +
                           title + Environment.NewLine +
                           mb.ToString("0.0") + " MB";

            string copy = "";
            try
            {
                if (idxCache != null && idxCache.ContainsKey(dates[idx]))
                {
                    Dictionary<string, object> e = idxCache[dates[idx]] as Dictionary<string, object>;
                    if (e != null && e.ContainsKey("copyright") && e["copyright"] != null)
                    {
                        copy = e["copyright"].ToString();
                    }
                }
            }
            catch { }
            lblCopy.Text = copy;
        }

        private void OnSelectionChanged()
        {
            int idx = lst.SelectedItems.Count > 0 ? lst.SelectedItems[0].Index : -1;
            ShowPreview(idx);
            btnOpen.Enabled = idx >= 0 && idx < files.Count;
            btnRemove.Enabled = CheckedCount() > 0;
        }

        private void InvertAll()
        {
            foreach (ListViewItem it in lst.Items) { it.Checked = !it.Checked; }
            RefreshSum();
            btnRemove.Enabled = CheckedCount() > 0;
        }

        private void CheckAll(bool on)
        {
            foreach (ListViewItem it in lst.Items) { it.Checked = on; }
            RefreshSum();
            btnRemove.Enabled = CheckedCount() > 0;
        }

        private void RefreshSum()
        {
            double mb = 0;
            foreach (string f in files)
            {
                try { mb += new FileInfo(f).Length / 1024.0 / 1024.0; } catch { }
            }
            lblSum.Text = S.FavSum(lst.Items.Count, mb);
        }

        private int CheckedCount()
        {
            int n = 0;
            foreach (ListViewItem it in lst.Items) { if (it.Checked) { n++; } }
            return n;
        }

        private void OpenSelected()
        {
            if (lst.SelectedItems.Count == 0) { return; }
            int idx = lst.SelectedItems[0].Index;
            if (idx < 0 || idx >= files.Count) { return; }
            try { Program.OpenTarget(files[idx]); } catch { }
        }

        private void RemoveChecked()
        {
            List<string> picked = new List<string>();
            foreach (ListViewItem it in lst.Items) { if (it.Checked) { picked.Add((string)it.Tag); } }
            if (picked.Count == 0) { return; }

            if (MessageBox.Show(S.FavConfirm(picked.Count), Program.AppName,
                    MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
            {
                return;
            }

            Cursor prev = Cursor;
            string outText = "";
            try
            {
                Cursor = Cursors.WaitCursor;
                outText = Program.RunScriptCapture("-UnfavDate " + string.Join(",", picked.ToArray()));
            }
            catch { }
            finally { Cursor = prev; }

            int okN = PickInt(outText, "ok");
            int skipN = PickInt(outText, "skip");
            int failN = PickInt(outText, "fail");

            if (failN > 0 || skipN > 0)
            {
                MessageBox.Show(S.PinFavBatch(okN, skipN, failN), Program.AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(S.FavDone(okN), Program.AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            LoadList();
        }

        private static int PickInt(string json, string key)
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    if (pic != null && pic.Image != null)
                    {
                        Image old = pic.Image;
                        pic.Image = null;
                        old.Dispose();
                    }
                }
                catch { }
            }
            base.Dispose(disposing);
        }
    }
}
