using Microsoft.Win32;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace AppSweep;

public sealed class InstalledProductService
{
    private static readonly Regex GuidRegex = new(@"^\{[0-9A-Fa-f-]+\}$", RegexOptions.Compiled);
    private static readonly Regex GuidInTextRegex = new(@"\{[0-9A-Fa-f-]+\}", RegexOptions.Compiled);

    public IReadOnlyList<InstalledProduct> GetInstalledProducts()
    {
        var products = new Dictionary<string, InstalledProduct>(StringComparer.OrdinalIgnoreCase);

        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using var uninstallRoot = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                if (uninstallRoot is null)
                {
                    continue;
                }

                foreach (var subKeyName in uninstallRoot.GetSubKeyNames())
                {
                    using var subKey = uninstallRoot.OpenSubKey(subKeyName);
                    if (subKey is null)
                    {
                        continue;
                    }

                    var productCode = NormalizeProductCode(subKeyName, subKey.GetValue("UninstallString")?.ToString());
                    var displayName = subKey.GetValue("DisplayName")?.ToString()?.Trim();
                    var uninstallString = subKey.GetValue("UninstallString")?.ToString() ?? string.Empty;
                    var isInstallerEntry = ReadRegistryInt(subKey, "WindowsInstaller") == 1 ||
                                           uninstallString.Contains("msiexec", StringComparison.OrdinalIgnoreCase);

                    if (!isInstallerEntry || string.IsNullOrWhiteSpace(productCode) || string.IsNullOrWhiteSpace(displayName))
                    {
                        continue;
                    }

                    products[productCode] = new InstalledProduct
                    {
                        ProductCode = productCode,
                        Name = displayName,
                        Version = subKey.GetValue("DisplayVersion")?.ToString()?.Trim() ?? "Unknown",
                        InstallDate = FormatInstallDate(subKey.GetValue("InstallDate")?.ToString()),
                        UninstallString = uninstallString
                    };
                }
            }
            catch
            {
                // Ignore one registry view and keep the other view's results.
            }
        }

        return products.Values
            .OrderBy(product => product.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(product => product.ProductCode, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<bool> UninstallProductAsync(
        string productCode,
        bool forceRemoval,
        Action<string> log,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(productCode) || productCode == "(Unknown)")
        {
            log("Cannot uninstall a product without a valid product code.");
            return false;
        }

        log($"Attempting to uninstall product with code: {productCode}");

        if (forceRemoval)
        {
            return ForceRemoveProduct(productCode, log);
        }

        var psi = new ProcessStartInfo
        {
            FileName = "msiexec.exe",
            Arguments = $"/x {productCode} /qn /norestart",
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        try
        {
            using var process = Process.Start(psi);
            if (process is null)
            {
                log("Failed to start msiexec.exe.");
                return false;
            }

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            if (process.ExitCode == 0)
            {
                log("Product successfully uninstalled.");
                return true;
            }

            log($"Uninstall failed with exit code: {process.ExitCode}");
            log("Try using Force Remove for this product.");
            return false;
        }
        catch (Exception ex)
        {
            log($"Error removing product: {ex.Message}");
            return false;
        }
    }

    private static bool ForceRemoveProduct(string productCode, Action<string> log)
    {
        log("Using force removal method...");

        var uninstallPaths = new[]
        {
            $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{productCode}",
            $@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\{productCode}"
        };

        var removed = false;
        foreach (var path in uninstallPaths)
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                var parentPath = Path.GetDirectoryName(path.Replace('/', '\\')) ?? string.Empty;
                using var uninstallRoot = baseKey.OpenSubKey(parentPath, writable: true);
                if (uninstallRoot is null)
                {
                    continue;
                }

                var keyName = Path.GetFileName(path);
                uninstallRoot.DeleteSubKeyTree(keyName, throwOnMissingSubKey: false);
                log($"Removed registry key: HKLM\\{path}");
                removed = true;
            }
            catch (Exception ex)
            {
                log($"Failed to remove registry key HKLM\\{path}: {ex.Message}");
            }
        }

        if (removed)
        {
            log("Force removal completed. You may need to restart your computer.");
            return true;
        }

        log("No registry keys were removed.");
        return false;
    }

    private static string NormalizeProductCode(string subKeyName, string? uninstallString)
    {
        if (GuidRegex.IsMatch(subKeyName))
        {
            return subKeyName;
        }

        var match = GuidInTextRegex.Match(uninstallString ?? string.Empty);
        return match.Success ? match.Value : "(Unknown)";
    }

    private static string FormatInstallDate(string? installDate)
    {
        if (string.IsNullOrWhiteSpace(installDate))
        {
            return "Unknown";
        }

        if (installDate.Length == 8 &&
            DateTime.TryParseExact(installDate, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var parsed))
        {
            return parsed.ToString("MM/dd/yyyy");
        }

        return installDate;
    }

    private static int? ReadRegistryInt(RegistryKey key, string name)
    {
        var value = key.GetValue(name);
        return value switch
        {
            int i => i,
            string s when int.TryParse(s, out var parsed) => parsed,
            _ => null
        };
    }
}
