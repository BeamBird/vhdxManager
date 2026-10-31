using System.Diagnostics;
using System.Runtime.InteropServices;
using VhdxManager.Models;
using VhdxManager.Services;

namespace VhdxManager.UI;

/// <summary>主窗口：虚拟磁盘列表 + 详情 + 操作。</summary>
internal sealed class MainForm : Form
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr ShellExecute(
        IntPtr hwnd, string? operation, string file, string? parameters, string? directory, int showCommand);

    private readonly VhdxLibrary _library = new();
    private readonly List<VhdxCard> _cards = new();

    private float _scale = 1f;
    private bool _busy;
    private bool _layingOutCards;
    private string? _selectedPath;
    private string? _capacityText;
    private bool _detailsLoaded;

    private Panel _header = null!;
    private Label _appTitle = null!;
    private Label _appSubtitle = null!;
    private ModernButton _newButton = null!;
    private ModernButton _addButton = null!;
    private Panel _body = null!;
    private CardPanel _listPanel = null!;
    private Label _listTitle = null!;
    private Label _listCount = null!;
    private ModernButton _refreshButton = null!;
    private FlowLayoutPanel _cardHost = null!;
    private Label _emptyLabel = null!;
    private Panel _sidePanel = null!;
    private CardPanel _detailCard = null!;
    private Label _detailName = null!;
    private Label _detailPath = null!;
    private Label _typeValue = null!;
    private Label _capacityValue = null!;
    private Label _sizeValue = null!;
    private Label _statusValue = null!;
    private Panel _actionPanel = null!;
    private ModernButton _mountButton = null!;
    private ModernButton _openButton = null!;
    private ModernButton _compactButton = null!;
    private ModernButton _unmountButton = null!;
    private ModernButton _deleteButton = null!;
    private CardPanel _logCard = null!;
    private TextBox _logBox = null!;
    private Panel _footer = null!;
    private Label _statusLabel = null!;

    public MainForm()
    {
        Text = "VHDX 磁盘管理";
        // 尺寸在 OnLoad 中按屏幕与 DPI 计算，因此位置也手动居中（CenterScreen 会使用旧尺寸）。
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Theme.Bg;
        Font = Theme.Ui(9.5f);
        DoubleBuffered = true;
        AllowDrop = true;
        KeyPreview = true;
        Icon = BuildAppIcon();
    }

    /// <summary>生成与页头 Logo 一致的程序图标（圆角主色方块 + 白色 V，用于标题栏与任务栏）。</summary>
    private static Icon BuildAppIcon()
    {
        const int size = 64;
        using var bmp = new Bitmap(size, size);

        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

            var logo = new RectangleF(6f, 6f, size - 12f, size - 12f);
            Theme.FillRounded(g, logo, 15f, Theme.Primary);

            using var font = new Font("Segoe UI", 30f, FontStyle.Bold, GraphicsUnit.Pixel);
            TextRenderer.DrawText(
                g,
                "V",
                font,
                Rectangle.Round(logo),
                Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        IntPtr hicon = bmp.GetHicon();
        return Icon.FromHandle(hicon);
    }

    private int S(int value) => (int)Math.Round(value * _scale);

    private void CenterOnScreen()
    {
        Rectangle work = Screen.FromControl(this).WorkingArea;
        int x = work.X + Math.Max(0, (work.Width - Width) / 2);
        int y = work.Y + Math.Max(0, (work.Height - Height) / 2);
        Location = new Point(x, y);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        _scale = DeviceDpi / 96f;

        Rectangle workArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 800);
        int width = Math.Min(S(1100), (int)(workArea.Width * 0.94));
        int height = Math.Min(S(720), (int)(workArea.Height * 0.92));

        ClientSize = new Size(width, height);
        MinimumSize = new Size(Math.Min(S(900), width), Math.Min(S(600), height));

        _library.Load();

        BuildHeader();
        BuildBody();
        BuildFooter();

        _header.Dock = DockStyle.Top;
        _footer.Dock = DockStyle.Bottom;
        _body.Dock = DockStyle.Fill;

        Controls.Add(_body);
        Controls.Add(_header);
        Controls.Add(_footer);

        DragEnter += OnDragEnterFiles;
        DragDrop += OnDragDropFiles;
        FormClosing += (_, _) => _library.Save();

        LayoutContent();
        RefreshList();
        CenterOnScreen();

        SetStatus("就绪。所有虚拟磁盘操作均需要管理员权限。");
        Log("VHDX 磁盘管理已启动（管理员权限）。");
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutContent();
    }

    // ---------------------------------------------------------------- 布局

    private void BuildHeader()
    {
        _header = new Panel
        {
            Height = S(88),
            BackColor = Theme.Surface
        };

        _appTitle = new Label
        {
            Text = "VHDX 磁盘管理",
            Font = Theme.Ui(14f, FontStyle.Bold),
            ForeColor = Theme.TextPrimary,
            BackColor = Color.Transparent,
            AutoSize = false,
            Location = new Point(S(78), S(23)),
            Size = new Size(S(420), S(28)),
            TextAlign = ContentAlignment.MiddleLeft
        };

        _appSubtitle = new Label
        {
            Text = "创建 · 挂载 · 压缩 · 删除，一个界面管理所有虚拟磁盘",
            Font = Theme.Ui(9f),
            ForeColor = Theme.TextSecondary,
            BackColor = Color.Transparent,
            AutoSize = false,
            Location = new Point(S(78), S(50)),
            Size = new Size(S(520), S(20)),
            TextAlign = ContentAlignment.MiddleLeft
        };

        _newButton = new ModernButton("新建虚拟磁盘", ButtonKind.Primary)
        {
            Size = new Size(S(152), S(42)),
            Glyph = "＋"
        };
        _newButton.Click += OnNewClick;

        _addButton = new ModernButton("添加已有文件", ButtonKind.Secondary)
        {
            Size = new Size(S(140), S(42))
        };
        _addButton.Click += OnAddClick;

        _header.Controls.Add(_appTitle);
        _header.Controls.Add(_appSubtitle);
        _header.Controls.Add(_newButton);
        _header.Controls.Add(_addButton);

        _header.Paint += (_, e) =>
        {
            Graphics g = e.Graphics;
            Theme.SetupQuality(g);

            using (var pen = new Pen(Theme.Border))
            {
                g.DrawLine(pen, 0, _header.Height - 1, _header.Width, _header.Height - 1);
            }

            var logo = new RectangleF(S(24), S(24), S(40), S(40));
            Theme.FillRounded(g, logo, S(12), Theme.Primary);

            using var font = Theme.Ui(15f, FontStyle.Bold);
            TextRenderer.DrawText(
                g,
                "V",
                font,
                Rectangle.Round(logo),
                Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        };
    }

    private void BuildBody()
    {
        _body = new Panel { BackColor = Theme.Bg };

        _listPanel = new CardPanel { Radius = 16 };
        _listTitle = new Label
        {
            Text = "我的虚拟磁盘",
            Font = Theme.Ui(11.5f, FontStyle.Bold),
            ForeColor = Theme.TextPrimary,
            BackColor = Color.Transparent,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft
        };

        _listCount = new Label
        {
            Font = Theme.Ui(9f),
            ForeColor = Theme.TextMuted,
            BackColor = Color.Transparent,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "0 个"
        };

        _refreshButton = new ModernButton("刷新", ButtonKind.Ghost)
        {
            Size = new Size(S(72), S(30)),
            Radius = 15
        };
        _refreshButton.Font = Theme.Ui(9f);
        _refreshButton.Click += (_, _) =>
        {
            RefreshList();
            SetStatus("列表已刷新。");
        };

        _cardHost = new FlowLayoutPanel
        {
            AutoScroll = true,
            WrapContents = false,
            FlowDirection = FlowDirection.TopDown,
            BackColor = Theme.Surface,
            Padding = new Padding(0)
        };
        _cardHost.ClientSizeChanged += (_, _) => LayoutCards();

        _emptyLabel = new Label
        {
            Text = "还没有虚拟磁盘\r\n\r\n点击右上角「新建虚拟磁盘」创建，\r\n或把已有的 .vhdx 文件拖到这里。",
            Font = Theme.Ui(9.5f),
            ForeColor = Theme.TextMuted,
            BackColor = Color.Transparent,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter
        };

        _listPanel.Controls.Add(_listTitle);
        _listPanel.Controls.Add(_listCount);
        _listPanel.Controls.Add(_refreshButton);
        _listPanel.Controls.Add(_cardHost);
        _listPanel.Controls.Add(_emptyLabel);

        _sidePanel = new Panel { BackColor = Theme.Bg };

        BuildDetailCard();
        BuildActionPanel();
        BuildLogCard();

        _sidePanel.Controls.Add(_detailCard);
        _sidePanel.Controls.Add(_actionPanel);
        _sidePanel.Controls.Add(_logCard);

        _body.Controls.Add(_listPanel);
        _body.Controls.Add(_sidePanel);

        foreach (Control dropTarget in new Control[] { _body, _listPanel, _cardHost, _emptyLabel, _sidePanel })
        {
            dropTarget.AllowDrop = true;
            dropTarget.DragEnter += OnDragEnterFiles;
            dropTarget.DragDrop += OnDragDropFiles;
        }
    }

    private void BuildDetailCard()
    {
        _detailCard = new CardPanel { Radius = 16 };

        _detailName = new Label
        {
            Font = Theme.Ui(10.8f, FontStyle.Bold),
            ForeColor = Theme.TextPrimary,
            BackColor = Color.Transparent,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            Text = "未选择虚拟磁盘"
        };

        _detailPath = new Label
        {
            Font = Theme.Ui(8.5f),
            ForeColor = Theme.TextMuted,
            BackColor = Color.Transparent,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            Text = "从左侧选择一个虚拟磁盘"
        };

        var divider = new Panel { BackColor = Theme.Border };

        _typeValue = MakeValueLabel();
        _capacityValue = MakeValueLabel();
        _sizeValue = MakeValueLabel();

        Label typeCaption = MakeCaptionLabel("类型");
        Label capacityCaption = MakeCaptionLabel("容量上限");
        Label sizeCaption = MakeCaptionLabel("实际占用");

        _statusValue = new Label
        {
            Font = Theme.Ui(9f, FontStyle.Bold),
            ForeColor = Theme.TextMuted,
            BackColor = Color.Transparent,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "● 未挂载"
        };

        _detailCard.Controls.Add(_detailName);
        _detailCard.Controls.Add(_detailPath);
        _detailCard.Controls.Add(divider);
        _detailCard.Controls.Add(_typeValue);
        _detailCard.Controls.Add(_capacityValue);
        _detailCard.Controls.Add(_sizeValue);
        _detailCard.Controls.Add(typeCaption);
        _detailCard.Controls.Add(capacityCaption);
        _detailCard.Controls.Add(sizeCaption);
        _detailCard.Controls.Add(_statusValue);

        _detailCard.Tag = (divider, typeCaption, capacityCaption, sizeCaption);
    }

    private Label MakeValueLabel() => new()
    {
        Font = Theme.Ui(10f, FontStyle.Bold),
        ForeColor = Theme.TextPrimary,
        BackColor = Color.Transparent,
        AutoSize = false,
        TextAlign = ContentAlignment.MiddleLeft,
        Text = "—"
    };

    private Label MakeCaptionLabel(string text) => new()
    {
        Text = text,
        Font = Theme.Ui(8.3f),
        ForeColor = Theme.TextMuted,
        BackColor = Color.Transparent,
        AutoSize = false,
        TextAlign = ContentAlignment.MiddleLeft
    };

    private void BuildActionPanel()
    {
        _actionPanel = new Panel { BackColor = Theme.Bg };

        _mountButton = new ModernButton("挂载磁盘", ButtonKind.Primary);
        _mountButton.Click += async (_, _) => await MountSelectedAsync();

        _openButton = new ModernButton("打开文件夹", ButtonKind.Secondary);
        _openButton.Click += (_, _) => OpenSelectedFolder();

        _compactButton = new ModernButton("压缩", ButtonKind.Secondary);
        _compactButton.Click += async (_, _) => await CompactSelectedAsync();

        _unmountButton = new ModernButton("卸载", ButtonKind.Secondary);
        _unmountButton.Click += async (_, _) => await UnmountSelectedAsync();

        _deleteButton = new ModernButton("删除虚拟磁盘文件", ButtonKind.DangerSoft);
        _deleteButton.Click += async (_, _) => await DeleteSelectedAsync();

        _actionPanel.Controls.Add(_mountButton);
        _actionPanel.Controls.Add(_openButton);
        _actionPanel.Controls.Add(_compactButton);
        _actionPanel.Controls.Add(_unmountButton);
        _actionPanel.Controls.Add(_deleteButton);
    }

    private void BuildLogCard()
    {
        _logCard = new CardPanel { Radius = 16 };

        var logTitle = new Label
        {
            Text = "操作日志",
            Font = Theme.Ui(9.5f, FontStyle.Bold),
            ForeColor = Theme.TextSecondary,
            BackColor = Color.Transparent,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft
        };

        _logBox = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BorderStyle = BorderStyle.None,
            BackColor = Theme.Surface,
            ForeColor = Theme.TextSecondary,
            Font = new Font("Consolas", 8.6f)
        };

        _logCard.Controls.Add(logTitle);
        _logCard.Controls.Add(_logBox);
        _logCard.Tag = logTitle;
    }

    private void BuildFooter()
    {
        _footer = new Panel
        {
            Height = S(36),
            BackColor = Theme.Surface
        };

        _statusLabel = new Label
        {
            Font = Theme.Ui(9f),
            ForeColor = Theme.TextSecondary,
            BackColor = Color.Transparent,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "就绪"
        };

        var adminLabel = new Label
        {
            Text = "● 管理员权限",
            Font = Theme.Ui(9f),
            ForeColor = Theme.Success,
            BackColor = Color.Transparent,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleRight
        };

        _footer.Controls.Add(_statusLabel);
        _footer.Controls.Add(adminLabel);
        _footer.Tag = adminLabel;

        _footer.Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.Border);
            e.Graphics.DrawLine(pen, 0, 0, _footer.Width, 0);
        };
    }

    private void LayoutContent()
    {
        if (_header is null || _body is null)
        {
            return;
        }

        int pad = S(18);
        int gap = S(16);

        // Header
        _appTitle.Location = new Point(S(78), S(23));
        _appSubtitle.Location = new Point(S(78), S(50));
        _newButton.Location = new Point(_header.Width - pad - _newButton.Width, (S(88) - _newButton.Height) / 2);
        _addButton.Location = new Point(_newButton.Left - S(10) - _addButton.Width, (S(88) - _addButton.Height) / 2);

        // Footer
        _statusLabel.Bounds = new Rectangle(pad, 0, _footer.Width - S(260), _footer.Height);
        if (_footer.Tag is Label admin)
        {
            admin.Bounds = new Rectangle(_footer.Width - pad - S(150), 0, S(150), _footer.Height);
        }

        // Body
        int sideWidth = S(350);
        int contentWidth = _body.Width - pad * 2 - gap - sideWidth;

        _listPanel.Bounds = new Rectangle(pad, pad, contentWidth, _body.Height - pad * 2);
        _sidePanel.Bounds = new Rectangle(_listPanel.Right + gap, pad, sideWidth, _body.Height - pad * 2);

        int inner = S(18);
        int listInnerWidth = _listPanel.Width - inner * 2;

        _listTitle.Location = new Point(inner, S(16));
        _listCount.Location = new Point(_listTitle.Right + S(8), S(21));
        _refreshButton.Location = new Point(_listPanel.Width - inner - _refreshButton.Width, S(14));

        _cardHost.Bounds = new Rectangle(inner, S(54), listInnerWidth, _listPanel.Height - S(54) - inner);
        _emptyLabel.Bounds = new Rectangle(inner, S(54), listInnerWidth, _listPanel.Height - S(54) - inner);

        // 详情卡片
        _detailCard.Bounds = new Rectangle(0, 0, sideWidth, S(168));
        _detailName.Bounds = new Rectangle(inner, S(14), sideWidth - inner * 2, S(24));
        _detailPath.Bounds = new Rectangle(inner, S(40), sideWidth - inner * 2, S(18));

        if (_detailCard.Tag is ValueTuple<Panel, Label, Label, Label> parts)
        {
            parts.Item1.Bounds = new Rectangle(inner, S(68), sideWidth - inner * 2, 1);

            int columnWidth = (sideWidth - inner * 2 - S(24)) / 3;
            _typeValue.Bounds = new Rectangle(inner, S(82), columnWidth, S(22));
            _capacityValue.Bounds = new Rectangle(inner + columnWidth + S(12), S(82), columnWidth, S(22));
            _sizeValue.Bounds = new Rectangle(inner + (columnWidth + S(12)) * 2, S(82), columnWidth, S(22));

            parts.Item2.Bounds = new Rectangle(inner, S(105), columnWidth, S(16));
            parts.Item3.Bounds = new Rectangle(inner + columnWidth + S(12), S(105), columnWidth, S(16));
            parts.Item4.Bounds = new Rectangle(inner + (columnWidth + S(12)) * 2, S(105), columnWidth, S(16));
        }

        _statusValue.Bounds = new Rectangle(inner, S(132), sideWidth - inner * 2, S(20));

        // 操作按钮
        int actionTop = _detailCard.Bottom + S(12);
        int buttonHeight = S(42);
        int rowGap = S(8);
        int columnGap = S(10);
        int buttonWidth = (sideWidth - columnGap) / 2;

        _actionPanel.Bounds = new Rectangle(0, actionTop, sideWidth, buttonHeight * 3 + rowGap * 2);

        _mountButton.Bounds = new Rectangle(0, 0, buttonWidth, buttonHeight);
        _openButton.Bounds = new Rectangle(buttonWidth + columnGap, 0, sideWidth - buttonWidth - columnGap, buttonHeight);
        _compactButton.Bounds = new Rectangle(0, buttonHeight + rowGap, buttonWidth, buttonHeight);
        _unmountButton.Bounds = new Rectangle(buttonWidth + columnGap, buttonHeight + rowGap, sideWidth - buttonWidth - columnGap, buttonHeight);
        _deleteButton.Bounds = new Rectangle(0, (buttonHeight + rowGap) * 2, sideWidth, buttonHeight);

        // 日志卡片（窗口太矮时让位给操作按钮）
        int logTop = _actionPanel.Bottom + S(12);
        int logHeight = _sidePanel.Height - logTop;
        _logCard.Visible = logHeight >= S(80);

        if (_logCard.Visible)
        {
            _logCard.Bounds = new Rectangle(0, logTop, sideWidth, logHeight);

            if (_logCard.Tag is Label logTitle)
            {
                logTitle.Bounds = new Rectangle(S(14), S(10), sideWidth - S(28), S(20));
                _logBox.Bounds = new Rectangle(S(14), S(34), sideWidth - S(28), Math.Max(S(20), logHeight - S(46)));
            }
        }

        LayoutCards();
    }

    private void LayoutCards()
    {
        if (_cardHost is null || _layingOutCards)
        {
            return;
        }

        _layingOutCards = true;
        try
        {
            int width = _cardHost.ClientSize.Width - S(4);
            if (_cardHost.VerticalScroll.Visible)
            {
                width -= SystemInformation.VerticalScrollBarWidth;
            }

            width = Math.Max(S(220), width);
            foreach (VhdxCard card in _cards)
            {
                card.Width = width;
            }
        }
        finally
        {
            _layingOutCards = false;
        }
    }

    // ---------------------------------------------------------------- 列表

    private void RefreshList()
    {
        _library.RefreshAll();

        foreach (VhdxCard card in _cards)
        {
            _cardHost.Controls.Remove(card);
            card.Dispose();
        }

        _cards.Clear();

        foreach (VhdxEntry entry in _library.Items)
        {
            var card = new VhdxCard(entry);
            card.Click += (_, _) => SelectPath(entry.Path);
            card.Activated += (_, _) =>
            {
                SelectPath(entry.Path);
                OpenSelectedFolder();
            };
            _cards.Add(card);
            _cardHost.Controls.Add(card);
        }

        if (_selectedPath is not null && _library.Items.All(i => !string.Equals(i.Path, _selectedPath, StringComparison.OrdinalIgnoreCase)))
        {
            _selectedPath = null;
        }

        _listCount.Text = _library.Items.Count + " 个";
        _emptyLabel.Visible = _library.Items.Count == 0;
        _cardHost.Visible = _library.Items.Count > 0;

        LayoutCards();
        SelectPath(_selectedPath);
    }

    private void SelectPath(string? path)
    {
        _selectedPath = path;
        _capacityText = null;
        _detailsLoaded = false;

        foreach (VhdxCard card in _cards)
        {
            card.Selected = path is not null && string.Equals(card.Entry.Path, path, StringComparison.OrdinalIgnoreCase);
        }

        UpdateDetail();

        if (path is not null)
        {
            _ = LoadDetailsAsync(path);
        }
    }

    private VhdxEntry? SelectedEntry => _selectedPath is null
        ? null
        : _library.Items.FirstOrDefault(i => string.Equals(i.Path, _selectedPath, StringComparison.OrdinalIgnoreCase));

    private async Task LoadDetailsAsync(string path)
    {
        try
        {
            VhdxDetails? details = await VhdxService.GetDetailsAsync(path).ConfigureAwait(true);
            if (!string.Equals(_selectedPath, path, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _detailsLoaded = true;

            if (details is null)
            {
                UpdateDetail();
                return;
            }

            _capacityText = details.VirtualSizeText;

            VhdxEntry? entry = SelectedEntry;
            if (entry is not null && details.IsDynamicKnown)
            {
                entry.TypeIsDynamic = details.IsDynamic;
            }

            UpdateDetail();
        }
        catch
        {
            // 详情读取失败不影响主流程。
        }
    }

    private void UpdateDetail()
    {
        VhdxEntry? entry = SelectedEntry;
        bool has = entry is not null;

        _detailName.Text = has ? entry!.FileName : "未选择虚拟磁盘";
        _detailPath.Text = has ? entry!.Path : "从左侧选择一个虚拟磁盘";
        _typeValue.Text = has ? (entry!.TypeIsDynamic ? "动态扩展" : "固定") : "—";

        // VHDX 头解析很快，先在 UI 线程直接取到容量，避免出现“读取中…”。
        if (has && _capacityText is null && File.Exists(entry!.Path))
        {
            long? size = VhdxReader.TryReadVirtualSize(entry.Path);
            if (size is not null)
            {
                _capacityText = FormatUtil.Bytes(size.Value);
            }
        }

        _capacityValue.Text = has ? (_capacityText ?? (_detailsLoaded ? "—" : "读取中…")) : "—";
        _sizeValue.Text = has && File.Exists(entry!.Path)
            ? FormatUtil.Bytes(new FileInfo(entry.Path).Length)
            : "—";

        if (!has)
        {
            _statusValue.Text = "● 未挂载";
            _statusValue.ForeColor = Theme.TextMuted;
        }
        else if (entry!.Mounted && !string.IsNullOrEmpty(entry.DriveLetter))
        {
            _statusValue.Text = "● 已挂载到 " + DriveUtil.ToDisplay(entry.DriveLetter);
            _statusValue.ForeColor = Theme.Success;
        }
        else if (entry.Mounted)
        {
            _statusValue.Text = "● 已挂载（未分配盘符）";
            _statusValue.ForeColor = Theme.Warning;
        }
        else
        {
            _statusValue.Text = "● 未挂载";
            _statusValue.ForeColor = Theme.TextMuted;
        }

        // 已挂载时按钮改为“重新挂载”，用于重新识别盘符。
        _mountButton.Text = entry is { Mounted: true } ? "重新挂载" : "挂载磁盘";
        _mountButton.Enabled = has && !_busy;
        _unmountButton.Enabled = has && !_busy && entry!.Mounted;
        _compactButton.Enabled = has && !_busy;
        _openButton.Enabled = has && !_busy;
        _deleteButton.Enabled = has && !_busy;
    }

    // ---------------------------------------------------------------- 操作

    private async void OnNewClick(object? sender, EventArgs e)
    {
        if (_busy)
        {
            return;
        }

        using var dialog = new CreateVhdxForm(_library.Settings);
        bool confirmed = dialog.ShowDialog(this) == DialogResult.OK;

        if (!confirmed || dialog.Options is null)
        {
            _library.Save();
            return;
        }

        VhdxCreateOptions options = dialog.Options;
        _library.Save();

        await ExecuteAsync("正在创建虚拟磁盘…（较大的固定大小磁盘可能需要几分钟）", async progress =>
        {
            VhdxResult result = await VhdxService.CreateAsync(options, progress);

            // 即使后续格式化失败，只要文件已经生成就纳入管理，方便用户手动处理。
            if (File.Exists(options.Path))
            {
                VhdxEntry entry = _library.AddOrUpdate(options.Path);
                entry.Mounted = result.DriveLetter is not null;
                entry.DriveLetter = result.DriveLetter;
                entry.TypeIsDynamic = options.Dynamic;
                _selectedPath = options.Path;

                if (result.DriveLetter is not null)
                {
                    Log($"已挂载为 {DriveUtil.ToDisplay(result.DriveLetter)}");
                    await WaitForVolumeAsync(result.DriveLetter);
                    OpenInExplorer(DriveUtil.ToRoot(result.DriveLetter));
                }
            }

            return result;
        });
    }

    private void OnAddClick(object? sender, EventArgs e)
    {
        if (_busy)
        {
            return;
        }

        using var dialog = new OpenFileDialog
        {
            Title = "选择已有的虚拟磁盘",
            Filter = "VHDX 虚拟磁盘 (*.vhdx)|*.vhdx|所有文件 (*.*)|*.*",
            Multiselect = true,
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        AddFiles(dialog.FileNames);
    }

    private void AddFiles(IEnumerable<string> files)
    {
        var added = new List<string>();

        foreach (string file in files)
        {
            if (!file.EndsWith(".vhdx", StringComparison.OrdinalIgnoreCase) || !File.Exists(file))
            {
                continue;
            }

            string full = Path.GetFullPath(file);
            bool exists = _library.Items.Any(i => string.Equals(i.Path, full, StringComparison.OrdinalIgnoreCase));
            _library.AddOrUpdate(full);
            added.Add(full);

            if (exists)
            {
                _selectedPath = full;
            }
        }

        if (added.Count == 0)
        {
            SetStatus("没有可添加的 .vhdx 文件。");
            return;
        }

        _selectedPath ??= added[0];
        _library.Save();
        RefreshList();
        SetStatus($"已添加 {added.Count} 个虚拟磁盘。");
        Log($"添加 {added.Count} 个虚拟磁盘文件。");
    }

    private async Task MountSelectedAsync()
    {
        VhdxEntry? entry = SelectedEntry;
        if (entry is null)
        {
            return;
        }

        string path = entry.Path;
        bool remount = entry.Mounted;

        await ExecuteAsync(remount ? "正在重新挂载虚拟磁盘…" : "正在挂载虚拟磁盘…", async _ =>
        {
            // 已挂载时先卸载再挂载，这样能重新识别盘符（盘符可能因程序重启而丢失）。
            if (remount)
            {
                VhdxResult unmount = await VhdxService.UnmountAsync(path);
                if (!unmount.Success)
                {
                    return unmount;
                }

                VhdxEntry? stale = _library.Items.FirstOrDefault(i => string.Equals(i.Path, path, StringComparison.OrdinalIgnoreCase));
                if (stale is not null)
                {
                    stale.Mounted = false;
                    stale.DriveLetter = null;
                }
            }

            VhdxResult result = await VhdxService.MountAsync(path);

            if (result.Success)
            {
                VhdxEntry? current = _library.Items.FirstOrDefault(i => string.Equals(i.Path, path, StringComparison.OrdinalIgnoreCase));
                if (current is not null)
                {
                    current.Mounted = true;
                    current.DriveLetter = result.DriveLetter;
                }

                if (result.DriveLetter is not null)
                {
                    Log($"已挂载为 {DriveUtil.ToDisplay(result.DriveLetter)}");

                    // 等卷真正就绪（format 之后需要一点时间），否则资源管理器可能打不开。
                    await WaitForVolumeAsync(result.DriveLetter);
                    OpenInExplorer(DriveUtil.ToRoot(result.DriveLetter));
                }
                else
                {
                    Log("已挂载，但未检测到盘符（磁盘可能尚未分区或格式化）。");
                }
            }

            return result;
        });
    }

    /// <summary>等待盘符对应的卷就绪（最多约 4 秒）。</summary>
    private static async Task WaitForVolumeAsync(string? driveLetter)
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            if (DriveUtil.IsReady(driveLetter))
            {
                return;
            }

            await Task.Delay(200);
        }
    }

    private async Task UnmountSelectedAsync()
    {
        VhdxEntry? entry = SelectedEntry;
        if (entry is null)
        {
            return;
        }

        string path = entry.Path;

        await ExecuteAsync("正在卸载虚拟磁盘…", async _ =>
        {
            VhdxResult result = await VhdxService.UnmountAsync(path);

            if (result.Success)
            {
                VhdxEntry? current = _library.Items.FirstOrDefault(i => string.Equals(i.Path, path, StringComparison.OrdinalIgnoreCase));
                if (current is not null)
                {
                    current.Mounted = false;
                    current.DriveLetter = null;
                }
            }

            return result;
        });
    }

    private async Task CompactSelectedAsync()
    {
        VhdxEntry? entry = SelectedEntry;
        if (entry is null)
        {
            return;
        }

        string path = entry.Path;
        long before = File.Exists(path) ? new FileInfo(path).Length : 0;

        bool confirmed = UiDialog.Confirm(
            this,
            "压缩虚拟磁盘",
            $"将回收「{entry.FileName}」中未被使用的空间。\n\n" +
            $"当前文件大小：{FormatUtil.Bytes(before)}\n" +
            "压缩前会短暂卸载该磁盘，完成后自动重新挂载。\n" +
            "建议先关闭正在使用该磁盘的程序。",
            "开始压缩");

        if (!confirmed)
        {
            return;
        }

        await ExecuteAsync("正在压缩虚拟磁盘…（耗时取决于文件大小）", async _ =>
        {
            VhdxResult result = await VhdxService.CompactAsync(path);

            VhdxEntry? current = _library.Items.FirstOrDefault(i => string.Equals(i.Path, path, StringComparison.OrdinalIgnoreCase));
            if (current is not null && result.Success)
            {
                current.Mounted = VhdxService.IsLocked(path);
                current.DriveLetter = result.DriveLetter ?? (current.Mounted ? current.DriveLetter : null);
            }

            return result;
        });
    }

    private async Task DeleteSelectedAsync()
    {
        VhdxEntry? entry = SelectedEntry;
        if (entry is null)
        {
            return;
        }

        string path = entry.Path;

        bool confirmed = UiDialog.Confirm(
            this,
            "删除虚拟磁盘",
            $"确定要删除「{entry.FileName}」吗？\n\n" +
            $"{path}\n\n" +
            "文件会被永久删除（不会进入回收站），磁盘内的所有数据都将丢失。",
            "永久删除",
            "取消",
            danger: true);

        if (!confirmed)
        {
            return;
        }

        await ExecuteAsync("正在删除虚拟磁盘…", async _ =>
        {
            VhdxResult result = await VhdxService.DeleteAsync(path);

            if (result.Success)
            {
                _library.Remove(path);
                if (string.Equals(_selectedPath, path, StringComparison.OrdinalIgnoreCase))
                {
                    _selectedPath = null;
                }
            }

            return result;
        });
    }

    private void OpenSelectedFolder()
    {
        VhdxEntry? entry = SelectedEntry;
        if (entry is null)
        {
            return;
        }

        if (entry.Mounted)
        {
            string root = DriveUtil.ToRoot(entry.DriveLetter);
            if (root.Length > 0)
            {
                OpenInExplorer(root);
                return;
            }

            UiDialog.Info(
                this,
                "未检测到盘符",
                "该虚拟磁盘已经挂载，但程序没有拿到它的盘符。\r\n\r\n" +
                "可以点「重新挂载」让程序重新识别盘符；若仍然没有盘符，说明磁盘还没有分区或格式化。");
            return;
        }

        if (File.Exists(entry.Path))
        {
            OpenInExplorer(entry.Path, selectFile: true);
        }
        else
        {
            UiDialog.Info(this, "文件不存在", "该虚拟磁盘文件已被移动或删除。");
        }
    }

    private void OpenInExplorer(string target, bool selectFile = false)
    {
        try
        {
            if (selectFile)
            {
                if (!File.Exists(target))
                {
                    UiDialog.Info(this, "无法打开", "文件不存在，可能已被移动或删除。");
                    return;
                }

                // 在资源管理器中定位并选中文件
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{target}\"")
                {
                    UseShellExecute = true
                });
                return;
            }

            // 用 ShellExecute 的 explore 动词打开目录：比 explorer.exe 命令行可靠得多，
            // 不会因为路径解析（反斜杠结尾、引号转义）而退回到“文档”文件夹。
            long code = ShellExecute(IntPtr.Zero, "explore", target, null, null, 1).ToInt64();
            if (code > 32)
            {
                return;
            }

            Log($"打开文件夹失败（ShellExecute={code}）：{target}");

            if (!IsPathReady(target))
            {
                UiDialog.Info(
                    this,
                    "无法打开",
                    $"{target} 当前不可访问。\r\n\r\n" +
                    "如果资源管理器中能看到该磁盘，请稍后再试；否则可以点「重新挂载」重新识别盘符。");
                return;
            }

            Process.Start(new ProcessStartInfo("explorer.exe", target) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log("无法打开资源管理器：" + ex.Message);
            UiDialog.Info(this, "无法打开文件夹", ex.Message);
        }
    }

    private static bool IsPathReady(string target)
    {
        try
        {
            string? root = Path.GetPathRoot(target);
            if (!string.IsNullOrEmpty(root) && string.Equals(root.TrimEnd('\\'), target.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            {
                return new DriveInfo(root).IsReady;
            }

            return Directory.Exists(target);
        }
        catch
        {
            return false;
        }
    }

    private async Task ExecuteAsync(string busyText, Func<IProgress<string>, Task<VhdxResult>> worker)
    {
        if (_busy)
        {
            return;
        }

        SetBusy(true, busyText);

        try
        {
            var progress = new Progress<string>(SetStatus);
            VhdxResult result = await worker(progress);

            SetStatus(result.Message);
            Log((result.Success ? "✔ " : "✘ ") + result.Message);

            if (!result.Success && !string.IsNullOrWhiteSpace(result.Detail))
            {
                foreach (string line in result.Detail.Split('\n', StringSplitOptions.RemoveEmptyEntries).Take(4))
                {
                    Log("    " + line.Trim());
                }
            }

            if (!result.Success)
            {
                UiDialog.Info(this, "操作未完成", result.Message);
            }
        }
        catch (Exception ex)
        {
            SetStatus("操作失败。");
            Log("✘ " + ex.Message);
            UiDialog.Info(this, "出错了", ex.Message);
        }
        finally
        {
            SetBusy(false);
            RefreshList();
            _library.Save();
        }
    }

    private void SetBusy(bool busy, string? text = null)
    {
        _busy = busy;
        UseWaitCursor = busy;

        _newButton.Enabled = !busy;
        _addButton.Enabled = !busy;
        _refreshButton.Enabled = !busy;

        if (!string.IsNullOrEmpty(text))
        {
            SetStatus(text);
        }

        UpdateDetail();
    }

    private void SetStatus(string text)
    {
        if (_statusLabel is not null)
        {
            _statusLabel.Text = text;
        }
    }

    private void Log(string message)
    {
        if (_logBox is null)
        {
            return;
        }

        _logBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");

        if (_logBox.TextLength > 20000)
        {
            _logBox.Text = _logBox.Text[^12000..];
        }

        _logBox.SelectionStart = _logBox.TextLength;
        _logBox.ScrollToCaret();
    }

    // ---------------------------------------------------------------- 拖拽

    private void OnDragEnterFiles(object? sender, DragEventArgs e)
    {
        if (_busy)
        {
            return;
        }

        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
        {
            e.Effect = DragDropEffects.Copy;
        }
    }

    private void OnDragDropFiles(object? sender, DragEventArgs e)
    {
        if (_busy || e.Data?.GetData(DataFormats.FileDrop) is not string[] files)
        {
            return;
        }

        AddFiles(files);
    }
}
