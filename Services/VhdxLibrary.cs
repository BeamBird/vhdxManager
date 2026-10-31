using System.Text.Json;
using VhdxManager.Models;

namespace VhdxManager.Services;

/// <summary>管理"我的虚拟磁盘"清单，并持久化到 %AppData%\VhdxManager\library.json。</summary>
public sealed class VhdxLibrary
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    public VhdxLibrary()
    {
        string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VhdxManager");
        Directory.CreateDirectory(folder);
        _filePath = Path.Combine(folder, "library.json");
    }

    public VhdxSettings Settings { get; private set; } = new();

    public List<VhdxEntry> Items => Settings.Items;

    public void Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return;
            }

            string json = File.ReadAllText(_filePath);
            VhdxSettings? loaded = JsonSerializer.Deserialize<VhdxSettings>(json, JsonOptions);
            if (loaded is not null)
            {
                Settings = loaded;
                Settings.Items ??= new List<VhdxEntry>();
            }
        }
        catch
        {
            Settings = new VhdxSettings();
        }
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(_filePath, JsonSerializer.Serialize(Settings, JsonOptions));
        }
        catch
        {
            // 保存失败不影响使用。
        }
    }

    /// <summary>添加（或更新）一个 VHDX 文件记录，返回对应条目。</summary>
    public VhdxEntry AddOrUpdate(string path)
    {
        VhdxEntry? existing = Items.FirstOrDefault(
            i => string.Equals(i.Path, path, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            existing.Mounted = VhdxService.IsLocked(path);
            return existing;
        }

        var entry = new VhdxEntry
        {
            Path = path,
            Mounted = VhdxService.IsLocked(path)
        };

        Items.Add(entry);
        return entry;
    }

    public void Remove(string path)
    {
        Items.RemoveAll(i => string.Equals(i.Path, path, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>刷新全部条目的挂载状态（文件暂时不可用时不丢弃记录，方便移动硬盘/网络位置使用）。</summary>
    public void RefreshAll()
    {
        foreach (VhdxEntry entry in Items)
        {
            if (!File.Exists(entry.Path))
            {
                entry.Mounted = false;
                entry.DriveLetter = null;
                continue;
            }

            entry.Mounted = VhdxService.IsLocked(entry.Path);
            if (!entry.Mounted)
            {
                entry.DriveLetter = null;
            }
        }
    }
}
