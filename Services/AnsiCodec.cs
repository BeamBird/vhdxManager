using System.Runtime.InteropServices;

namespace VhdxManager.Services;

/// <summary>
/// diskpart 读写依赖系统 ANSI / OEM 代码页（中文系统为 936）。
/// .NET 默认不带非 Unicode 代码页，这里直接走 Win32 API 转换，避免额外依赖。
/// </summary>
internal static class AnsiCodec
{
    private static readonly uint AnsiCodePage = GetACP();
    private static readonly uint OemCodePage = GetOEMCP();

    [DllImport("kernel32.dll")]
    private static extern uint GetACP();

    [DllImport("kernel32.dll")]
    private static extern uint GetOEMCP();

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int WideCharToMultiByte(
        uint codePage, uint flags, string str, int charCount,
        [Out] byte[]? bytes, int byteCount, IntPtr defaultChar, IntPtr usedDefaultChar);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int MultiByteToWideChar(
        uint codePage, uint flags, [In] byte[] bytes, int byteCount,
        [Out] char[]? chars, int charCount);

    /// <summary>把字符串转换为系统 ANSI 编码字节（用于写 diskpart 脚本文件）。</summary>
    public static byte[] ToAnsi(string text)
    {
        return Convert(text, AnsiCodePage);
    }

    /// <summary>把 diskpart 输出的原始字节（OEM 代码页）解码为字符串。</summary>
    public static string FromOem(byte[] bytes) => Decode(bytes, OemCodePage);

    /// <summary>把 ANSI 字节解码为字符串（用于往返校验）。</summary>
    public static string FromAnsi(byte[] bytes) => Decode(bytes, AnsiCodePage);

    /// <summary>判断字符串能否被系统 ANSI 代码页完整表示（diskpart 脚本文件依赖该编码）。</summary>
    public static bool CanRepresent(string text) => FromAnsi(ToAnsi(text)) == text;

    private static string Decode(byte[] bytes, uint codePage)
    {
        if (bytes.Length == 0)
        {
            return string.Empty;
        }

        int length = MultiByteToWideChar(codePage, 0, bytes, bytes.Length, null, 0);
        if (length <= 0)
        {
            return string.Empty;
        }

        var buffer = new char[length];
        MultiByteToWideChar(codePage, 0, bytes, bytes.Length, buffer, length);
        return new string(buffer);
    }

    private static byte[] Convert(string text, uint codePage)
    {
        if (text.Length == 0)
        {
            return Array.Empty<byte>();
        }

        int length = WideCharToMultiByte(codePage, 0, text, text.Length, null, 0, IntPtr.Zero, IntPtr.Zero);
        if (length <= 0)
        {
            return Array.Empty<byte>();
        }

        var buffer = new byte[length];
        WideCharToMultiByte(codePage, 0, text, text.Length, buffer, length, IntPtr.Zero, IntPtr.Zero);
        return buffer;
    }
}
