using System.IO;
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

    private async void App_Startup(object sender, StartupEventArgs e)
    {
        try
        {
            StartupDiagnostics.HookGlobalHandlers();
            StartupDiagnostics.Log("App startup");

            var exportRequest = ParseExportRequest(e.Args);
            if (exportRequest is not null)
            {
                await ExportProductsAsync(exportRequest).ConfigureAwait(true);
                Shutdown(0);
                return;
            }

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
            MessageBox.Show(
                $"AppSweep failed to start.\n\n{ex}",
                "AppSweep startup error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    private static ExportRequest? ParseExportRequest(IEnumerable<string> args)
    {
        var values = args.ToArray();
        for (var i = 0; i < values.Length; i++)
        {
            var current = values[i];
            if (!current.StartsWith("--export", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var outputPath = string.Empty;
            var equalsIndex = current.IndexOf('=');
            if (equalsIndex >= 0)
            {
                outputPath = current[(equalsIndex + 1)..].Trim();
            }
            else if (i + 1 < values.Length && !values[i + 1].StartsWith("-", StringComparison.Ordinal))
            {
                outputPath = values[i + 1].Trim();
            }

            return new ExportRequest(string.IsNullOrWhiteSpace(outputPath) ? null : outputPath);
        }

        return null;
    }

    private async Task ExportProductsAsync(ExportRequest request)
    {
        var service = new InstalledProductService();
        StartupDiagnostics.Log("Export mode requested; loading installed products...");
        var products = await Task.Run(() => service.GetInstalledProducts()).ConfigureAwait(true);
        var exportPath = ResolveExportPath(request.OutputPath);

        StartupDiagnostics.Log($"Exporting {products.Count} products to {exportPath}");
        CsvExportService.Export(products, exportPath);
        StartupDiagnostics.Log($"Export complete: {exportPath}");
    }

    private static string ResolveExportPath(string? requestedPath)
    {
        if (string.IsNullOrWhiteSpace(requestedPath))
        {
            return Path.Combine(
                Environment.CurrentDirectory,
                $"AppSweep-export-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
        }

        return Path.GetFullPath(requestedPath);
    }

    private sealed record ExportRequest(string? OutputPath);
}
