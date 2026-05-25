using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace SnappyDocsConvert.App.Services;

public sealed class AppStartupLogger
{
    private readonly object _gate = new();

    public string LogDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "kmb-file-tools",
        "logs");

    public string AppLogPath => Path.Combine(LogDirectory, "app.log");

    public string CrashLogPath => Path.Combine(LogDirectory, "crash.log");

    public void Initialize()
    {
        Directory.CreateDirectory(LogDirectory);
        Log("startup logger initialized");
        Log($"app={AppVersionInfo.AppName}");
        Log($"version={AppVersionInfo.Version}");
        Log($"os={Environment.OSVersion}");
        Log($"culture={CultureInfo.CurrentUICulture.Name}");
        Log($"baseDirectory={AppContext.BaseDirectory}");
        Log($"settingsPath={new AppSettingsService().SettingsPath}");
    }

    public void RegisterHandlers(Application app)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            WriteCrash(args.ExceptionObject as Exception ?? new Exception(args.ExceptionObject?.ToString() ?? "Unknown unhandled exception"));
        };

        app.DispatcherUnhandledException += (_, args) =>
        {
            WriteCrash(args.Exception);
            ShowCrashMessage();
            args.Handled = true;
            app.Shutdown(1);
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            WriteCrash(args.Exception);
            args.SetObserved();
        };
    }

    public void Log(string message)
    {
        lock (_gate)
        {
            Directory.CreateDirectory(LogDirectory);
            File.AppendAllText(AppLogPath, $"[{DateTimeOffset.Now:O}] {message}{Environment.NewLine}");
        }
    }

    public void WriteCrash(Exception exception)
    {
        lock (_gate)
        {
            Directory.CreateDirectory(LogDirectory);
            var text = $"""
                [{DateTimeOffset.Now:O}] fatal startup/application error
                app={AppVersionInfo.AppName}
                version={AppVersionInfo.Version}
                os={Environment.OSVersion}
                culture={CultureInfo.CurrentUICulture.Name}
                baseDirectory={AppContext.BaseDirectory}
                exception={exception}

                """;
            File.AppendAllText(CrashLogPath, text);
            File.AppendAllText(AppLogPath, text);
        }
    }

    public void ShowCrashMessage()
    {
        MessageBox.Show(
            $"kmb file tools failed to start. A crash log was written to:{Environment.NewLine}{CrashLogPath}",
            "kmb file tools startup error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
