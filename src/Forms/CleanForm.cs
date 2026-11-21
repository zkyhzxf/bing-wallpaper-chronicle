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
    internal sealed class CleanForm : Form
    {
        public enum Target { Archive, Cache, ArchiveIndex, Favorites }

        private readonly RadioButton rbRecycle;
        private readonly RadioButton rbPermanent;
        private readonly Button btnStart;
        private readonly Label lblNothing;

        public bool Permanent { get; private set; }

        public CleanForm(Target target)
        {
            string title = S.CleanTtlArchive;
            string dir = Program.ArchiveImgPath;
            string filter = "*.jpg";
            bool hasMode = true;

            if (target == Target.Cache) { title = S.CleanTtlCache; dir = Program.CacheDirPath; }
            else if (target == Target.ArchiveIndex) { title = S.CleanTtlIndex; dir = Program.ArchiveDirPath; filter = "*.json"; hasMode = false; }
            else if (target == Target.Favorites) { title = S.CleanTtlFav; dir = Program.FavDirPath; }

            Text = Program.AppName + title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            ClientSize = new Size(520, hasMode ? 400 : 300);
            try { Font = new Font("Microsoft YaHei UI", 9F); } catch { }

            // 统计
            int totalN = 0, favN = 0, pinN = 0, curN = 0;
            long totalB = 0;
            HashSet<string> favDates = new HashSet<string>();
            Dictionary<string, object> pinned = null;
            string curWp = "";
            try
            {
                foreach (string f in Directory.GetFiles(dir, filter))
                {
                    totalN++;
                    long sz = 0;
                    try { sz = new FileInfo(f).Length; } catch { }
                    totalB += sz;
                }
                if (target != Target.ArchiveIndex)
                {
                    foreach (string f in Directory.GetFiles(Program.FavDirPath, "*.jpg"))
                    {
                        string fn = Path.GetFileName(f);
                        if (fn.Length >= 8) { favDates.Add(fn.Substring(0, 8)); }
                    }
                    pinned = Program.ReadPinnedPublic();
                    curWp = Program.CurrentWallpaperPath();
                }
            }
            catch { }

            long actualB = 0;
            int actualN = 0;
            if (target != Target.ArchiveIndex)
            {
                try
                {
                    foreach (string f in Directory.GetFiles(dir, filter))
                    {
                        string fn = Path.GetFileName(f);
                        string d8 = fn.Length >= 8 ? fn.Substring(0, 8) : "";
                        bool prot = false;
                        if (d8.Length == 8)
                        {
                            if (favDates.Contains(d8)) { favN++; prot = true; }
                            else if (pinned != null && pinned.ContainsKey(d8)) { pinN++; prot = true; }
                        }
                        if (!prot && !string.IsNullOrEmpty(curWp) &&
                            string.Equals(curWp, f, StringComparison.OrdinalIgnoreCase)) { curN++; prot = true; }
                        if (!prot)
                        {
                            actualN++;
                            try { actualB += new FileInfo(f).Length; } catch { }
                        }
                    }
                }
                catch { }
            }
            else { actualN = totalN; actualB = totalB; }

            // 统计区
            Panel box = new Panel();
            box.BorderStyle = BorderStyle.FixedSingle;
            box.SetBounds(18, 16, 484, hasMode ? 176 : 96);
            Controls.Add(box);

            int yy = 10;
            box.Controls.Add(MkLabel(S.CleanTotal, 12, yy));
            box.Controls.Add(MkValue(totalN + (target == Target.ArchiveIndex ? " 个" : " 张"), 320, yy));
            yy += 24;
            box.Controls.Add(MkLabel(S.CleanSize, 12, yy));
            box.Controls.Add(MkValue(FmtSize(totalB), 320, yy));

            if (hasMode && target != Target.ArchiveIndex)
            {
                yy += 30;
                Label sep = new Label();
                sep.BorderStyle = BorderStyle.Fixed3D;
                sep.SetBounds(12, yy, 460, 2);
                box.Controls.Add(sep);
                yy += 8;
                if (favN > 0) { box.Controls.Add(MkLabel(S.CleanFavProtected, 12, yy)); box.Controls.Add(MkValue(favN + " 张", 320, yy)); yy += 22; }
                if (pinN > 0) { box.Controls.Add(MkLabel(S.CleanPinProtected, 12, yy)); box.Controls.Add(MkValue(pinN + " 张", 320, yy)); yy += 22; }
                if (curN > 0) { box.Controls.Add(MkLabel(S.CleanCurProtected, 12, yy)); box.Controls.Add(MkValue(curN + " 张", 320, yy)); yy += 22; }
                yy += 6;
                Label l2 = MkLabel(S.CleanActual, 12, yy);
                l2.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
                box.Controls.Add(l2);
                Label v2 = MkValue(actualN + " 张，" + FmtSize(actualB), 320, yy);
                v2.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
                box.Controls.Add(v2);
            }

            // 删除方式
            int ny = box.Bottom + 14;
            if (hasMode)
            {
                Label ml = MkLabel(S.CleanModeTitle, 18, ny);
                ml.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
                Controls.Add(ml);
                ny += 24;

                rbRecycle = new RadioButton();
                rbRecycle.Text = S.CleanToRecycle + "    " + S.CleanToRecycle2;
                rbRecycle.SetBounds(30, ny, 460, 22);
                Controls.Add(rbRecycle);
                ny += 24;

                rbPermanent = new RadioButton();
                rbPermanent.Text = S.CleanToDelete + "    " + S.CleanToDelete2;
                rbPermanent.SetBounds(30, ny, 460, 22);
                Controls.Add(rbPermanent);
                ny += 26;

                if (target == Target.Cache) { rbPermanent.Checked = true; }
                else { rbRecycle.Checked = true; }
            }

            // 提示
            lblNothing = new Label();
            lblNothing.SetBounds(18, ny, 484, 20);
            lblNothing.ForeColor = Color.Firebrick;
            if (actualN == 0) { lblNothing.Text = S.CleanNothing; }
            else if (target == Target.ArchiveIndex) { lblNothing.Text = S.CleanIndexHint; lblNothing.ForeColor = SystemColors.GrayText; }
            Controls.Add(lblNothing);

            // 按钮
            int by = ClientSize.Height - 52;
            btnStart = new Button();
            btnStart.Text = S.CleanStart;
            btnStart.SetBounds(18, by, 120, 34);
            btnStart.Enabled = actualN > 0;
            btnStart.Click += delegate { Accept(); };
            Controls.Add(btnStart);

            Button btnCancel = new Button();
            btnCancel.Text = S.BtnCancel;
            btnCancel.SetBounds(416, by, 86, 34);
            btnCancel.DialogResult = DialogResult.Cancel;
            Controls.Add(btnCancel);

            AcceptButton = btnStart;
            CancelButton = btnCancel;
        }

        private static Label MkLabel(string text, int x, int y)
        {
            Label l = new Label();
            l.Text = text;
            l.SetBounds(x, y, 300, 20);
            return l;
        }

        private static Label MkValue(string text, int x, int y)
        {
            Label l = new Label();
            l.Text = text;
            l.SetBounds(x, y, 150, 20);
            l.TextAlign = ContentAlignment.MiddleRight;
            return l;
        }

        private static string FmtSize(long b)
        {
            if (b >= 1073741824L) { return (b / 1073741824.0).ToString("0.0") + " GB"; }
            if (b >= 1048576L) { return (b / 1048576.0).ToString("0.0") + " MB"; }
            if (b >= 1024L) { return (b / 1024.0).ToString("0.0") + " KB"; }
            return b + " B";
        }

        private void Accept()
        {
            Permanent = (rbPermanent != null && rbPermanent.Checked);
            DialogResult = DialogResult.OK;
            Close();
        }
    }
    /// <summary>按关键词找壁纸：输入 → 列表 → 确认后换图。</summary>
}
