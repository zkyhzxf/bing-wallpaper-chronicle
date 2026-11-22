using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace BingWallpaperLite
{
    /// <summary>照片墙用的深/浅色配色，跟随系统「应用主题」。</summary>
    internal static class WallTheme
    {
        public static bool Dark = false;
        public static Color Bg, Card, CardHover, Border, Text, TextDim, Accent, ImageBg;

        static WallTheme()
        {
            Apply();
        }

        public static void Apply()
        {
            try
            {
                object v = Microsoft.Win32.Registry.GetValue(
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                    "AppsUseLightTheme", 1);
                Dark = !(v != null && Convert.ToInt32(v) == 1);
            }
            catch { Dark = false; }

            if (Dark)
            {
                Bg = Color.FromArgb(27, 27, 27);
                Card = Color.FromArgb(35, 35, 35);
                CardHover = Color.FromArgb(45, 45, 45);
                Border = Color.FromArgb(58, 58, 58);
                Text = Color.FromArgb(232, 232, 232);
                TextDim = Color.FromArgb(150, 150, 150);
                Accent = Color.FromArgb(76, 154, 255);
                ImageBg = Color.FromArgb(10, 10, 10);
            }
            else
            {
                Bg = Color.FromArgb(243, 243, 243);
                Card = Color.FromArgb(255, 255, 255);
                CardHover = Color.FromArgb(250, 250, 250);
                Border = Color.FromArgb(224, 224, 224);
                Text = Color.FromArgb(32, 32, 32);
                TextDim = Color.FromArgb(104, 104, 104);
                Accent = Color.FromArgb(42, 111, 219);
                ImageBg = Color.FromArgb(232, 232, 232);
            }
        }
    }

    /// <summary>照片墙的一张卡片：圆角、悬停、选中、缺图虚线格、加载转圈，全部自绘。</summary>
    internal sealed class WallCard : Control
    {
        public const int CardWidth = 340;
        public const int CardHeight = 246;
        public const int ImageW = 320;
        public const int ImageH = 180;

        public string Date8 = "";
        public string TitleText = "";
        public bool HasImage = false;
        public bool Loading = false;
        public bool Selected = false;
        public Image Thumb = null;

        private bool hover = false;
        private int spin = 0;
        private static readonly Font FontDate;
        private static readonly Font FontTitle;
        private static readonly Font FontYear;

        static WallCard()
        {
            try
            {
                FontDate = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
                FontTitle = new Font("Microsoft YaHei UI", 9F);
                FontYear = new Font("Microsoft YaHei UI", 24F, FontStyle.Bold);
            }
            catch { }
        }

        public WallCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                     | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                     | ControlStyles.StandardClick | ControlStyles.StandardDoubleClick, true);
            Size = new Size(CardWidth, CardHeight);
            Cursor = Cursors.Hand;
        }

        public void TickSpin()
        {
            if (!Loading) { return; }
            spin = (spin + 28) % 360;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        private static GraphicsPath Round(Rectangle r, int radius)
        {
            GraphicsPath p = new GraphicsPath();
            int d = radius * 2;
            if (d <= 0) { p.AddRectangle(r); return p; }
            try
            {
                p.AddArc(r.X, r.Y, d, d, 180, 90);
                p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                p.CloseFigure();
            }
            catch { p.AddRectangle(r); }
            return p;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            Rectangle img = new Rectangle(10, 10, ImageW, ImageH);

            using (GraphicsPath card = Round(bounds, 10))
            {
                using (SolidBrush b = new SolidBrush(hover ? WallTheme.CardHover : WallTheme.Card)) { g.FillPath(b, card); }

                using (GraphicsPath ip = Round(img, 8))
                {
                    using (SolidBrush ib = new SolidBrush(WallTheme.ImageBg)) { g.FillPath(ib, ip); }

                    if (Thumb != null)
                    {
                        GraphicsState st = g.Save();
                        g.SetClip(ip);
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        try { g.DrawImage(Thumb, img); } catch { }
                        g.Restore(st);
                    }
                    else if (Loading)
                    {
                        using (Pen sp = new Pen(WallTheme.Accent, 3f))
                        {
                            sp.StartCap = LineCap.Round;
                            sp.EndCap = LineCap.Round;
                            g.DrawArc(sp, img.X + img.Width / 2 - 16, img.Y + img.Height / 2 - 16, 32, 32, spin, 260);
                        }
                    }
                    else
                    {
                        Rectangle dash = new Rectangle(img.X + 40, img.Y + 34, img.Width - 80, img.Height - 68);
                        using (Pen dp = new Pen(WallTheme.TextDim, 1.4f))
                        {
                            dp.DashStyle = DashStyle.Dash;
                            using (GraphicsPath dpp = Round(dash, 8)) { g.DrawPath(dp, dpp); }
                        }
                        using (SolidBrush tb = new SolidBrush(WallTheme.TextDim))
                        using (StringFormat sf = new StringFormat())
                        {
                            sf.Alignment = StringAlignment.Center;
                            sf.LineAlignment = StringAlignment.Center;
                            g.DrawString("＋  " + S.WallCardFetch, FontDate, tb, dash, sf);
                        }
                    }
                }

                // 年份水印
                if (Date8.Length == 8)
                {
                    string yr = Date8.Substring(0, 4);
                    int alpha = WallTheme.Dark ? 22 : 28;
                    using (SolidBrush yb = new SolidBrush(Color.FromArgb(alpha, WallTheme.Text)))
                    {
                        SizeF ys = g.MeasureString(yr, FontYear);
                        g.DrawString(yr, FontYear, yb, img.Right - ys.Width - 6, img.Bottom - ys.Height + 6);
                    }
                }

                // 日期 + 标题
                using (SolidBrush ab = new SolidBrush(Selected ? WallTheme.Accent : WallTheme.TextDim))
                {
                    string md = Date8.Length == 8 ? (Date8.Substring(4, 2) + "-" + Date8.Substring(6, 2)) : "——";
                    g.DrawString(md, FontDate, ab, 14, ImageH + 18);
                }
                string title = TitleText;
                if (title.Length == 0) { title = S.NoTitle; }
                using (SolidBrush tbr = new SolidBrush(WallTheme.Text))
                using (StringFormat sf = new StringFormat())
                {
                    sf.Trimming = StringTrimming.EllipsisCharacter;
                    sf.FormatFlags = StringFormatFlags.NoWrap;
                    Rectangle tr = new Rectangle(64, ImageH + 17, ImageW - 58, 20);
                    g.DrawString(title, FontTitle, tbr, tr, sf);
                }

                // 描边
                Color bc = WallTheme.Border;
                float bw = 1f;
                if (Selected) { bc = WallTheme.Accent; bw = 2f; }
                else if (hover) { bc = WallTheme.Accent; bw = 1.4f; }
                using (Pen bp = new Pen(bc, bw)) { g.DrawPath(bp, card); }
            }
        }
    }
}
