using System;
using System.Drawing;
using System.Windows.Forms;

namespace BingWallpaperLite
{
    /// <summary>帮助窗口：只读富文本，章节标题自动加粗着色。</summary>
    internal sealed class HelpForm : Form
    {
        public HelpForm()
        {
            Text = Program.AppName + S.HelpTitle;
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = true;
            MinimizeBox = false;
            ShowInTaskbar = true;
            ClientSize = new Size(720, 560);
            MinimumSize = new Size(520, 400);
            try { Font = new Font("Microsoft YaHei UI", 9F); } catch { }

            RichTextBox rtb = new RichTextBox();
            rtb.ReadOnly = true;
            rtb.BorderStyle = BorderStyle.None;
            rtb.BackColor = Color.White;
            rtb.ScrollBars = RichTextBoxScrollBars.Vertical;
            rtb.SetBounds(18, 16, 684, 480);
            rtb.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            try { rtb.Font = new Font("Microsoft YaHei UI", 9.5F); } catch { }
            rtb.Text = S.HelpBody;
            Controls.Add(rtb);

            HighlightHeadings(rtb);

            Button btnClose = new Button();
            btnClose.Text = S.BtnOK;
            btnClose.SetBounds(614, 512, 86, 32);
            btnClose.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnClose.DialogResult = DialogResult.OK;
            Controls.Add(btnClose);

            AcceptButton = btnClose;
            CancelButton = btnClose;
        }

        /// <summary>把形如「一、」「1.」开头的行加粗着色，其余保持正文样式。</summary>
        private static void HighlightHeadings(RichTextBox rtb)
        {
            try
            {
                Font headFont = new Font(rtb.Font, FontStyle.Bold);
                string text = rtb.Text;
                int pos = 0;
                string[] lines = text.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    string raw = lines[i];
                    string line = raw.TrimEnd('\r');
                    if (line.Length > 1 && IsHeading(line))
                    {
                        rtb.Select(pos, line.Length);
                        rtb.SelectionFont = headFont;
                        rtb.SelectionColor = Color.FromArgb(0, 70, 140);
                    }
                    pos += raw.Length + 1;
                }
                rtb.Select(0, 0);
            }
            catch { }
        }

        private static bool IsHeading(string line)
        {
            if (line.Length < 2) { return false; }
            char c = line[0];
            if ("一二三四五六七八九十".IndexOf(c) >= 0)
            {
                int dun = line.IndexOf('、');
                if (dun > 0 && dun < 4) { return true; }
            }
            if (char.IsDigit(c))
            {
                int dot = line.IndexOf('.');
                if (dot > 0 && dot <= 2) { return true; }
            }
            return false;
        }
    }
}
