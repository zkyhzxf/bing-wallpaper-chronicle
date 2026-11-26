using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;

namespace BingWallpaperLite
{
    /// <summary>
    /// 羽毛的矢量画法。彩蛋里飘的羽毛和 logo 换成的那张图共用这一份，
    /// 免得同一个图案在两处长得不一样。
    /// 不用 emoji：🪶 依赖 Segoe UI Emoji，字体缺失的系统上会变成豆腐块。
    /// </summary>
    internal static class FeatherArt
    {
        /// <summary>一根竖直朝上的羽毛轮廓，中心 (cx, cy)，总长 len。</summary>
        private static GraphicsPath BuildPath(float cx, float cy, float len)
        {
            GraphicsPath p = new GraphicsPath();
            float w = len * 0.32f;
            float top = cy - len / 2f;
            float bottom = cy + len / 2f;
            // 两段对称贝塞尔收成水滴形：上端尖、下端略圆
            p.AddBezier(cx, top,
                        cx + w * 1.10f, top + len * 0.20f,
                        cx + w * 0.92f, top + len * 0.74f,
                        cx, bottom);
            p.AddBezier(cx, bottom,
                        cx - w * 0.92f, top + len * 0.74f,
                        cx - w * 1.10f, top + len * 0.20f,
                        cx, top);
            p.CloseFigure();
            return p;
        }

        /// <summary>
        /// 渲染成一张正方形位图，背景透明。
        /// size 建议 96：48 在 150% 缩放的屏上会糊，256 纯属占内存。
        /// </summary>
        public static Bitmap Render(int size)
        {
            // 有素材就用素材：assets\feather.png 由 build.ps1 嵌成资源 feather.png。
            // 换素材只换这张 png，不用碰代码。
            // 素材自带的 alpha 是准的，原样返回。
            // （早先版本会做 MakeHardAlpha 硬阈值化，那是在用 TransparencyKey 抠色时
            //   为了消掉品红边才需要的；现在走分层窗口的逐像素 alpha，柔和边缘原样保留。）
            Bitmap asset = TryLoadAsset(size);
            if (asset != null) { return asset; }

            Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);

                float len = size * 0.88f;
                float cx = size / 2f;
                float cy = size / 2f;

                // 羽身：暖白，半透明
                using (GraphicsPath path = BuildPath(cx, cy, len))
                using (SolidBrush body = new SolidBrush(Color.FromArgb(228, 250, 247, 240)))
                {
                    g.FillPath(body, path);
                }

                // 羽枝：中间长、两头短，向上斜
                float rachis = len * 0.90f;
                float y0 = cy - rachis / 2f;
                int barbs = 13;
                float penW = Math.Max(1f, size / 64f);
                using (Pen pen = new Pen(Color.FromArgb(120, 205, 196, 180), penW))
                {
                    for (int i = 1; i <= barbs; i++)
                    {
                        float t = (float)i / (barbs + 1);
                        float y = y0 + rachis * t;
                        float spread = (float)Math.Sin(t * Math.PI) * (size * 0.24f);
                        if (spread < 1.2f) { continue; }
                        g.DrawLine(pen, cx, y, cx + spread, y - spread * 0.55f);
                        g.DrawLine(pen, cx, y, cx - spread, y - spread * 0.55f);
                    }
                }

                // 羽轴：从羽尖到羽根
                using (Pen axis = new Pen(Color.FromArgb(200, 176, 164, 146), Math.Max(1.2f, size / 46f)))
                {
                    g.DrawLine(axis, cx, cy - len * 0.47f, cx, cy + len * 0.47f);
                }
            }

            // 关键一步：把半透明像素二值化。
            //
            // 承载它的窗体用 TransparencyKey 抠背景色，而抠色只认「完全等于该颜色」的像素。
            // 抗锯齿边缘是「羽毛色 × alpha + 品红 × (1-alpha)」，既不是羽毛色也不是品红，
            // 于是被保留下来 —— 在屏幕上就是一圈粉边，羽毛会变成粉色小椭圆。
            // 阈值化之后每个像素要么完全可见、要么完全透明，粉边消失，
            // 代价是轮廓硬了一个像素，在飘动的小尺寸下看不出来。
            MakeHardAlpha(bmp);
            return bmp;
        }

        /// <summary>读嵌入的素材图并按目标尺寸重采样。没有素材返回 null，调用方退回矢量绘制。</summary>
        private static Bitmap TryLoadAsset(int size)
        {
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                using (Stream s = asm.GetManifestResourceStream("feather.png"))
                {
                    if (s == null) { return null; }
                    using (Image raw = Image.FromStream(s))
                    {
                        Bitmap dst = new Bitmap(size, size, PixelFormat.Format32bppArgb);
                        using (Graphics g = Graphics.FromImage(dst))
                        {
                            g.SmoothingMode = SmoothingMode.AntiAlias;
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            g.Clear(Color.Transparent);
                            g.DrawImage(raw, 0, 0, size, size);
                        }
                        return dst;
                    }
                }
            }
            catch { return null; }
        }

        /// <summary>alpha ≥ 128 的像素置为不透明，其余清零。颜色不动。</summary>
        private static void MakeHardAlpha(Bitmap bmp)
        {
            Rectangle rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            BitmapData data = bmp.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try
            {
                int stride = data.Stride;
                int bytes = stride * bmp.Height;
                byte[] buf = new byte[bytes];
                System.Runtime.InteropServices.Marshal.Copy(data.Scan0, buf, 0, bytes);
                for (int y = 0; y < bmp.Height; y++)
                {
                    int row = y * stride;
                    for (int x = 0; x < bmp.Width; x++)
                    {
                        int i = row + x * 4;
                        if (buf[i + 3] >= 128) { buf[i + 3] = 255; }
                        else { buf[i] = 0; buf[i + 1] = 0; buf[i + 2] = 0; buf[i + 3] = 0; }
                    }
                }
                System.Runtime.InteropServices.Marshal.Copy(buf, 0, data.Scan0, bytes);
            }
            finally { bmp.UnlockBits(data); }
        }
    }
}
