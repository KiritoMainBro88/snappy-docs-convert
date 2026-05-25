using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using SnappyDocsConvert.Core.Models;

namespace SnappyDocsConvert.Core.Services.Office;

public sealed class OfficeComRunner : IOfficeComRunner
{
    private const int WdExportFormatPdf = 17;
    private const int PpFixedFormatTypePdf = 2;

    public async Task<OfficeComExportResult> ExportToPdfAsync(
        OfficeAppKind appKind,
        string inputPath,
        string outputPdfPath,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            return OfficeComExportResult.Failed(
                "Microsoft Office COM automation is only supported on Windows.",
                TimeSpan.Zero);
        }

        var stopwatch = Stopwatch.StartNew();
        var exportTask = RunOnStaThreadAsync(() =>
        {
#pragma warning disable CA1416
            switch (appKind)
            {
                case OfficeAppKind.Word:
                    ExportWordDocument(inputPath, outputPdfPath);
                    break;
                case OfficeAppKind.PowerPoint:
                    ExportPowerPointPresentation(inputPath, outputPdfPath);
                    break;
                default:
                    throw new NotSupportedException($"Unsupported Office app kind: {appKind}");
            }
#pragma warning restore CA1416
        });

        try
        {
            await exportTask.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            return OfficeComExportResult.Succeeded(stopwatch.Elapsed);
        }
        catch (TimeoutException)
        {
            ObserveFault(exportTask);
            stopwatch.Stop();
            return OfficeComExportResult.Timeout(stopwatch.Elapsed);
        }
        catch (OperationCanceledException)
        {
            ObserveFault(exportTask);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return OfficeComExportResult.Failed(ex.Message, stopwatch.Elapsed);
        }
    }

    [SupportedOSPlatform("windows")]
    private static Task RunOnStaThreadAsync(Action action)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                action();
                tcs.TrySetResult();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        thread.IsBackground = true;
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        return tcs.Task;
    }

    [SupportedOSPlatform("windows")]
    private static void ExportWordDocument(string inputPath, string outputPdfPath)
    {
        object? app = null;
        object? documents = null;
        object? document = null;

        try
        {
            app = CreateComObject(OfficeComProgIds.WordApplication);
            SetPropertyIfAvailable(app, "Visible", false);
            SetPropertyIfAvailable(app, "DisplayAlerts", 0);
            SetPropertyIfAvailable(app, "AutomationSecurity", 3);

            documents = GetProperty(app, "Documents");
            document = InvokeMethod(
                documents,
                "Open",
                inputPath,
                false,
                true,
                false,
                Missing.Value,
                Missing.Value,
                Missing.Value,
                Missing.Value,
                Missing.Value,
                Missing.Value,
                Missing.Value,
                false,
                Missing.Value,
                Missing.Value,
                true,
                Missing.Value)
                ?? throw new InvalidOperationException("Word returned no document instance.");

            InvokeMethod(document, "ExportAsFixedFormat", outputPdfPath, WdExportFormatPdf);
        }
        finally
        {
            TryInvoke(document, "Close", false);
            TryInvoke(app, "Quit", false);
            ReleaseComObject(document);
            ReleaseComObject(documents);
            ReleaseComObject(app);
        }
    }

    [SupportedOSPlatform("windows")]
    private static void ExportPowerPointPresentation(string inputPath, string outputPdfPath)
    {
        object? app = null;
        object? presentations = null;
        object? presentation = null;

        try
        {
            app = CreateComObject(OfficeComProgIds.PowerPointApplication);
            SetPropertyIfAvailable(app, "DisplayAlerts", 1);

            presentations = GetProperty(app, "Presentations");
            presentation = InvokeMethod(
                presentations,
                "Open",
                inputPath,
                -1,
                0,
                0)
                ?? throw new InvalidOperationException("PowerPoint returned no presentation instance.");

            InvokeMethod(presentation, "ExportAsFixedFormat", outputPdfPath, PpFixedFormatTypePdf);
        }
        finally
        {
            TryInvoke(presentation, "Close");
            TryInvoke(app, "Quit");
            ReleaseComObject(presentation);
            ReleaseComObject(presentations);
            ReleaseComObject(app);
        }
    }

    [SupportedOSPlatform("windows")]
    private static object CreateComObject(string progId)
    {
        var type = Type.GetTypeFromProgID(progId)
            ?? throw new InvalidOperationException($"{progId} ProgID not found.");
        return Activator.CreateInstance(type)
            ?? throw new InvalidOperationException($"Could not create {progId} COM instance.");
    }

    private static object GetProperty(object target, string name)
        => target
            .GetType()
            .InvokeMember(name, BindingFlags.GetProperty, null, target, null)
            ?? throw new InvalidOperationException($"COM property '{name}' returned null.");

    private static object? InvokeMethod(object target, string name, params object?[] args)
        => target
            .GetType()
            .InvokeMember(name, BindingFlags.InvokeMethod, null, target, args);

    private static void TryInvoke(object? target, string name, params object?[] args)
    {
        if (target is null)
        {
            return;
        }

        try
        {
            InvokeMethod(target, name, args);
        }
        catch
        {
            // Best-effort COM cleanup. Original export failure stays authoritative.
        }
    }

    private static void SetPropertyIfAvailable(object target, string name, object value)
    {
        try
        {
            target.GetType().InvokeMember(
                name,
                BindingFlags.SetProperty,
                null,
                target,
                new[] { value });
        }
        catch
        {
            // Some Office versions omit optional properties; continue with core export.
        }
    }

    [SupportedOSPlatform("windows")]
    private static void ReleaseComObject(object? comObject)
    {
        if (comObject is null || !Marshal.IsComObject(comObject))
        {
            return;
        }

        Marshal.FinalReleaseComObject(comObject);
    }

    private static void ObserveFault(Task task)
        => _ = task.ContinueWith(
            completed => _ = completed.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
}
