using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace BingWallpaperLite
{
    /// <summary>
    /// 照片墙：把历年同一天的图一次摆出来。
    /// 临时下载落 cache\wall，缩略图落 cache\thumbs（按日期命名，一张一份）；
    /// 关窗时弹窗二选：暂存到归档、或连同本次缩略图一起丢掉。
    /// </summary>
    internal sealed class PhotoWallForm : Form
    {
        private const int ThumbW = 320;
        private const int ThumbH = 180;
        private const int Gap = 16;
        private const int Pad = 12;

        private readonly List<WallCard> cards = new List<WallCard>();
        private readonly List<string> dates = new List<string>();
        private readonly Dictionary<string, Image> thumbs = new Dictionary<string, Image>();
        private readonly List<string> usedThumbs = new List<string>();
        private readonly List<string> missing = new List<string>();

        private readonly Panel host;
        private readonly Label lblStatus;
        private readonly Button btnFetch;
        private readonly ComboBox cmbSort;
        private readonly Button btnSetWall;
        private readonly Button btnFav;
        private readonly Button btnOpen;
        private readonly CheckBox chkLocal;

        private readonly System.Windows.Forms.Timer ticker;
        private string selected = "";
        private readonly string openWallpaper = "";
        private bool working = false;
        private int localCount = 0;

        public PhotoWallForm()
        {
            try { WallTheme.Apply(); } catch { }
            Text = Program.AppName + " · " + S.WallTitle(DateTime.Today.ToString("MMdd"));
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = true;
            MinimizeBox = true;
            ShowInTaskbar = true;
            try { Font = new Font("Microsoft YaHei UI", 9F); } catch { }
            BackColor = WallTheme.Bg;

            int wantW = 4 * (WallCard.CardWidth + Gap) + Pad * 2 + 24;
            int capW = 1200;
            int capH = 700;
            try
            {
                Rectangle wa = Screen.PrimaryScreen.WorkingArea;
                capW = wa.Width - 80;
                capH = wa.Height - 100;
            }
            catch { }
            int startW = Math.Min(wantW, capW);
            try
            {
                int prefer = (int)(Screen.PrimaryScreen.WorkingArea.Width * 0.8);
                if (prefer > startW && prefer <= capW) { startW = prefer; }
            }
            catch { }
            ClientSize = new Size(startW, Math.Max(600, Math.Min(820, capH)));

            int yearNow = DateTime.Today.Year;
            for (int y = yearNow - 1; y >= Program.ArchiveMinDateValue.Year; y--)
            {
                DateTime dt;
                try { dt = new DateTime(y, DateTime.Today.Month, DateTime.Today.Day); }
                catch { continue; }
                if (dt < Program.ArchiveMinDateValue) { continue; }
                dates.Add(dt.ToString("yyyyMMdd"));
            }

            // ---------------------------------------------------------------- 顶部工具条
            Panel top = new Panel();
            top.Dock = DockStyle.Top;
            top.Height = 44;
            top.BackColor = WallTheme.Bg;
            Controls.Add(top);

            Label lblSort = new Label();
            lblSort.Text = S.WallSortLabel;
            lblSort.SetBounds(12, 13, 40, 20);
            lblSort.ForeColor = WallTheme.TextDim;
            top.Controls.Add(lblSort);

            cmbSort = new ComboBox();
            cmbSort.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbSort.SetBounds(52, 9, 120, 24);
            cmbSort.Items.Add(S.WallSortNew);
            cmbSort.Items.Add(S.WallSortOld);
            cmbSort.SelectedIndex = 0;
            cmbSort.SelectedIndexChanged += delegate { LoadData(); };
            top.Controls.Add(cmbSort);

            chkLocal = new CheckBox();
            chkLocal.Text = S.WallFilterLocal;
            chkLocal.SetBounds(190, 12, 120, 22);
            chkLocal.ForeColor = WallTheme.Text;
            chkLocal.CheckedChanged += delegate { LoadData(); };
            top.Controls.Add(chkLocal);




            // ---------------------------------------------------------------- 底部栏
            Panel bottom = new Panel();
            bottom.Dock = DockStyle.Bottom;
            bottom.Height = 56;
            bottom.BackColor = WallTheme.Bg;
            Controls.Add(bottom);

            // 右侧按钮组用 FlowLayoutPanel 从右往左排，窗口变窄也不会互相压住
            FlowLayoutPanel bar = new FlowLayoutPanel();
            bar.Dock = DockStyle.Right;
            bar.FlowDirection = FlowDirection.LeftToRight;
            bar.WrapContents = false;
            bar.AutoSize = true;
            bar.Padding = new Padding(0, 12, 10, 0);
            bottom.Controls.Add(bar);

            btnSetWall = new Button();
            btnSetWall.Text = S.WallBtnSet;
            btnSetWall.Size = new Size(96, 30);
            btnSetWall.Margin = new Padding(4, 0, 4, 0);
            btnSetWall.Enabled = false;
            btnSetWall.Click += delegate { ApplySelected(); };
            bar.Controls.Add(btnSetWall);

            btnFav = new Button();
            btnFav.Text = S.WallBtnFav;
            btnFav.Size = new Size(110, 30);
            btnFav.Margin = new Padding(4, 0, 4, 0);
            btnFav.Enabled = false;
            btnFav.Click += delegate { FavSelected(); };
            bar.Controls.Add(btnFav);

            btnOpen = new Button();
            btnOpen.Text = S.WallBtnOpen;
            btnOpen.Size = new Size(96, 30);
            btnOpen.Margin = new Padding(4, 0, 4, 0);
            btnOpen.Enabled = false;
            btnOpen.Click += delegate { OpenSelected(); };
            bar.Controls.Add(btnOpen);

            btnFetch = new Button();
            btnFetch.Text = S.WallFetchMissing;
            btnFetch.Size = new Size(112, 30);
            btnFetch.Margin = new Padding(4, 0, 4, 0);
            btnFetch.Click += delegate { FetchAll(); };
            bar.Controls.Add(btnFetch);

            Button btnClose = new Button();
            btnClose.Text = S.BtnClose;
            btnClose.Size = new Size(80, 30);
            btnClose.Margin = new Padding(4, 0, 0, 0);
            btnClose.Click += delegate { Close(); };
            bar.Controls.Add(btnClose);

            lblStatus = new Label();
            lblStatus.Dock = DockStyle.Fill;
            lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            lblStatus.Padding = new Padding(14, 0, 0, 0);
            lblStatus.ForeColor = WallTheme.TextDim;
            bottom.Controls.Add(lblStatus);
            lblStatus.BringToFront();

            // ---------------------------------------------------------------- 卡片区
            host = new Panel();
            host.Dock = DockStyle.Fill;
            host.AutoScroll = true;
            host.BackColor = WallTheme.Bg;
            Controls.Add(host);
            host.Resize += delegate { LayoutCards(); };

            ticker = new System.Windows.Forms.Timer();
            ticker.Interval = 150;
            ticker.Tick += delegate
            {
                foreach (WallCard c in cards) { c.TickSpin(); }
            };
            ticker.Start();

            // Dock 按 Z 序从高到低分配空间：把 Fill 的 host 放到最底层，顶部/底部才不会被它压住
            Controls.SetChildIndex(host, Controls.Count - 1);

            try { openWallpaper = Program.CurrentWallpaperPath(); } catch { }
            try { Program.TrimThumbs(); } catch { }
            LoadData();
        }

        // ---------------------------------------------------------------- 数据

        private static string FindLocal(string d8)
        {
            try
            {
                string[] dirs = new string[] { Program.FavDirPath, Program.CacheDirPath, Program.ArchiveImgPath, Program.WallDirPath };
                foreach (string dir in dirs)
                {
                    if (!Directory.Exists(dir)) { continue; }
                    string[] hit = Directory.GetFiles(dir, d8 + "_*.jpg");
                    if (hit.Length > 0) { return hit[0]; }
                }
                string om = Program.OldMonthDir(d8);
                if (om != null && Directory.Exists(om))
                {
                    string[] hit = Directory.GetFiles(om, d8 + "_*.jpg");
                    if (hit.Length > 0) { return hit[0]; }
                }
            }
            catch { }
            return null;
        }

        private static string ThumbPath(string d8)
        {
            return Path.Combine(Program.ThumbsDirPath, d8 + ".jpg");
        }

        private void ClearCards()
        {
            foreach (WallCard c in cards)
            {
                try { host.Controls.Remove(c); c.Dispose(); } catch { }
            }
            cards.Clear();
            host.Controls.Clear();
        }

        private void LoadData()
        {
            ClearCards();
            missing.Clear();
            usedThumbs.Clear();
            localCount = 0;
            selected = "";

            Dictionary<string, object> idx = null;
            try { idx = Program.LoadArchiveIndex(); } catch { }

            List<string> order = new List<string>(dates);
            if (cmbSort.SelectedIndex == 1) { order.Reverse(); }

            foreach (string d8 in order)
            {
                string src = FindLocal(d8);
                if (chkLocal.Checked && src == null) { continue; }
                if (src == null) { missing.Add(d8); }
                else
                {
                    localCount++;
                    usedThumbs.Add(src);
                    string tp = ThumbPath(d8);
                    if (File.Exists(tp)) { usedThumbs.Add(tp); }
                }

                string title = "";
                try
                {
                    Dictionary<string, object> e = (idx != null && idx.ContainsKey(d8)) ? (idx[d8] as Dictionary<string, object>) : null;
                    if (e != null && e.ContainsKey("title") && e["title"] != null) { title = e["title"].ToString(); }
                }
                catch { }

                WallCard card = new WallCard();
                card.Date8 = d8;
                card.TitleText = title;
                card.HasImage = (src != null);
                card.SuspendLayout();
                card.Tag = src;
                card.Click += delegate { OnCardClick(card); };
                card.DoubleClick += delegate { OnCardDoubleClick(card); };
                card.MouseUp += delegate(object s, MouseEventArgs me)
                {
                    if (me.Button == MouseButtons.Right)
                    {
                        ShowCardMenu(card, card.PointToScreen(me.Location));
                    }
                };
                cards.Add(card);
                host.Controls.Add(card);
                card.ResumeLayout();

                if (src != null) { AttachThumb(card, d8, src); }
            }

            LayoutCards();
            UpdateStatus();
        }

        private void AttachThumb(WallCard card, string d8, string src)
        {
            Image cached;
            if (thumbs.TryGetValue(d8, out cached))
            {
                card.Thumb = cached;
                card.Invalidate();
                return;
            }
            string tp = ThumbPath(d8);
            if (File.Exists(tp))
            {
                try
                {
                    card.Thumb = LoadNoLock(tp);
                    thumbs[d8] = card.Thumb;
                    card.Invalidate();
                    return;
                }
                catch { }
            }

            card.Loading = true;
            string s = src;
            ThreadPool.QueueUserWorkItem(delegate
            {
                string made = MakeThumbFile(s, d8);
                if (made == null)
                {
                    try { BeginInvoke((MethodInvoker)delegate { card.Loading = false; card.Invalidate(); }); } catch { }
                    return;
                }
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        try
                        {
                            card.Thumb = LoadNoLock(made);
                            thumbs[d8] = card.Thumb;
                        }
                        catch { }
                        card.Loading = false;
                        card.Invalidate();
                    });
                }
                catch { }
            });
        }

        /// <summary>列数按目标宽度算，再把可用宽度均分给每张卡片 —— 右侧不留空白。</summary>
        private void LayoutCards()
        {
            if (cards.Count == 0) { return; }
            int avail = host.ClientSize.Width - Pad * 2 - 22;
            if (avail < WallCard.MinWidth) { avail = WallCard.MinWidth; }
            int cols = Math.Max(1, (avail + Gap) / (WallCard.CardWidth + Gap));
            int cellW = (avail - Gap * (cols - 1)) / cols;
            if (cellW < WallCard.MinWidth) { cellW = WallCard.MinWidth; }
            if (cellW > WallCard.MaxWidth) { cellW = WallCard.MaxWidth; }
            int cellH = WallCard.HeightFor(cellW);
            for (int i = 0; i < cards.Count; i++)
            {
                int col = i % cols;
                int row = i / cols;
                cards[i].SetBounds(Pad + col * (cellW + Gap),
                                   Pad + row * (cellH + Gap),
                                   cellW, cellH);
            }
        }


        private void UpdateStatus()
        {
            try
            {
                string txt = string.Format(S.WallStatusFmt, localCount, dates.Count, missing.Count);
                if (selected.Length > 0)
                {
                    string t = "";
                    foreach (WallCard c in cards)
                    {
                        if (c.Date8 == selected) { t = c.TitleText; break; }
                    }
                    txt = selected.Substring(0, 4) + "-" + selected.Substring(4, 2) + "-" + selected.Substring(6, 2)
                          + "  " + (t.Length > 0 ? t : S.NoTitle) + "    ｜    " + txt;
                }
                else if (missing.Count == 0)
                {
                    txt = txt + "  " + S.WallIdleHint;
                }
                lblStatus.Text = txt;
                btnFetch.Enabled = (missing.Count > 0) && !working;
                bool hasSel = false;
                if (selected.Length > 0)
                {
                    foreach (WallCard c in cards)
                    {
                        if (c.Date8 == selected && c.HasImage) { hasSel = true; break; }
                    }
                }
                if (btnSetWall != null) { btnSetWall.Enabled = hasSel && !working; }
                if (btnFav != null)
                {
                    btnFav.Enabled = hasSel && !working;
                    bool fav = false;
                    if (hasSel) { try { fav = Program.IsDateFavorited(selected); } catch { } }
                    btnFav.Text = fav ? S.WallBtnUnfav : S.WallBtnFav;
                }
                if (btnOpen != null) { btnOpen.Enabled = hasSel && !working; }
            }
            catch { }
        }

        // ---------------------------------------------------------------- 交互

        private void OnCardClick(WallCard card)
        {
            if (card == null) { return; }
            if (!card.HasImage)
            {
                FetchOne(card);
                return;
            }
            foreach (WallCard c in cards) { if (c.Selected) { c.Selected = false; c.Invalidate(); } }
            card.Selected = true;
            card.Invalidate();
            selected = card.Date8;
            UpdateStatus();
        }

        private void OnCardDoubleClick(WallCard card)
        {
            if (card == null) { return; }
            string src = card.Tag as string;
            if (string.IsNullOrEmpty(src)) { FetchOne(card); return; }
            try
            {
                int code = Program.RunScriptExit("-Use \"" + src + "\"");
                if (code == 0 || code == 4 || code == 8)
                {
                    MessageBox.Show(S.WallApplied, Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch { }
        }

        private WallCard SelectedCard()
        {
            if (selected.Length == 0) { return null; }
            foreach (WallCard c in cards) { if (c.Date8 == selected) { return c; } }
            return null;
        }

        private void ApplySelected()
        {
            WallCard c = SelectedCard();
            if (c == null) { return; }
            string src = c.Tag as string;
            if (string.IsNullOrEmpty(src)) { return; }
            working = true;
            UpdateStatus();
            try
            {
                int code = Program.RunScriptExit("-Use \"" + src + "\"");
                if (code == 0 || code == 4 || code == 8)
                {
                    MessageBox.Show(S.WallApplied, Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            finally
            {
                working = false;
                UpdateStatus();
            }
        }

        /// <summary>收藏 = 先设为当前图，再进收藏夹（-Favorite 操作的是 state 里那张）。</summary>
        private void FavSelected()
        {
            WallCard c = SelectedCard();
            if (c == null) { return; }
            string src = c.Tag as string;
            if (string.IsNullOrEmpty(src)) { return; }
            bool wasFav = false;
            try { wasFav = Program.IsDateFavorited(c.Date8); } catch { }
            working = true;
            UpdateStatus();
            try
            {
                int code = Program.RunScriptExit("-Use \"" + src + "\"");
                if (code != 0 && code != 4 && code != 8)
                {
                    MessageBox.Show(S.WallFavFail, Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                int code2 = Program.RunScriptExit(wasFav ? "-Unfavorite" : "-Favorite");
                MessageBox.Show(code2 == 0 ? (wasFav ? S.WallUnfavDone : S.WallFavDone) : S.WallFavFail,
                    Program.AppName, MessageBoxButtons.OK,
                    code2 == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
            finally
            {
                working = false;
                UpdateStatus();
            }
        }

        private void OpenSelected()
        {
            WallCard c = SelectedCard();
            if (c == null) { return; }
            string src = c.Tag as string;
            if (!string.IsNullOrEmpty(src)) { ShowInFolder(src); }
        }

        /// <summary>右键菜单：看图相关的几个动作。</summary>
        private void ShowCardMenu(WallCard card, Point screenPt)
        {
            ContextMenuStrip m = new ContextMenuStrip();
            try { m.Font = new Font("Microsoft YaHei UI", 9F); } catch { }
            string src = card.Tag as string;
            if (!string.IsNullOrEmpty(src))
            {
                m.Items.Add(S.WallSetAs, null, delegate { OnCardDoubleClick(card); });
                m.Items.Add(S.WallShowInFolder, null, delegate { ShowInFolder(src); });
                m.Items.Add(S.WallCopyTitle, null, delegate
                {
                    try { Clipboard.SetText(card.TitleText.Length > 0 ? card.TitleText : card.Date8); } catch { }
                });
            }
            else
            {
                m.Items.Add(S.WallCardFetch, null, delegate { FetchOne(card); });
            }
            m.Show(screenPt);
        }

        private static void ShowInFolder(string path)
        {
            try { System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + path + "\""); } catch { }
        }

        private void FetchOne(WallCard card)
        {
            if (working || card == null) { return; }
            string d8 = card.Date8;
            working = true;
            card.Loading = true;
            card.Invalidate();
            lblStatus.Text = S.WallFetching;
            btnFetch.Enabled = false;
            ThreadPool.QueueUserWorkItem(delegate
            {
                Program.RunScriptExit("-WallDate " + d8);
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        working = false;
                        LoadData();
                    });
                }
                catch { }
            });
        }

        private void FetchAll()
        {
            if (working || missing.Count == 0) { return; }
            working = true;
            btnFetch.Enabled = false;
            List<string> todo = new List<string>(missing);
            foreach (WallCard c in cards)
            {
                if (!c.HasImage) { c.Loading = true; c.Invalidate(); }
            }
            ThreadPool.QueueUserWorkItem(delegate
            {
                int ok = 0;
                for (int i = 0; i < todo.Count; i++)
                {
                    string d8 = todo[i];
                    int cur = i + 1;
                    try
                    {
                        BeginInvoke((MethodInvoker)delegate
                        {
                            lblStatus.Text = string.Format(S.WallFetchingN, cur, todo.Count);
                        });
                    }
                    catch { }
                    int code = Program.RunScriptExit("-WallDate " + d8);
                    if (code == 0 || code == 4 || code == 8) { ok++; }
                }
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        working = false;
                        LoadData();
                        lblStatus.Text = S.WallFetchDone(ok, todo.Count);
                    });
                }
                catch { }
            });
        }

        // ---------------------------------------------------------------- 缩略图

        private static Bitmap LoadNoLock(string file)
        {
            byte[] bytes = File.ReadAllBytes(file);
            using (MemoryStream ms = new MemoryStream(bytes))
            using (Image src = Image.FromStream(ms))
            {
                return new Bitmap(src);
            }
        }

        private static string MakeThumbFile(string src, string d8)
        {
            try
            {
                if (!Directory.Exists(Program.ThumbsDirPath)) { Directory.CreateDirectory(Program.ThumbsDirPath); }
                string dest = ThumbPath(d8);
                byte[] bytes = File.ReadAllBytes(src);
                using (MemoryStream ms = new MemoryStream(bytes))
                using (Image image = Image.FromStream(ms))
                using (Bitmap bmp = new Bitmap(ThumbW, ThumbH))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                        g.DrawImage(image, 0, 0, ThumbW, ThumbH);
                    }
                    bmp.Save(dest, ImageFormat.Jpeg);
                }
                return dest;
            }
            catch { return null; }
        }

        private int CountWall()
        {
            try
            {
                if (!Directory.Exists(Program.WallDirPath)) { return 0; }
                return Directory.GetFiles(Program.WallDirPath, "*.jpg").Length;
            }
            catch { return 0; }
        }

        // ---------------------------------------------------------------- 关窗

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (e.Cancel) { return; }

            try
            {
                int nWall = CountWall();
                if (nWall <= 0)
                {
                    // 没有临时图：仍然弹窗确认，不再用托盘气泡（那条路常被系统吞掉）
                    using (WallCloseForm f = new WallCloseForm(0))
                    {
                        if (f.ShowDialog(this) != DialogResult.OK) { e.Cancel = true; return; }
                    }
                    return;
                }

                string choice = Program.CfgString("wallOnClose", "");
                if (choice != "keep" && choice != "drop")
                {
                    using (WallCloseForm f = new WallCloseForm(nWall))
                    {
                        if (f.ShowDialog(this) != DialogResult.OK) { e.Cancel = true; return; }
                        choice = f.Keep ? "keep" : "drop";
                        if (f.Remember) { Program.CfgSetString("wallOnClose", choice); }
                    }
                }

                if (choice == "keep")
                {
                    Program.RunScriptExit("-WallKeep");
                    MessageBox.Show(S.WallResultKeep(nWall), Program.AppName,
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    // 桌面壁纸如果正指向临时目录里的文件，先换回打开照片墙前那张，再销毁
                    try
                    {
                        string curWp = Program.CurrentWallpaperPath();
                        if (!string.IsNullOrEmpty(curWp)
                            && curWp.StartsWith(Program.WallDirPath, StringComparison.OrdinalIgnoreCase)
                            && !string.IsNullOrEmpty(openWallpaper)
                            && File.Exists(openWallpaper))
                        {
                            Program.RunScriptExit("-Use \"" + openWallpaper + "\"");
                        }
                    }
                    catch { }
                    Program.RunScriptExit("-WallDrop");
                    foreach (string t in usedThumbs)
                    {
                        try
                        {
                            if (t.StartsWith(Program.ThumbsDirPath, StringComparison.OrdinalIgnoreCase) && File.Exists(t))
                            {
                                File.Delete(t);
                            }
                        }
                        catch { }
                    }
                    MessageBox.Show(S.WallResultDrop(nWall), Program.AppName,
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch { }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { ticker.Stop(); ticker.Dispose(); } catch { }
                foreach (Image im in thumbs.Values) { try { im.Dispose(); } catch { } }
                thumbs.Clear();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>照片墙关窗时的询问：有临时图给两个去向；没有就只是确认关闭。</summary>
    internal sealed class WallCloseForm : Form
    {
        public bool Keep { get; private set; }
        public bool Remember { get; private set; }

        public WallCloseForm(int n)
        {
            bool hasTmp = n > 0;
            Text = Program.AppName + " · " + S.WallCloseTitle;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(460, hasTmp ? 210 : 160);
            try { Font = new Font("Microsoft YaHei UI", 9F); } catch { }

            Label lbl = new Label();
            lbl.AutoSize = false;
            lbl.SetBounds(16, 14, 428, 62);
            lbl.Text = hasTmp ? S.WallCloseBody(n) : S.WallCloseIdleBody;
            Controls.Add(lbl);

            if (hasTmp)
            {
                CheckBox chk = new CheckBox();
                chk.Text = S.WallCloseRemember;
                chk.SetBounds(16, 84, 320, 24);
                Controls.Add(chk);

                Button btnKeep = new Button();
                btnKeep.Text = S.WallCloseKeep;
                btnKeep.SetBounds(16, 156, 150, 32);
                btnKeep.Click += delegate { Keep = true; Remember = chk.Checked; DialogResult = DialogResult.OK; Close(); };
                Controls.Add(btnKeep);

                Button btnDrop = new Button();
                btnDrop.Text = S.WallCloseDrop;
                btnDrop.SetBounds(294, 156, 150, 32);
                btnDrop.Click += delegate { Keep = false; Remember = chk.Checked; DialogResult = DialogResult.OK; Close(); };
                Controls.Add(btnDrop);

                Button btnBack = new Button();
                btnBack.Text = S.WallCloseBack;
                btnBack.SetBounds(174, 156, 112, 32);
                btnBack.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
                Controls.Add(btnBack);

                AcceptButton = btnKeep;
                CancelButton = btnBack;
            }
            else
            {
                Button btnOk = new Button();
                btnOk.Text = S.BtnClose;
                btnOk.SetBounds(16, 108, 150, 32);
                btnOk.Click += delegate { DialogResult = DialogResult.OK; Close(); };
                Controls.Add(btnOk);

                Button btnBack2 = new Button();
                btnBack2.Text = S.WallCloseBack;
                btnBack2.SetBounds(294, 108, 150, 32);
                btnBack2.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
                Controls.Add(btnBack2);

                AcceptButton = btnOk;
                CancelButton = btnBack2;
            }
        }
    }
}
