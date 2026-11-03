using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace LanAudio.Receiver
{
    /// <summary>
    /// アプリ／トレイアイコンの描画。
    /// tools/make-icon.ps1（Windows PowerShell の Add-Type）からも使うので古い C# 構文で書くこと。
    /// </summary>
    public static class IconArt
    {
        public static readonly Color Accent = Color.FromArgb(30, 120, 200);
        public static readonly Color Disabled = Color.FromArgb(140, 140, 140);
        static readonly Color BadgeColor = Color.FromArgb(46, 189, 89);

        /// <summary>スピーカー＋音波の絵。badge=true で右下に緑の丸（動作中）を付ける</summary>
        public static Bitmap Render(int size, Color background, bool badge)
        {
            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                float s = size;

                using (var path = RoundedRect(0, 0, s, s, s * 0.22f))
                using (var bg = new SolidBrush(background))
                    g.FillPath(bg, path);

                PointF[] speaker =
                {
                    P(s, 0.16f, 0.38f), P(s, 0.30f, 0.38f), P(s, 0.48f, 0.22f),
                    P(s, 0.48f, 0.78f), P(s, 0.30f, 0.62f), P(s, 0.16f, 0.62f),
                };
                g.FillPolygon(Brushes.White, speaker);

                using (var pen = new Pen(Color.White, Math.Max(1f, s * 0.075f)))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    DrawWave(g, pen, s, 0.15f);
                    DrawWave(g, pen, s, 0.28f);
                }

                if (badge)
                {
                    float d = s * 0.46f;
                    float border = Math.Max(1f, s * 0.07f);
                    g.FillEllipse(Brushes.White, s - d, s - d, d, d);
                    using (var b = new SolidBrush(BadgeColor))
                        g.FillEllipse(b, s - d + border, s - d + border, d - border * 2, d - border * 2);
                }
            }
            return bmp;
        }

        static void DrawWave(Graphics g, Pen pen, float s, float radius)
        {
            float cx = s * 0.48f, cy = s * 0.5f, r = s * radius;
            g.DrawArc(pen, cx - r, cy - r, r * 2, r * 2, -45, 90);
        }

        static PointF P(float s, float x, float y)
        {
            return new PointF(s * x, s * y);
        }

        static GraphicsPath RoundedRect(float x, float y, float w, float h, float r)
        {
            var path = new GraphicsPath();
            float d = r * 2;
            path.AddArc(x, y, d, d, 180, 90);
            path.AddArc(x + w - d, y, d, d, 270, 90);
            path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
            path.AddArc(x, y + h - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
