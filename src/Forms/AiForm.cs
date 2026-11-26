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
    internal sealed class AiForm : Form
    {
        public AiForm()
        {
            Text = Program.AppName + S.AiTitle;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            ClientSize = new Size(660, 540);
            try { Font = new Font("Microsoft YaHei UI", 9F); } catch { }

            Label intro = new Label();
            intro.AutoSize = false;
            intro.SetBounds(20, 16, 620, 76);
            intro.Text = S.AiIntro;
            Controls.Add(intro);
            int boxTop = intro.Bottom + 10;

            TextBox box = new TextBox();
            box.Multiline = true;
            box.ScrollBars = ScrollBars.Vertical;
            box.ReadOnly = true;
            box.SetBounds(20, boxTop, 620, 340);
            box.Font = new Font("Consolas", 9F);
            box.Text = BuildPrompt();
            box.Select(0, 0);
            Controls.Add(box);

            int btnTop = box.Bottom + 16;

            Button btnPath = new Button();
            btnPath.Text = S.AiCopyPath;
            btnPath.SetBounds(20, btnTop, 130, 32);
            btnPath.Click += delegate { CopyTo(Program.ScriptPathPublic, btnPath); };
            Controls.Add(btnPath);

            Button btnCmds = new Button();
            btnCmds.Text = S.AiCopyCmds;
            btnCmds.SetBounds(160, btnTop, 140, 32);
            btnCmds.Click += delegate { CopyTo(BuildCommands(), btnCmds); };
            Controls.Add(btnCmds);

            Button btnPrompt = new Button();
            btnPrompt.Text = S.AiCopyPrompt;
            btnPrompt.SetBounds(310, btnTop, 170, 32);
            btnPrompt.Click += delegate { CopyTo(BuildPrompt(), btnPrompt); };
            Controls.Add(btnPrompt);

            Button btnClose = new Button();
            btnClose.Text = S.BtnOK;
            btnClose.SetBounds(554, btnTop, 86, 32);
            btnClose.DialogResult = DialogResult.OK;
            Controls.Add(btnClose);

            AcceptButton = btnClose;
            CancelButton = btnClose;

            ClientSize = new Size(660, btnTop + 32 + 20);
        }

        private static string BuildCommands()
        {
            // 文本在 Strings.AiCommands 里，中英各一份；这里只负责取当前语言
            return S.AiCommands(Program.ScriptPathPublic);
        }

        private static string BuildPrompt()
        {
            // 正文在 Strings.AiPromptBody 里，中英各一份
            return S.AiPromptBody(Program.ScriptPathPublic);
        }

        private static void CopyTo(string text, Button b)
        {
            try
            {
                Clipboard.SetText(text);
                string old = b.Text;
                b.Text = S.AiCopied;
                System.Windows.Forms.Timer tm = new System.Windows.Forms.Timer();
                tm.Interval = 1200;
                tm.Tick += delegate { b.Text = old; tm.Stop(); tm.Dispose(); };
                tm.Start();
            }
            catch { }
        }
    }
    /// <summary>清理前弹窗：显示数量与体积，选择删除方式。</summary>
}
