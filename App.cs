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

            var options = CommandLineOptions.Parse(e.Args);
            if (options.HasHelp)
            {
                MessageBox.Show(
                    GetHelpText(),
                    "AppSweep help",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                Shutdown(0);
                return;
            }

            if (options.HasExport)
            {
                var exported = await ExportProductsAsync(options.ExportPath).ConfigureAwait(true);
                Shutdown(exported ? 0 : 1);
                return;
            }

            if (options.HasRemove)
            {
                if (string.IsNullOrWhiteSpace(options.RemovePattern))
                {
                    MessageBox.Show(
                        "AppSweep --remove requires a program name or wildcard pattern.",
                        "AppSweep command-line error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    Shutdown(-1);
                    return;
                }

                var removed = await RemoveProductsAsync(options.RemovePattern).ConfigureAwait(true);
                Shutdown(removed ? 0 : 1);
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

    private async Task<bool> ExportProductsAsync(string? requestedPath)
    {
        var service = new InstalledProductService();
        StartupDiagnostics.Log("Export mode requested; loading installed products...");
        var products = await Task.Run(() => service.GetInstalledProducts()).ConfigureAwait(true);
        var exportPath = ResolveExportPath(requestedPath);

        StartupDiagnostics.Log($"Exporting {products.Count} products to {exportPath}");
        CsvExportService.Export(products, exportPath);
        StartupDiagnostics.Log($"Export complete: {exportPath}");
        return true;
    }

    private async Task<bool> RemoveProductsAsync(string pattern)
    {
        var service = new InstalledProductService();
        StartupDiagnostics.Log($"Remove mode requested for pattern: {pattern}");
        var products = await Task.Run(() => service.GetInstalledProducts()).ConfigureAwait(true);
        var matches = products.Where(product => ProductMatcher.Matches(pattern, product.Name)).ToArray();

        if (matches.Length == 0)
        {
            StartupDiagnostics.Log($"No installed products matched '{pattern}'.");
            return false;
        }

        StartupDiagnostics.Log($"Matched {matches.Length} installed product(s) for '{pattern}'.");

        var allSucceeded = true;
        foreach (var product in matches)
        {
            StartupDiagnostics.Log($"Removing {product.Name} ({product.ProductCode})...");
            var removed = await service.RemoveProductAsync(product, RemovalMethod.Auto, StartupDiagnostics.Log, CancellationToken.None).ConfigureAwait(true);
            allSucceeded &= removed;
        }

        StartupDiagnostics.Log(allSucceeded
            ? "Removal command completed successfully."
            : "Removal command completed with one or more failures.");
        return allSucceeded;
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

    private static string GetHelpText() =>
        "AppSweep command-line options:\n\n" +
        "  --help            Show this help text\n" +
        "  --export <path>   Export installed products to CSV\n" +
        "  --remove <name>   Remove installed products matching a name or wildcard pattern\n\n" +
        "Examples:\n" +
        "  AppSweep.exe --help\n" +
        "  AppSweep.exe --export out.csv\n" +
        "  AppSweep.exe --remove Adobe*";
}
