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
    internal sealed class SetupForm : Form
    {
        private readonly TextBox txtPath;

        public string SelectedPath { get { return txtPath.Text.Trim(); } }
        public bool PortableMode { get; private set; }

        public SetupForm(string defaultPath)
        {
            Text = Program.AppName + (S.IsEn ? " · Setup" : " · 安装");
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            ClientSize = new Size(540, 296);
            try { Font = new Font("Microsoft YaHei UI", 9F); }
            catch { }

            Label pre = new Label();
            pre.SetBounds(16, 76, 500, 40);
            pre.AutoSize = false;
            pre.ForeColor = Color.FromArgb(198, 92, 0);
            pre.Text = S.IsEn
                ? "Your desktop wallpaper will change to today's Bing image right after install."
                : "安装后会立即把桌面壁纸换成今天的必应图。";
            Controls.Add(pre);

            Label lbl = new Label();
            lbl.Text = S.IsEn ? "Install location:" : "选择安装位置：";
            lbl.SetBounds(16, 16, 200, 20);
            Controls.Add(lbl);

            txtPath = new TextBox();
            txtPath.Text = defaultPath;
            txtPath.SetBounds(16, 40, 424, 23);
            Controls.Add(txtPath);

            Button btnBrowse = new Button();
            btnBrowse.Text = S.BtnBrowse;
            btnBrowse.SetBounds(448, 39, 76, 25);
            btnBrowse.Click += delegate
            {
                using (FolderBrowserDialog d = new FolderBrowserDialog())
                {
                    d.Description = S.IsEn ? ("Choose a folder for " + Program.AppName) : ("选择 " + Program.AppName + " 的安装文件夹");
                    d.SelectedPath = Directory.Exists(txtPath.Text) ? txtPath.Text : Path.GetDirectoryName(txtPath.Text);
                    d.ShowNewFolderButton = true;
                    if (d.ShowDialog(this) == DialogResult.OK) { txtPath.Text = d.SelectedPath; }
                }
            };
            Controls.Add(btnBrowse);

            Label hint = new Label();
            hint.SetBounds(16, 74, 508, 108);
            hint.Text =
                S.SetupNote1 + S.SetupNote2 + S.SetupNote3 + S.SetupNote4;
            Controls.Add(hint);

            Button btnInstall = new Button();
            btnInstall.Text = S.BtnInstallTx;
            btnInstall.SetBounds(288, 214, 88, 30);
            btnInstall.Click += delegate
            {
                string p = SelectedPath;
                if (p.Length == 0) { MessageBox.Show(S.WarnNoPath, Program.AppName); return; }
                try { Directory.CreateDirectory(p); }
                catch (Exception ex)
                {
                    MessageBox.Show(S.WarnBadDir + ex.Message, Program.AppName);
                    return;
                }
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(btnInstall);

            Button btnPortable = new Button();
            btnPortable.Text = S.BtnPortable;
            btnPortable.SetBounds(184, 214, 96, 30);
            btnPortable.Click += delegate
            {
                PortableMode = true;
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(btnPortable);

            Button btnCancel = new Button();
            btnCancel.Text = S.BtnCancel;
            btnCancel.SetBounds(384, 214, 88, 30);
            btnCancel.DialogResult = DialogResult.Cancel;
            Controls.Add(btnCancel);

            AcceptButton = btnInstall;
            CancelButton = btnCancel;
        }
    }
}
