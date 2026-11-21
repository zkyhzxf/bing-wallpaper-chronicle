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
    internal sealed class CalendarForm : Form
    {
        private readonly MonthCalendar cal;
        private readonly Label hint;
        private readonly Button ok;
        private readonly Button cancel;

        public string SelectedDate { get; private set; }

        public CalendarForm()
        {
            Text = Program.AppName + (S.IsEn ? " · Search by date" : " · 按日期搜索");
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            ClientSize = new Size(300, 380);
            try { Font = new Font("Microsoft YaHei UI", 9F); } catch { }

            cal = new MonthCalendar();
            cal.MaxSelectionCount = 1;
            try
            {
                cal.MinDate = new DateTime(2016, 3, 5);
                cal.MaxDate = DateTime.Today;
                cal.SetDate(DateTime.Today);
            }
            catch { }
            cal.SetBounds(12, 12, 240, 240);
            Controls.Add(cal);

            hint = new Label();
            hint.AutoSize = false;
            hint.Height = 44;
            hint.ForeColor = SystemColors.GrayText;
            hint.Text = S.IsEn ? "Last 8 days: official original\r\nSince Mar 2016: archive original (online)" : "最近 8 天：官方原图\r\n2016-03 起：归档原图（需联网）";
            Controls.Add(hint);

            ok = new Button();
            ok.Text = S.BtnApply;
            ok.Size = new Size(88, 30);
            ok.DialogResult = DialogResult.OK;
            ok.Click += delegate
            {
                SelectedDate = cal.SelectionStart.ToString("yyyyMMdd");
            };
            Controls.Add(ok);

            cancel = new Button();
            cancel.Text = S.BtnCancel;
            cancel.Size = new Size(88, 30);
            cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Relayout();
        }

        /// <summary>DPI 缩放完成后按日历实际高度重排下面三个控件，避免被盖住。</summary>
        private void Relayout()
        {
            try
            {
                int left = 14;
                int width = cal.Right - left;
                if (width < 200) { width = 200; }

                hint.Left = left;
                hint.Width = width;
                hint.Top = cal.Bottom + 10;

                int btnY = hint.Bottom + 12;
                ok.Top = btnY;
                cancel.Top = btnY;
                ok.Left = (ClientSize.Width - ok.Width - cancel.Width - 12) / 2;
                cancel.Left = ok.Right + 12;

                ClientSize = new Size(ClientSize.Width, btnY + ok.Height + 14);
            }
            catch { }
        }
    }
    // -------------------------------------------------------------------- 卸载确认

    /// <summary>AI 调用说明：展示调用方式，三个复制按钮。</summary>
}
