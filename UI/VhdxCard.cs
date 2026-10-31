using System.Drawing.Drawing2D;
using VhdxManager.Models;
using VhdxManager.Services;

namespace VhdxManager.UI;

/// <summary>列表中的虚拟磁盘卡片。</summary>
internal sealed class VhdxCard : Control
{
    private readonly Font _nameFont;
    private readonly Font _pathFont;
    private readonly Font _metaFont;
    private readonly Font _badgeFont;
    private bool _hover;
    private bool _selected;

    public VhdxCard(VhdxEntry entry)
    {
        Entry = entry;

        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.SupportsTransparentBackColor,
            true);

        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        Height = Theme.S(this, 96);
        _nameFont = Theme.Ui(10.5f, FontStyle.Bold);
        _pathFont = Theme.Ui(8.8f);
        _metaFont = Theme.Ui(9f);
        _badgeFont = Theme.Ui(8.4f, FontStyle.Bold);
    }

    public VhdxEntry Entry { get; }

    public event EventHandler? Activated;

    public bool Selected
    {
        get => _selected;
        set
        {
            if (_selected == value)
            {
                return;
            }

            _selected = value;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Theme.SetupQuality(g);

        float s = Theme.Scale(this);
        var bounds = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        float radius = 12 * s;

        Color fill = Selected
            ? Theme.PrimarySoft
            : _hover ? ColorTranslator.FromHtml("#FBFCFF") : Theme.Surface;

        Color border = Selected
            ? Theme.Primary
            : _hover ? Theme.BorderStrong : Theme.Border;

        Theme.FillRounded(g, new RectangleF(0, 0, Width, Height), radius, fill);
        Theme.StrokeRounded(g, bounds, radius, border, Selected ? 1.6f : 1f);

        int pad = (int)(16 * s);
        int iconSize = (int)(42 * s);
        var iconRect = new RectangleF(pad, (Height - iconSize) / 2f, iconSize, iconSize);

        Theme.FillRounded(g, iconRect, 11 * s, Selected ? Color.White : Theme.SurfaceAlt);
        DrawDiskIcon(g, iconRect, Selected ? Theme.Primary : ColorTranslator.FromHtml("#8A99B8"), s);

        int textLeft = (int)(iconRect.Right + 14 * s);

        // 右侧徽章预留宽度
        string typeText = Entry.TypeIsDynamic ? "动态扩展" : "固定大小";
        Size typeSize = TextRenderer.MeasureText(g, typeText, _badgeFont, Size.Empty, TextFormatFlags.NoPadding);
        int typeBadgeWidth = typeSize.Width + (int)(16 * s);
        int typeBadgeHeight = (int)(22 * s);
        var typeBadge = new RectangleF(Width - pad - typeBadgeWidth, pad + 2 * s, typeBadgeWidth, typeBadgeHeight);

        int textRight = (int)typeBadge.X - (int)(10 * s);

        var nameRect = new Rectangle(textLeft, (int)(18 * s), Math.Max(20, textRight - textLeft), (int)(22 * s));
        Theme.Text(g, Entry.FileName, _nameFont, Theme.TextPrimary, nameRect, Theme.SingleLineLeft);

        var pathRect = new Rectangle(
            textLeft,
            (int)(43 * s),
            Math.Max(20, Width - pad - textLeft),
            (int)(18 * s));
        Theme.Text(g, Entry.Path, _pathFont, Theme.TextMuted, pathRect, Theme.SingleLineLeft);

        // 类型徽章
        Theme.FillRounded(g, typeBadge, 6 * s,
            Selected ? Color.White : Entry.TypeIsDynamic ? Theme.PrimarySoft : ColorTranslator.FromHtml("#F1F3F8"));
        Theme.Text(g, typeText, _badgeFont,
            Selected ? Theme.Primary : Entry.TypeIsDynamic ? Theme.Primary : Theme.TextSecondary,
            Rectangle.Round(typeBadge), Theme.SingleLineCenter);

        // 挂载状态
        string statusText;
        Color statusColor;
        if (Entry.Mounted && !string.IsNullOrEmpty(Entry.DriveLetter))
        {
            statusText = "已挂载 " + DriveUtil.ToDisplay(Entry.DriveLetter);
            statusColor = Theme.Success;
        }
        else if (Entry.Mounted)
        {
            statusText = "已挂载（无盘符）";
            statusColor = Theme.Warning;
        }
        else
        {
            statusText = "未挂载";
            statusColor = ColorTranslator.FromHtml("#A7B1C4");
        }

        Size statusSize = TextRenderer.MeasureText(g, statusText, _metaFont, Size.Empty, TextFormatFlags.NoPadding);
        int dotSize = (int)(8 * s);
        int statusWidth = dotSize + (int)(8 * s) + statusSize.Width;
        int statusX = Width - pad - statusWidth;
        int statusY = Height - (int)(32 * s);

        Theme.Dot(g, new Point(statusX + dotSize / 2, statusY + (int)(9 * s)), dotSize, statusColor);
        Theme.Text(g, statusText, _metaFont, statusColor,
            new Rectangle(statusX + dotSize + (int)(8 * s), statusY, statusSize.Width + 4, (int)(18 * s)),
            Theme.SingleLineLeft);
    }

    private void DrawDiskIcon(Graphics g, RectangleF area, Color color, float s)
    {
        float w = area.Width * 0.44f;
        float h = area.Height * 0.44f;
        var rect = new RectangleF(area.X + (area.Width - w) / 2f, area.Y + (area.Height - h) / 2f, w, h);

        using GraphicsPath path = Theme.Rounded(rect, 3.5f * s);
        using var pen = new Pen(color, 1.7f * s);
        g.DrawPath(pen, path);

        float lineInset = w * 0.2f;
        g.DrawLine(pen,
            rect.X + lineInset, rect.Y + h * 0.34f,
            rect.Right - lineInset, rect.Y + h * 0.34f);
        g.DrawLine(pen,
            rect.X + lineInset, rect.Y + h * 0.66f,
            rect.Right - lineInset, rect.Y + h * 0.66f);

        using var brush = new SolidBrush(color);
        float dot = 2.4f * s;
        g.FillEllipse(brush, rect.Right - lineInset - dot, rect.Y + h * 0.34f - dot / 2 - 0.5f, dot, dot);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnDoubleClick(EventArgs e)
    {
        Activated?.Invoke(this, EventArgs.Empty);
        base.OnDoubleClick(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _nameFont.Dispose();
            _pathFont.Dispose();
            _metaFont.Dispose();
            _badgeFont.Dispose();
        }

        base.Dispose(disposing);
    }
}
