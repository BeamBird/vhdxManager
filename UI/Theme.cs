using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace VhdxManager.UI;

/// <summary>统一的配色、字体与绘制辅助。</summary>
internal static class Theme
{
    public static readonly Color Bg = ColorTranslator.FromHtml("#F2F4F9");
    public static readonly Color Surface = Color.White;
    public static readonly Color SurfaceAlt = ColorTranslator.FromHtml("#F7F9FC");
    public static readonly Color Border = ColorTranslator.FromHtml("#E6EAF2");
    public static readonly Color BorderStrong = ColorTranslator.FromHtml("#D5DCE9");
    public static readonly Color TextPrimary = ColorTranslator.FromHtml("#1B2333");
    public static readonly Color TextSecondary = ColorTranslator.FromHtml("#6E7A92");
    public static readonly Color TextMuted = ColorTranslator.FromHtml("#9AA5BA");
    public static readonly Color Primary = ColorTranslator.FromHtml("#3B6FF5");
    public static readonly Color PrimaryHover = ColorTranslator.FromHtml("#2B5DE0");
    public static readonly Color PrimarySoft = ColorTranslator.FromHtml("#EDF2FF");
    public static readonly Color Success = ColorTranslator.FromHtml("#12A46B");
    public static readonly Color SuccessSoft = ColorTranslator.FromHtml("#E7F7F0");
    public static readonly Color Warning = ColorTranslator.FromHtml("#E08A00");
    public static readonly Color WarningSoft = ColorTranslator.FromHtml("#FDF3E2");
    public static readonly Color Danger = ColorTranslator.FromHtml("#DE3B45");
    public static readonly Color DangerHover = ColorTranslator.FromHtml("#C72F39");
    public static readonly Color DangerSoft = ColorTranslator.FromHtml("#FDECEE");

    private static readonly string FamilyName = ResolveFamily();

    public static Font Ui(float size, FontStyle style = FontStyle.Regular)
        => new(FamilyName, size, style, GraphicsUnit.Point);

    public static float Scale(Control control) => control.DeviceDpi / 96f;

    /// <summary>向上追溯，取控件实际可见的背景色（跳过 Transparent）。</summary>
    public static Color EffectiveBackColor(Control? control)
    {
        Control? current = control;
        while (current is not null)
        {
            if (current.BackColor.A > 0)
            {
                return current.BackColor;
            }

            current = current.Parent;
        }

        return Bg;
    }

    public static int S(Control control, int value) => (int)Math.Round(value * Scale(control));

    private static string ResolveFamily()
    {
        string[] candidates = { "Microsoft YaHei UI", "Microsoft YaHei", "Segoe UI", "Tahoma" };
        foreach (string candidate in candidates)
        {
            try
            {
                using var font = new FontFamily(candidate);
                return candidate;
            }
            catch
            {
                // 尝试下一个。
            }
        }

        return SystemFonts.MessageBoxFont?.Name ?? "Segoe UI";
    }

    public static void SetupQuality(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
    }

    public static GraphicsPath Rounded(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();

        if (radius <= 0.5f || bounds.Width <= 1 || bounds.Height <= 1)
        {
            path.AddRectangle(bounds);
            return path;
        }

        float d = radius * 2;
        if (d > bounds.Width)
        {
            d = bounds.Width;
        }

        if (d > bounds.Height)
        {
            d = bounds.Height;
        }

        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static void FillRounded(Graphics g, RectangleF bounds, float radius, Color color)
    {
        using GraphicsPath path = Rounded(bounds, radius);
        using var brush = new SolidBrush(color);
        g.FillPath(brush, path);
    }

    public static void StrokeRounded(Graphics g, RectangleF bounds, float radius, Color color, float width = 1f)
    {
        RectangleF inset = new(bounds.X + width / 2f, bounds.Y + width / 2f, bounds.Width - width, bounds.Height - width);
        using GraphicsPath path = Rounded(inset, radius);
        using var pen = new Pen(color, width);
        g.DrawPath(pen, path);
    }

    public static void Text(
        Graphics g,
        string text,
        Font font,
        Color color,
        Rectangle bounds,
        TextFormatFlags flags)
    {
        TextRenderer.DrawText(g, text, font, bounds, color, flags | TextFormatFlags.NoPadding);
    }

    public const TextFormatFlags SingleLineLeft =
        TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis;

    public const TextFormatFlags SingleLineRight =
        TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.EndEllipsis;

    public const TextFormatFlags SingleLineCenter =
        TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis;

    /// <summary>绘制高对比度的小圆点。</summary>
    public static void Dot(Graphics g, Point center, int diameter, Color color)
    {
        using var brush = new SolidBrush(color);
        g.FillEllipse(brush, center.X - diameter / 2f, center.Y - diameter / 2f, diameter, diameter);
    }
}
