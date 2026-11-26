using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace BingWallpaperLite
{
    /// <summary>
    /// 关于窗口：传统布局 —— 左上角图标，右侧名称与版本，中间正文，底部状态与按钮。
    /// 正文不再用只读 TextBox：那个白底在深色主题下是一块突兀的亮斑，
    /// 而且文字不能选中复制，除了「能滚动」之外没有一样好处。
    ///
    /// 双击左上角图标是个彩蛋：图标变羽毛，桌面开始飘羽毛，关窗即恢复。
    /// </summary>
    internal sealed class AboutForm : Form
    {
        private readonly Label lblUpdate;
        private readonly Button btnRecheck;
        private readonly PictureBox logo;
        private readonly ToolTip logoTip;
        private bool probing;

        // 彩蛋状态：一个开关管两件事（图标换不换、层开不开），不会出现半开半关
        private bool eggOn;
        private FeatherLayer egg;
        private Image logoNormal;
        private Image logoFeather;

        public AboutForm(string aboutText)
        {
            Text = S.AppNameFull + (S.IsEn ? " · About" : " · 关于");
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            ClientSize = new Size(680, 690);
            try { Font = new Font("Microsoft YaHei UI", 9F); } catch { }
            // 必须 Clone：Form.Icon 只是存引用，using 里那个 Icon 出了作用域就被 Dispose，
            // 等窗体 CreateHandle 去取 Icon.Handle 时直接 ObjectDisposedException 崩掉
            try
            {
                using (Icon ic = LoadWindowIcon())
                {
                    if (ic != null) { Icon = (Icon)ic.Clone(); }
                }
            }
            catch { }

            // ---- 标题区：图标 + 名称 + 版本 + 一句话
            logo = new PictureBox();
            logo.SetBounds(20, 16, 48, 48);
            logo.SizeMode = PictureBoxSizeMode.Zoom;
            logo.BackColor = Color.Transparent;
            logo.Cursor = Cursors.Hand;
            try { logoNormal = LoadLogo(96); if (logoNormal != null) { logo.Image = logoNormal; } }
            catch { }
            logo.DoubleClick += delegate { ToggleEgg(); };
            Controls.Add(logo);

            logoTip = new ToolTip();
            try { logoTip.SetToolTip(logo, S.EggTip); } catch { }

            Label name = new Label();
            name.AutoSize = false;
            name.SetBounds(84, 16, 570, 26);
            try { name.Font = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold); } catch { }
            name.Text = S.AppNameFull;
            Controls.Add(name);

            Label ver = new Label();
            ver.AutoSize = false;
            ver.SetBounds(86, 44, 240, 18);
            ver.ForeColor = SystemColors.GrayText;
            ver.Text = "v" + Program.AppVersion;
            Controls.Add(ver);

            Label tag = new Label();
            tag.AutoSize = false;
            tag.SetBounds(86, 64, 574, 18);
            tag.ForeColor = SystemColors.GrayText;
            tag.Text = S.AboutTagline;
            Controls.Add(tag);

            Label rule = new Label();
            rule.AutoSize = false;
            rule.SetBounds(20, 92, 640, 2);
            rule.BorderStyle = BorderStyle.Fixed3D;
            Controls.Add(rule);

            // ---- 正文：直接排在窗体上，跟着主题走
            Label body = new Label();
            body.AutoSize = false;
            body.SetBounds(20, 102, 640, 504);
            try { body.Font = new Font("Microsoft YaHei UI", 8F); } catch { }
            body.Text = aboutText == null ? "" : aboutText;
            Controls.Add(body);

            // ---- 底部：更新状态 + 四个按钮
            lblUpdate = new Label();
            lblUpdate.SetBounds(20, 616, 400, 20);
            lblUpdate.AutoSize = false;
            lblUpdate.ForeColor = SystemColors.GrayText;
            lblUpdate.Text = S.UpdChecking;
            Controls.Add(lblUpdate);

            Button btnAuthor = new Button();
            btnAuthor.Text = S.AboutBtnAuthor;
            btnAuthor.SetBounds(20, 646, 110, 30);
            btnAuthor.Click += delegate { Program.OpenAuthorPage(); };
            Controls.Add(btnAuthor);

            Button btnReleases = new Button();
            btnReleases.Text = S.AboutBtnReleases;
            btnReleases.SetBounds(140, 646, 110, 30);
            btnReleases.Click += delegate { Program.OpenReleasesPage(); };
            Controls.Add(btnReleases);

            btnRecheck = new Button();
            btnRecheck.Text = S.AboutBtnRecheck;
            btnRecheck.SetBounds(260, 646, 96, 30);
            btnRecheck.Click += delegate { StartProbe(); };
            Controls.Add(btnRecheck);

            Button btnClose = new Button();
            btnClose.Text = S.AboutBtnClose;
            btnClose.SetBounds(556, 646, 104, 30);
            btnClose.DialogResult = DialogResult.Cancel;
            btnClose.Click += delegate { Close(); };
            Controls.Add(btnClose);

            CancelButton = btnClose;
            AcceptButton = btnRecheck;

            Shown += delegate { StartProbe(); };
        }

        // ================================================================ 图标与彩蛋

        /// <summary>
        /// 关于窗口的图标：优先用内嵌的 logo.png（96×96）。
        /// 退回路径是 ico 的 32×32 再放大 —— 40/48/128 这几个尺寸在
        /// .NET Framework 的 Icon.ToBitmap() 上会抛 ArgumentOutOfRangeException
        /// （stride 算错，报「请求的范围扩展超过了数组的结尾」），只有 16/32 和
        /// 默认尺寸是安全的。这也是一开始左上角一片空白的原因。
        /// </summary>
        private static Image LoadLogo(int size)
        {
            // 1) 内嵌 PNG
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                using (Stream s = asm.GetManifestResourceStream("logo.png"))
                {
                    if (s != null)
                    {
                        using (Image raw = Image.FromStream(s))
                        {
                            return ScaleImage(raw, size);
                        }
                    }
                }
            }
            catch { }

            // 2) 退回 32×32 ico
            try
            {
                string dir = Path.GetDirectoryName(Application.ExecutablePath);
                if (!string.IsNullOrEmpty(dir))
                {
                    string p = Path.Combine(dir, "bing.ico");
                    if (File.Exists(p))
                    {
                        using (Icon ic = new Icon(p, 32, 32))
                        using (Bitmap b32 = ic.ToBitmap())
                        {
                            return ScaleImage(b32, size);
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>高质量缩放并复制成独立位图（FromStream 的图不能脱离流使用）。</summary>
        private static Bitmap ScaleImage(Image src, int size)
        {
            Bitmap dst = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(dst))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);
                g.DrawImage(src, 0, 0, size, size);
            }
            return dst;
        }

        /// <summary>
        /// 双击之后换上的那张图。读嵌入的 egg.png（来自 SakuraClock 的 app.ico），
        /// 没有就退回矢量羽毛 —— 素材缺失时彩蛋仍然能开。
        /// </summary>
        private static Image LoadEggIcon(int size)
        {
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                using (Stream s = asm.GetManifestResourceStream("egg.png"))
                {
                    if (s != null)
                    {
                        using (Image raw = Image.FromStream(s))
                        {
                            return ScaleImage(raw, size);
                        }
                    }
                }
            }
            catch { }
            return FeatherArt.Render(size);
        }

        /// <summary>窗口标题栏那个小图标：32×32 是 ico 里能安全转位图的尺寸。</summary>
        private static Icon LoadWindowIcon()
        {
            try
            {
                string dir = Path.GetDirectoryName(Application.ExecutablePath);
                if (!string.IsNullOrEmpty(dir))
                {
                    string p = Path.Combine(dir, "bing.ico");
                    if (File.Exists(p)) { return new Icon(p, 32, 32); }
                }
            }
            catch { }
            try { return Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            return null;
        }

        /// <summary>双击图标：开彩蛋 / 关彩蛋。图标与飘落层一起变，不会只生效一半。</summary>
        private void ToggleEgg()
        {
            if (eggOn) { StopEgg(); return; }

            try
            {
                if (logoFeather == null) { logoFeather = LoadEggIcon(96); }
                logo.Image = logoFeather;
            }
            catch { }

            try
            {
                egg = new FeatherLayer();
                egg.Show();
            }
            catch { egg = null; }

            eggOn = true;
        }

        private void StopEgg()
        {
            eggOn = false;
            try
            {
                if (egg != null)
                {
                    egg.Stop();
                    egg.Close();
                    egg.Dispose();
                }
            }
            catch { }
            egg = null;
            try { if (logo != null && logoNormal != null) { logo.Image = logoNormal; } } catch { }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            // 彩蛋层归这个窗口管：窗口一关就撤，不留一片羽毛在桌面上转
            StopEgg();
            try { if (logo != null) { logo.Image = null; } } catch { }
            try { if (logoTip != null) { logoTip.Dispose(); } } catch { }
            try { if (logoNormal != null) { logoNormal.Dispose(); logoNormal = null; } } catch { }
            try { if (logoFeather != null) { logoFeather.Dispose(); logoFeather = null; } } catch { }
            base.OnFormClosed(e);
        }

        // ================================================================ 更新检查

        private void StartProbe()
        {
            if (probing || IsDisposed) { return; }
            probing = true;
            btnRecheck.Enabled = false;
            lblUpdate.ForeColor = SystemColors.GrayText;
            lblUpdate.Text = S.UpdChecking;

            Thread t = new Thread(delegate()
            {
                UpdateProbe p = null;
                try { p = Program.CheckLatestRelease(); }
                catch { }
                try
                {
                    if (IsDisposed || !IsHandleCreated) { return; }
                    BeginInvoke((MethodInvoker)delegate { ShowProbe(p); });
                }
                catch { }
            });
            t.IsBackground = true;
            t.Start();
        }

        private void ShowProbe(UpdateProbe p)
        {
            probing = false;
            if (btnRecheck != null) { btnRecheck.Enabled = true; }
            if (lblUpdate == null || lblUpdate.IsDisposed) { return; }

            if (p == null)
            {
                lblUpdate.ForeColor = Color.FromArgb(180, 35, 24);
                lblUpdate.Text = S.UpdFailedPrefix + (S.IsEn ? "internal error" : "内部错误");
                return;
            }
            if (!p.Ok)
            {
                lblUpdate.ForeColor = Color.FromArgb(180, 35, 24);
                lblUpdate.Text = S.UpdFailedPrefix + p.Error;
                return;
            }

            string latest = p.Tag.Trim().TrimStart('v', 'V');
            if (Program.CompareVersion(latest, Program.AppVersion) > 0)
            {
                lblUpdate.ForeColor = Color.FromArgb(198, 92, 0);
                lblUpdate.Text = S.UpdNewer(latest, Program.AppVersion);
            }
            else
            {
                lblUpdate.ForeColor = Color.FromArgb(26, 127, 55);
                lblUpdate.Text = S.UpdLatest(Program.AppVersion);
            }
        }
    }
}
