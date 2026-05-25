using System.Runtime.InteropServices;
using System.Windows;
using System.IO;
using SnappyDocsConvert.App.Services;
using SnappyDocsConvert.App.ViewModels;

namespace SnappyDocsConvert.App;

public partial class App : Application
{
    private readonly AppStartupLogger _startupLogger = new();

    protected override async void OnStartup(StartupEventArgs e)
    {
        _startupLogger.Initialize();
        _startupLogger.RegisterHandlers(this);
        _startupLogger.Log("OnStartup begin");

        try
        {
            base.OnStartup(e);

            var factory = new AppServiceFactory();
            if (e.Args.Any(arg => string.Equals(arg, "--self-check", StringComparison.OrdinalIgnoreCase)))
            {
                _startupLogger.Log("self-check begin");
                NativeConsole.AttachToParent();
                var exitCode = await SelfCheckRunner.RunAsync(factory, Console.Out, CancellationToken.None);
                _startupLogger.Log($"self-check end exitCode={exitCode}");
                Shutdown(exitCode);
                return;
            }

            _startupLogger.Log("create main window");
            var window = new MainWindow
            {
                DataContext = new MainWindowViewModel(factory)
            };
            _startupLogger.Log("show main window");
            window.Show();
            _startupLogger.Log("OnStartup complete");
        }
        catch (Exception ex)
        {
            _startupLogger.WriteCrash(ex);
            _startupLogger.ShowCrashMessage();
            Shutdown(1);
        }
    }

    private static class NativeConsole
    {
        private const uint AttachParentProcess = 0xFFFFFFFF;

        public static void AttachToParent()
        {
            if (OperatingSystem.IsWindows())
            {
                AttachConsole(AttachParentProcess);
                var writer = new StreamWriter(Console.OpenStandardOutput())
                {
                    AutoFlush = true
                };
                Console.SetOut(writer);
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AttachConsole(uint dwProcessId);
    }
}
