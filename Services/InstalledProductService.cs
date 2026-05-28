using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace AppSweep;

public sealed class InstalledProductService
{
    private static readonly Regex GuidRegex = new(@"^\{[0-9A-Fa-f-]+\}$", RegexOptions.Compiled);
    private static readonly Regex GuidInTextRegex = new(@"\{[0-9A-Fa-f-]+\}", RegexOptions.Compiled);
    private static readonly Regex WindowsOperatingSystemNameRegex = new(@"(^|\b)(microsoft\s+)?windows\s+operating\s+system(\b|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly string[] UninstallRoots =
    {
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
    };

    public IReadOnlyList<InstalledProduct> GetInstalledProducts()
    {
        var products = new Dictionary<string, InstalledProduct>(StringComparer.OrdinalIgnoreCase);

        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        {
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                try
                {
                    using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                    foreach (var rootPath in UninstallRoots)
                    {
                        using var uninstallRoot = baseKey.OpenSubKey(rootPath);
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

                            if (!isInstallerEntry || string.IsNullOrWhiteSpace(productCode) || string.IsNullOrWhiteSpace(displayName) || IsWindowsOperatingSystemProduct(displayName))
                            {
                                continue;
                            }

                            var installSource = subKey.GetValue("InstallSource")?.ToString()?.Trim() ?? string.Empty;
                            var localPackage = subKey.GetValue("LocalPackage")?.ToString()?.Trim() ?? string.Empty;
                            var sourceStatus = DetermineSourceStatus(localPackage, installSource);
                            var registryScope = $"{hive}\\{view}";

                            products[productCode] = new InstalledProduct
                            {
                                ProductCode = productCode,
                                Name = displayName,
                                Version = subKey.GetValue("DisplayVersion")?.ToString()?.Trim() ?? "Unknown",
                                InstallDate = FormatInstallDate(subKey.GetValue("InstallDate")?.ToString()),
                                UninstallString = uninstallString,
                                InstallSource = installSource,
                                LocalPackage = localPackage,
                                SourceStatus = sourceStatus,
                                RegistryScope = registryScope
                            };
                        }
                    }
                }
                catch
                {
                    // Keep other registry views/hives if one path is inaccessible.
                }
            }
        }

        return products.Values
            .OrderBy(product => product.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(product => product.ProductCode, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<bool> RemoveProductAsync(
        InstalledProduct product,
        RemovalMethod method,
        Action<string> log,
        CancellationToken cancellationToken = default)
    {
        if (product is null)
        {
            log("No product was selected.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(product.ProductCode) || product.ProductCode == "(Unknown)")
        {
            log($"Cannot remove '{product.Name}' because it does not have a valid product code.");
            return false;
        }

        log($"Target: {product.Name} | {product.ProductCode} | Source: {product.SourceStatus}");

        return method switch
        {
            RemovalMethod.WindowsInstallerApi => TryWindowsInstallerApi(product, log),
            RemovalMethod.MsiExec => await TryMsiexecAsync(product, log, cancellationToken).ConfigureAwait(false),
            RemovalMethod.OrphanedRegistryCleanup => TryRemoveRegistryEntries(product, log),
            RemovalMethod.Auto => await TryAutoRemovalAsync(product, log, cancellationToken).ConfigureAwait(false),
            _ => false
        };
    }

    private async Task<bool> TryAutoRemovalAsync(
        InstalledProduct product,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        log("Auto removal order: Windows Installer API → msiexec → orphaned entry cleanup.");

        if (TryWindowsInstallerApi(product, log))
        {
            return true;
        }

        if (await TryMsiexecAsync(product, log, cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        log("Trying orphaned entry cleanup because the uninstall path could not complete.");
        return TryRemoveRegistryEntries(product, log);
    }

    private static bool TryWindowsInstallerApi(InstalledProduct product, Action<string> log)
    {
        log("Trying Windows Installer API uninstall...");

        try
        {
            var result = MsiConfigureProductEx(product.ProductCode, 0, InstallState.Absent, "REBOOT=ReallySuppress");
            if (result == 0 || result == 3010 || result == 1641)
            {
                log(result == 0
                    ? "Windows Installer API uninstall completed successfully."
                    : $"Windows Installer API uninstall completed with reboot-related code {result}.");
                return true;
            }

            log($"Windows Installer API uninstall failed with code {result}.");
            return false;
        }
        catch (Exception ex)
        {
            log($"Windows Installer API uninstall threw an exception: {ex.Message}");
            return false;
        }
    }

    private static async Task<bool> TryMsiexecAsync(
        InstalledProduct product,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        log("Trying msiexec.exe uninstall...");

        try
        {
            var logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AppSweep",
                "Logs");
            Directory.CreateDirectory(logDirectory);

            var logFile = Path.Combine(logDirectory, $"msiexec-{SanitizeFileName(product.ProductCode)}-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            var psi = new ProcessStartInfo
            {
                FileName = "msiexec.exe",
                Arguments = $"/x {product.ProductCode} /qn /norestart /l*v \"{logFile}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var process = Process.Start(psi);
            if (process is null)
            {
                log("Failed to start msiexec.exe.");
                return false;
            }

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            if (process.ExitCode is 0 or 3010 or 1641)
            {
                log(process.ExitCode == 0
                    ? $"msiexec uninstall completed successfully. Log: {logFile}"
                    : $"msiexec uninstall completed with reboot-related code {process.ExitCode}. Log: {logFile}");
                return true;
            }

            log($"msiexec uninstall failed with exit code {process.ExitCode}. Log: {logFile}");
            return false;
        }
        catch (Exception ex)
        {
            log($"msiexec uninstall threw an exception: {ex.Message}");
            return false;
        }
    }

    private static bool TryRemoveRegistryEntries(InstalledProduct product, Action<string> log)
    {
        log("Trying orphaned registry entry cleanup...");

        var removedAny = false;
        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        {
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                try
                {
                    using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                    foreach (var rootPath in UninstallRoots)
                    {
                        using var uninstallRoot = baseKey.OpenSubKey(rootPath, writable: true);
                        if (uninstallRoot is null)
                        {
                            continue;
                        }

                        if (TryDeleteSubKey(uninstallRoot, product.ProductCode))
                        {
                            removedAny = true;
                            log($"Removed registry entry: {hive}\\{view}\\{rootPath}\\{product.ProductCode}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    log($"Registry cleanup failed for {hive}\\{view}: {ex.Message}");
                }
            }
        }

        if (!removedAny)
        {
            log("No uninstall registry entries were removed.");
            return false;
        }

        log("Registry cleanup completed. Refresh the list to confirm the entry is gone.");
        return true;
    }

    private static bool TryDeleteSubKey(RegistryKey parentKey, string subKeyName)
    {
        try
        {
            parentKey.DeleteSubKeyTree(subKeyName);
            return true;
        }
        catch
        {
            return false;
        }
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

    private static bool IsWindowsOperatingSystemProduct(string displayName)
    {
        return WindowsOperatingSystemNameRegex.IsMatch(displayName)
               || (displayName.Contains("Microsoft Windows", StringComparison.OrdinalIgnoreCase)
                   && displayName.Contains("Operating System", StringComparison.OrdinalIgnoreCase));
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

    private static string DetermineSourceStatus(string localPackage, string installSource)
    {
        if (!string.IsNullOrWhiteSpace(localPackage) && File.Exists(localPackage))
        {
            return "Cached package available";
        }

        if (!string.IsNullOrWhiteSpace(installSource) &&
            (Directory.Exists(installSource) || File.Exists(installSource)))
        {
            return "Source path available";
        }

        if (!string.IsNullOrWhiteSpace(localPackage) || !string.IsNullOrWhiteSpace(installSource))
        {
            return "Source missing";
        }

        return "Unknown";
    }

    private static int ReadRegistryInt(RegistryKey key, string name)
    {
        var value = key.GetValue(name);
        return value switch
        {
            int i => i,
            string s when int.TryParse(s, out var parsed) => parsed,
            _ => 0
        };
    }

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var chars = value.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray();
        return new string(chars);
    }

    [DllImport("msi.dll", CharSet = CharSet.Unicode)]
    private static extern uint MsiConfigureProductEx(string szProduct, int iInstallLevel, InstallState eInstallState, string? szCommandLine);

    private enum InstallState : int
    {
        Absent = 2
    }
}
