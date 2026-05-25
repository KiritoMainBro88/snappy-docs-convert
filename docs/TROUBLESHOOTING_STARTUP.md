# Startup Troubleshooting

This guide is for cases where `kmb file tools` shows a loading cursor but no window appears, or exits before the main window is visible.

## Run From PowerShell

Open PowerShell in the extracted portable folder and run:

```powershell
.\SnappyDocsConvert.App.exe
```

Self-check mode verifies service composition without opening the UI:

```powershell
.\SnappyDocsConvert.App.exe --self-check
```

Self-check is not proof that the GUI can open. Release smoke should also run the GUI launch check:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\smoke-gui-launch.ps1 -ExePath .\artifacts\kmb-file-tools-portable-win-x64-<version>\SnappyDocsConvert.App.exe
```

## Local Logs

Startup diagnostics are written locally only:

```text
%LOCALAPPDATA%\kmb-file-tools\logs\app.log
%LOCALAPPDATA%\kmb-file-tools\logs\crash.log
```

The app logs startup milestones, app version, OS version, culture, base directory, and settings path. It does not upload logs and should not log document contents or secrets.

If the app fails during startup, it writes `crash.log` and shows a small message box with the log path.

## What To Send In A Bug Report

When reporting startup problems on GitHub or Discord, include:

- App version.
- Portable ZIP or installer EXE name.
- Windows version.
- Whether `--self-check` exits successfully.
- The latest `crash.log` contents.
- A screenshot of the error dialog if one appears.

Do not paste secrets, private document contents, tokens, or unrelated personal file paths.

## Runtime Requirement

The current portable package is framework-dependent. Windows x64 machines need the .NET 9 Desktop Runtime installed unless a future self-contained package is published.
