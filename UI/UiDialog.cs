namespace VhdxManager.UI;

/// <summary>风格统一的提示 / 确认对话框。</summary>
internal static class UiDialog
{
    public static void Info(IWin32Window? owner, string title, string message, string okText = "知道了")
    {
        using var dialog = new MessageDialog(title, message, okText, "取消", showCancel: false, danger: false);
        dialog.ShowDialog(owner);
    }

    public static bool Confirm(
        IWin32Window? owner,
        string title,
        string message,
        string okText = "确定",
        string cancelText = "取消",
        bool danger = false)
    {
        using var dialog = new MessageDialog(title, message, okText, cancelText, showCancel: true, danger: danger);
        return dialog.ShowDialog(owner) == DialogResult.OK;
    }
}

internal sealed class MessageDialog : Form
{
    private readonly string _message;
    private readonly string _okText;
    private readonly string _cancelText;
    private readonly bool _showCancel;
    private readonly bool _danger;
    private Font _titleFont = null!;
    private Font _messageFont = null!;
    private Label _messageLabel = null!;

    public MessageDialog(string title, string message, string okText, string cancelText, bool showCancel, bool danger)
    {
        _message = message;
        _okText = okText;
        _cancelText = cancelText;
        _showCancel = showCancel;
        _danger = danger;

        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Theme.Surface;
        Font = Theme.Ui(9.5f);
        DoubleBuffered = true;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        float s = DeviceDpi / 96f;
        int S(int v) => (int)Math.Round(v * s);

        int width = S(480);
        int padding = S(26);
        int contentWidth = width - padding * 2;

        _titleFont = Theme.Ui(12f, FontStyle.Bold);
        _messageFont = Theme.Ui(9.5f);

        Size messageSize = TextRenderer.MeasureText(
            _message,
            _messageFont,
            new Size(contentWidth, int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);

        int titleHeight = S(26);
        int messageHeight = Math.Max(S(28), messageSize.Height + S(6));
        int buttonHeight = S(38);
        int height = padding + titleHeight + S(12) + messageHeight + S(28) + buttonHeight + padding;

        ClientSize = new Size(width, height);

        Rectangle workArea = Screen.FromControl(this).WorkingArea;
        Location = new Point(
            workArea.X + Math.Max(0, (workArea.Width - Width) / 2),
            workArea.Y + Math.Max(0, (workArea.Height - Height) / 2));

        var titleLabel = new Label
        {
            Text = Text,
            Font = _titleFont,
            ForeColor = Theme.TextPrimary,
            BackColor = Color.Transparent,
            AutoSize = false,
            Location = new Point(padding, padding),
            Size = new Size(contentWidth, titleHeight),
            TextAlign = ContentAlignment.MiddleLeft
        };

        _messageLabel = new Label
        {
            Text = _message,
            Font = _messageFont,
            ForeColor = Theme.TextSecondary,
            BackColor = Color.Transparent,
            AutoSize = false,
            Location = new Point(padding, padding + titleHeight + S(12)),
            Size = new Size(contentWidth, messageHeight),
            TextAlign = ContentAlignment.TopLeft
        };

        int buttonY = height - padding - buttonHeight;

        var okButton = new ModernButton(_okText, _danger ? ButtonKind.Danger : ButtonKind.Primary)
        {
            Size = new Size(S(104), buttonHeight),
            Location = new Point(width - padding - S(104), buttonY),
            DialogResult = DialogResult.OK
        };

        Controls.Add(titleLabel);
        Controls.Add(_messageLabel);
        Controls.Add(okButton);
        AcceptButton = okButton;

        if (_showCancel)
        {
            var cancelButton = new ModernButton(_cancelText, ButtonKind.Secondary)
            {
                Size = new Size(S(88), buttonHeight),
                Location = new Point(width - padding - S(104) - S(10) - S(88), buttonY),
                DialogResult = DialogResult.Cancel
            };
            Controls.Add(cancelButton);
            CancelButton = cancelButton;
        }

        ActiveControl = okButton;
    }
}
