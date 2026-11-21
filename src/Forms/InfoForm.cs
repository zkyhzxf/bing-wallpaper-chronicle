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
    internal sealed class InfoForm : Form
    {
        private const string MoreUrl = "https://bing.wdbyte.com/zh-cn/";
        private const int ThumbW = 240;
        private const int ThumbH = 135;

        private readonly Bitmap thumb;

        /// <summary>读入内存再解码，避免 Image.FromFile 锁定壁纸文件。</summary>
        private static Bitmap MakeThumb(string file, int w, int h)
        {
            try
            {
                if (string.IsNullOrEmpty(file) || !File.Exists(file)) { return null; }
                byte[] bytes = File.ReadAllBytes(file);
                using (MemoryStream ms = new MemoryStream(bytes))
                using (Image src = Image.FromStream(ms))
                {
                    Bitmap bmp = new Bitmap(w, h);
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                        g.DrawImage(src, 0, 0, w, h);
                    }
                    return bmp;
                }
            }
            catch { return null; }
        }

        public InfoForm(string date, string title, string copyright, string file, string sizeText, string searchUrl)
        {
            Text = Program.AppName + S.InfoTitle;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            ClientSize = new Size(620, 350);
            try { Font = new Font("Microsoft YaHei UI", 9F); } catch { }

            // 左侧缩略图
            thumb = MakeThumb(file, ThumbW, ThumbH);
            int textLeft = 20;
            int textWidth = 580;
            if (thumb != null)
            {
                PictureBox pic = new PictureBox();
                pic.Image = thumb;
                pic.SizeMode = PictureBoxSizeMode.Zoom;
                pic.SetBounds(20, 20, ThumbW, ThumbH);
                pic.BorderStyle = BorderStyle.FixedSingle;
                Controls.Add(pic);
                textLeft = 20 + ThumbW + 18;
                textWidth = 580 - ThumbW - 18;
            }

            int y = thumb != null ? 20 : 16;

            Label lbl = new Label();
            lbl.AutoSize = false;
            lbl.SetBounds(textLeft, y, textWidth, thumb != null ? ThumbH : 220);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine(S.InfoDate + date);
            sb.AppendLine();
            if (title.Length > 0) { sb.AppendLine(S.InfoTitleLine + title); sb.AppendLine(); }
            if (copyright.Length > 0) { sb.AppendLine(S.InfoCopy + copyright); sb.AppendLine(); }
            if (file.Length > 0) { sb.AppendLine(S.InfoFile + file); }
            if (sizeText.Length > 0) { sb.AppendLine(S.InfoSize + sizeText); }

            lbl.Text = sb.ToString().Trim();
            Controls.Add(lbl);

            int by = 20 + ThumbH + 26;
            if (thumb == null) { by = 268; }

            Button btnSearch = new Button();
            btnSearch.Text = S.InfoBtnSearch;
            btnSearch.SetBounds(20, by, 130, 32);
            btnSearch.Enabled = searchUrl.Length > 0;
            btnSearch.Click += delegate { Program.OpenUrl(searchUrl); };
            Controls.Add(btnSearch);

            Button btnMore = new Button();
            btnMore.Text = S.InfoBtnMore;
            btnMore.SetBounds(162, by, 130, 32);
            btnMore.Click += delegate { Program.OpenUrl(MoreUrl); };
            Controls.Add(btnMore);

            Button btnClose = new Button();
            btnClose.Text = S.BtnOK;
            btnClose.SetBounds(514, by, 86, 32);
            btnClose.DialogResult = DialogResult.OK;
            Controls.Add(btnClose);

            AcceptButton = btnClose;
            CancelButton = btnClose;

            // 窗口高度跟着内容走
            ClientSize = new Size(620, by + 32 + 20);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && thumb != null) { thumb.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
