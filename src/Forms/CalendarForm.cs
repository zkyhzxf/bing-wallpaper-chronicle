using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace BingWallpaperLite
{
    internal sealed class CalendarForm : Form
    {
        private readonly MonthCalendar cal;
        private readonly Label hint;
        private readonly Button ok;
        private readonly Button cancel;
        private readonly Button btnToday;
        private readonly PictureBox pic;
        private readonly Label lblDate;
        private readonly Label lblTitle;
        private readonly Label lblWhere;
        private readonly Label lblSize;
        private readonly Label lblCopy;
        private Dictionary<string, object> idxCache;
        private CalendarHook hook;

        public string SelectedDate { get; private set; }

        public CalendarForm()
        {
            Text = Program.AppName + (S.IsEn ? " · Search by date" : " · 按日期搜索");
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            ClientSize = new Size(480, 430);
            try { Font = new Font("Microsoft YaHei UI", 9F); } catch { }

            cal = new MonthCalendar();
            cal.MaxSelectionCount = 1;

            // 默认停在当前壁纸那天；读不到 / 格式不对 / 越界，一律回落到今天
            DateTime init = DateTime.Today;
            try
            {
                cal.MinDate = Program.ArchiveMinDateValue;
                cal.MaxDate = DateTime.Today;
                string cur = Program.CurrentStateDate();
                if (cur.Length == 8)
                {
                    DateTime d;
                    if (DateTime.TryParseExact(cur, "yyyyMMdd", null,
                            System.Globalization.DateTimeStyles.None, out d))
                    {
                        if (d >= cal.MinDate && d <= cal.MaxDate) { init = d; }
                    }
                }
                cal.SetDate(init);
            }
            catch { }
            cal.SetBounds(12, 12, 240, 240);
            cal.DateChanged += delegate { RefreshInfo(); };
            Controls.Add(cal);

            // 右侧：预览 + 文字
            pic = new PictureBox();
            pic.SetBounds(264, 12, 204, 115);
            pic.BorderStyle = BorderStyle.FixedSingle;
            pic.SizeMode = PictureBoxSizeMode.Zoom;
            pic.Visible = false;
            Controls.Add(pic);

            lblDate = new Label();
            lblDate.SetBounds(264, 136, 204, 18);
            lblDate.ForeColor = SystemColors.GrayText;
            Controls.Add(lblDate);

            lblTitle = new Label();
            lblTitle.SetBounds(264, 156, 204, 44);
            lblTitle.AutoSize = false;
            try { lblTitle.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold); } catch { }
            Controls.Add(lblTitle);

            lblWhere = new Label();
            lblWhere.SetBounds(264, 204, 204, 18);
            Controls.Add(lblWhere);

            lblSize = new Label();
            lblSize.SetBounds(264, 224, 204, 18);
            lblSize.ForeColor = SystemColors.GrayText;
            Controls.Add(lblSize);

            lblCopy = new Label();
            lblCopy.SetBounds(264, 246, 204, 72);
            lblCopy.AutoSize = false;
            lblCopy.ForeColor = SystemColors.GrayText;
            try { lblCopy.Font = new Font("Microsoft YaHei UI", 8F); } catch { }
            Controls.Add(lblCopy);

            hint = new Label();
            hint.AutoSize = false;
            hint.SetBounds(12, 262, 240, 62);
            hint.ForeColor = SystemColors.GrayText;
            hint.Text = S.IsEn
                ? "Last 8 days: official original\r\nSince Mar 2016: archive original (online)\r\n" + S.CalBoldHint
                : "最近 8 天：官方原图\r\n2016-03 起：归档原图（需联网）\r\n" + S.CalBoldHint;
            Controls.Add(hint);

            btnToday = new Button();
            btnToday.Text = S.BtnToday;
            btnToday.Size = new Size(66, 30);
            btnToday.Click += delegate
            {
                try { cal.SetDate(DateTime.Today); } catch { }
            };
            Controls.Add(btnToday);

            // 不设 DialogResult：否则点它窗体会自动关闭，取消确认也拦不住
            ok = new Button();
            ok.Text = S.BtnApply;
            ok.Size = new Size(88, 30);
            ok.Click += delegate { AcceptPick(); };
            Controls.Add(ok);

            cancel = new Button();
            cancel.Text = S.BtnCancel;
            cancel.Size = new Size(88, 30);
            cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;

            try { idxCache = Program.LoadArchiveIndex(); } catch { }
            try { cal.BoldedDates = LocalDates().ToArray(); } catch { }
            RefreshInfo();
        }

        /// <summary>本地已有的日期（收藏 / 缓存 / 归档），供日历加粗。</summary>
        private List<DateTime> LocalDates()
        {
            List<DateTime> list = new List<DateTime>();
            HashSet<string> seen = new HashSet<string>();
            string[] dirs = new string[] { Program.FavDirPath, Program.CacheDirPath, Program.ArchiveImgPath };
            // 老存档按月分目录，目录本身不参与，单独扫
            try
            {
                string oldRoot = Program.OldDirPath;
                if (!string.IsNullOrEmpty(oldRoot) && Directory.Exists(oldRoot))
                {
                    foreach (string om in Directory.GetDirectories(oldRoot))
                    {
                        foreach (string f in Directory.GetFiles(om, "*.jpg"))
                        {
                            string fn2 = Path.GetFileName(f);
                            if (fn2.Length < 8) { continue; }
                            string d8b = fn2.Substring(0, 8);
                            if (!seen.Add(d8b)) { continue; }
                            DateTime dobj;
                            if (DateTime.TryParseExact(d8b, "yyyyMMdd", null,
                                    System.Globalization.DateTimeStyles.None, out dobj))
                            {
                                if (dobj >= cal.MinDate && dobj <= cal.MaxDate) { list.Add(dobj); }
                            }
                        }
                    }
                }
            }
            catch { }
            foreach (string dir in dirs)
            {
                try
                {
                    if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) { continue; }
                    foreach (string f in Directory.GetFiles(dir, "*.jpg"))
                    {
                        string fn = Path.GetFileName(f);
                        if (fn.Length < 8) { continue; }
                        string d8 = fn.Substring(0, 8);
                        if (!seen.Add(d8)) { continue; }
                        DateTime d;
                        if (DateTime.TryParseExact(d8, "yyyyMMdd", null,
                                System.Globalization.DateTimeStyles.None, out d))
                        {
                            if (d >= cal.MinDate && d <= cal.MaxDate) { list.Add(d); }
                        }
                    }
                }
                catch { }
            }
            return list;
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

        /// <summary>切换日期时先销毁旧图，再按需加载新图。</summary>
        private void ShowPreview(string date)
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

            string file = Program.FindImageFilePath(date);
            if (file == null) { pic.Visible = false; return; }

            Bitmap bmp = MakeThumb(file, 204, 115);
            if (bmp != null) { pic.Image = bmp; pic.Visible = true; }
            else { pic.Visible = false; }
        }

        /// <summary>把当前选中日期的标题、位置、体积、版权与预览刷到右侧。</summary>
        private void RefreshInfo()
        {
            try
            {
                string d = cal.SelectionStart.ToString("yyyyMMdd");
                string title = "";
                string copy = "";

                if (idxCache != null && idxCache.ContainsKey(d))
                {
                    Dictionary<string, object> e = idxCache[d] as Dictionary<string, object>;
                    if (e != null)
                    {
                        if (e.ContainsKey("title")) { title = e["title"] == null ? "" : e["title"].ToString(); }
                        if (e.ContainsKey("copyright")) { copy = e["copyright"] == null ? "" : e["copyright"].ToString(); }
                    }
                }
                if (string.IsNullOrEmpty(title)) { title = S.CalNoTitle; }

                long bytes = 0;
                string where = Program.FindImageLocation(d, out bytes);

                lblDate.Text = cal.SelectionStart.ToString("yyyy-MM-dd");
                lblTitle.Text = title;
                lblWhere.Text = where;
                lblSize.Text = bytes > 0 ? (bytes / 1048576.0).ToString("0.0") + " MB" : "";
                lblCopy.Text = copy;

                ShowPreview(d);
            }
            catch { }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Relayout();
            try
            {
                hook = new CalendarHook(this);
                hook.AssignHandle(cal.Handle);
            }
            catch { }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            try
            {
                if (hook != null) { hook.ReleaseHandle(); hook = null; }
            }
            catch { }
            base.OnFormClosed(e);
        }

        /// <summary>按左右两栏实际高度重排底部按钮，避免任何一栏被压住。</summary>
        private void Relayout()
        {
            try
            {
                int left = 14;
                int width = cal.Right - left;
                if (width < 200) { width = 200; }

                hint.Left = left;
                hint.Width = width;
                hint.Top = cal.Bottom + 10;

                // 按钮位置：取左栏与右栏底部的较大值
                int bottomL = hint.Bottom;
                int bottomR = lblCopy.Bottom;
                int btnY = Math.Max(bottomL, bottomR) + 12;

                btnToday.Top = btnY;
                ok.Top = btnY;
                cancel.Top = btnY;

                // 左边一个辅助动作，右边一组主操作
                btnToday.Left = 12;
                cancel.Left = ClientSize.Width - 12 - cancel.Width;
                ok.Left = cancel.Left - 12 - ok.Width;

                ClientSize = new Size(ClientSize.Width, btnY + ok.Height + 14);
            }
            catch { }
        }

        /// <summary>MonthCalendar 的窗口类不带 CS_DBLCLKS，收不到 WM_LBUTTONDBLCLK，自己判双击。</summary>
        private sealed class CalendarHook : NativeWindow
        {
            private readonly CalendarForm owner;
            private DateTime lastDown = DateTime.MinValue;
            private Point lastPos = Point.Empty;

            public CalendarHook(CalendarForm f) { owner = f; }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == 0x0201)   // WM_LBUTTONDOWN
                {
                    int lp = m.LParam.ToInt32();
                    Point p = new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF));
                    DateTime now = DateTime.Now;
                    if ((now - lastDown).TotalMilliseconds < 500 &&
                        Math.Abs(p.X - lastPos.X) < 6 &&
                        Math.Abs(p.Y - lastPos.Y) < 6)
                    {
                        lastDown = DateTime.MinValue;
                        CalendarForm f = owner;
                        try { f.BeginInvoke((MethodInvoker)delegate { f.AcceptPick(); }); }
                        catch { }
                    }
                    else
                    {
                        lastDown = now;
                        lastPos = p;
                    }
                }
                base.WndProc(ref m);
            }
        }

        /// <summary>选中日期直接换图，窗口留着继续挑。</summary>
        private void AcceptPick()
        {
            string d = cal.SelectionStart.ToString("yyyyMMdd");
            if (string.IsNullOrEmpty(d)) { return; }
            SelectedDate = d;
            ok.Enabled = false;
            lblWhere.Text = S.SrchWorking;
            string d8 = d;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                int code = Program.RunScriptExit("-Date " + d8);
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        ok.Enabled = true;
                        bool done = (code == 0 || code == 8);
                        try { RefreshInfo(); } catch { }
                        try { cal.BoldedDates = LocalDates().ToArray(); } catch { }
                        if (code == 11) { lblWhere.Text = S.WallApplyFail; }
                        else if (code == 12) { lblWhere.Text = S.FutureDateHint; }
                        else { lblWhere.Text = done ? S.SrchSwitched(d8) : S.SrchSwitchFail; }
                    });
                }
                catch { }
            });
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
