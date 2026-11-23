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
    internal sealed class InfoForm : Form
    {
        private const int ThumbW = 240;
        private const int ThumbH = 135;

        private Bitmap thumb;
        private PictureBox pic;
        private Label lbl;
        private Label lblStoryHd;
        private Label lblStory;
        private Button btnExpand;
        private Button btnPrevNav;
        private Button btnNextNav;
        private string curDate = "";
        private string curSearchUrl = "";
        private string curFile = "";
        private Button btnLearn;
        private Button btnFolder;
        private int storySeq = 0;
        private int storyY = 0;
        private bool storyExpanded = false;
        private string storyFull = "";
        private Button btnFav;
        private readonly System.Collections.Generic.HashSet<string> pfetched =
            new System.Collections.Generic.HashSet<string>();

        /// <summary>读入内存再解码，避免 Image.FromFile 锁定壁纸文件。</summary>
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

        public InfoForm(string date, string title, string copyright, string file, string sizeText, string searchUrl)
        {
            Text = Program.AppName + S.InfoTitle;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            ClientSize = new Size(620, 350);
            try { Font = new Font("Microsoft YaHei UI", 9F); } catch { }

            // 左侧缩略图
            thumb = MakeThumb(file, ThumbW, ThumbH);
            int textLeft = 20;
            int textWidth = 580;
            // 缩略图区固定占位：翻页换图时布局不跳
            pic = new PictureBox();
            pic.Image = thumb;
            pic.SizeMode = PictureBoxSizeMode.Zoom;
            pic.SetBounds(20, 20, ThumbW, ThumbH);
            pic.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(pic);
            textLeft = 20 + ThumbW + 18;
            textWidth = 580 - ThumbW - 18;

            int y = 20;

            lbl = new Label();
            lbl.AutoSize = false;
            // 高度必须装得下「日期/标题/版权/文件/体积」五段含空行，长路径还要换行；
            // 原来用 ThumbH(135) 会把「文件」和「体积」裁掉
            lbl.SetBounds(textLeft, y, textWidth, 210);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine(S.InfoDate + date);
            sb.AppendLine();
            if (title.Length > 0) { sb.AppendLine(S.InfoTitleLine + title); sb.AppendLine(); }
            if (copyright.Length > 0) { sb.AppendLine(S.InfoCopy + copyright); sb.AppendLine(); }
            if (file.Length > 0) { sb.AppendLine(S.InfoFile + file); }
            if (sizeText.Length > 0) { sb.AppendLine(S.InfoSize + sizeText); }

            lbl.Text = sb.ToString().Trim();
            Controls.Add(lbl);

            int by = 20 + 210 + 26;

            // 翻页行（翻页 = 换桌面壁纸，窗口内容随之刷新）
            Button btnPrev = new Button();
            btnPrev.Text = S.InfoBtnPrev;
            btnPrev.SetBounds(20, by, 96, 30);
            btnPrev.Click += delegate { Step(1); };
            Controls.Add(btnPrev);

            Button btnNext = new Button();
            btnNext.Text = S.InfoBtnNext;
            btnNext.SetBounds(126, by, 96, 30);
            btnNext.Click += delegate { Step(-1); };
            Controls.Add(btnNext);
            btnPrevNav = btnPrev;
            btnNextNav = btnNext;

            btnFav = new Button();
            btnFav.Text = S.InfoBtnFav;
            btnFav.SetBounds(232, by, 110, 30);
            btnFav.Click += delegate { ToggleFavorite(); };
            Controls.Add(btnFav);

            by += 40;

            // 这两个按钮的 onClick 读字段而不是构造函数参数：翻页后 Reload 会更新字段，
            // 闭包捕获参数的话，点多少次都还是第一张图的那个链接 / 路径。
            curSearchUrl = searchUrl == null ? "" : searchUrl;
            curFile = file == null ? "" : file;

            btnLearn = new Button();
            btnLearn.Text = S.InfoBtnSearch;
            btnLearn.SetBounds(20, by, 110, 32);
            btnLearn.Enabled = curSearchUrl.Length > 0;
            btnLearn.Click += delegate { if (curSearchUrl.Length > 0) { Program.OpenUrl(curSearchUrl); } };
            Controls.Add(btnLearn);

            btnFolder = new Button();
            btnFolder.Text = S.InfoBtnFolder;
            btnFolder.SetBounds(140, by, 130, 32);
            btnFolder.Enabled = curFile.Length > 0;
            btnFolder.Click += delegate { if (curFile.Length > 0) { Program.ShowInFolder(curFile); } };
            Controls.Add(btnFolder);

            Button btnMore = new Button();
            btnMore.Text = S.InfoBtnMore;
            btnMore.SetBounds(280, by, 130, 32);
            btnMore.Click += delegate { OpenSameDayWall(); };
            Controls.Add(btnMore);

            Button btnClose = new Button();
            btnClose.Text = S.BtnOK;
            btnClose.SetBounds(514, by, 86, 32);
            // 非模态窗口：DialogResult 不会关窗，必须显式 Close
            btnClose.Click += delegate { Close(); };
            Controls.Add(btnClose);

            AcceptButton = btnClose;
            CancelButton = btnClose;
            // 非模态窗体的 CancelButton 不会自动响应 Esc，得自己接
            KeyPreview = true;
            KeyDown += delegate(object s0, KeyEventArgs e0)
            {
                if (e0.KeyCode == Keys.Escape) { Close(); }
            };



            // 窗口高度跟着内容走
            ClientSize = new Size(620, by + 32 + 20);

            // ---------------------------------------------------------------- 每日故事
            // 正文按需抓取（本地缓存优先），拿到才显示，拿不到整块隐藏
            int sy = by + 32 + 14;
            storyY = sy;
            lblStoryHd = new Label();
            lblStoryHd.Text = S.StoryTitle;
            lblStoryHd.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            lblStoryHd.SetBounds(20, sy, 580, 20);
            lblStoryHd.Visible = false;
            Controls.Add(lblStoryHd);

            lblStory = new Label();
            lblStory.AutoSize = false;
            lblStory.SetBounds(20, sy + 22, 580, 56);
            lblStory.Visible = false;
            Controls.Add(lblStory);

            btnExpand = new Button();
            btnExpand.Text = S.StoryExpand;
            btnExpand.SetBounds(20, sy + 82, 92, 26);
            btnExpand.Visible = false;
            btnExpand.Click += delegate { ToggleStory(); };
            Controls.Add(btnExpand);

            curDate = date;
            RefreshFavState();
            LoadStoryAsync(date);
            PrefetchAround(date);
        }

        /// <summary>按日期异步取故事并显示；没有故事时整块隐藏。</summary>
        private void LoadStoryAsync(string storyDate)
        {
            if (storyDate == null) { return; }
            string sd = storyDate.Replace("-", "").Replace("/", "").Trim();
            if (sd.Length != 8 || !Program.ShowStory) { return; }
            int seq = storySeq;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                string story = "";
                try { story = Program.GetStory(sd); } catch { }
                if (string.IsNullOrEmpty(story)) { return; }
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        if (seq != storySeq) { return; }
                        // 每次换故事都回到收起态：高度、按钮位置、按钮文字一起复位，
                        // 否则上一张展开过的 150 高度会留到这一张，正文被裁出省略号
                        bool longText = story.Length > 150;
                        string brief = longText ? story.Substring(0, 150) + "…" : story;
                        storyFull = story;
                        storyExpanded = false;
                        lblStoryHd.Visible = true;
                        lblStory.Text = brief;
                        lblStory.SetBounds(20, storyY + 22, 580, 56);
                        lblStory.Visible = true;
                        btnExpand.Text = S.StoryExpand;
                        btnExpand.SetBounds(20, storyY + 82, 92, 26);
                        btnExpand.Visible = longText;
                        ClientSize = new Size(620, storyY + 22 + (longText ? 92 : 62) + 16);
                    });
                }
                catch { }
            });
        }

        /// <summary>左右键翻页。用 ProcessCmdKey 而不是 KeyDown：按钮有焦点时方向键会被 Tab 导航吃掉。</summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Left) { Step(1); return true; }
            if (keyData == Keys.Right) { Step(-1); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        /// <summary>预取相邻日期的图，翻到的时候能直接命中本地。</summary>
        private void PrefetchAround(string date)
        {
            try
            {
                string d = (date ?? "").Replace("-", "").Trim();
                if (d.Length != 8) { return; }
                DateTime dt;
                if (!DateTime.TryParseExact(d, "yyyyMMdd", null,
                        System.Globalization.DateTimeStyles.None, out dt)) { return; }
                string[] targets = new string[] {
                    dt.AddDays(-1).ToString("yyyyMMdd"),
                    dt.AddDays(1).ToString("yyyyMMdd")
                };
                DateTime today = DateTime.Today;
                foreach (string t in targets)
                {
                    DateTime td;
                    if (!DateTime.TryParseExact(t, "yyyyMMdd", null,
                            System.Globalization.DateTimeStyles.None, out td)) { continue; }
                    if (td < Program.ArchiveMinDateValue || td > today) { continue; }
                    if (pfetched.Contains(t)) { continue; }
                    pfetched.Add(t);
                    string tt = t;
                    System.Threading.ThreadPool.QueueUserWorkItem(delegate
                    {
                        try { Program.RunPrefetch(tt); } catch { }
                    });
                }
            }
            catch { }
        }

        /// <summary>按当前壁纸是否在收藏夹里，刷新按钮文字。</summary>
        private void RefreshFavState()
        {
            if (btnFav == null) { return; }
            try
            {
                bool fav = false;
                try { fav = Program.IsCurrentFavorited(); } catch { }
                btnFav.Text = fav ? S.InfoBtnFaved : S.InfoBtnFav;
            }
            catch { }
        }

        /// <summary>收藏 / 取消收藏，与托盘菜单的收藏项走完全相同的脚本指令。</summary>
        private void ToggleFavorite()
        {
            bool isFav = false;
            try { isFav = Program.IsCurrentFavorited(); } catch { }
            string args = isFav ? "-Unfavorite" : "-Favorite";
            if (btnFav != null) { btnFav.Enabled = false; }
            SetNavEnabled(false);
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                int code = Program.RunScriptExit(args);
                string[] st6 = null;
                if (code == 0) { try { st6 = Program.ReadStateForInfo(); } catch { } }
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        if (btnFav != null) { btnFav.Enabled = true; }
                        SetNavEnabled(true);
                        if (code == 3)
                        {
                            MessageBox.Show(S.InfoFavFail, Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            RefreshFavState();
                            return;
                        }
                        if (st6 != null) { Reload(st6[0], st6[1], st6[2], st6[3], st6[4], st6[5]); }
                        else { RefreshFavState(); }
                        // 窗口里的收藏改了，托盘菜单那项也得跟着变
                        try { Program.RefreshAllPublic(); } catch { }
                    });
                }
                catch { }
            });
        }

        private void SetNavEnabled(bool on)
        {
            if (btnPrevNav != null) { btnPrevNav.Enabled = on; }
            if (btnNextNav != null) { btnNextNav.Enabled = on; }
        }

        /// <summary>原地翻页：dir &gt; 0 更早，dir &lt; 0 更新。翻页会同时更换桌面壁纸。</summary>
        private void Step(int dir)
        {
            // 翻页进行中：按钮已禁用，键盘也一并挡住
            if (btnPrevNav != null && !btnPrevNav.Enabled) { return; }
            SetNavEnabled(false);
            string oldTitle = Text;
            Text = Program.AppName + " · " + S.InfoSwitching;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                // 单次翻页失败的真实来源：后台预取正占着脚本锁、或源站一时抖动。
                // 这两种都能自己好，所以在会话内自动重试，只有耗尽预算才把失败交给用户。
                const int MaxTries = 3;
                const long BudgetMs = 100000;
                System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
                int code = -1;
                string[] st6 = null;
                for (int attempt = 1; attempt <= MaxTries; attempt++)
                {
                    int cur = attempt;
                    if (cur > 1)
                    {
                        try
                        {
                            BeginInvoke((MethodInvoker)delegate
                            {
                                Text = Program.AppName + " · " + string.Format(S.InfoRetrying, cur, MaxTries);
                            });
                        }
                        catch { }
                    }
                    code = Program.RunStep(dir);
                    if (code == 0 || code == 4 || code == 8 || code == 11)
                    {
                        try { st6 = Program.ReadStateForInfo(); } catch { }
                        break;
                    }
                    if (attempt >= MaxTries || sw.ElapsedMilliseconds > BudgetMs) { break; }
                    System.Threading.Thread.Sleep(1200 * attempt);
                }
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        Text = oldTitle;
                        SetNavEnabled(true);
                        if (st6 == null)
                        {
                            MessageBox.Show(S.InfoSwitchFail, Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }
                        Reload(st6[0], st6[1], st6[2], st6[3], st6[4], st6[5]);
                    });
                }
                catch { }
            });
        }

        /// <summary>展开 / 收起每日故事。展开状态只影响本张图，翻页由 Reload 复位。</summary>
        private void ToggleStory()
        {
            if (string.IsNullOrEmpty(storyFull)) { return; }
            storyExpanded = !storyExpanded;
            bool longText = storyFull.Length > 150;
            if (storyExpanded)
            {
                lblStory.Text = storyFull;
                lblStory.SetBounds(20, storyY + 22, 580, 150);
                btnExpand.Text = S.StoryCollapse;
                btnExpand.SetBounds(20, storyY + 22 + 150 + 8, 92, 26);
                ClientSize = new Size(620, storyY + 22 + 150 + 8 + 26 + 12);
            }
            else
            {
                lblStory.Text = longText ? (storyFull.Substring(0, 150) + "…") : storyFull;
                lblStory.SetBounds(20, storyY + 22, 580, 56);
                btnExpand.Text = S.StoryExpand;
                btnExpand.SetBounds(20, storyY + 82, 92, 26);
                ClientSize = new Size(620, storyY + 22 + (longText ? 92 : 62) + 16);
            }
        }

        /// <summary>用新数据刷新窗口内容，布局保持不动。</summary>
        /// <summary>按当前 state.json 重载全部内容。窗口常驻期间被再次打开时用它。</summary>
        internal void ReloadFromState()
        {
            try
            {
                string[] st = Program.ReadStateForInfo();
                if (st == null) { return; }
                Reload(st[0], st[1], st[2], st[3], st[4], st[5]);
            }
            catch { }
        }

        private void Reload(string date, string title, string copyright, string file, string sizeText, string searchUrl)
        {
            curDate = date;
            curSearchUrl = searchUrl == null ? "" : searchUrl;
            curFile = file == null ? "" : file;
            if (btnLearn != null) { btnLearn.Enabled = curSearchUrl.Length > 0; }
            if (btnFolder != null) { btnFolder.Enabled = curFile.Length > 0; }
            try
            {
                if (thumb != null) { thumb.Dispose(); thumb = null; }
                thumb = MakeThumb(file, ThumbW, ThumbH);
                if (pic != null) { pic.Image = thumb; }
            }
            catch { }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine(S.InfoDate + date);
            sb.AppendLine();
            if (!string.IsNullOrEmpty(title)) { sb.AppendLine(S.InfoTitleLine + title); sb.AppendLine(); }
            if (!string.IsNullOrEmpty(copyright)) { sb.AppendLine(S.InfoCopy + copyright); sb.AppendLine(); }
            if (!string.IsNullOrEmpty(file)) { sb.AppendLine(S.InfoFile + file); }
            if (!string.IsNullOrEmpty(sizeText)) { sb.AppendLine(S.InfoSize + sizeText); }
            lbl.Text = sb.ToString().Trim();

            RefreshFavState();
            storySeq++;
            if (lblStoryHd != null) { lblStoryHd.Visible = false; }
            if (lblStory != null) { lblStory.Visible = false; }
            if (btnExpand != null)
            {
                btnExpand.Visible = false;
                btnExpand.Text = S.StoryExpand;
                btnExpand.SetBounds(20, storyY + 82, 92, 26);
            }
            if (lblStory != null) { lblStory.SetBounds(20, storyY + 22, 580, 56); }
            storyExpanded = false;
            storyFull = "";
            ClientSize = new Size(620, storyY + 16);
            LoadStoryAsync(date);
            PrefetchAround(date);
        }

        /// <summary>
        /// 打开「往年的今天」照片墙：按当前这张图的月日，列出历年同一天。
        /// 以前这个按钮是把用户送到第三方站点（比本程序少 12 年数据，而且已经打不开），
        /// 现在改成用本机已有的照片墙 —— 我们自己就是这条链上最全的一环。
        /// </summary>
        private void OpenSameDayWall()
        {
            string d = curDate;
            if (string.IsNullOrEmpty(d) || d.Length != 8) { return; }
            string mmdd = d.Substring(4, 4);
            string[] ds = null;
            try
            {
                Dictionary<string, object> idx = Program.LoadArchiveIndex();
                if (idx != null)
                {
                    List<string> list = new List<string>();
                    foreach (string k in idx.Keys)
                    {
                        if (!string.IsNullOrEmpty(k) && k.Length == 8 && k.Substring(4, 4) == mmdd) { list.Add(k); }
                    }
                    list.Sort();
                    if (list.Count > 0) { ds = list.ToArray(); }
                }
            }
            catch { }
            if (ds == null || ds.Length == 0) { ds = new string[] { d }; }
            try
            {
                using (PhotoWallForm f = new PhotoWallForm(ds, S.WallTitle(mmdd)))
                {
                    f.ShowDialog(this);
                }
            }
            catch { }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && thumb != null) { thumb.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
