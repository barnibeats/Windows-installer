// Visual style: deep dark background, translucent cards, pill controls,
// violet -> cyan gradient accents. A light theme is included.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

static class Theme
{
    public static bool Dark = true;
    public static event Action Changed;

    public static Color Bg, Bg2, Card, CardHover, Border, Text, Muted, Field;
    public static Color A1 = ColorTranslator.FromHtml("#7c5cff");
    public static Color A2 = ColorTranslator.FromHtml("#00d4ff");
    public static Color A3 = ColorTranslator.FromHtml("#ff4fd8");
    public static Color Ok, Warn, Bad;

    public static Font Body, Small, Bold, Title, Big, Mono;

    static Theme()
    {
        Body = new Font("Segoe UI", 9.5f);
        Small = new Font("Segoe UI", 8.5f);
        Bold = new Font("Segoe UI Semibold", 9.5f);
        Title = new Font("Segoe UI Semibold", 11f);
        Big = new Font("Segoe UI Semibold", 15f);
        Mono = new Font("Consolas", 9f);
        Apply(true);
    }

    static Color H(string s) { return ColorTranslator.FromHtml(s); }

    static void Apply(bool dark)
    {
        Dark = dark;
        if (dark)
        {
            Bg = H("#07080f"); Bg2 = H("#0e1020"); Card = H("#161829"); CardHover = H("#212332"); Border = H("#2a2d42");
            Text = H("#e8eaf6"); Muted = H("#9097b8"); Field = H("#0f1120");
            Ok = H("#3ddc97"); Warn = H("#ffb84f"); Bad = H("#ff5c7a");
        }
        else
        {
            Bg = H("#f4f6ff"); Bg2 = H("#e9ecff"); Card = H("#fbfcff"); CardHover = H("#ffffff"); Border = H("#d3d8ee");
            Text = H("#141833"); Muted = H("#5a6185"); Field = H("#ffffff");
            Ok = H("#168a5b"); Warn = H("#b86e00"); Bad = H("#d6264a");
        }
    }

    public static void Set(bool dark)
    {
        if (dark == Dark) return;
        Apply(dark);
        Action a = Changed;
        if (a != null) a();
    }

    // ---- drawing helpers ----------------------------------------------------------------------
    public static Color Mix(Color a, Color b, float t)
    {
        return Color.FromArgb(
            (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
    }

    public static Color Alpha(Color c, int alpha) { return Color.FromArgb(alpha, c.R, c.G, c.B); }

    public static GraphicsPath Round(Rectangle r, int radius)
    {
        int d = radius * 2;
        if (d > r.Height) d = r.Height;
        if (d > r.Width) d = r.Width;
        GraphicsPath p = new GraphicsPath();
        if (d <= 0) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static LinearGradientBrush Grad(Rectangle r, Color a, Color b, float angle)
    {
        if (r.Width < 1) r.Width = 1;
        if (r.Height < 1) r.Height = 1;
        return new LinearGradientBrush(r, a, b, angle);
    }

    public static void Quality(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
    }

    // soft colored glow in the page background
    public static void Glow(Graphics g, int cx, int cy, int radius, Color c, int alpha)
    {
        using (GraphicsPath p = new GraphicsPath())
        {
            p.AddEllipse(cx - radius, cy - radius, radius * 2, radius * 2);
            using (PathGradientBrush b = new PathGradientBrush(p))
            {
                b.CenterColor = Color.FromArgb(alpha, c);
                b.SurroundColors = new Color[] { Color.FromArgb(0, c) };
                g.FillPath(b, p);
            }
        }
    }

    public static void PaintBackdrop(Graphics g, Rectangle r)
    {
        using (SolidBrush b = new SolidBrush(Bg)) g.FillRectangle(b, r);
        GraphicsState st = g.Save();
        g.SmoothingMode = SmoothingMode.AntiAlias;
        int k = Dark ? 52 : 38;
        Glow(g, r.Left + 60, r.Top + 20, 380, A1, k);
        Glow(g, r.Right - 40, r.Bottom - 40, 420, A2, k - 14);
        Glow(g, r.Right - 140, r.Top + 120, 260, A3, k - 24);
        g.Restore(st);
    }
}
