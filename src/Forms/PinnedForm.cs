using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace BingWallpaperLite
{
    /// <summary>自动清理保留：查看、预览、移出清单、加入收藏、清理失效项。只解除保留，永不删图。</summary>
    internal sealed class PinnedForm : Form
    {
        private readonly ListView lst;
        private readonly Label lblSum;
        private readonly Label lblInfo;
        private readonly PictureBox pic;
        private readonly Button btnRemove;
        private readonly Button btnOpen;
        private readonly Button btnFav;
        private readonly Button btnPurge;

        private readonly TextBox txtSearch;
        private int sortCol = 0;
        private bool sortAsc = true;
        private int totalRows = 0;
        private readonly List<string> dates = new List<string>();
        private readonly List<string> files = new List<string>();

        public PinnedForm()
        {
            Text = Program.AppName + S.PinTitle;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            ClientSize = new Size(620, 556);
            try { Font = new Font("Microsoft YaHei UI", 9F); } catch { }

            Label l1 = new Label();
            l1.SetBounds(16, 12, 588, 20);
            l1.Text = S.PinIntro1;
            Controls.Add(l1);


            lblSum = new Label();
            lblSum.SetBounds(16, 38, 176, 20);
            lblSum.AutoSize = false;
            Controls.Add(lblSum);

            Label lblSearch = new Label();
            lblSearch.Text = S.IsEn ? "Find" : "搜索";
            lblSearch.SetBounds(198, 40, 34, 18);
            Controls.Add(lblSearch);

            txtSearch = new TextBox();
            txtSearch.SetBounds(232, 36, 120, 24);
            txtSearch.TextChanged += delegate { LoadList(); };
            Program.SetCueBanner(txtSearch, S.FavSearchHint);
            Controls.Add(txtSearch);

            Button btnAll = new Button();
            btnAll.Text = S.IsEn ? "All" : "全选";
            btnAll.SetBounds(364, 36, 58, 24);
            btnAll.Click += delegate { CheckAll(true); };
            Controls.Add(btnAll);

            Button btnInvert = new Button();
            btnInvert.Text = S.FavInvert;
            btnInvert.SetBounds(428, 36, 58, 24);
            btnInvert.Click += delegate { InvertAll(); };
            Controls.Add(btnInvert);

            Button btnNone = new Button();
            btnNone.Text = S.IsEn ? "None" : "全不选";
            btnNone.SetBounds(492, 36, 78, 24);
            btnNone.Click += delegate { CheckAll(false); };
            Controls.Add(btnNone);

            lst = new ListView();
            lst.SetBounds(16, 66, 588, 226);
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
            lst.ColumnClick += delegate(object s, ColumnClickEventArgs e) { SortBy(e.Column); };
            Controls.Add(lst);

            pic = new PictureBox();
            pic.SetBounds(16, 302, 240, 135);
            pic.BorderStyle = BorderStyle.FixedSingle;
            pic.SizeMode = PictureBoxSizeMode.Zoom;
            pic.Visible = false;
            Controls.Add(pic);

            lblInfo = new Label();
            lblInfo.SetBounds(268, 302, 336, 135);
            lblInfo.AutoSize = false;
            lblInfo.ForeColor = SystemColors.GrayText;
            lblInfo.TextAlign = ContentAlignment.MiddleCenter;
            Controls.Add(lblInfo);

            Label note = new Label();
            note.SetBounds(16, 446, 588, 56);
            note.AutoSize = false;
            note.ForeColor = SystemColors.GrayText;
            note.Text = S.PinNote;
            Controls.Add(note);

            btnPurge = new Button();
            btnPurge.SetBounds(16, 512, 140, 30);
            btnPurge.Visible = false;
            btnPurge.Click += delegate { PurgeDead(); };
            Controls.Add(btnPurge);

            btnFav = new Button();
            btnFav.Text = S.PinFav;
            btnFav.SetBounds(164, 512, 96, 30);
            btnFav.Click += delegate { AddToFavorites(); };
            Controls.Add(btnFav);

            btnRemove = new Button();
            btnRemove.SetBounds(268, 512, 104, 30);
            btnRemove.Click += delegate { RemoveChecked(); };
            Controls.Add(btnRemove);

            btnOpen = new Button();
            btnOpen.Text = S.PinOpenFile;
            btnOpen.SetBounds(380, 512, 96, 30);
            btnOpen.Click += delegate { OpenSelected(); };
            Controls.Add(btnOpen);

            Button btnClose = new Button();
            btnClose.Text = S.PinClose;
            btnClose.SetBounds(484, 512, 80, 30);
            btnClose.DialogResult = DialogResult.Cancel;
            Controls.Add(btnClose);
            CancelButton = btnClose;

            LoadList();
        }

        // ---------------------------------------------------------------- 数据

        private void LoadList()
        {
            int keepSel = lst.SelectedItems.Count > 0 ? lst.SelectedItems[0].Index : -1;

            lst.Items.Clear();
            dates.Clear();
            files.Clear();

            Dictionary<string, object> pins = Program.ReadPinnedMap();
            Dictionary<string, object> titles = null;
            try { titles = Program.LoadArchiveIndex(); } catch { }

            List<string> keys = new List<string>();
            foreach (string k in pins.Keys) { keys.Add(k); }
            keys.Sort();

            // ① 采集
            List<string[]> rows = new List<string[]>();
            foreach (string d in keys)
            {
                string file = FindFileOfDate(d);
                string title = TitleOf(d, titles);
                double mb = -1;
                try { if (file != null) { mb = new FileInfo(file).Length / 1024.0 / 1024.0; } } catch { }
                string cpr = "";
                if (titles != null && titles.ContainsKey(d))
                {
                    Dictionary<string, object> e = titles[d] as Dictionary<string, object>;
                    if (e != null && e.ContainsKey("copyright") && e["copyright"] != null) { cpr = e["copyright"].ToString(); }
                }
                rows.Add(new string[] { d, title, mb.ToString("0.0"), file == null ? "" : file, cpr });
            }
            totalRows = rows.Count;

            // ② 过滤
            string kw = (txtSearch == null ? "" : txtSearch.Text.Trim());
            if (kw.Length > 0)
            {
                rows = rows.FindAll(delegate(string[] r)
                {
                    if (r[0].IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0) { return true; }
                    if (r[1].IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0) { return true; }
                    return r[4].IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0;
                });
            }

            // ③ 排序
            rows.Sort(delegate(string[] a, string[] b)
            {
                int cmp;
                if (sortCol == 0) { cmp = string.Compare(a[0], b[0], StringComparison.Ordinal); }
                else if (sortCol == 1) { cmp = string.Compare(a[1], b[1], StringComparison.CurrentCulture); }
                else
                {
                    double ma = -1, mbb = -1;
                    double.TryParse(a[2], out ma);
                    double.TryParse(b[2], out mbb);
                    cmp = ma.CompareTo(mbb);
                }
                return sortAsc ? cmp : -cmp;
            });

            // ④ 渲染
            foreach (string[] r in rows)
            {
                ListViewItem it = new ListViewItem(r[0]);
                it.SubItems.Add(r[1]);
                if (r[3].Length == 0) { it.SubItems.Add(S.PinGone); it.ForeColor = SystemColors.GrayText; }
                else { it.SubItems.Add(r[2] + " MB"); }
                it.Tag = r[0];
                lst.Items.Add(it);
                dates.Add(r[0]);
                files.Add(r[3].Length == 0 ? null : r[3]);
            }

            RefreshSum();

            // 打开就选中第一行，避免预览区一进来就空着
            if (lst.Items.Count > 0)
            {
                int idx = keepSel;
                if (idx < 0 || idx >= lst.Items.Count) { idx = 0; }
                lst.Items[idx].Selected = true;
                lst.Items[idx].Focused = true;
            }
            else
            {
                ShowPreview(-1);
            }
            OnSelectionChanged();
        }

        private static string TitleOf(string date, Dictionary<string, object> index)
        {
            if (index != null && index.ContainsKey(date))
            {
                Dictionary<string, object> e = index[date] as Dictionary<string, object>;
                if (e != null && e.ContainsKey("title"))
                {
                    string t = e["title"] == null ? "" : e["title"].ToString();
                    if (t.Length > 0) { return t; }
                }
            }
            return S.PinNoTitle;
        }

        private static string FindFileOfDate(string date)
        {
            string[] dirs = new string[] { Program.CacheDirPath, Program.FavDirPath, Program.ArchiveImgPath };
            foreach (string d in dirs)
            {
                try
                {
                    if (string.IsNullOrEmpty(d) || !Directory.Exists(d)) { continue; }
                    string[] hit = Directory.GetFiles(d, date + "_*.jpg");
                    if (hit.Length > 0) { return hit[0]; }
                }
                catch { }
            }
            return null;
        }

        // ---------------------------------------------------------------- 预览

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
            // 先销毁旧的，不管接下来显示不显示
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

            if (lst.Items.Count == 0)
            {
                pic.Visible = false;
                SetHint(S.PinListEmpty);
                return;
            }
            if (idx < 0 || idx >= files.Count)
            {
                pic.Visible = false;
                SetHint(S.PinNoSelection);
                return;
            }
            if (files[idx] == null)
            {
                pic.Visible = false;
                SetHint(S.PinGone);
                return;
            }

            Bitmap bmp = MakeThumb(files[idx], 240, 135);
            if (bmp != null) { pic.Image = bmp; pic.Visible = true; }
            else { pic.Visible = false; }

            string title = lst.Items[idx].SubItems.Count > 1 ? lst.Items[idx].SubItems[1].Text : "";
            double mb = -1;
            try { mb = new FileInfo(files[idx]).Length / 1024.0 / 1024.0; } catch { }

            string rel = files[idx];
            try
            {
                string r = Program.ArchiveDirPath;
                if (!string.IsNullOrEmpty(r) && rel.StartsWith(r, StringComparison.OrdinalIgnoreCase))
                {
                    rel = rel.Substring(r.Length).TrimStart('\\');
                }
                else
                {
                    rel = Path.GetFileName(rel);
                }
            }
            catch { }
            if (rel.Length > 44) { rel = rel.Substring(0, 20) + "…" + rel.Substring(rel.Length - 20); }

            lblInfo.ForeColor = SystemColors.ControlText;
            lblInfo.TextAlign = ContentAlignment.TopLeft;
            lblInfo.Text = dates[idx] + Environment.NewLine +
                           title + Environment.NewLine +
                           (mb >= 0 ? (mb.ToString("0.0") + " MB") : S.PinGone) + Environment.NewLine +
                           rel;
        }

        private void OnSelectionChanged()
        {
            int idx = lst.SelectedItems.Count > 0 ? lst.SelectedItems[0].Index : -1;
            ShowPreview(idx);

            bool hasFile = idx >= 0 && idx < files.Count && files[idx] != null;
            btnOpen.Enabled = hasFile;
            btnFav.Enabled = hasFile && !InFavorites(idx);
        }

        private bool InFavorites(int idx)
        {
            try
            {
                if (idx < 0 || idx >= files.Count || files[idx] == null) { return false; }
                string fav = Program.FavDirPath;
                return !string.IsNullOrEmpty(fav) &&
                       Path.GetFullPath(files[idx]).StartsWith(
                           Path.GetFullPath(fav).TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        // ---------------------------------------------------------------- 操作

        private void SortBy(int col)
        {
            if (col < 0 || col > 2) { return; }
            if (sortCol == col) { sortAsc = !sortAsc; }
            else { sortCol = col; sortAsc = true; }
            LoadList();
        }

        private void InvertAll()
        {
            foreach (ListViewItem it in lst.Items) { it.Checked = !it.Checked; }
            RefreshSum();
        }

        private void CheckAll(bool on)
        {
            foreach (ListViewItem it in lst.Items) { it.Checked = on; }
            RefreshSum();
        }

        private void RefreshSum()
        {
            int n = 0;
            double mb = 0;
            int dead = 0;
            for (int i = 0; i < lst.Items.Count; i++)
            {
                ListViewItem it = lst.Items[i];
                if (it.Checked) { n++; }
                if (i < files.Count && files[i] == null) { dead++; }
                else if (i < files.Count)
                {
                    try { mb += new FileInfo(files[i]).Length / 1024.0 / 1024.0; } catch { }
                }
            }
            if (totalRows > 0 && lst.Items.Count != totalRows) {
                lblSum.Text = S.FavSumFiltered(lst.Items.Count, totalRows, mb);
            } else {
                lblSum.Text = S.PinSum(lst.Items.Count, mb);
            }
            btnRemove.Text = S.PinRemove;
            btnRemove.Enabled = CheckedCount() > 0;
            btnPurge.Text = S.PinPurgeDead(dead);
            btnPurge.Enabled = dead > 0;
            btnPurge.Visible = dead > 0;
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
            if (idx < 0 || idx >= files.Count || files[idx] == null)
            {
                MessageBox.Show(S.PinGone, Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try { Program.OpenTarget(files[idx]); } catch { }
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

        private void AddToFavorites()
        {
            // 勾了就用勾选的，没勾就用当前选中那一行
            List<string> picked = new List<string>();
            foreach (ListViewItem it in lst.Items)
            {
                if (it.Checked) { picked.Add((string)it.Tag); }
            }
            if (picked.Count == 0)
            {
                if (lst.SelectedItems.Count == 0) { return; }
                picked.Add((string)lst.SelectedItems[0].Tag);
            }

            Cursor prev = Cursor;
            string outText = "";
            try
            {
                Cursor = Cursors.WaitCursor;
                outText = Program.RunScriptCapture("-FavDate " + string.Join(",", picked.ToArray()));
            }
            catch { }
            finally { Cursor = prev; }

            string err = "";
            try
            {
                System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(
                    outText ?? "", "\"error\"\\s*:\\s*\"([^\"]*)\"");
                if (m.Success) { err = m.Groups[1].Value; }
            }
            catch { }

            if (err == "badarg")
            {
                MessageBox.Show(S.PinFavNoFile, Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int okN = PickInt(outText, "ok");
            int skipN = PickInt(outText, "skip");
            int failN = PickInt(outText, "fail");

            MessageBox.Show(S.PinFavBatch(okN, skipN, failN), Program.AppName,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            LoadList();
        }

        private void RemoveChecked()
        {
            List<string> picked = new List<string>();
            foreach (ListViewItem it in lst.Items) { if (it.Checked) { picked.Add((string)it.Tag); } }
            if (picked.Count == 0) { return; }

            if (MessageBox.Show(S.PinConfirm(picked.Count), Program.AppName,
                    MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
            {
                return;
            }

            Dictionary<string, object> pins = Program.ReadPinnedMap();
            foreach (string d in picked) { pins.Remove(d); }
            Program.WritePinnedMap(pins);
            LoadList();
        }

        private void PurgeDead()
        {
            Dictionary<string, object> pins = Program.ReadPinnedMap();
            List<string> dead = new List<string>();
            for (int i = 0; i < files.Count; i++)
            {
                if (files[i] == null && i < dates.Count) { dead.Add(dates[i]); }
            }
            if (dead.Count == 0) { return; }
            foreach (string d in dead) { pins.Remove(d); }
            Program.WritePinnedMap(pins);
            LoadList();
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
