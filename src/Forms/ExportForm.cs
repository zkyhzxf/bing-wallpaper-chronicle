using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace BingWallpaperLite
{
    /// <summary>导出已下载壁纸：勾选要导出的种类 + 选择目标文件夹。</summary>
    internal sealed class ExportForm : Form
    {
        private readonly CheckBox cbCache;
        private readonly CheckBox cbFav;
        private readonly CheckBox cbArch;
        private readonly TextBox txtDest;
        private readonly Label lblSum;
        private readonly Button btnOk;

        private readonly string[] dirs = new string[3];
        private readonly int[] counts = new int[3];
        private readonly double[] mbs = new double[3];

        /// <summary>用户勾选的源目录（只含确实有图片的）。</summary>
        public List<string> SelectedDirs { get; private set; }

        public string DestPath { get { return txtDest.Text.Trim(); } }

        public ExportForm()
        {
            Text = Program.AppName + S.ExpTitle;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            ClientSize = new Size(520, 264);
            try { Font = new Font("Microsoft YaHei UI", 9F); } catch { }

            dirs[0] = Program.CacheDirPath;
            dirs[1] = Program.FavDirPath;
            dirs[2] = Program.ArchiveImgPath;
            for (int i = 0; i < 3; i++) { Stat(dirs[i], out counts[i], out mbs[i]); }

            Label lblWhich = new Label();
            lblWhich.Text = S.ExpWhich;
            lblWhich.SetBounds(16, 14, 200, 20);
            Controls.Add(lblWhich);

            cbCache = MakeCheck(44, dirs[0], counts[0], mbs[0], true);
            cbFav = MakeCheck(70, dirs[1], counts[1], mbs[1], true);
            cbArch = MakeCheck(96, dirs[2], counts[2], mbs[2], false);

            Panel line = new Panel();
            line.SetBounds(16, 126, 488, 1);
            line.BackColor = SystemColors.ControlLight;
            Controls.Add(line);

            Label lblTo = new Label();
            lblTo.Text = S.ExpTo;
            lblTo.SetBounds(16, 136, 60, 20);
            Controls.Add(lblTo);

            txtDest = new TextBox();
            txtDest.SetBounds(76, 134, 320, 23);
            txtDest.Text = DefaultDest();
            txtDest.TextChanged += delegate { RefreshSum(); };
            Controls.Add(txtDest);

            Button btnBrowse = new Button();
            btnBrowse.Text = S.BtnBrowse;
            btnBrowse.SetBounds(404, 133, 76, 25);
            btnBrowse.Click += delegate
            {
                string before = txtDest.Text;
                using (FolderBrowserDialog d = new FolderBrowserDialog())
                {
                    d.Description = S.ExpTo;
                    d.ShowNewFolderButton = true;
                    try { if (Directory.Exists(before)) { d.SelectedPath = before; } } catch { }
                    if (d.ShowDialog(this) != DialogResult.OK) { return; }
                    if (Program.IsInsideProgramDir(d.SelectedPath))
                    {
                        MessageBox.Show(S.WarnInsideApp, Program.AppName,
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        txtDest.Text = before;
                        return;
                    }
                    txtDest.Text = d.SelectedPath;
                }
            };
            Controls.Add(btnBrowse);

            lblSum = new Label();
            lblSum.SetBounds(16, 168, 488, 20);
            lblSum.AutoSize = false;
            lblSum.ForeColor = SystemColors.GrayText;
            Controls.Add(lblSum);

            btnOk = new Button();
            btnOk.Text = S.ExpBtn;
            btnOk.SetBounds(300, 220, 90, 30);
            btnOk.Click += delegate { Accept(); };
            Controls.Add(btnOk);

            Button btnCancel = new Button();
            btnCancel.Text = S.BtnCancel;
            btnCancel.SetBounds(400, 220, 90, 30);
            btnCancel.DialogResult = DialogResult.Cancel;
            Controls.Add(btnCancel);

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            RefreshSum();
        }

        private CheckBox MakeCheck(int top, string dir, int n, double mb, bool defChecked)
        {
            CheckBox cb = new CheckBox();
            cb.SetBounds(24, top, 470, 22);
            cb.Text = TitleOf(dir) + "   " + n + " " + (S.IsEn ? "items" : "张") + "   " + mb.ToString("0.0") + " MB";
            cb.Enabled = n > 0;
            cb.Checked = defChecked && n > 0;
            cb.CheckedChanged += delegate { RefreshSum(); };
            Controls.Add(cb);
            return cb;
        }

        private static string TitleOf(string dir)
        {
            if (string.Equals(dir, Program.CacheDirPath, StringComparison.OrdinalIgnoreCase)) { return S.IsEn ? "Cache" : "缓存"; }
            if (string.Equals(dir, Program.FavDirPath, StringComparison.OrdinalIgnoreCase)) { return S.IsEn ? "Favorites" : "收藏"; }
            return S.IsEn ? "Archived" : "历史归档";
        }

        private static string DefaultDest()
        {
            try
            {
                string desk = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                return Path.Combine(desk, S.IsEn ? "WallpaperBackup" : "壁纸备份");
            }
            catch { return ""; }
        }

        private void RefreshSum()
        {
            int n = 0;
            double mb = 0;
            CheckBox[] cbs = new CheckBox[] { cbCache, cbFav, cbArch };
            for (int i = 0; i < 3; i++)
            {
                if (cbs[i] != null && cbs[i].Checked) { n += counts[i]; mb += mbs[i]; }
            }
            lblSum.Text = S.ExpSum(n, mb);
            if (btnOk != null) { btnOk.Enabled = n > 0; }
        }

        private void Accept()
        {
            List<string> picked = new List<string>();
            CheckBox[] cbs = new CheckBox[] { cbCache, cbFav, cbArch };
            for (int i = 0; i < 3; i++)
            {
                if (cbs[i] != null && cbs[i].Checked && counts[i] > 0) { picked.Add(dirs[i]); }
            }
            if (picked.Count == 0)
            {
                MessageBox.Show(S.ExpEmpty, Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string dest = DestPath;
            if (dest.Length == 0)
            {
                MessageBox.Show(S.WarnNoPath, Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtDest.Focus();
                return;
            }
            if (Program.IsInsideProgramDir(dest))
            {
                MessageBox.Show(S.WarnInsideApp, Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtDest.Focus();
                return;
            }

            try { Directory.CreateDirectory(dest); }
            catch (Exception ex)
            {
                MessageBox.Show(S.WarnBadDir + ex.Message, Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SelectedDirs = picked;
            DialogResult = DialogResult.OK;
            Close();
        }

        private static void Stat(string dir, out int n, out double mb)
        {
            n = 0;
            mb = 0;
            try
            {
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) { return; }
                foreach (string f in Directory.GetFiles(dir, "*.jpg"))
                {
                    n++;
                    try { mb += new FileInfo(f).Length / 1024.0 / 1024.0; } catch { }
                }
            }
            catch { }
        }
    }
}
