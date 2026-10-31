namespace VhdxManager.Models;

/// <summary>一个受管理的 VHDX 文件条目（对应库中的一条记录）。</summary>
public sealed class VhdxEntry
{
    /// <summary>VHDX 文件的绝对路径。</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>最近一次成功挂载后获得的盘符，例如 "F"；未挂载时为 null。</summary>
    public string? DriveLetter { get; set; }

    /// <summary>上次运行时的挂载状态（运行时由占用检测刷新）。</summary>
    public bool Mounted { get; set; }

    /// <summary>是否动态扩展类型（检测不到时按动态显示）。</summary>
    public bool TypeIsDynamic { get; set; } = true;

    public string FileName => System.IO.Path.GetFileName(Path);

    public string Directory => System.IO.Path.GetDirectoryName(Path) ?? string.Empty;
}

/// <summary>持久化配置根对象。</summary>
public sealed class VhdxSettings
{
    public List<VhdxEntry> Items { get; set; } = new();

    /// <summary>创建向导上次使用的容量（GB）。</summary>
    public double LastSizeGb { get; set; } = 5;

    /// <summary>创建向导上次是否勾选动态扩容。</summary>
    public bool LastDynamic { get; set; } = true;

    /// <summary>创建向导上次使用的存放目录。</summary>
    public string? LastDirectory { get; set; }
}
