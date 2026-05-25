using System.Runtime.InteropServices;
using System.Windows;
using System.IO;
using SnappyDocsConvert.App.Services;
using SnappyDocsConvert.App.ViewModels;

namespace SnappyDocsConvert.App;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var factory = new AppServiceFactory();
        if (e.Args.Any(arg => string.Equals(arg, "--self-check", StringComparison.OrdinalIgnoreCase)))
        {
            NativeConsole.AttachToParent();
            var exitCode = await SelfCheckRunner.RunAsync(factory, Console.Out, CancellationToken.None);
            Shutdown(exitCode);
            return;
        }

        var window = new MainWindow
        {
            DataContext = new MainWindowViewModel(factory)
        };
        window.Show();
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
