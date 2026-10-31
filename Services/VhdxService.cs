using System.Globalization;
using System.Text.RegularExpressions;

namespace VhdxManager.Services;

/// <summary>操作结果。</summary>
public sealed class VhdxResult
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    /// <summary>操作完成后该虚拟磁盘对应的盘符（如 "F"），没有则为 null。</summary>
    public string? DriveLetter { get; init; }

    /// <summary>diskpart 原始输出，用于日志展示。</summary>
    public string Detail { get; init; } = string.Empty;

    public static VhdxResult Ok(string message, string? drive = null, string detail = "")
        => new() { Success = true, Message = message, DriveLetter = drive, Detail = detail };

    public static VhdxResult Fail(string message, string detail = "")
        => new() { Success = false, Message = message, Detail = detail };
}

/// <summary>VHDX 详情。</summary>
public sealed class VhdxDetails
{
    public string TypeText { get; init; } = "—";

    public string VirtualSizeText { get; init; } = "—";

    /// <summary>虚拟容量（字节）；未知时为 0。</summary>
    public long VirtualSizeBytes { get; init; }

    public bool IsDynamic { get; init; }

    /// <summary>类型是否确实读取成功（磁盘挂载时无法判断）。</summary>
    public bool IsDynamicKnown { get; init; }
}

/// <summary>创建参数。</summary>
public sealed class VhdxCreateOptions
{
    public string Path { get; init; } = string.Empty;

    /// <summary>容量上限（字节）。</summary>
    public long SizeBytes { get; init; }

    /// <summary>true = 动态扩展，false = 固定大小。</summary>
    public bool Dynamic { get; init; } = true;

    /// <summary>创建后自动分区、格式化并挂载。</summary>
    public bool FormatAndMount { get; init; } = true;

    /// <summary>卷标。</summary>
    public string Label { get; init; } = "VHDX";
}

/// <summary>基于 diskpart 的 VHDX 操作封装。</summary>
public static class VhdxService
{
    public const long MinSizeBytes = 16L * 1024 * 1024;              // 16 MB
    public const long MaxSizeBytes = 64L * 1024 * 1024 * 1024 * 1024; // 64 TB（VHDX 上限）

    /// <summary>创建虚拟磁盘。</summary>
    public static async Task<VhdxResult> CreateAsync(
        VhdxCreateOptions options,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        string path = options.Path;

        if (string.IsNullOrWhiteSpace(path))
        {
            return VhdxResult.Fail("请指定 VHDX 文件的存放位置。");
        }

        if (!path.EndsWith(".vhdx", StringComparison.OrdinalIgnoreCase))
        {
            return VhdxResult.Fail("文件扩展名必须是 .vhdx。");
        }

        if (!AnsiCodec.CanRepresent(path))
        {
            return VhdxResult.Fail("文件路径包含系统无法识别的字符，请改用中文或英文路径。");
        }

        if (File.Exists(path))
        {
            return VhdxResult.Fail("目标位置已存在同名文件，请更换文件名。");
        }

        string? directory = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            return VhdxResult.Fail("目标文件夹不存在。");
        }

        if (options.SizeBytes < MinSizeBytes || options.SizeBytes > MaxSizeBytes)
        {
            return VhdxResult.Fail("容量需在 16 MB 到 64 TB 之间。");
        }

        if (!options.Dynamic)
        {
            long free = GetFreeSpace(directory);
            if (free > 0 && options.SizeBytes > free - (256L * 1024 * 1024))
            {
                return VhdxResult.Fail(
                    $"固定大小磁盘需要立即占用 {FormatUtil.Bytes(options.SizeBytes)} 空间，" +
                    $"但该磁盘仅剩 {FormatUtil.Bytes(free)} 可用。请改用动态扩展，或换一个位置。");
            }
        }

        long maximumMb = Math.Max(16, (long)Math.Round(options.SizeBytes / 1024d / 1024d));
        string quoted = Quote(path);
        string typeArgument = options.Dynamic ? "expandable" : "fixed";

        var commands = new List<string>
        {
            $"create vdisk file={quoted} maximum={maximumMb} type={typeArgument}"
        };

        if (options.FormatAndMount)
        {
            string label = SanitizeLabel(options.Label);
            commands.Add($"select vdisk file={quoted}");
            commands.Add("attach vdisk");
            commands.Add("create partition primary");
            commands.Add("select partition 1");
            commands.Add("format fs=ntfs quick label=" + Quote(label));
            commands.Add("assign");
        }

        progress?.Report("正在生成虚拟磁盘文件…");
        HashSet<string> before = GetDriveLetters();

        DiskPartResult result = await DiskPartRunner.RunAsync(commands, cancellationToken).ConfigureAwait(false);

        bool fileCreated = File.Exists(path);

        if (!result.Success || !fileCreated)
        {
            // create 成功但后续 attach/format 失败时，文件通常已经建立。
            string reason = !fileCreated
                ? "创建失败：" + result.ErrorSummary
                : "虚拟磁盘文件已创建，但挂载或格式化未完成：" + result.ErrorSummary;

            if (!fileCreated)
            {
                TryDeleteFile(path);
                return VhdxResult.Fail(reason, result.Output);
            }

            string? partialDrive = options.FormatAndMount ? await WaitForNewDriveAsync(before, TimeSpan.FromSeconds(3)).ConfigureAwait(false) : null;
            return VhdxResult.Ok(reason, partialDrive, result.Output);
        }

        string? drive = options.FormatAndMount
            ? await WaitForNewDriveAsync(before, TimeSpan.FromSeconds(12)).ConfigureAwait(false)
            : null;

        // WaitForNewDriveAsync 返回值已带冒号（如 "E:"），不能再手动拼接一个
        string message = options.FormatAndMount && drive is not null
            ? $"已创建并挂载到 {DriveUtil.ToDisplay(drive)}，卷标 {SanitizeLabel(options.Label)}。"
            : options.FormatAndMount
                ? "已创建并挂载，但未检测到新盘符。"
                : "已创建虚拟磁盘文件（约 4 MB 空盘，未分区格式化）。";

        return VhdxResult.Ok(message, drive, result.Output);
    }

    /// <summary>挂载（附加）虚拟磁盘。</summary>
    public static async Task<VhdxResult> MountAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            return VhdxResult.Fail("文件不存在，可能已被移动或删除。");
        }

        if (!AnsiCodec.CanRepresent(path))
        {
            return VhdxResult.Fail("文件路径包含系统无法识别的字符。");
        }

        HashSet<string> before = GetDriveLetters();

        DiskPartResult result = await DiskPartRunner.RunAsync(
            new[] { $"select vdisk file={Quote(path)}", "attach vdisk" },
            cancellationToken).ConfigureAwait(false);

        string? drive = await WaitForNewDriveAsync(before, TimeSpan.FromSeconds(result.Success ? 10 : 2)).ConfigureAwait(false);

        if (!result.Success)
        {
            string message = IsLocked(path)
                ? "该虚拟磁盘已经处于挂载状态。"
                : "挂载失败：" + result.ErrorSummary;
            return VhdxResult.Fail(message, result.Output);
        }

        if (drive is null)
        {
            return VhdxResult.Ok("已挂载，但未检测到盘符：该磁盘可能尚未分区或格式化。", null, result.Output);
        }

        return VhdxResult.Ok($"已挂载到 {DriveUtil.ToDisplay(drive)}。", drive, result.Output);
    }

    /// <summary>卸载（分离）虚拟磁盘。</summary>
    public static async Task<VhdxResult> UnmountAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            return VhdxResult.Fail("文件不存在，可能已被移动或删除。");
        }

        if (!IsLocked(path))
        {
            return VhdxResult.Ok("该虚拟磁盘当前未挂载。");
        }

        DiskPartResult result = await DiskPartRunner.RunAsync(
            new[] { $"select vdisk file={Quote(path)}", "detach vdisk" },
            cancellationToken).ConfigureAwait(false);

        // 等待系统释放文件占用（最多 10 秒）。
        for (int i = 0; i < 25; i++)
        {
            if (!IsLocked(path))
            {
                break;
            }

            await Task.Delay(400, cancellationToken).ConfigureAwait(false);
        }

        if (!result.Success && IsLocked(path))
        {
            return VhdxResult.Fail("卸载失败：" + result.ErrorSummary, result.Output);
        }

        return VhdxResult.Ok("已卸载。", null, result.Output);
    }

    /// <summary>压缩（回收未使用的动态扩展空间）。</summary>
    public static async Task<VhdxResult> CompactAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            return VhdxResult.Fail("文件不存在，可能已被移动或删除。");
        }

        if (!AnsiCodec.CanRepresent(path))
        {
            return VhdxResult.Fail("文件路径包含系统无法识别的字符。");
        }

        long beforeSize = new FileInfo(path).Length;
        bool wasMounted = IsLocked(path);
        bool remount = wasMounted;

        if (wasMounted)
        {
            DiskPartResult detach = await DiskPartRunner.RunAsync(
                new[] { $"select vdisk file={Quote(path)}", "detach vdisk" },
                cancellationToken).ConfigureAwait(false);

            for (int i = 0; i < 25 && IsLocked(path); i++)
            {
                await Task.Delay(400, cancellationToken).ConfigureAwait(false);
            }

            if (IsLocked(path))
            {
                return VhdxResult.Fail("压缩失败：无法卸载正在使用中的虚拟磁盘。" + detach.ErrorSummary, detach.Output);
            }
        }

        DiskPartResult result = await DiskPartRunner.RunAsync(
            new[]
            {
                $"select vdisk file={Quote(path)}",
                "attach vdisk readonly",
                "compact vdisk",
                "detach vdisk"
            },
            cancellationToken).ConfigureAwait(false);

        // 兜底：即使中途失败也尝试分离，避免残留只读挂载。
        if (IsLocked(path))
        {
            await DiskPartRunner.RunAsync(new[] { $"select vdisk file={Quote(path)}", "detach vdisk" }, cancellationToken).ConfigureAwait(false);
            for (int i = 0; i < 15 && IsLocked(path); i++)
            {
                await Task.Delay(300, cancellationToken).ConfigureAwait(false);
            }
        }

        long afterSize = File.Exists(path) ? new FileInfo(path).Length : beforeSize;
        long saved = Math.Max(0, beforeSize - afterSize);

        if (!result.Success)
        {
            return VhdxResult.Fail("压缩失败：" + result.ErrorSummary, result.Output);
        }

        string? drive = null;
        if (remount)
        {
            VhdxResult mount = await MountAsync(path, cancellationToken).ConfigureAwait(false);
            drive = mount.DriveLetter;
        }

        string message = saved > 0
            ? $"压缩完成，释放 {FormatUtil.Bytes(saved)}（{FormatUtil.Bytes(beforeSize)} → {FormatUtil.Bytes(afterSize)}）。"
            : "压缩完成，没有可回收的空间。";

        return VhdxResult.Ok(message, drive, result.Output);
    }

    /// <summary>删除虚拟磁盘文件（如已挂载会先卸载）。</summary>
    public static async Task<VhdxResult> DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            return VhdxResult.Ok("文件已不存在。");
        }

        if (IsLocked(path))
        {
            VhdxResult unmount = await UnmountAsync(path, cancellationToken).ConfigureAwait(false);
            if (!unmount.Success)
            {
                return VhdxResult.Fail("删除失败：虚拟磁盘正在使用中，且无法自动卸载。" + unmount.Message, unmount.Detail);
            }
        }

        for (int attempt = 0; attempt < 4; attempt++)
        {
            try
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
                return VhdxResult.Ok("已删除虚拟磁盘文件。");
            }
            catch (IOException) when (attempt < 3)
            {
                await Task.Delay(600, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return VhdxResult.Fail("删除失败：" + ex.Message);
            }
        }

        return VhdxResult.Fail("删除失败：文件仍被占用。");
    }

    /// <summary>
    /// 读取虚拟磁盘详情（类型、容量上限）。
    /// 容量优先直接解析 VHDX 文件结构，磁盘已挂载时同样可以读到。
    /// </summary>
    public static async Task<VhdxDetails?> GetDetailsAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        long? virtualSize = VhdxReader.TryReadVirtualSize(path);

        if (virtualSize is null)
        {
            // 不是标准 VHDX（或结构损坏）时退回 diskpart。
            return await GetDetailsFromDiskPartAsync(path, cancellationToken).ConfigureAwait(false);
        }

        // 磁盘已挂载时 diskpart 无法打开文件，此时类型未知（容量仍可读）。
        bool? isDynamic = IsLocked(path)
            ? null
            : await TryReadDynamicFromDiskPartAsync(path, cancellationToken).ConfigureAwait(false);

        return new VhdxDetails
        {
            TypeText = isDynamic switch
            {
                true => "动态扩展",
                false => "固定大小",
                _ => "—"
            },
            VirtualSizeText = FormatUtil.Bytes(virtualSize.Value),
            VirtualSizeBytes = virtualSize.Value,
            IsDynamic = isDynamic ?? true,
            IsDynamicKnown = isDynamic.HasValue
        };
    }

    /// <summary>兜底方案：通过 diskpart 读取详情。</summary>
    private static async Task<VhdxDetails?> GetDetailsFromDiskPartAsync(string path, CancellationToken cancellationToken)
    {
        if (!AnsiCodec.CanRepresent(path))
        {
            return null;
        }

        DiskPartResult result = await DiskPartRunner.RunAsync(
            new[] { $"select vdisk file={Quote(path)}", "detail vdisk" },
            cancellationToken).ConfigureAwait(false);

        if (!result.Success)
        {
            return null;
        }

        string typeText = "—";
        string virtualSize = "—";
        bool isDynamic = true;

        foreach (string rawLine in result.Output.Split('\n'))
        {
            string line = rawLine.Trim();

            if (line.Length == 0)
            {
                continue;
            }

            if (line.Contains("虚拟大小", StringComparison.OrdinalIgnoreCase)
                || line.Contains("irtual size", StringComparison.OrdinalIgnoreCase))
            {
                virtualSize = AfterColon(line);
            }
            else if (line.StartsWith("类型", StringComparison.OrdinalIgnoreCase)
                     || line.StartsWith("Type", StringComparison.OrdinalIgnoreCase))
            {
                typeText = AfterColon(line);
                isDynamic = !typeText.Contains("固定", StringComparison.OrdinalIgnoreCase)
                            && !typeText.Contains("fixed", StringComparison.OrdinalIgnoreCase);
            }
        }

        if (typeText == "—")
        {
            return null;
        }

        return new VhdxDetails
        {
            TypeText = typeText,
            VirtualSizeText = virtualSize,
            IsDynamic = isDynamic
        };
    }

    /// <summary>通过 diskpart 判断是否动态扩展；读不到返回 null。</summary>
    private static async Task<bool?> TryReadDynamicFromDiskPartAsync(string path, CancellationToken cancellationToken)
    {
        if (!AnsiCodec.CanRepresent(path))
        {
            return null;
        }

        DiskPartResult result = await DiskPartRunner.RunAsync(
            new[] { $"select vdisk file={Quote(path)}", "detail vdisk" },
            cancellationToken).ConfigureAwait(false);

        if (!result.Success)
        {
            return null;
        }

        foreach (string rawLine in result.Output.Split('\n'))
        {
            string line = rawLine.Trim();
            if (line.StartsWith("类型", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("Type", StringComparison.OrdinalIgnoreCase))
            {
                string value = AfterColon(line);
                return !value.Contains("固定", StringComparison.OrdinalIgnoreCase)
                       && !value.Contains("fixed", StringComparison.OrdinalIgnoreCase);
            }
        }

        return null;
    }

    /// <summary>文件是否被占用（已挂载时会被虚拟磁盘服务独占）。</summary>
    public static bool IsLocked(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
        catch
        {
            return true;
        }
    }

    /// <summary>当前所有本地磁盘盘符（不含末尾反斜杠），如 "C"、"D"。</summary>
    public static HashSet<string> GetDriveLetters()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                if (drive.DriveType is DriveType.Fixed or DriveType.Removable or DriveType.Network)
                {
                    set.Add(drive.Name.TrimEnd('\\', '/'));
                }
            }
        }
        catch
        {
            // 枚举失败时忽略。
        }

        return set;
    }

    private static async Task<string?> WaitForNewDriveAsync(HashSet<string> before, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            HashSet<string> now = GetDriveLetters();
            string? added = now.Except(before, StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (added is not null)
            {
                return added;
            }

            await Task.Delay(350).ConfigureAwait(false);
        }

        return null;
    }

    private static long GetFreeSpace(string directory)
    {
        try
        {
            return new DriveInfo(Path.GetPathRoot(directory) ?? "C:\\").AvailableFreeSpace;
        }
        catch
        {
            return 0;
        }
    }

    private static string AfterColon(string line)
    {
        int index = line.IndexOfAny(new[] { ':', '：' });
        if (index < 0 || index == line.Length - 1)
        {
            return "—";
        }

        string value = line[(index + 1)..].Trim();
        return string.IsNullOrEmpty(value) ? "—" : value;
    }

    private static string Quote(string value) => "\"" + value + "\"";

    private static string SanitizeLabel(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return "VHDX";
        }

        string cleaned = Regex.Replace(label, @"[""\\/:*?<>|\s]+", "_").Trim('_');
        if (cleaned.Length == 0)
        {
            cleaned = "VHDX";
        }

        return cleaned.Length > 24 ? cleaned[..24] : cleaned;
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // 忽略。
        }
    }
}

/// <summary>字节数格式化。</summary>
public static class FormatUtil
{
    public static string Bytes(long bytes)
    {
        if (bytes < 0)
        {
            return "—";
        }

        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double value = bytes;
        int unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        string format = unit == 0 ? "0" : "0.##";
        return value.ToString(format, CultureInfo.CurrentCulture) + " " + units[unit];
    }
}
