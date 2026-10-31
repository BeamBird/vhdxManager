namespace VhdxManager.UI;

internal enum ButtonKind
{
    Primary,
    Secondary,
    Ghost,
    Danger,
    DangerSoft
}

/// <summary>圆角自绘按钮。</summary>
internal sealed class ModernButton : Control, IButtonControl
{
    private bool _hover;
    private bool _pressed;

    public ModernButton(string text, ButtonKind kind = ButtonKind.Secondary)
    {
        Text = text;
        Kind = kind;
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.SupportsTransparentBackColor
            | ControlStyles.Selectable,
            true);

        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        Font = Theme.Ui(9.5f);
        Height = 38;
        Width = 132;
    }

    public ButtonKind Kind { get; set; }

    /// <summary>可选的前置图标字符（如 "＋"）。</summary>
    public string? Glyph { get; set; }

    /// <summary>圆角半径（96 DPI 基准）。</summary>
    public int Radius { get; set; } = 8;

    public DialogResult DialogResult { get; set; } = DialogResult.None;

    public void NotifyDefault(bool value)
    {
    }

    public void PerformClick()
    {
        if (Enabled)
        {
            OnClick(EventArgs.Empty);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Theme.SetupQuality(g);

        float s = Theme.Scale(this);
        var bounds = new RectangleF(0, 0, Width, Height);
        float radius = Radius * s;

        (Color fill, Color border, Color foreground) = ResolveColors();

        if (fill.A > 0)
        {
            Theme.FillRounded(g, bounds, radius, fill);
        }

        if (border.A > 0)
        {
            Theme.StrokeRounded(g, bounds, radius, border);
        }

        string content = string.IsNullOrEmpty(Glyph) ? Text : Glyph + "  " + Text;
        Theme.Text(g, content, Font, foreground, ClientRectangle, Theme.SingleLineCenter);
    }

    private (Color Fill, Color Border, Color Foreground) ResolveColors()
    {
        if (!Enabled)
        {
            return Kind switch
            {
                ButtonKind.Primary => (ColorTranslator.FromHtml("#E8ECF4"), Color.Transparent, Theme.TextMuted),
                ButtonKind.Danger => (ColorTranslator.FromHtml("#F6DDE0"), Color.Transparent, ColorTranslator.FromHtml("#C79AA0")),
                ButtonKind.DangerSoft => (ColorTranslator.FromHtml("#F7F2F3"), Color.Transparent, ColorTranslator.FromHtml("#BFA5A8")),
                _ => (Color.Transparent, Theme.Border, Theme.TextMuted)
            };
        }

        return Kind switch
        {
            ButtonKind.Primary => (
                _pressed ? Theme.PrimaryHover : _hover ? Theme.PrimaryHover : Theme.Primary,
                Color.Transparent,
                Color.White),

            ButtonKind.Danger => (
                _pressed || _hover ? Theme.DangerHover : Theme.Danger,
                Color.Transparent,
                Color.White),

            ButtonKind.DangerSoft => (
                _pressed ? ColorTranslator.FromHtml("#F9DCE0") : _hover ? ColorTranslator.FromHtml("#FBE4E7") : Theme.DangerSoft,
                Color.Transparent,
                Theme.Danger),

            ButtonKind.Ghost => (
                _pressed ? ColorTranslator.FromHtml("#E9EDF5") : _hover ? ColorTranslator.FromHtml("#F1F4F9") : Color.Transparent,
                Color.Transparent,
                Theme.TextSecondary),

            _ => (
                _pressed ? ColorTranslator.FromHtml("#EDF1F8") : _hover ? Theme.SurfaceAlt : Theme.Surface,
                _hover ? Theme.BorderStrong : Theme.Border,
                Theme.TextPrimary)
        };
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
        _pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _pressed = true;
            Invalidate();
        }

        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _pressed = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);

        if (DialogResult != DialogResult.None && FindForm() is Form form && form.Modal)
        {
            form.DialogResult = DialogResult;
        }
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        Cursor = Enabled ? Cursors.Hand : Cursors.Default;
        Invalidate();
        base.OnEnabledChanged(e);
    }

    protected override void OnTextChanged(EventArgs e)
    {
        Invalidate();
        base.OnTextChanged(e);
    }
}

/// <summary>拨动开关。</summary>
internal sealed class ToggleSwitch : Control
{
    private bool _checked = true;
    private bool _hover;

    public ToggleSwitch()
    {
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.SupportsTransparentBackColor,
            true);

        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        Size = new Size(46, 26);
    }

    public event EventHandler? CheckedChanged;

    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value)
            {
                return;
            }

            _checked = value;
            Invalidate();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Theme.SetupQuality(g);

        float s = Theme.Scale(this);
        float height = Height;
        float width = Width;
        float radius = height / 2f;

        Color track = _checked
            ? (_hover ? Theme.PrimaryHover : Theme.Primary)
            : (_hover ? ColorTranslator.FromHtml("#CBD3E2") : ColorTranslator.FromHtml("#DDE3EE"));

        Theme.FillRounded(g, new RectangleF(0, 0, width, height), radius, track);

        float knob = height - 6 * s;
        float x = _checked ? width - knob - 3 * s : 3 * s;
        using var brush = new SolidBrush(Color.White);
        g.FillEllipse(brush, x, 3 * s, knob, knob);
    }

    protected override void OnClick(EventArgs e)
    {
        Checked = !Checked;
        base.OnClick(e);
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
}

/// <summary>圆角输入框（内部包裹原生 TextBox）。</summary>
internal sealed class InputField : Panel
{
    private TextBox _box = null!;
    private bool _focused;

    public InputField(int height = 42)
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        BackColor = Theme.Surface;

        _box = new TextBox
        {
            BorderStyle = BorderStyle.None,
            BackColor = Theme.Surface,
            ForeColor = Theme.TextPrimary,
            Font = Theme.Ui(10.5f)
        };

        _box.GotFocus += (_, _) => { _focused = true; Invalidate(); };
        _box.LostFocus += (_, _) => { _focused = false; Invalidate(); };
        _box.TextChanged += (_, _) => ValueChanged?.Invoke(this, EventArgs.Empty);

        Controls.Add(_box);

        // 高度放在最后设置：OnLayout 需要 _box 已就绪。
        Height = height;
    }

    /// <summary>输入内容变化。</summary>
    public event EventHandler? ValueChanged;

    public TextBox Box => _box;

    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override string Text
    {
        get => _box.Text;
        set => _box.Text = value ?? string.Empty;
    }

    public string PlaceholderText
    {
        get => _box.PlaceholderText;
        set => _box.PlaceholderText = value;
    }

    public bool ReadOnly
    {
        get => _box.ReadOnly;
        set
        {
            _box.ReadOnly = value;
            _box.BackColor = value ? Theme.SurfaceAlt : Theme.Surface;
            BackColor = _box.BackColor;
        }
    }

    public Font TextFont
    {
        get => _box.Font;
        set
        {
            _box.Font = value;
            LayoutBox();
        }
    }

    public TextBox RawTextBox => _box;

    protected override void OnLayout(LayoutEventArgs e)
    {
        LayoutBox();
        base.OnLayout(e);
    }

    private void LayoutBox()
    {
        if (_box is null)
        {
            return;
        }

        float s = Theme.Scale(this);
        int pad = (int)(14 * s);
        int boxHeight = _box.PreferredHeight;
        _box.SetBounds(pad, Math.Max(0, (Height - boxHeight) / 2), Math.Max(10, Width - pad * 2), boxHeight);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        Theme.SetupQuality(g);

        float s = Theme.Scale(this);
        var bounds = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
        Color border = _focused ? Theme.Primary : Theme.Border;
        Theme.FillRounded(g, new RectangleF(0, 0, Width, Height), 8 * s, BackColor);
        Theme.StrokeRounded(g, bounds, 8 * s, border, _focused ? 1.6f : 1f);
    }
}

/// <summary>自绘复选框。</summary>
internal sealed class ModernCheckBox : Control
{
    private readonly Font _font;
    private bool _checked = true;
    private bool _hover;

    public ModernCheckBox(string text, bool @checked = true)
    {
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.SupportsTransparentBackColor,
            true);

        Text = text;
        _checked = @checked;
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        Font = Theme.Ui(9.5f);
        _font = Font;
        Height = 24;
        Width = 320;
    }

    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value)
            {
                return;
            }

            _checked = value;
            Invalidate();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? CheckedChanged;

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Theme.SetupQuality(g);

        float s = Theme.Scale(this);
        int boxSize = (int)(18 * s);
        var box = new RectangleF(0, (Height - boxSize) / 2f, boxSize, boxSize);

        if (_checked)
        {
            Theme.FillRounded(g, box, 5 * s, Theme.Primary);
            using var pen = new Pen(Color.White, 2f * s)
            {
                StartCap = System.Drawing.Drawing2D.LineCap.Round,
                EndCap = System.Drawing.Drawing2D.LineCap.Round,
                LineJoin = System.Drawing.Drawing2D.LineJoin.Round
            };
            g.DrawLines(pen, new[]
            {
                new PointF(box.X + boxSize * 0.24f, box.Y + boxSize * 0.52f),
                new PointF(box.X + boxSize * 0.44f, box.Y + boxSize * 0.72f),
                new PointF(box.X + boxSize * 0.76f, box.Y + boxSize * 0.30f)
            });
        }
        else
        {
            Theme.FillRounded(g, box, 5 * s, _hover ? Theme.SurfaceAlt : Theme.Surface);
            Theme.StrokeRounded(g, box, 5 * s, _hover ? Theme.BorderStrong : Theme.Border, 1.2f);
        }

        var textRect = new Rectangle((int)(boxSize + 10 * s), 0, Math.Max(10, Width - boxSize - (int)(10 * s)), Height);
        Theme.Text(g, Text, _font, Theme.TextPrimary, textRect, Theme.SingleLineLeft);
    }

    protected override void OnClick(EventArgs e)
    {
        Checked = !Checked;
        base.OnClick(e);
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
}

/// <summary>圆角卡片面板（白底 + 1px 描边）。</summary>
internal class CardPanel : Panel
{
    public CardPanel()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        BackColor = Theme.Surface;
    }

    public int Radius { get; set; } = 14;

    public Color BorderColor { get; set; } = Theme.Border;

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Theme.SetupQuality(g);

        float s = Theme.Scale(this);
        var bounds = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
        Theme.FillRounded(g, new RectangleF(0, 0, Width, Height), Radius * s, BackColor);
        Theme.StrokeRounded(g, bounds, Radius * s, BorderColor);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // 圆角之外透出父容器的底色，避免出现白色直角。
        // 父容器背景可能是 Transparent，需要向上追溯到真正的底色，否则会画出黑边。
        using var brush = new SolidBrush(Theme.EffectiveBackColor(this));
        e.Graphics.FillRectangle(brush, ClientRectangle);
    }
}
