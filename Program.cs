using Microsoft.UI.Xaml;
using WinRT;

namespace AppSweep;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        StartupDiagnostics.HookGlobalHandlers();
        StartupDiagnostics.Log("Program.Main start");
        ComWrappersSupport.InitializeComWrappers();
        Application.Start(_ =>
        {
            StartupDiagnostics.Log("Application.Start callback");
            var app = new App();
        });
    }
}
