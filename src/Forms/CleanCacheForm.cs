using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace BingWallpaperLite
{
    /// <summary>
    /// 立即清理：一次手动清理，范围自己勾。
    /// 每项两行 —— 第一行勾选框 + 名称 + 张数（右对齐），第二行灰字说清后果，
    /// 免得名字长被截断、或者用户看不懂勾了会发生什么。
    /// 前四项按上面的天数清；第 5、6 项是整目录清空；第 7 项动收藏，标红。
    /// </summary>
    internal sealed class CleanCacheForm : Form
    {
        private readonly NumericUpDown days;
        private readonly CheckBox[] boxes;
        private readonly string[] keys;

        private const int RowH = 58;
        private const int BoxX = 14;
        private const int BoxW = 300;
        private const int NumX = 320;
        private const int NumW = 208;
        private const int DescX = 34;
        private const int DescW = 494;

        /// <summary>确定后传给 -Keep 的天数。</summary>
        public int KeepDays { get; private set; }

        /// <summary>确定后勾中的项目，逗号分隔：cache,thumbs,wall,stories,archive,old,fav。</summary>
        public string Items { get; private set; }

        public CleanCacheForm(int curKeep, string archInfo, string oldInfo, int favCount)
        {
            keys = new string[] { "cache", "thumbs", "wall", "stories", "archive", "old", "fav" };
            boxes = new CheckBox[keys.Length];

            KeepDays = curKeep;
            Items = "cache";

            Text = Program.AppName + S.CnTitle;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            ClientSize = new Size(560, 612);
            Font = new Font("Microsoft YaHei UI", 9F);

            Label lblKeep = new Label();
            lblKeep.Text = S.CnKeepDays;
            lblKeep.AutoSize = false;
            lblKeep.SetBounds(16, 20, 62, 22);
            Controls.Add(lblKeep);

            days = new NumericUpDown();
            days.Minimum = 1;
            days.Maximum = 365;
            days.Value = Clamp(curKeep, 1, 365);
            days.SetBounds(82, 16, 72, 24);
            Controls.Add(days);

            Label lblUnit = new Label();
            lblUnit.Text = S.CnDayUnit;
            lblUnit.AutoSize = false;
            lblUnit.SetBounds(160, 20, 300, 22);
            Controls.Add(lblUnit);

            Label lblHint = new Label();
            lblHint.Text = S.CnHint;
            lblHint.AutoSize = false;
            lblHint.ForeColor = SystemColors.GrayText;
            lblHint.SetBounds(16, 46, 528, 20);
            Controls.Add(lblHint);

            GroupBox box = new GroupBox();
            box.Text = S.AcItems;
            box.SetBounds(12, 72, 536, RowH * 7 + 26);
            Controls.Add(box);

            string[] names = new string[]
            {
                S.CnItemCache, S.CnItemThumbs, S.CnItemWall, S.CnItemStories,
                S.CnItemArch, S.CnItemOld, S.CnItemFav
            };
            string[] descs = new string[]
            {
                S.CnItemCacheDesc, S.CnItemThumbsDesc, S.CnItemWallDesc, S.CnItemStoriesDesc,
                S.CnItemArchDesc, S.CnItemOldDesc, S.CnItemFavDesc
            };
            string[] nums = new string[]
            {
                "", "", "", "", archInfo, oldInfo, favCount > 0 ? (favCount + " 张") : ""
            };

            Font small = new Font("Microsoft YaHei UI", 8F);
            for (int i = 0; i < boxes.Length; i++)
            {
                int y = 20 + i * RowH;

                CheckBox cb = new CheckBox();
                cb.Text = names[i];
                cb.AutoSize = false;
                cb.SetBounds(BoxX, y, BoxW, 22);
                cb.Checked = (i == 0);
                if (i == 6) { cb.ForeColor = Color.FromArgb(192, 0, 0); }
                boxes[i] = cb;
                box.Controls.Add(cb);

                if (nums[i].Length > 0)
                {
                    Label ln = new Label();
                    ln.Text = nums[i];
                    ln.AutoSize = false;
                    ln.TextAlign = ContentAlignment.MiddleRight;
                    ln.SetBounds(NumX, y + 1, NumW, 20);
                    if (i == 6) { ln.ForeColor = Color.FromArgb(192, 0, 0); }
                    box.Controls.Add(ln);
                }

                Label ld = new Label();
                ld.Text = descs[i];
                ld.AutoSize = false;
                ld.Font = small;
                ld.ForeColor = SystemColors.GrayText;
                ld.SetBounds(DescX, y + 22, DescW, 34);
                box.Controls.Add(ld);
            }

            Label lblNote = new Label();
            lblNote.Text = S.CnProtectNote;
            lblNote.AutoSize = false;
            lblNote.ForeColor = SystemColors.GrayText;
            lblNote.SetBounds(16, 72 + RowH * 7 + 36, 528, 36);
            Controls.Add(lblNote);

            Button ok = new Button();
            ok.Text = S.CnTitle;
            ok.SetBounds(420, 570, 68, 26);
            ok.Click += delegate
            {
                KeepDays = (int)days.Value;
                Items = JoinChecked();
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(ok);

            Button cancel = new Button();
            cancel.Text = S.BtnCancel;
            cancel.SetBounds(494, 570, 56, 26);
            cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
        }

        private string JoinChecked()
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < boxes.Length; i++)
            {
                if (!boxes[i].Checked) { continue; }
                if (sb.Length > 0) { sb.Append(","); }
                sb.Append(keys[i]);
            }
            return sb.ToString();
        }

        private static decimal Clamp(int v, int lo, int hi)
        {
            if (v < lo) { return lo; }
            if (v > hi) { return hi; }
            return v;
        }

        /// <summary>跟着鼠标出现并夹进工作区，理由同 TimeForm：托盘 owner 在 0,0。</summary>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            try
            {
                Point p = Cursor.Position;
                Rectangle wa = Screen.FromPoint(p).WorkingArea;
                int x = p.X - Width / 2;
                int y = p.Y - Height / 2;
                if (x < wa.Left) { x = wa.Left; }
                if (y < wa.Top) { y = wa.Top; }
                if (x + Width > wa.Right) { x = wa.Right - Width; }
                if (y + Height > wa.Bottom) { y = wa.Bottom - Height; }
                Location = new Point(x, y);
            }
            catch { }
        }
    }
}
