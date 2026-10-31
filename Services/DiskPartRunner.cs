using System.Diagnostics;
using System.Text;

namespace VhdxManager.Services;

/// <summary>diskpart 执行结果。</summary>
public sealed class DiskPartResult
{
    public DiskPartResult(bool success, int exitCode, string output)
    {
        Success = success;
        ExitCode = exitCode;
        Output = output;
    }

    public bool Success { get; }

    public int ExitCode { get; }

    public string Output { get; }

    /// <summary>失败时给用户看的简短说明。</summary>
    public string ErrorSummary
    {
        get
        {
            if (Success)
            {
                return string.Empty;
            }

            string? line = Output
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .FirstOrDefault(l => l.Contains("错误", StringComparison.OrdinalIgnoreCase)
                                     || l.Contains("error", StringComparison.OrdinalIgnoreCase)
                                     || l.Contains("失败", StringComparison.OrdinalIgnoreCase)
                                     || l.Contains("无法", StringComparison.OrdinalIgnoreCase));

            return string.IsNullOrWhiteSpace(line) ? "diskpart 执行失败。" : line;
        }
    }
}

/// <summary>通过 diskpart 脚本执行虚拟磁盘操作（Windows 全版本内置，无需 Hyper-V）。</summary>
public static class DiskPartRunner
{
    /// <summary>出现这些字样即认为 diskpart 报错（diskpart 失败时退出码并不可靠）。</summary>
    private static readonly string[] ErrorMarkers =
    {
        "错误", "失败", "无法", "拒绝访问",
        "error", "invalid", "failed", "denied", "not found"
    };

    /// <summary>diskpart 同一时刻只处理一个脚本，串行化避免并发冲突。</summary>
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static async Task<DiskPartResult> RunAsync(IEnumerable<string> commands, CancellationToken cancellationToken = default)
    {
        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await RunCoreAsync(commands, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Gate.Release();
        }
    }

    private static async Task<DiskPartResult> RunCoreAsync(IEnumerable<string> commands, CancellationToken cancellationToken)
    {
        string script = string.Join("\r\n", commands) + "\r\nexit\r\n";
        string scriptPath = Path.Combine(Path.GetTempPath(), "vhdxmgr_" + Guid.NewGuid().ToString("N") + ".txt");

        try
        {
            await File.WriteAllBytesAsync(scriptPath, AnsiCodec.ToAnsi(script), cancellationToken).ConfigureAwait(false);

            var psi = new ProcessStartInfo("diskpart.exe", $"/s \"{scriptPath}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                StandardOutputEncoding = Encoding.Latin1,
                StandardErrorEncoding = Encoding.Latin1
            };

            using Process process = Process.Start(psi)
                ?? throw new InvalidOperationException("无法启动 diskpart.exe。");

            try
            {
                process.StandardInput.Close();
            }
            catch
            {
                // 忽略：diskpart 不读取标准输入时关闭可能抛异常。
            }

            Task<string> outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            Task<string> errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(60));

            try
            {
                await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // 忽略。
                }

                throw new TimeoutException("diskpart 执行超时，操作可能仍在后台进行，请稍后在磁盘管理中确认状态。");
            }

            string rawAscii = await outputTask.ConfigureAwait(false) + await errorTask.ConfigureAwait(false);
            string output = AnsiCodec.FromOem(Encoding.Latin1.GetBytes(rawAscii));

            bool hasError = ErrorMarkers.Any(m => output.Contains(m, StringComparison.OrdinalIgnoreCase));
            bool success = process.ExitCode == 0 && !hasError;

            return new DiskPartResult(success, process.ExitCode, output.Trim());
        }
        finally
        {
            try
            {
                File.Delete(scriptPath);
            }
            catch
            {
                // 临时文件删除失败不影响主流程。
            }
        }
    }
}
