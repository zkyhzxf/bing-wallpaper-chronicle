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
    internal sealed class UninstallForm : Form
    {
        private readonly CheckBox chkFav;
        private readonly CheckBox chkArch;
        private readonly Label lbl;

        public bool KeepFavorites { get { return chkFav.Checked; } }
        public bool KeepArchive { get { return chkArch.Checked; } }

        public UninstallForm(int favCount, int archCount, int cacheCount, double cacheMB, string installPath)
        {
            Text = Program.AppName + (S.IsEn ? " · Uninstall" : " · 卸载");
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            ClientSize = new Size(470, 396);
            try { Font = new Font("Microsoft YaHei UI", 9F); } catch { }

            lbl = new Label();
            lbl.AutoSize = false;
            lbl.SetBounds(16, 16, 438, 150);
            lbl.Text =
                S.DlHead + "\r\n" +
                S.DlStartup + "\r\n" +
                S.DlTask + "\r\n" +
                S.DlUninstEntry + "\r\n" +
                S.CacheLineN(cacheCount, cacheMB) + "\r\n" +
                S.DlArchiveIdx + "\r\n\r\n" +
                S.DlKeepProgram;
            Controls.Add(lbl);

            chkFav = new CheckBox();
            chkFav.SetBounds(16, 176, 438, 24);
            chkFav.Text = S.KeepFavN(favCount);
            chkFav.Checked = true;
            chkFav.Enabled = favCount > 0;
            Controls.Add(chkFav);

            chkArch = new CheckBox();
            chkArch.SetBounds(16, 206, 438, 24);
            chkArch.Text = S.KeepArchN(archCount);
            chkArch.Checked = true;
            chkArch.Enabled = archCount > 0;
            Controls.Add(chkArch);

            Label hint = new Label();
            hint.SetBounds(36, 236, 418, 54);
            hint.ForeColor = SystemColors.GrayText;
            hint.Text = S.DlUninstHint +
                        (archCount > 0
                            ? S.ArchNoteAlt
                            : S.DlNoArch);
            Controls.Add(hint);

            Label pathLabel = new Label();
            pathLabel.SetBounds(16, 300, 438, 20);
            pathLabel.ForeColor = SystemColors.GrayText;
            pathLabel.Text = S.DlProgramDir + installPath;
            Controls.Add(pathLabel);

            Button ok = new Button();
            ok.Text = S.BtnUninstall;
            ok.SetBounds(270, 336, 88, 30);
            ok.DialogResult = DialogResult.OK;
            Controls.Add(ok);

            Button cancel = new Button();
            cancel.Text = S.BtnCancel;
            cancel.SetBounds(366, 336, 88, 30);
            cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
        }
    }
    // -------------------------------------------------------------------- 安装向导
}
