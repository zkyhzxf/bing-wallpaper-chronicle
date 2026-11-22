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

            Button btnLearn = new Button();
            btnLearn.Text = S.InfoBtnSearch;
            btnLearn.SetBounds(20, by, 110, 32);
            btnLearn.Enabled = searchUrl.Length > 0;
            btnLearn.Click += delegate { Program.OpenUrl(searchUrl); };
            Controls.Add(btnLearn);

            Button btnFolder = new Button();
            btnFolder.Text = S.InfoBtnFolder;
            btnFolder.SetBounds(140, by, 130, 32);
            btnFolder.Enabled = !string.IsNullOrEmpty(file);
            btnFolder.Click += delegate { Program.ShowInFolder(file); };
            Controls.Add(btnFolder);

            Button btnMore = new Button();
            btnMore.Text = S.InfoBtnMore;
            btnMore.SetBounds(280, by, 130, 32);
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

            // ---------------------------------------------------------------- 每日故事
            // 正文按需抓取（本地缓存优先），拿到才显示，拿不到整块隐藏
            int sy = by + 32 + 14;
            Label lblStoryHd = new Label();
            lblStoryHd.Text = S.StoryTitle;
            lblStoryHd.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            lblStoryHd.SetBounds(20, sy, 580, 20);
            lblStoryHd.Visible = false;
            Controls.Add(lblStoryHd);

            Label lblStory = new Label();
            lblStory.AutoSize = false;
            lblStory.SetBounds(20, sy + 22, 580, 56);
            lblStory.Visible = false;
            Controls.Add(lblStory);

            Button btnExpand = new Button();
            btnExpand.Text = S.StoryExpand;
            btnExpand.SetBounds(20, sy + 82, 92, 26);
            btnExpand.Visible = false;
            Controls.Add(btnExpand);

            string storyDate = (date ?? "").Replace("-", "").Replace("/", "").Trim();
            if (storyDate.Length == 8 && Program.ShowStory)
            {
                System.Threading.ThreadPool.QueueUserWorkItem(delegate
                {
                    string story = "";
                    try { story = Program.GetStory(storyDate); } catch { }
                    if (string.IsNullOrEmpty(story)) { return; }
                    try
                    {
                        BeginInvoke((MethodInvoker)delegate
                        {
                            bool longText = story.Length > 150;
                            string brief = longText ? story.Substring(0, 150) + "…" : story;
                            lblStoryHd.Visible = true;
                            lblStory.Text = brief;
                            lblStory.Visible = true;
                            ClientSize = new Size(620, sy + 22 + (longText ? 92 : 62) + 16);
                            if (longText)
                            {
                                btnExpand.Visible = true;
                                btnExpand.Click += delegate
                                {
                                    lblStory.Text = story;
                                    btnExpand.Visible = false;
                                    lblStory.SetBounds(20, sy + 22, 580, 150);
                                    ClientSize = new Size(620, sy + 22 + 150 + 16);
                                };
                            }
                        });
                    }
                    catch { }
                });
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && thumb != null) { thumb.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
