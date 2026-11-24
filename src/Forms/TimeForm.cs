using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace BingWallpaperLite
{
    /// <summary>
    /// 自定义每日换图时间。只收一个 HH:mm —— 计划任务的触发时刻就是它。
    /// 预设覆盖不到的时刻（比如 07:35）走这里，不再让用户去改注册任务。
    /// </summary>
    internal sealed class TimeForm : Form
    {
        private readonly DateTimePicker picker;

        /// <summary>确定后是 "HH:mm"，取消是空串。</summary>
        public string SelectedHm { get; private set; }

        public TimeForm(int currentMinutes)
        {
            SelectedHm = "";

            Text = Program.AppName + S.TaskTimeCustom;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(268, 118);
            Font = new Font("Microsoft YaHei UI", 9F);

            Label tip = new Label();
            tip.Text = S.TaskTimePickHint;
            tip.AutoSize = false;
            tip.SetBounds(16, 14, 236, 20);
            Controls.Add(tip);

            picker = new DateTimePicker();
            picker.Format = DateTimePickerFormat.Custom;
            picker.CustomFormat = "HH:mm";
            picker.ShowUpDown = true;
            picker.SetBounds(16, 40, 110, 26);
            DateTime baseDay = DateTime.Today;
            int hh = currentMinutes / 60;
            int mm = currentMinutes % 60;
            if (hh < 0 || hh > 23) { hh = 0; }
            if (mm < 0 || mm > 59) { mm = 0; }
            picker.Value = baseDay.AddHours(hh).AddMinutes(mm);
            Controls.Add(picker);

            Button ok = new Button();
            ok.Text = S.BtnOK;
            ok.SetBounds(140, 78, 52, 26);
            ok.Click += delegate
            {
                SelectedHm = picker.Value.ToString("HH:mm", CultureInfo.InvariantCulture);
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
    }
}
