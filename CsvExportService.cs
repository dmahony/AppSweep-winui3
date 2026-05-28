using System.Globalization;
using System.IO;
using System.Text;

namespace AppSweep;

internal static class CsvExportService
{
    private static readonly string[] Headers =
    [
        "Product Code",
        "Name",
        "Version",
        "Install Date",
        "Uninstall String",
        "Install Source",
        "Local Package",
        "Source Status",
        "Registry Scope",
        "Details Tooltip"
    ];

    public static void Export(IEnumerable<InstalledProduct> products, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(products);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var writer = new StreamWriter(outputPath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        writer.WriteLine(string.Join(",", Headers.Select(EscapeCsvValue)));

        foreach (var product in products)
        {
            var fields = new[]
            {
                product.ProductCode,
                product.Name,
                product.Version,
                product.InstallDate,
                product.UninstallString,
                product.InstallSource,
                product.LocalPackage,
                product.SourceStatus,
                product.RegistryScope,
                product.DetailsTooltip
            };

            writer.WriteLine(string.Join(",", fields.Select(EscapeCsvValue)));
        }
    }

    private static string EscapeCsvValue(string? value)
    {
        var text = value ?? string.Empty;
        var needsQuotes = text.Contains(',') || text.Contains('"') || text.Contains('\r') || text.Contains('\n');
        if (needsQuotes)
        {
            text = '"' + text.Replace("\"", "\"\"") + '"';
        }

        return text;
    }
}
