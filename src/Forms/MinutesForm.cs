using System;
using System.Drawing;
using System.Windows.Forms;

namespace BingWallpaperLite
{
    /// <summary>
    /// 轮播间隔输入：只收一个 1–720 的整数分钟。
    /// 摆放逻辑与 TimeForm 一致 —— owner 是隐藏的托盘窗体，CenterParent 会把它算到屏幕外。
    /// </summary>
    internal sealed class MinutesForm : Form
    {
        private readonly NumericUpDown picker;

        /// <summary>确定后是 1–720 之间的整数；取消保持 -1。</summary>
        public int SelectedMinutes { get; private set; }

        public MinutesForm(int current)
        {
            SelectedMinutes = -1;

            Text = Program.AppName + S.RotateCustomTitle;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            ClientSize = new Size(268, 118);
            Font = new Font("Microsoft YaHei UI", 9F);

            Label tip = new Label();
            tip.Text = S.RotateCustomHint;
            tip.AutoSize = false;
            tip.SetBounds(16, 14, 236, 20);
            Controls.Add(tip);

            picker = new NumericUpDown();
            picker.Minimum = 1;
            picker.Maximum = 720;
            picker.Value = (current < 1 ? 30 : (current > 720 ? 720 : current));
            picker.SetBounds(16, 40, 110, 26);
            Controls.Add(picker);

            Button ok = new Button();
            ok.Text = S.BtnOK;
            ok.SetBounds(140, 78, 52, 26);
            ok.Click += delegate
            {
                SelectedMinutes = (int)picker.Value;
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(ok);

            Button cancel = new Button();
            cancel.Text = S.BtnCancel;
            cancel.SetBounds(200, 78, 52, 26);
            cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
        }

        /// <summary>跟着鼠标出现，并夹进所在屏幕的工作区。</summary>
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
