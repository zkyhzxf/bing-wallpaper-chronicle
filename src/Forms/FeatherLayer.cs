using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace BingWallpaperLite
{
    /// <summary>
    /// 彩蛋：桌面壁纸层飘羽毛。
    ///
    /// 用 UpdateLayeredWindow 做逐像素 alpha，而不是 TransparencyKey 抠色。
    /// 抠色的代价是：抗锯齿边缘是「羽毛色 × alpha + 抠色 × (1-alpha)」的混合值，
    /// 既不是羽毛色也不是抠色，于是被保留下来 —— 屏幕上就是一圈品红边，
    /// 半透明的羽枝更是直接被吃掉，羽毛会变成几根紫色细线。
    ///
    /// 窗口样式：
    ///   · WS_EX_LAYERED    分层窗口，逐像素 alpha 的前提
    ///   · WS_EX_TRANSPARENT 鼠标穿透，点桌面图标不受影响
    ///   · WS_EX_TOOLWINDOW 不进 Alt+Tab
    ///   · 不设 TopMost，压到 z 序底部 —— 任何窗口打开都会盖住它，
    ///     所以它真的只是「桌面上的动画」，不会跑到别人的窗口前面
    /// </summary>
    internal sealed class FeatherLayer : Form
    {
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;

        private const int HWND_BOTTOM = 1;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;

        private const int ULW_ALPHA = 2;
        private const byte AC_SRC_OVER = 0;
        private const byte AC_SRC_ALPHA = 1;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE { public int cx; public int cy; }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("user32.dll")] private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst,
            ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pprSrc, int crKey,
            ref BLENDFUNCTION pblend, int dwFlags);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr h);

        /// <summary>一根羽毛的瞬时状态。全部用屏幕绝对坐标，跨显示器不会错位。</summary>
        private sealed class Puff
        {
            public float X, Y;
            public float Vx, Vy;
            public float Rot, RotSpeed;
            public float Phase, PhaseSpeed;
            public float Scale;
        }

        private readonly Puff[] puffs;
        private readonly Bitmap art;
        private readonly Timer tick;
        private readonly Random rnd = new Random();
        private readonly Rectangle screen;
        private Bitmap buffer;

        public FeatherLayer()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            Text = "";

            screen = SystemInformation.VirtualScreen;
            Bounds = screen;

            art = FeatherArt.Render(256);

            puffs = new Puff[16];
            for (int i = 0; i < puffs.Length; i++)
            {
                puffs[i] = new Puff();
                Reset(puffs[i], true);
            }

            tick = new Timer();
            tick.Interval = 40;   // 25 fps：飘落用不着 60，省一半 CPU
            tick.Tick += delegate { Step(); DrawFrame(); };
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_LAYERED;       // 逐像素 alpha
                cp.ExStyle |= WS_EX_TRANSPARENT;   // 鼠标穿透
                cp.ExStyle |= WS_EX_TOOLWINDOW;    // 不进 Alt+Tab
                cp.ExStyle |= WS_EX_NOACTIVATE;    // 不抢焦点
                return cp;
            }
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            try
            {
                SetWindowPos(Handle, (IntPtr)HWND_BOTTOM, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            }
            catch { }

            try
            {
                buffer = new Bitmap(screen.Width, screen.Height, PixelFormat.Format32bppArgb);
                DrawFrame();
            }
            catch { }

            tick.Start();
        }

        /// <summary>散开（首次）或从屏幕上沿重新飘下来（重生）。</summary>
        private void Reset(Puff p, bool scatter)
        {
            p.X = rnd.Next(screen.Left, screen.Right);
            p.Y = scatter
                ? rnd.Next(screen.Top, screen.Bottom)
                : screen.Top - rnd.Next(40, 160);
            p.Vx = (float)(rnd.NextDouble() * 20 - 10);
            p.Vy = (float)(22 + rnd.NextDouble() * 26);
            p.Rot = rnd.Next(0, 360);
            p.RotSpeed = (float)(rnd.NextDouble() * 26 - 13);
            p.Phase = (float)(rnd.NextDouble() * Math.PI * 2);
            p.PhaseSpeed = (float)(0.6 + rnd.NextDouble() * 1.2);
            p.Scale = (float)(0.34 + rnd.NextDouble() * 0.30);
        }

        private void Step()
        {
            for (int i = 0; i < puffs.Length; i++)
            {
                Puff p = puffs[i];
                p.Phase += p.PhaseSpeed * 0.04f;
                // 横摆叠在下落上：纯直线上下会像雨点，不像羽毛
                p.X += (p.Vx + (float)Math.Sin(p.Phase) * 26f) * 0.04f;
                p.Y += p.Vy * 0.04f;
                p.Rot += p.RotSpeed * 0.04f;

                if (p.Y > screen.Bottom + 120) { Reset(p, false); }
                if (p.X < screen.Left - 120) { p.X = screen.Right + 100; }
                else if (p.X > screen.Right + 120) { p.X = screen.Left - 100; }
            }
        }

        /// <summary>整帧重绘到 ARGB 缓冲，再一次性推给分层窗口。</summary>
        private void DrawFrame()
        {
            if (buffer == null || IsDisposed) { return; }
            try
            {
                using (Graphics g = Graphics.FromImage(buffer))
                {
                    g.CompositingMode = CompositingMode.SourceCopy;
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.Clear(Color.Transparent);

                    for (int i = 0; i < puffs.Length; i++)
                    {
                        Puff p = puffs[i];
                        GraphicsState st = g.Save();
                        try
                        {
                            g.TranslateTransform(p.X - screen.Left, p.Y - screen.Top);
                            g.RotateTransform(p.Rot);
                            g.ScaleTransform(p.Scale, p.Scale);
                            g.DrawImage(art, -art.Width / 2f, -art.Height / 2f, art.Width, art.Height);
                        }
                        catch { }
                        finally { g.Restore(st); }
                    }
                }
                PushLayered();
            }
            catch { }
        }

        private void PushLayered()
        {
            IntPtr screenDc = IntPtr.Zero, memDc = IntPtr.Zero, hBmp = IntPtr.Zero, oldBmp = IntPtr.Zero;
            try
            {
                screenDc = GetDC(IntPtr.Zero);
                memDc = CreateCompatibleDC(screenDc);
                // FromArgb(0) 让 GDI 按预乘 alpha 处理，AC_SRC_ALPHA 才认
                hBmp = buffer.GetHbitmap(Color.FromArgb(0));
                oldBmp = SelectObject(memDc, hBmp);

                SIZE size = new SIZE();
                size.cx = buffer.Width;
                size.cy = buffer.Height;
                POINT src = new POINT();
                src.X = 0; src.Y = 0;
                POINT dst = new POINT();
                dst.X = screen.Left; dst.Y = screen.Top;

                BLENDFUNCTION blend = new BLENDFUNCTION();
                blend.BlendOp = AC_SRC_OVER;
                blend.BlendFlags = 0;
                blend.SourceConstantAlpha = 255;
                blend.AlphaFormat = AC_SRC_ALPHA;

                UpdateLayeredWindow(Handle, screenDc, ref dst, ref size, memDc, ref src, 0, ref blend, ULW_ALPHA);
            }
            catch { }
            finally
            {
                if (hBmp != IntPtr.Zero)
                {
                    try { SelectObject(memDc, oldBmp); } catch { }
                    try { DeleteObject(hBmp); } catch { }
                }
                if (memDc != IntPtr.Zero) { try { DeleteDC(memDc); } catch { } }
                if (screenDc != IntPtr.Zero) { try { ReleaseDC(IntPtr.Zero, screenDc); } catch { } }
            }
        }

        /// <summary>停动画。窗体会被调用方 Dispose。</summary>
        public void Stop()
        {
            try { tick.Stop(); } catch { }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { if (tick != null) { tick.Stop(); tick.Dispose(); } } catch { }
                try { if (art != null) { art.Dispose(); } } catch { }
                try { if (buffer != null) { buffer.Dispose(); } } catch { }
            }
            base.Dispose(disposing);
        }
    }
}
