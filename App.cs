using Microsoft.UI.Xaml;

namespace AppSweep;

public sealed class App : Application
{
    private Window? _window;

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            StartupDiagnostics.Log("App.OnLaunched");
            StartupDiagnostics.Log("Creating main window");
            _window = new MainWindow();
            StartupDiagnostics.Log("Main window constructed");
            _window.Activate();
            StartupDiagnostics.Log("Main window activated");
        }
        catch (Exception ex)
        {
            StartupDiagnostics.LogException("OnLaunched failure", ex);
            throw;
        }
    }
}
