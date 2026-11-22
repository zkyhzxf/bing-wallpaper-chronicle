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
        private readonly CheckBox cbIndex;
        private readonly CheckBox cbOld;

        public bool Permanent { get; private set; }
        public bool WithIndex { get; private set; }
        public bool WithOld { get; private set; }

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

            // 统计：清空收藏走独立口径——目录里的图都要离开收藏夹，
            // 当前壁纸那张是"移回缓存"，不是"保留在收藏里"
            int totalN = 0, favN = 0, curN = 0;
            long totalB = 0;
            long actualB = 0;
            int actualN = 0;

            if (target == Target.Favorites)
            {
                string curWpF = "";
                try
                {
                    curWpF = Program.CurrentWallpaperPath();
                    foreach (string f in Directory.GetFiles(dir, filter))
                    {
                        totalN++;
                        long sz = 0;
                        try { sz = new FileInfo(f).Length; } catch { }
                        totalB += sz;
                        if (!string.IsNullOrEmpty(curWpF) &&
                            string.Equals(curWpF, f, StringComparison.OrdinalIgnoreCase))
                        {
                            curN++;
                        }
                        else
                        {
                            actualN++;
                            actualB += sz;
                        }
                    }
                }
                catch { }
            }
            else if (target == Target.ArchiveIndex)
            {
                try
                {
                    foreach (string f in Directory.GetFiles(dir, filter))
                    {
                        totalN++;
                        try { totalB += new FileInfo(f).Length; } catch { }
                    }
                }
                catch { }
                actualN = totalN;
                actualB = totalB;
            }
            else
            {
                HashSet<string> favDates = new HashSet<string>();
                string curWp = "";
                try
                {
                    foreach (string f in Directory.GetFiles(dir, filter))
                    {
                        totalN++;
                        try { totalB += new FileInfo(f).Length; } catch { }
                    }
                    foreach (string f in Directory.GetFiles(Program.FavDirPath, "*.jpg"))
                    {
                        string fn = Path.GetFileName(f);
                        if (fn.Length >= 8) { favDates.Add(fn.Substring(0, 8)); }
                    }
                    curWp = Program.CurrentWallpaperPath();
                }
                catch { }

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

            // 老存档单独统计：2009-08 ~ 2016-02，按月分目录
            int oldN = 0;
            long oldB = 0;
            if (target == Target.Archive)
            {
                try
                {
                    string oldRoot = Program.OldDirPath;
                    if (Directory.Exists(oldRoot))
                    {
                        foreach (string om in Directory.GetDirectories(oldRoot))
                        {
                            foreach (string of in Directory.GetFiles(om, "*.jpg"))
                            {
                                oldN++;
                                try { oldB += new FileInfo(of).Length; } catch { }
                            }
                        }
                    }
                }
                catch { }
            }

            // 统计区
            Panel box = new Panel();
            box.BorderStyle = BorderStyle.FixedSingle;
            int boxH = hasMode ? 176 : 96;
            if (target == Target.Archive) { boxH = 216; }
            box.SetBounds(18, 16, 484, boxH);
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

                if (curN > 0)
                {
                    string curLabel = (target == Target.Favorites) ? S.CleanCurMoved : S.CleanCurProtected;
                    box.Controls.Add(MkLabel(curLabel, 12, yy));
                    box.Controls.Add(MkValue(curN + " 张", 320, yy));
                    yy += 22;
                }
                yy += 6;
                Label l2 = MkLabel(S.CleanActual, 12, yy);
                l2.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
                box.Controls.Add(l2);
                Label v2 = MkValue(actualN + " 张，" + FmtSize(actualB), 320, yy);
                v2.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
                box.Controls.Add(v2);

                if (target == Target.Archive)
                {
                    Label capHint = new Label();
                    capHint.SetBounds(12, yy + 28, 460, 54);
                    capHint.ForeColor = SystemColors.GrayText;
                    capHint.Text = S.CleanCapHint(Program.ArchiveCapMB());
                    box.Controls.Add(capHint);
                }
            }

            // 清理归档时多给一个索引开关（默认不勾：索引只有几百 KB，删了反而要重下）
            int ny = box.Bottom + 14;
            if (target == Target.Archive)
            {
                cbIndex = new CheckBox();
                cbIndex.Text = S.CleanWithIndex;
                cbIndex.SetBounds(18, ny, 484, 22);
                cbIndex.Checked = false;
                cbIndex.CheckedChanged += delegate
                {
                    if (btnStart != null) { btnStart.Enabled = (actualN > 0) || cbIndex.Checked; }
                };
                Controls.Add(cbIndex);

                Label idxHint = new Label();
                idxHint.SetBounds(40, ny + 22, 462, 18);
                idxHint.ForeColor = SystemColors.GrayText;
                idxHint.Text = S.CleanWithIndexHint;
                Controls.Add(idxHint);
                ny += 48;

                cbOld = new CheckBox();
                cbOld.Text = S.CleanWithOld(oldN, Math.Round(oldB / 1024.0 / 1024.0, 1));
                cbOld.SetBounds(18, ny, 484, 22);
                cbOld.Checked = false;
                cbOld.Enabled = oldN > 0;
                cbOld.CheckedChanged += delegate
                {
                    if (btnStart != null) { btnStart.Enabled = (actualN > 0) || cbIndex.Checked || cbOld.Checked; }
                };
                Controls.Add(cbOld);

                Label oldHint = new Label();
                oldHint.SetBounds(40, ny + 22, 462, 18);
                oldHint.ForeColor = SystemColors.GrayText;
                oldHint.Text = S.CleanWithOldHint;
                Controls.Add(oldHint);
                ny += 48;

                ClientSize = new Size(520, 508);
            }

            // 删除方式
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
            btnStart.Enabled = (actualN > 0) || (cbIndex != null && cbIndex.Checked);
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
            WithIndex = (cbIndex != null && cbIndex.Checked);
            WithOld = (cbOld != null && cbOld.Checked);
            DialogResult = DialogResult.OK;
            Close();
        }
    }
    /// <summary>按关键词找壁纸：输入 → 列表 → 确认后换图。</summary>
}
