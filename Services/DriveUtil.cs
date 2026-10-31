namespace VhdxManager.Services;

/// <summary>盘符处理的统一入口，避免出现 "X:"、"X::" 之类的拼接错误。</summary>
public static class DriveUtil
{
    /// <summary>把 "X"、"X:"、"X:\" 统一成 "X"。空值返回空字符串。</summary>
    public static string NormalizeLetter(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string text = value.Trim().TrimEnd('\\', '/').TrimEnd(':').Trim();
        return text.Length == 1 ? text.ToUpperInvariant() : string.Empty;
    }

    /// <summary>"X" -> "X:\"；无法识别时返回空字符串。</summary>
    public static string ToRoot(string? letter)
    {
        string normalized = NormalizeLetter(letter);
        return normalized.Length == 0 ? string.Empty : normalized + ":\\";
    }

    /// <summary>用于界面显示的 "X:"；无法识别时返回空字符串。</summary>
    public static string ToDisplay(string? letter)
    {
        string normalized = NormalizeLetter(letter);
        return normalized.Length == 0 ? string.Empty : normalized + ":";
    }

    /// <summary>盘符对应的卷是否已就绪。</summary>
    public static bool IsReady(string? letter)
    {
        string root = ToRoot(letter);
        if (root.Length == 0)
        {
            return false;
        }

        try
        {
            return new DriveInfo(root).IsReady;
        }
        catch
        {
            return false;
        }
    }
}
