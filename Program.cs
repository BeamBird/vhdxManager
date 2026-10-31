using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using VhdxManager.UI;

namespace VhdxManager;

internal static class Program
{
    [DllImport("user32.dll")]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

    [STAThread]
    private static void Main()
    {
        // 确保高 DPI 下布局精确（app.manifest 已声明时此调用会自动失效）。
        try
        {
            SetProcessDpiAwarenessContext(new IntPtr(-4)); // PER_MONITOR_AWARE_V2
        }
        catch
        {
            // 忽略。
        }

        ApplicationConfiguration.Initialize();

        // 正常双击运行时 app.manifest 已保证管理员权限；
        // 这里兜底处理直接以 dotnet run / 未提升方式启动的情况。
        if (!IsAdministrator())
        {
            if (TryRelaunchElevated())
            {
                return;
            }

            MessageBox.Show(
                "虚拟磁盘的创建、挂载、压缩与删除都需要管理员权限。\r\n请右键程序，选择“以管理员身份运行”。",
                "需要管理员权限",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, args) => ReportCrash(args.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, args) => ReportCrash(args.ExceptionObject as Exception);

        Application.Run(new MainForm());
    }

    /// <summary>把未处理异常写入日志，方便排查（不打断用户操作）。</summary>
    private static void ReportCrash(Exception? exception)
    {
        try
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VhdxManager");
            Directory.CreateDirectory(folder);
            File.AppendAllText(
                Path.Combine(folder, "error.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\r\n{exception}\r\n\r\n");
        }
        catch
        {
            // 忽略。
        }

        if (exception is not null)
        {
            MessageBox.Show(exception.Message, "出错了", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static bool IsAdministrator()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static bool TryRelaunchElevated()
    {
        try
        {
            string exePath = Environment.ProcessPath ?? Application.ExecutablePath;
            if (string.IsNullOrEmpty(exePath) ||
                Path.GetFileNameWithoutExtension(exePath).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            {
                exePath = Application.ExecutablePath;
            }

            var psi = new ProcessStartInfo(exePath)
            {
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = AppContext.BaseDirectory
            };
            Process.Start(psi);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
