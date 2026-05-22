# AppSweep WinUI 3

A WinUI 3 rewrite of the original AppSweep.exe GUI.

## What it does

- Enumerates installed MSI products from the registry
- Lets you search by name, product code, version, or install date
- Supports selecting multiple products
- Runs standard MSI uninstall via `msiexec.exe`
- Supports force removal by deleting installer registry keys
- Writes activity into an in-app log

## Build

This repo is intended to be built on GitHub Actions on Windows.

Workflow:
- `.github/workflows/build.yml`

Artifacts are published from the workflow as a downloadable build output.

## Notes

- The app requests administrator privileges at launch.
- Force removal is registry cleanup only; it does not remove files or services.
