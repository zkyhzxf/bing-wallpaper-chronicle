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
    /// 关窗时二选：暂存到归档、或连同本次缩略图一起丢掉。
    /// </summary>
    internal sealed class PhotoWallForm : Form
    {
        private const int CellW = 320;
        private const int CellH = 180;
        private const int Cols = 4;
        private const int ThumbW = 320;
        private const int ThumbH = 180;

        private readonly List<string> dates = new List<string>();
        private readonly List<PictureBox> boxes = new List<PictureBox>();
        private readonly List<Label> captions = new List<Label>();
        private readonly List<string> usedFiles = new List<string>();
        private readonly List<string> missing = new List<string>();
        private readonly Label lblStatus;
        private readonly Button btnFetch;
        private readonly Button btnClose;
        private bool working = false;

        public PhotoWallForm()
        {
            string today = DateTime.Today.ToString("MMdd");
            Text = Program.AppName + " · " + S.WallTitle(today);
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = true;
            MinimizeBox = true;
            ShowInTaskbar = true;
            // 默认宽度取屏幕工作区的 80%，至少放得下 4 列
            int wantW = Cols * (CellW + 22) + 44;
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
            ClientSize = new Size(startW, Math.Max(560, Math.Min(780, capH)));
            try { Font = new Font("Microsoft YaHei UI", 9F); } catch { }

            int yearNow = DateTime.Today.Year;
            for (int y = yearNow - 1; y >= Program.ArchiveMinDateValue.Year; y--)
            {
                DateTime dt;
                try { dt = new DateTime(y, DateTime.Today.Month, DateTime.Today.Day); }
                catch { continue; }
                if (dt < Program.ArchiveMinDateValue) { continue; }
                dates.Add(dt.ToString("yyyyMMdd"));
            }

            Panel host = new Panel();
            host.Dock = DockStyle.Fill;
            host.AutoScroll = true;
            Controls.Add(host);

            FlowLayoutPanel flow = new FlowLayoutPanel();
            flow.Dock = DockStyle.Fill;
            flow.AutoScroll = true;
            flow.Padding = new Padding(6);
            host.Controls.Add(flow);

            for (int i = 0; i < dates.Count; i++)
            {
                string d8 = dates[i];
                Panel cell = new Panel();
                cell.SetBounds(0, 0, CellW + 8, CellH + 44);
                cell.Margin = new Padding(6);

                PictureBox pic = new PictureBox();
                pic.SetBounds(4, 4, CellW, CellH);
                pic.SizeMode = PictureBoxSizeMode.Zoom;
                pic.BackColor = Color.FromArgb(36, 36, 36);
                pic.Tag = d8;
                pic.Cursor = Cursors.Hand;
                pic.DoubleClick += delegate { ApplyWallpaper(pic.Tag as string); };
                pic.Click += delegate(object s, EventArgs e) { OnCellClick(s as PictureBox); };
                cell.Controls.Add(pic);
                boxes.Add(pic);

                Label cap = new Label();
                cap.SetBounds(4, CellH + 6, CellW, 34);
                cap.AutoSize = false;
                cap.Text = d8.Substring(0, 4) + "-" + d8.Substring(4, 2) + "-" + d8.Substring(6, 2) + "  …";
                cell.Controls.Add(cap);
                captions.Add(cap);

                flow.Controls.Add(cell);
            }

            Panel bottom = new Panel();
            bottom.Dock = DockStyle.Bottom;
            bottom.Height = 46;
            Controls.Add(bottom);
            bottom.BringToFront();

            lblStatus = new Label();
            lblStatus.SetBounds(12, 14, 520, 20);
            lblStatus.ForeColor = SystemColors.GrayText;
            bottom.Controls.Add(lblStatus);

            btnFetch = new Button();
            btnFetch.Text = S.WallFetchMissing;
            btnFetch.SetBounds(560, 10, 140, 28);
            btnFetch.Click += delegate { FetchAll(); };
            bottom.Controls.Add(btnFetch);

            btnClose = new Button();
            btnClose.Text = S.BtnClose;
            btnClose.SetBounds(712, 10, 90, 28);
            btnClose.Click += delegate { Close(); };
            bottom.Controls.Add(btnClose);
            AcceptButton = btnClose;

            try { Program.TrimThumbs(); } catch { }
            LoadCells();
        }

        /// <summary>逐个格子找本地文件：收藏 → 缓存 → 归档 → 老存档 → 照片墙临时目录。</summary>
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

        /// <summary>缩略图走磁盘缓存；没有就后台生成，生成完回填。</summary>
        private void LoadCells()
        {
            missing.Clear();
            Dictionary<string, object> idx = null;
            try { idx = Program.LoadArchiveIndex(); } catch { }
            int have = 0;
            for (int i = 0; i < dates.Count; i++)
            {
                string d8 = dates[i];
                string src = FindLocal(d8);
                string title = "";
                try
                {
                    Dictionary<string, object> e = (idx != null && idx.ContainsKey(d8)) ? (idx[d8] as Dictionary<string, object>) : null;
                    if (e != null && e.ContainsKey("title") && e["title"] != null) { title = e["title"].ToString(); }
                }
                catch { }
                if (title.Length > 40) { title = title.Substring(0, 40) + "…"; }
                captions[i].Text = d8.Substring(0, 4) + "-" + d8.Substring(4, 2) + "-" + d8.Substring(6, 2) + "  "
                                   + (title.Length > 0 ? title : S.NoTitle);

                if (src == null)
                {
                    missing.Add(d8);
                    boxes[i].Image = null;
                    boxes[i].BackColor = Color.FromArgb(28, 28, 28);
                    captions[i].ForeColor = SystemColors.GrayText;
                    continue;
                }
                have++;
                usedFiles.Add(src);
                captions[i].ForeColor = SystemColors.ControlText;
                string thumb = ThumbPath(d8);
                if (File.Exists(thumb))
                {
                    try { boxes[i].Image = LoadNoLock(thumb); } catch { }
                    usedFiles.Add(thumb);
                }
                else
                {
                    string s = src;
                    int slot = i;
                    ThreadPool.QueueUserWorkItem(delegate
                    {
                        string made = MakeThumbFile(s, d8);
                        if (made == null) { return; }
                        try
                        {
                            BeginInvoke((MethodInvoker)delegate
                            {
                                try { boxes[slot].Image = LoadNoLock(made); } catch { }
                            });
                        }
                        catch { }
                    });
                }
            }
            UpdateStatus(have);
        }

        private void UpdateStatus(int have)
        {
            try
            {
                string txt = string.Format(S.WallStatusFmt, have, dates.Count, missing.Count);
                if (missing.Count == 0) { txt = txt + "  " + S.WallIdleHint; }
                lblStatus.Text = txt;
                btnFetch.Enabled = (missing.Count > 0) && !working;
            }
            catch { }
        }

        private void OnCellClick(PictureBox pic)
        {
            if (pic == null || working) { return; }
            string d8 = pic.Tag as string;
            if (string.IsNullOrEmpty(d8)) { return; }
            if (FindLocal(d8) != null) { return; }
            working = true;
            lblStatus.Text = S.WallFetching;
            btnFetch.Enabled = false;
            ThreadPool.QueueUserWorkItem(delegate
            {
                int code = Program.RunScriptExit("-WallDate " + d8);
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        working = false;
                        LoadCells();
                    });
                }
                catch { }
            });
        }

        /// <summary>串行补齐：脚本用全局运行锁，并发只会互相挡。</summary>
        private void FetchAll()
        {
            if (working || missing.Count == 0) { return; }
            working = true;
            btnFetch.Enabled = false;
            List<string> todo = new List<string>(missing);
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
                        LoadCells();
                        lblStatus.Text = S.WallFetchDone(ok, todo.Count);
                    });
                }
                catch { }
            });
        }

        /// <summary>双击格子：把这张设为桌面壁纸（走 -Date，和托盘里的换图完全同一条路）。</summary>
        private void ApplyWallpaper(string d8)
        {
            if (string.IsNullOrEmpty(d8) || working) { return; }
            if (FindLocal(d8) == null) { OnCellClick(null); }
            string path = FindLocal(d8);
            if (path == null) { return; }
            try { Program.RunScriptExit("-Use \"" + path + "\""); } catch { }
            try { MessageBox.Show(S.WallApplied, Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information); } catch { }
        }

        private static string ThumbPath(string d8)
        {
            return Path.Combine(Program.ThumbsDirPath, d8 + ".jpg");
        }

        private static Bitmap LoadNoLock(string file)
        {
            byte[] bytes = File.ReadAllBytes(file);
            using (MemoryStream ms = new MemoryStream(bytes))
            using (Image src = Image.FromStream(ms))
            {
                return new Bitmap(src);
            }
        }

        /// <summary>生成并落盘缩略图；源图不存在或解码失败返回 null。</summary>
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

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (e.Cancel) { return; }
            try
            {
                int nWall = CountWall();
                if (nWall <= 0)
                {
                    // 没有临时图就没有去处要选，但仍给一句反馈，免得"关了什么都没发生"
                    Program.Notify(S.WallCloseTitle, S.WallClosedIdle);
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
                    Program.Notify(S.WallCloseKeep, string.Format(S.WallClosedKeep, nWall));
                }
                else
                {
                    Program.RunScriptExit("-WallDrop");
                    Program.Notify(S.WallCloseDrop, string.Format(S.WallClosedDrop, nWall));
                    foreach (string t in usedFiles)
                    {
                        try
                        {
                            // 只清缩略图；原图属于 cache / archive / 收藏，不在这里动
                            if (t.StartsWith(Program.ThumbsDirPath, StringComparison.OrdinalIgnoreCase) && File.Exists(t))
                            {
                                File.Delete(t);
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }
    }

    /// <summary>照片墙关窗时的二选：暂存到归档，或不保留。没有「取消」——不想选就别关窗。</summary>
    internal sealed class WallCloseForm : Form
    {
        public bool Keep { get; private set; }
        public bool Remember { get; private set; }

        public WallCloseForm(int n)
        {
            Text = Program.AppName + " · " + S.WallCloseTitle;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(440, 200);
            try { Font = new Font("Microsoft YaHei UI", 9F); } catch { }

            Label lbl = new Label();
            lbl.SetBounds(16, 14, 408, 60);
            lbl.Text = S.WallCloseBody(n);
            Controls.Add(lbl);

            CheckBox chk = new CheckBox();
            chk.Text = S.WallCloseRemember;
            chk.SetBounds(16, 80, 300, 24);
            Controls.Add(chk);

            Button btnKeep = new Button();
            btnKeep.Text = S.WallCloseKeep;
            btnKeep.SetBounds(16, 150, 150, 32);
            btnKeep.Click += delegate { Keep = true; Remember = chk.Checked; DialogResult = DialogResult.OK; Close(); };
            Controls.Add(btnKeep);

            Button btnDrop = new Button();
            btnDrop.Text = S.WallCloseDrop;
            btnDrop.SetBounds(276, 150, 150, 32);
            btnDrop.Click += delegate { Keep = false; Remember = chk.Checked; DialogResult = DialogResult.OK; Close(); };
            Controls.Add(btnDrop);

            Button btnBack = new Button();
            btnBack.Text = S.WallCloseBack;
            btnBack.SetBounds(176, 150, 90, 32);
            btnBack.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(btnBack);

            AcceptButton = btnKeep;
            CancelButton = btnBack;
        }
    }
}
