# AppSweep WPF

A Windows desktop app for removing MSI-based applications when the original installer source is missing.

## What it does

- Enumerates installed MSI products from the registry
- Shows product code, version, install date, and source status
- Lets you search by name, product code, version, date, or source state
- Supports multi-select removal
- Supports multiple removal methods:
  - Windows Installer API
  - `msiexec.exe`
  - Orphaned uninstall-entry cleanup
  - Auto mode that tries the safe uninstall paths first
- Writes activity into an in-app log
- Runs as a standard WPF desktop app

## Build

This project targets Windows and uses WPF on .NET 8.

Typical build command on Windows:

```powershell
dotnet restore AppSweep.csproj
dotnet build AppSweep.csproj -c Release
```

You can also build it on the current Linux environment with Windows targeting enabled:

```bash
dotnet build AppSweep.csproj -c Release
```

## Notes

- Registry cleanup is a last resort and only removes broken uninstall entries.
- The code logs detailed uninstall output to `%LOCALAPPDATA%\\AppSweep\\Logs` when `msiexec.exe` is used.
