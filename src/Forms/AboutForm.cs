using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace BingWallpaperLite
{
    /// <summary>
    /// 关于窗口：传统布局 —— 左上角图标，右侧名称与版本，中间正文，底部状态与按钮。
    /// 正文不再用只读 TextBox：那个白底在深色主题下是一块突兀的亮斑，
    /// 而且文字不能选中复制，除了「能滚动」之外没有一样好处。
    /// </summary>
    internal sealed class AboutForm : Form
    {
        private readonly Label lblUpdate;
        private readonly Button btnRecheck;
        private readonly PictureBox logo;
        private bool probing;

        public AboutForm(string aboutText)
        {
            Text = Program.AppName + (S.IsEn ? " · About" : " · 关于");
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            ClientSize = new Size(680, 690);
            try { Font = new Font("Microsoft YaHei UI", 9F); } catch { }
            try { Icon = LoadAppIcon(32); } catch { }

            // ---- 标题区：图标 + 名称 + 版本 + 一句话
            logo = new PictureBox();
            logo.SetBounds(20, 16, 48, 48);
            logo.SizeMode = PictureBoxSizeMode.Zoom;
            logo.BackColor = Color.Transparent;
            try
            {
                using (Icon ic = LoadAppIcon(48))
                {
                    if (ic != null) { logo.Image = ic.ToBitmap(); }
                }
            }
            catch { }
            Controls.Add(logo);

            Label name = new Label();
            name.AutoSize = false;
            name.SetBounds(84, 16, 560, 26);
            try { name.Font = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold); } catch { }
            name.Text = Program.AppName;
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

        /// <summary>图标就在 exe 旁边，按需要的尺寸现取，不额外打包资源。</summary>
        private static string LoadIconPath()
        {
            try
            {
                string dir = Path.GetDirectoryName(Application.ExecutablePath);
                if (!string.IsNullOrEmpty(dir))
                {
                    string p = Path.Combine(dir, "bing.ico");
                    if (File.Exists(p)) { return p; }
                }
            }
            catch { }
            return "";
        }

        private static Icon LoadAppIcon(int size)
        {
            string p = LoadIconPath();
            if (p.Length > 0)
            {
                try { return new Icon(p, size, size); } catch { }
            }
            try { return Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            return null;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            try
            {
                if (logo != null && logo.Image != null) { logo.Image.Dispose(); logo.Image = null; }
            }
            catch { }
            base.OnFormClosed(e);
        }

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
