using System;
using System.Drawing;
using System.Windows.Forms;

namespace BingWallpaperLite
{
    /// <summary>
    /// 发现新版本的确认窗。三个出口固定映射到 Yes / No / Ignore：
    ///   Yes    = 现在下载并安装
    ///   No     = 以后再说（下次启动还会提）
    ///   Ignore = 跳过这个版本（更高的版本仍会提）
    /// 不用 MessageBox：它的按钮文字写死成「是 / 否 / 取消」，
    /// 「取消」看着像取消这次操作，实际却是「再也不提」，会误导。
    /// </summary>
    internal sealed class UpdateForm : Form
    {
        public UpdateForm(string latest, string cur, string size)
        {
            Text = Program.AppName + " · " + S.UpdNewTitle;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            ClientSize = new Size(468, 216);
            try { Font = new Font("Microsoft YaHei UI", 9F); } catch { }

            Label body = new Label();
            body.AutoSize = false;
            body.SetBounds(18, 16, 432, 96);
            body.Text = S.UpdNewBody(latest, cur, size);
            Controls.Add(body);

            Label tip = new Label();
            tip.AutoSize = false;
            tip.SetBounds(18, 116, 432, 40);
            tip.ForeColor = SystemColors.GrayText;
            tip.Text = S.UpdNewHint;
            Controls.Add(tip);

            Button btnNow = new Button();
            btnNow.Text = S.BtnUpgradeNow;
            btnNow.SetBounds(18, 168, 130, 32);
            btnNow.DialogResult = DialogResult.Yes;
            Controls.Add(btnNow);

            Button btnLater = new Button();
            btnLater.Text = S.BtnLater;
            btnLater.SetBounds(158, 168, 130, 32);
            btnLater.DialogResult = DialogResult.No;
            Controls.Add(btnLater);

            Button btnSkip = new Button();
            btnSkip.Text = S.BtnSkipVer;
            btnSkip.SetBounds(298, 168, 152, 32);
            btnSkip.DialogResult = DialogResult.Ignore;
            Controls.Add(btnSkip);

            AcceptButton = btnNow;
            CancelButton = btnLater;
        }
    }
}
