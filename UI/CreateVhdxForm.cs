using VhdxManager.Models;
using VhdxManager.Services;

namespace VhdxManager.UI;

/// <summary>新建虚拟磁盘向导：先设定容量与扩容方式，最后一步指定存放位置。</summary>
internal sealed class CreateVhdxForm : Form
{
    private readonly VhdxSettings _settings;

    private int _step;
    private Font _titleFont = null!;
    private Font _subtitleFont = null!;
    private Font _sectionFont = null!;
    private Font _hintFont = null!;

    private Panel _contentPanel = null!;
    private Panel _step1Panel = null!;
    private Panel _step2Panel = null!;
    private Label _stepIndicator = null!;
    private Label _errorLabel = null!;

    private InputField _sizeInput = null!;
    private ToggleSwitch _dynamicSwitch = null!;
    private InputField _nameInput = null!;
    private InputField _dirInput = null!;
    private ModernCheckBox _formatCheck = null!;
    private Label _summaryPath = null!;
    private Label _summaryType = null!;
    private Label _summaryCapacity = null!;

    private ModernButton _backButton = null!;
    private ModernButton _nextButton = null!;

    public CreateVhdxForm(VhdxSettings settings)
    {
        _settings = settings;

        Text = "新建虚拟磁盘";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Theme.Bg;
        Font = Theme.Ui(9.5f);
        DoubleBuffered = true;
        KeyPreview = true;
    }

    /// <summary>用户确认后的创建参数；取消时为 null。</summary>
    public VhdxCreateOptions? Options { get; private set; }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        float s = DeviceDpi / 96f;
        int S(int v) => (int)Math.Round(v * s);

        Rectangle workArea = Screen.FromControl(this).WorkingArea;
        ClientSize = new Size(
            Math.Min(S(640), (int)(workArea.Width * 0.94)),
            Math.Min(S(620), (int)(workArea.Height * 0.92)));
        Location = new Point(
            workArea.X + Math.Max(0, (workArea.Width - Width) / 2),
            workArea.Y + Math.Max(0, (workArea.Height - Height) / 2));

        int padding = S(28);

        _titleFont = Theme.Ui(17f, FontStyle.Bold);
        _subtitleFont = Theme.Ui(9.5f);
        _sectionFont = Theme.Ui(10f, FontStyle.Bold);
        _hintFont = Theme.Ui(9f);

        var title = new Label
        {
            Text = "新建虚拟磁盘",
            Font = _titleFont,
            ForeColor = Theme.TextPrimary,
            BackColor = Color.Transparent,
            AutoSize = false,
            Location = new Point(padding, S(26)),
            Size = new Size(S(400), S(34)),
            TextAlign = ContentAlignment.MiddleLeft
        };

        var subtitle = new Label
        {
            Text = "VHDX 虚拟磁盘是一个可挂载的磁盘镜像文件，容量可控、随取随用。",
            Font = _subtitleFont,
            ForeColor = Theme.TextSecondary,
            BackColor = Color.Transparent,
            AutoSize = false,
            Location = new Point(padding, S(62)),
            Size = new Size(S(560), S(20)),
            TextAlign = ContentAlignment.MiddleLeft
        };

        _stepIndicator = new Label
        {
            Font = Theme.Ui(9f, FontStyle.Bold),
            ForeColor = Theme.Primary,
            BackColor = Color.Transparent,
            AutoSize = false,
            Location = new Point(padding, S(104)),
            Size = new Size(S(560), S(20)),
            TextAlign = ContentAlignment.MiddleLeft
        };

        _contentPanel = new Panel
        {
            Location = new Point(padding, S(136)),
            Size = new Size(ClientSize.Width - padding * 2, S(364)),
            BackColor = Theme.Bg
        };

        _step1Panel = BuildStep1(S);
        _step2Panel = BuildStep2(S);
        _step2Panel.Visible = false;

        _contentPanel.Controls.Add(_step1Panel);
        _contentPanel.Controls.Add(_step2Panel);

        _errorLabel = new Label
        {
            Font = Theme.Ui(9f),
            ForeColor = Theme.Danger,
            BackColor = Color.Transparent,
            AutoSize = false,
            Location = new Point(padding, ClientSize.Height - S(108)),
            Size = new Size(ClientSize.Width - padding * 2, S(22)),
            TextAlign = ContentAlignment.MiddleLeft,
            Visible = false
        };

        var cancelButton = new ModernButton("取消", ButtonKind.Ghost)
        {
            Size = new Size(S(88), S(38))
        };
        cancelButton.Location = new Point(padding, ClientSize.Height - S(64));
        cancelButton.Click += (_, _) => Close();

        _backButton = new ModernButton("上一步", ButtonKind.Secondary)
        {
            Size = new Size(S(96), S(38)),
            Visible = false
        };
        _backButton.Location = new Point(ClientSize.Width - padding - S(132) - S(10) - S(96), ClientSize.Height - S(64));
        _backButton.Click += (_, _) => GoToStep(0);

        _nextButton = new ModernButton("下一步  →", ButtonKind.Primary)
        {
            Size = new Size(S(132), S(38))
        };
        _nextButton.Location = new Point(ClientSize.Width - padding - S(132), ClientSize.Height - S(64));
        _nextButton.Click += (_, _) => HandleNext();

        Controls.Add(title);
        Controls.Add(subtitle);
        Controls.Add(_stepIndicator);
        Controls.Add(_contentPanel);
        Controls.Add(_errorLabel);
        Controls.Add(cancelButton);
        Controls.Add(_backButton);
        Controls.Add(_nextButton);

        CancelButton = cancelButton;
        AcceptButton = _nextButton;

        UpdateStepVisuals();
    }

    private Panel BuildStep1( Func<int, int> S)
    {
        var card = new CardPanel
        {
            Location = new Point(0, 0),
            Size = _contentPanel.Size,
            Radius = 14
        };

        int inner = S(24);
        int width = card.Width - inner * 2;

        var capacityLabel = new Label
        {
            Text = "容量上限",
            Font = _sectionFont,
            ForeColor = Theme.TextPrimary,
            BackColor = Color.Transparent,
            AutoSize = false,
            Location = new Point(inner, inner),
            Size = new Size(width, S(22)),
            TextAlign = ContentAlignment.MiddleLeft
        };

        _sizeInput = new InputField(S(60))
        {
            Location = new Point(inner, inner + S(28)),
            Size = new Size(S(220), S(60))
        };
        _sizeInput.TextFont = Theme.Ui(22f, FontStyle.Bold);
        _sizeInput.Text = _settings.LastSizeGb > 0 ? _settings.LastSizeGb.ToString("0.##") : "5";
        _sizeInput.RawTextBox.TextAlign = HorizontalAlignment.Center;
        _sizeInput.RawTextBox.KeyPress += (_, e) =>
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) && e.KeyChar != '.')
            {
                e.Handled = true;
            }

            if (e.KeyChar == '.' && _sizeInput.Text.Contains('.'))
            {
                e.Handled = true;
            }
        };
        _sizeInput.ValueChanged += (_, _) => { UpdateSummary(); HideError(); };

        var unitLabel = new Label
        {
            Text = "GB",
            Font = Theme.Ui(15f, FontStyle.Bold),
            ForeColor = Theme.TextMuted,
            BackColor = Color.Transparent,
            AutoSize = false,
            Location = new Point(inner + S(232), inner + S(28)),
            Size = new Size(S(70), S(60)),
            TextAlign = ContentAlignment.MiddleLeft
        };

        var capacityHint = new Label
        {
            Text = "默认 5 GB，可设置 0.02 GB ~ 64 TB。虚拟磁盘的最大容量以此处为准。",
            Font = _hintFont,
            ForeColor = Theme.TextMuted,
            BackColor = Color.Transparent,
            AutoSize = false,
            Location = new Point(inner, inner + S(96)),
            Size = new Size(width, S(20)),
            TextAlign = ContentAlignment.MiddleLeft
        };

        var chips = new Panel
        {
            Location = new Point(inner, inner + S(124)),
            Size = new Size(width, S(30)),
            BackColor = Color.Transparent
        };

        double[] presets = { 5, 20, 50, 100, 256, 1024 };
        int chipX = 0;
        foreach (double preset in presets)
        {
            string text = preset >= 1024 ? "1 TB" : preset.ToString("0") + " GB";
            var chip = new ModernButton(text, ButtonKind.Secondary)
            {
                Size = new Size(S(78), S(30)),
                Location = new Point(chipX, 0),
                Radius = 15
            };
            chip.Font = Theme.Ui(8.8f);
            chip.Click += (_, _) =>
            {
                _sizeInput.Text = preset.ToString("0.##");
                HideError();
            };
            chips.Controls.Add(chip);
            chipX += S(78) + S(8);
        }

        var divider = new Panel
        {
            Location = new Point(inner, inner + S(172)),
            Size = new Size(width, 1),
            BackColor = Theme.Border
        };

        _dynamicSwitch = new ToggleSwitch
        {
            Location = new Point(inner, inner + S(192)),
            Size = new Size(S(46), S(26)),
            Checked = _settings.LastDynamic
        };

        var switchLabel = new Label
        {
            Text = "动态扩容（推荐）",
            Font = Theme.Ui(10f, FontStyle.Bold),
            ForeColor = Theme.TextPrimary,
            BackColor = Color.Transparent,
            AutoSize = false,
            Location = new Point(inner + S(58), inner + S(192)),
            Size = new Size(S(300), S(26)),
            TextAlign = ContentAlignment.MiddleLeft
        };

        var switchHint = new Label
        {
            Text = "开启后文件从小体积开始，随写入内容逐步增长，但永远不会超过上面设定的容量上限；\r\n关闭则创建时一次性占用全部空间（固定大小，读写更快）。",
            Font = _hintFont,
            ForeColor = Theme.TextSecondary,
            BackColor = Color.Transparent,
            AutoSize = false,
            Location = new Point(inner, inner + S(224)),
            Size = new Size(width, S(40)),
            TextAlign = ContentAlignment.TopLeft
        };

        var footerHint = new Label
        {
            Text = "提示：动态磁盘文件不会无限膨胀，达到上限后即写入失败，需先扩容或清理。",
            Font = _hintFont,
            ForeColor = Theme.TextMuted,
            BackColor = Color.Transparent,
            AutoSize = false,
            Location = new Point(inner, inner + S(274)),
            Size = new Size(width, S(20)),
            TextAlign = ContentAlignment.MiddleLeft
        };

        card.Controls.Add(capacityLabel);
        card.Controls.Add(_sizeInput);
        card.Controls.Add(unitLabel);
        card.Controls.Add(capacityHint);
        card.Controls.Add(chips);
        card.Controls.Add(divider);
        card.Controls.Add(_dynamicSwitch);
        card.Controls.Add(switchLabel);
        card.Controls.Add(switchHint);
        card.Controls.Add(footerHint);

        return card;
    }

    private Panel BuildStep2(Func<int, int> S)
    {
        var card = new CardPanel
        {
            Location = new Point(0, 0),
            Size = _contentPanel.Size,
            Radius = 14
        };

        int inner = S(24);
        int width = card.Width - inner * 2;

        var nameLabel = new Label
        {
            Text = "文件名",
            Font = _sectionFont,
            ForeColor = Theme.TextPrimary,
            BackColor = Color.Transparent,
            AutoSize = false,
            Location = new Point(inner, inner),
            Size = new Size(width, S(20)),
            TextAlign = ContentAlignment.MiddleLeft
        };

        _nameInput = new InputField(S(42))
        {
            Location = new Point(inner, inner + S(26)),
            Size = new Size(S(380), S(42))
        };
        _nameInput.Text = "VHDisk_" + DateTime.Now.ToString("yyyyMMdd");
        _nameInput.ValueChanged += (_, _) => { UpdateSummary(); HideError(); };

        var extensionLabel = new Label
        {
            Text = ".vhdx",
            Font = Theme.Ui(10f),
            ForeColor = Theme.TextMuted,
            BackColor = Color.Transparent,
            AutoSize = false,
            Location = new Point(inner + S(390), inner + S(26)),
            Size = new Size(S(70), S(42)),
            TextAlign = ContentAlignment.MiddleLeft
        };

        var dirLabel = new Label
        {
            Text = "存放位置",
            Font = _sectionFont,
            ForeColor = Theme.TextPrimary,
            BackColor = Color.Transparent,
            AutoSize = false,
            Location = new Point(inner, inner + S(84)),
            Size = new Size(width, S(20)),
            TextAlign = ContentAlignment.MiddleLeft
        };

        _dirInput = new InputField(S(42))
        {
            Location = new Point(inner, inner + S(110)),
            Size = new Size(width - S(96), S(42)),
            ReadOnly = true
        };
        _dirInput.Text = ResolveDefaultDirectory();

        var browseButton = new ModernButton("浏览…", ButtonKind.Secondary)
        {
            Size = new Size(S(88), S(42)),
            Location = new Point(inner + width - S(88), inner + S(110))
        };
        browseButton.Click += (_, _) => BrowseFolder();

        var summary = new CardPanel
        {
            Location = new Point(inner, inner + S(170)),
            Size = new Size(width, S(106)),
            Radius = 10,
            BackColor = Theme.SurfaceAlt,
            BorderColor = Theme.Border
        };

        summary.Controls.Add(MakeSummaryLabel("完整路径", 14, 10, 240, S));
        _summaryPath = MakeSummaryLabel(string.Empty, 14, 28, width - 30, S, bold: true);
        _summaryPath.AutoEllipsis = true;

        summary.Controls.Add(MakeSummaryLabel("类型", 14, 56, 100, S));
        _summaryType = MakeSummaryLabel(string.Empty, 14, 74, 200, S, bold: true);

        summary.Controls.Add(MakeSummaryLabel("容量上限", 250, 56, 120, S));
        _summaryCapacity = MakeSummaryLabel(string.Empty, 250, 74, 200, S, bold: true);

        summary.Controls.Add(_summaryPath);
        summary.Controls.Add(_summaryType);
        summary.Controls.Add(_summaryCapacity);

        _formatCheck = new ModernCheckBox("创建后自动分区、格式化（NTFS）并挂载", true)
        {
            Location = new Point(inner, inner + S(288)),
            Size = new Size(width, S(24))
        };

        // 单行提示：卡片高度有限（364），两行文字会被底部裁掉
        var formatHint = new Label
        {
            Text = "勾选后初始约 100 MB；不勾选仅生成 4 MB 空盘，适合压缩打包分发。",
            Font = _hintFont,
            ForeColor = Theme.TextMuted,
            BackColor = Color.Transparent,
            AutoSize = false,
            Location = new Point(inner, inner + S(316)),
            Size = new Size(width, S(18)),
            TextAlign = ContentAlignment.MiddleLeft
        };

        card.Controls.Add(nameLabel);
        card.Controls.Add(_nameInput);
        card.Controls.Add(extensionLabel);
        card.Controls.Add(dirLabel);
        card.Controls.Add(_dirInput);
        card.Controls.Add(browseButton);
        card.Controls.Add(summary);
        card.Controls.Add(_formatCheck);
        card.Controls.Add(formatHint);

        return card;
    }

    private Label MakeSummaryLabel(string text, int x, int y, int width, Func<int, int> S, bool bold = false)
    {
        return new Label
        {
            Text = text,
            Font = bold ? Theme.Ui(9.2f, FontStyle.Bold) : Theme.Ui(8.6f),
            ForeColor = bold ? Theme.TextPrimary : Theme.TextMuted,
            BackColor = Color.Transparent,
            AutoSize = false,
            Location = new Point(S(x), S(y)),
            Size = new Size(S(width), S(18)),
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private string ResolveDefaultDirectory()
    {
        if (!string.IsNullOrWhiteSpace(_settings.LastDirectory) && Directory.Exists(_settings.LastDirectory))
        {
            return _settings.LastDirectory!;
        }

        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Directory.Exists(documents) ? documents : AppContext.BaseDirectory;
    }

    private void BrowseFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择 VHDX 文件的存放位置",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
            SelectedPath = _dirInput.Text
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _dirInput.Text = dialog.SelectedPath;
            UpdateSummary();
            HideError();
        }
    }

    private void HandleNext()
    {
        if (_step == 0)
        {
            if (!TryParseSize(out _, out string error))
            {
                ShowError(error);
                return;
            }

            GoToStep(1);
            return;
        }

        if (!TryBuildOptions(out VhdxCreateOptions? options, out string optionError))
        {
            ShowError(optionError);
            return;
        }

        Options = options;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void GoToStep(int step)
    {
        _step = step;
        _step1Panel.Visible = step == 0;
        _step2Panel.Visible = step == 1;
        _backButton.Visible = step == 1;
        _nextButton.Text = step == 1 ? "创建虚拟磁盘" : "下一步  →";
        HideError();
        UpdateStepVisuals();
        UpdateSummary();
    }

    private void UpdateStepVisuals()
    {
        _stepIndicator.Text = _step == 0
            ? "第 1 步 / 共 2 步  ·  设定容量上限与扩容方式"
            : "第 2 步 / 共 2 步  ·  指定存放位置并创建";
    }

    private void UpdateSummary()
    {
        if (_summaryPath is null || _nameInput is null)
        {
            return;
        }

        string name = SanitizeFileName(_nameInput.Text.Trim());
        string directory = _dirInput.Text.Trim();
        _summaryPath.Text = Path.Combine(directory, name + ".vhdx");

        bool dynamicMode = _dynamicSwitch.Checked;
        _summaryType.Text = dynamicMode ? "动态扩展" : "固定大小";

        _summaryCapacity.Text = TryParseSize(out long bytes, out _)
            ? FormatUtil.Bytes(bytes) + (dynamicMode ? "（上限）" : "")
            : "—";
    }

    private bool TryParseSize(out long bytes, out string error)
    {
        bytes = 0;
        error = string.Empty;

        if (!double.TryParse(_sizeInput.Text.Trim(), out double gb) || gb <= 0)
        {
            error = "请输入有效的容量数值。";
            return false;
        }

        if (gb < VhdxService.MinSizeBytes / 1024d / 1024d / 1024d)
        {
            error = "容量太小，请至少设置为 0.02 GB。";
            return false;
        }

        double bytesValue = gb * 1024d * 1024d * 1024d;
        if (bytesValue > VhdxService.MaxSizeBytes)
        {
            error = "容量超出上限，VHDX 单文件最大 64 TB。";
            return false;
        }

        bytes = (long)bytesValue;
        return true;
    }

    private bool TryBuildOptions(out VhdxCreateOptions? options, out string error)
    {
        options = null;
        error = string.Empty;

        if (!TryParseSize(out long bytes, out error))
        {
            return false;
        }

        string name = SanitizeFileName(_nameInput.Text.Trim());
        if (string.IsNullOrWhiteSpace(name))
        {
            error = "请输入文件名。";
            return false;
        }

        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            error = "文件名包含非法字符。";
            return false;
        }

        string directory = _dirInput.Text.Trim();
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            error = "存放位置不存在，请重新选择。";
            return false;
        }

        string fullPath = Path.Combine(directory, name + ".vhdx");
        if (File.Exists(fullPath))
        {
            error = "该位置已存在同名文件，请更换文件名。";
            return false;
        }

        if (fullPath.Length > 240)
        {
            error = "路径过长，请缩短文件名或选择更浅的目录。";
            return false;
        }

        _settings.LastSizeGb = Math.Round(bytes / 1024d / 1024d / 1024d, 2);
        _settings.LastDynamic = _dynamicSwitch.Checked;
        _settings.LastDirectory = directory;

        options = new VhdxCreateOptions
        {
            Path = fullPath,
            SizeBytes = bytes,
            Dynamic = _dynamicSwitch.Checked,
            FormatAndMount = _formatCheck.Checked,
            Label = name
        };

        return true;
    }

    private static string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }

        return name.Trim();
    }

    private void ShowError(string message)
    {
        _errorLabel.Text = message;
        _errorLabel.Visible = true;
    }

    private void HideError()
    {
        if (_errorLabel is not null)
        {
            _errorLabel.Visible = false;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _titleFont?.Dispose();
            _subtitleFont?.Dispose();
            _sectionFont?.Dispose();
            _hintFont?.Dispose();
        }

        base.Dispose(disposing);
    }
}
