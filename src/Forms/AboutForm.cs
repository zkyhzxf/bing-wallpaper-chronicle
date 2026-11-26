using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace BingWallpaperLite
{
    /// <summary>关于窗口：正文 + 更新检查状态行 + 项目页按钮。</summary>
    internal sealed class AboutForm : Form
    {
        private readonly Label lblUpdate;
        private readonly Button btnRecheck;
        private bool probing;

        public AboutForm(string aboutText)
        {
            Text = Program.AppName + (S.IsEn ? " · About" : " · 关于");
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            ClientSize = new Size(660, 570);
            try { Font = new Font("Microsoft YaHei UI", 9F); } catch { }

            TextBox txt = new TextBox();
            txt.Multiline = true;
            txt.ReadOnly = true;
            txt.ScrollBars = ScrollBars.Vertical;
            txt.WordWrap = true;
            txt.SetBounds(16, 14, 628, 470);
            txt.BackColor = SystemColors.Window;
            try { txt.Font = new Font("Consolas", 8.5F); } catch { }
            txt.Text = aboutText == null ? "" : aboutText;
            txt.Select(0, 0);
            Controls.Add(txt);

            lblUpdate = new Label();
            lblUpdate.SetBounds(16, 494, 420, 20);
            lblUpdate.AutoSize = false;
            lblUpdate.ForeColor = SystemColors.GrayText;
            lblUpdate.Text = S.UpdChecking;
            Controls.Add(lblUpdate);

            Button btnReleases = new Button();
            btnReleases.Text = S.AboutBtnReleases;
            btnReleases.SetBounds(344, 526, 120, 30);
            btnReleases.Click += delegate { Program.OpenReleasesPage(); };
            Controls.Add(btnReleases);

            btnRecheck = new Button();
            btnRecheck.Text = S.AboutBtnRecheck;
            btnRecheck.SetBounds(472, 526, 92, 30);
            btnRecheck.Click += delegate { StartProbe(); };
            Controls.Add(btnRecheck);

            Button btnClose = new Button();
            btnClose.Text = S.AboutBtnClose;
            btnClose.SetBounds(572, 526, 72, 30);
            btnClose.DialogResult = DialogResult.Cancel;
            btnClose.Click += delegate { Close(); };
            Controls.Add(btnClose);

            CancelButton = btnClose;
            AcceptButton = btnRecheck;

            Shown += delegate { StartProbe(); };
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
