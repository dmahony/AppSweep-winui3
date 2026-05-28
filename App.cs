using System.Windows;

namespace AppSweep;

public partial class App : Application
{
    private MainWindow? _window;

    public App()
    {
        InitializeComponent();
        DispatcherUnhandledException += (_, e) =>
        {
            StartupDiagnostics.LogException("Application.DispatcherUnhandledException", e.Exception);
            e.Handled = true;
        };
    }

    private void App_Startup(object sender, StartupEventArgs e)
    {
        try
        {
            StartupDiagnostics.HookGlobalHandlers();
            StartupDiagnostics.Log("App startup");
            StartupDiagnostics.Log("Creating main window...");
            _window = new MainWindow();
            MainWindow = _window;
            StartupDiagnostics.Log("Showing main window...");
            _window.Show();
            StartupDiagnostics.Log("Main window shown");
        }
        catch (Exception ex)
        {
            StartupDiagnostics.LogException("App startup failure", ex);
            throw;
        }
    }
}
