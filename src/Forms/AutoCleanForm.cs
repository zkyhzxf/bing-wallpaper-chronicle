using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace BingWallpaperLite
{
    /// <summary>
    /// 自动清理设置：保留天数 + 每次换图后自动清理哪些项目。
    /// 布局与「立即清理」保持同一套（每项两行、张数右对齐），
    /// 用户在两个窗口之间不用重新学一遍。
    /// </summary>
    internal sealed class AutoCleanForm : Form
    {
        private readonly NumericUpDown days;
        private readonly NumericUpDown cap;
        private readonly CheckBox[] boxes;
        private readonly string[] keys;
        private readonly bool[] loaded;

        private const int RowH = 58;
        private const int BoxX = 14;
        private const int BoxW = 300;
        private const int NumX = 320;
        private const int NumW = 208;
        private const int DescX = 34;
        private const int DescW = 494;

        /// <summary>确定后的保留天数。</summary>
        public int KeepDays { get; private set; }

        /// <summary>确定后的清理项目，逗号分隔，如 "cache,archive"。</summary>
        public string Items { get; private set; }

        /// <summary>确定后的「我选的壁纸」容量上限（MB）。</summary>
        public int CapMB { get; private set; }

        public AutoCleanForm(int curDays, string curItems, string archInfo, int curCapMB)
        {
            keys = new string[] { "cache", "thumbs", "wall", "stories", "archive" };
            boxes = new CheckBox[keys.Length];
            loaded = new bool[keys.Length];

            KeepDays = curDays;
            Items = curItems == null ? "" : curItems;

            string hay = "," + Items.Replace(" ", "") + ",";
            for (int i = 0; i < keys.Length; i++)
            {
                loaded[i] = hay.IndexOf("," + keys[i] + ",", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            Text = Program.AppName + S.AcTitle;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            ClientSize = new Size(560, 516);
            Font = new Font("Microsoft YaHei UI", 9F);

            Label lblDays = new Label();
            lblDays.Text = S.AcKeepDays;
            lblDays.AutoSize = false;
            lblDays.SetBounds(16, 20, 84, 22);
            Controls.Add(lblDays);

            days = new NumericUpDown();
            days.Minimum = 8;
            days.Maximum = 365;
            days.Value = Clamp(curDays, 8, 365);
            days.SetBounds(104, 16, 72, 24);
            Controls.Add(days);

            Label lblUnit = new Label();
            lblUnit.Text = S.AcDayUnit;
            lblUnit.AutoSize = false;
            lblUnit.SetBounds(182, 20, 300, 22);
            Controls.Add(lblUnit);

            Label lblCap = new Label();
            lblCap.Text = S.AcCapLbl;
            lblCap.AutoSize = false;
            lblCap.SetBounds(300, 20, 72, 22);
            Controls.Add(lblCap);

            cap = new NumericUpDown();
            cap.Minimum = 100;
            cap.Maximum = 5000;
            cap.Increment = 100;
            cap.Value = Clamp(curCapMB, 100, 5000);
            cap.SetBounds(376, 16, 78, 24);
            Controls.Add(cap);

            Label lblCapUnit = new Label();
            lblCapUnit.Text = "MB";
            lblCapUnit.AutoSize = false;
            lblCapUnit.SetBounds(460, 20, 40, 22);
            Controls.Add(lblCapUnit);

            Label lblHint = new Label();
            lblHint.Text = S.AcKeepHint;
            lblHint.AutoSize = false;
            lblHint.ForeColor = SystemColors.GrayText;
            lblHint.SetBounds(16, 46, 528, 20);
            Controls.Add(lblHint);

            GroupBox box = new GroupBox();
            box.Text = S.AcItems;
            box.SetBounds(12, 72, 536, RowH * 5 + 26);
            Controls.Add(box);

            string[] names = new string[]
            {
                S.CnItemCache, S.CnItemThumbs, S.CnItemWall, S.CnItemStories, S.CnItemArch
            };
            string[] descs = new string[]
            {
                S.CnItemCacheDesc, S.CnItemThumbsDesc, S.CnItemWallDesc, S.CnItemStoriesDesc,
                S.AcItemArchiveDesc
            };
            string[] nums = new string[] { "", "", "", "", archInfo };

            Font small = new Font("Microsoft YaHei UI", 8F);
            for (int i = 0; i < boxes.Length; i++)
            {
                int y = 20 + i * RowH;

                CheckBox cb = new CheckBox();
                cb.Text = names[i];
                cb.AutoSize = false;
                cb.SetBounds(BoxX, y, BoxW, 22);
                cb.Checked = loaded[i];
                boxes[i] = cb;
                box.Controls.Add(cb);

                if (nums[i].Length > 0)
                {
                    Label ln = new Label();
                    ln.Text = nums[i];
                    ln.AutoSize = false;
                    ln.TextAlign = ContentAlignment.MiddleRight;
                    ln.SetBounds(NumX, y + 1, NumW, 20);
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

            Button ok = new Button();
            ok.Text = S.BtnOK;
            ok.SetBounds(426, 472, 56, 26);
            ok.Click += delegate
            {
                KeepDays = (int)days.Value;
                CapMB = (int)cap.Value;
                Items = JoinChecked();
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(ok);

            Button cancel = new Button();
            cancel.Text = S.BtnCancel;
            cancel.SetBounds(488, 472, 56, 26);
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
