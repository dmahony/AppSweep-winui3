using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace AppSweep;

internal static class StartupDiagnostics
{
    private const int AttachParentProcess = -1;

    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AppSweep",
        "startup.log");

    public static bool ConsoleAttached { get; private set; }

    public static void AttachConsoleIfAvailable()
    {
        try
        {
            ConsoleAttached = AttachConsole(AttachParentProcess);
        }
        catch
        {
            ConsoleAttached = false;
        }
    }

    public static void EnsureConsoleForCli()
    {
        if (ConsoleAttached)
        {
            return;
        }

        try
        {
            ConsoleAttached = AllocConsole();
        }
        catch
        {
            ConsoleAttached = false;
        }
    }

    public static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"{DateTime.Now:O} {message}{Environment.NewLine}");

            if (ConsoleAttached)
            {
                Console.WriteLine(message);
            }
        }
        catch
        {
            // Never let diagnostics crash startup.
        }
    }

    public static void LogException(string context, Exception ex)
    {
        Log($"{context}: {ex}");
    }

    public static void WriteStdOut(string message)
    {
        if (!ConsoleAttached)
        {
            return;
        }

        try
        {
            Console.Out.WriteLine(message);
        }
        catch
        {
            // Never let console writes crash startup.
        }
    }

    public static void WriteStdErr(string message)
    {
        if (!ConsoleAttached)
        {
            return;
        }

        try
        {
            Console.Error.WriteLine(message);
        }
        catch
        {
            // Never let console writes crash startup.
        }
    }

    public static void HookGlobalHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                LogException("UnhandledException", ex);
            }
            else
            {
                Log($"UnhandledException: {e.ExceptionObject}");
            }
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            LogException("UnobservedTaskException", e.Exception);
            e.SetObserved();
        };
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();
}
