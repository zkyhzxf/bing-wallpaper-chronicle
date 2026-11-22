using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace BingWallpaperLite
{
    /// <summary>
    /// 接管目录：把图片仓库换成外部目录，状态全落在 takeover\ 沙盒里。
    /// 退出时整个沙盒删掉，安装目录的配置一点都没动过。
    /// </summary>
    internal sealed class TakeoverForm : Form
    {
        private readonly Label lblTitle;
        private readonly Label lblState;
        private readonly Label lblPath;
        private readonly Button btnPick;
        private readonly Button btnExit;

        public TakeoverForm()
        {
            Text = Program.AppName + " · " + S.TkTitle;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            ClientSize = new Size(620, 268);
            try { Font = new Font("Microsoft YaHei UI", 9F); } catch { }

            lblTitle = new Label();
            lblTitle.SetBounds(18, 16, 580, 22);
            lblTitle.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
            lblTitle.Text = S.TkHeader;
            Controls.Add(lblTitle);

            lblState = new Label();
            lblState.SetBounds(18, 46, 580, 22);
            Controls.Add(lblState);

            lblPath = new Label();
            lblPath.AutoSize = false;
            lblPath.SetBounds(18, 72, 580, 60);
            lblPath.ForeColor = SystemColors.GrayText;
            lblPath.Text = S.TkTip;
            Controls.Add(lblPath);

            btnPick = new Button();
            btnPick.Text = S.TkPick;
            btnPick.SetBounds(18, 210, 160, 34);
            btnPick.Click += delegate { DoPick(); };
            Controls.Add(btnPick);

            btnExit = new Button();
            btnExit.Text = S.TkExit;
            btnExit.SetBounds(190, 210, 160, 34);
            btnExit.Click += delegate { DoExit(); };
            Controls.Add(btnExit);

            Button btnOpen = new Button();
            btnOpen.Text = S.WallBtnOpen;
            btnOpen.SetBounds(362, 210, 130, 34);
            btnOpen.Click += delegate { OpenRoot(); };
            Controls.Add(btnOpen);

            Button btnClose = new Button();
            btnClose.Text = S.BtnClose;
            btnClose.SetBounds(504, 210, 90, 34);
            btnClose.Click += delegate { Close(); };
            Controls.Add(btnClose);
            AcceptButton = btnClose;
            CancelButton = btnClose;

            RefreshState();
        }

        private void RefreshState()
        {
            try
            {
                string root = Program.CurrentTakeoverRoot();
                bool active = Program.TakeoverActive();
                if (active)
                {
                    int n = 0;
                    try { n = Directory.GetFiles(root, "*.jpg").Length; } catch { }
                    lblState.Text = S.TkOn;
                    lblPath.Text = S.TkOnBody(root, n);
                    btnPick.Text = S.TkChange;
                    btnExit.Enabled = true;
                }
                else if (!string.IsNullOrEmpty(root))
                {
                    lblState.Text = S.TkBrokenState;
                    lblPath.Text = S.TkBrokenBody(root);
                    btnPick.Text = S.TkPick;
                    btnExit.Enabled = true;
                }
                else
                {
                    lblState.Text = S.TkOff;
                    lblPath.Text = S.TkTip;
                    btnPick.Text = S.TkPick;
                    btnExit.Enabled = false;
                }
            }
            catch { }
        }

        private void DoPick()
        {
            using (FolderBrowserDialog d = new FolderBrowserDialog())
            {
                d.Description = S.TkChoose;
                d.ShowNewFolderButton = false;
                if (d.ShowDialog(this) != DialogResult.OK) { return; }
                string chosen = d.SelectedPath;
                if (string.IsNullOrEmpty(chosen)) { return; }
                if (Directory.GetFiles(chosen, "*.jpg").Length == 0)
                {
                    if (MessageBox.Show(S.TkEmptyWarn, Program.AppName, MessageBoxButtons.OKCancel,
                            MessageBoxIcon.Warning) != DialogResult.OK) { return; }
                }
                try
                {
                    string dir = Program.TakeoverDirPath;
                    if (!Directory.Exists(dir)) { Directory.CreateDirectory(dir); }
                    System.Web.Script.Serialization.JavaScriptSerializer js =
                        new System.Web.Script.Serialization.JavaScriptSerializer();
                    System.Collections.Generic.Dictionary<string, object> cfg =
                        new System.Collections.Generic.Dictionary<string, object>();
                    cfg["root"] = chosen;
                    cfg["enteredAt"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    File.WriteAllText(Program.TakeoverCfgPath, js.Serialize(cfg),
                        new System.Text.UTF8Encoding(false));
                    MessageBox.Show(S.TkEntered(chosen), Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    RefreshState();
                    Program.RefreshAfterTakeover();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(S.TkFail + "\r\n" + ex.Message, Program.AppName,
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void DoExit()
        {
            int favs = 0;
            try
            {
                string fd = Path.Combine(Program.TakeoverDirPath, "favorites");
                if (Directory.Exists(fd)) { favs = Directory.GetFiles(fd, "*.jpg").Length; }
            }
            catch { }
            string q = favs > 0 ? S.TkExitConfirmFav(favs) : S.TkExitConfirm;
            if (MessageBox.Show(q, Program.AppName, MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Warning) != DialogResult.OK) { return; }
            try
            {
                Program.ExitTakeover(true);
                MessageBox.Show(S.TkExited, Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                RefreshState();
                Program.RefreshAfterTakeover();
            }
            catch (Exception ex)
            {
                MessageBox.Show(S.TkFail + "\r\n" + ex.Message, Program.AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OpenRoot()
        {
            try
            {
                string root = Program.CurrentTakeoverRoot();
                if (string.IsNullOrEmpty(root)) { root = Program.TakeoverDirPath; }
                if (Directory.Exists(root)) { System.Diagnostics.Process.Start("explorer.exe", "\"" + root + "\""); }
            }
            catch { }
        }
    }
}
