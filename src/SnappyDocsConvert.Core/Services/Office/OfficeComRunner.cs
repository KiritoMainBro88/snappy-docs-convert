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
    private const int PpSaveAsPdf = 32;
    private const int XlTypePdf = 0;

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
                case OfficeAppKind.Excel:
                    ExportExcelWorkbook(inputPath, outputPdfPath);
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
            var errorMessage = OfficeComExceptionInterpreter.ResolveFailureMessage(ex);
            if (OfficeComExceptionInterpreter.TryDescribeDiagnostics(ex, out var diagnostic)
                && diagnostic is not null)
            {
                return new OfficeComExportResult(
                    false,
                    false,
                    stopwatch.Elapsed,
                    errorMessage,
                    new[] { diagnostic });
            }

            return OfficeComExportResult.Failed(errorMessage, stopwatch.Elapsed);
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

            // Macros never run during conversion; the pre-existing security state is restored
            // afterwards even when the open/export fails (Microsoft guidance for file opens).
            OfficeAutomationSecurity.RunWithForceDisabled(app, null, () =>
            {
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
            });
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

        // PowerPoint.Application is a shared class object: activation may ATTACH to a POWERPNT.EXE
        // the user has open instead of spawning one. Quitting an attached instance would close the
        // user's PowerPoint, so Quit happens only when exclusive ownership is proven: exactly one
        // fresh POWERPNT appeared after activation AND one of OUR windows resolves to that pid.
        var beforePids = SnapshotProcessIds(PowerPntProcessName);
        var quitInstance = false;

        try
        {
            app = CreateComObject(OfficeComProgIds.PowerPointApplication);
            quitInstance = ProveFreshPowerPointOwnership(app, beforePids);
            if (quitInstance)
            {
                // Fresh instance this process started: safe to configure.
                SetPropertyIfAvailable(app, "DisplayAlerts", 1);
            }

            // Security envelope restores the previous AutomationSecurity even on failure, so an
            // attached (possibly user-owned) instance is never left with modified macro security.
            OfficeAutomationSecurity.RunWithForceDisabled(app, null, () =>
            {
                presentations = GetProperty(app, "Presentations");
                presentation = InvokeMethod(
                    presentations,
                    "Open",
                    inputPath,
                    -1,
                    0,
                    0)
                    ?? throw new InvalidOperationException("PowerPoint returned no presentation instance.");

                // Prefer Presentation.SaveAs(path, ppSaveAsPDF=32): the late-bound IDispatch path for
                // ExportAsFixedFormat frequently throws DISP_E_TYPEMISMATCH (0x80020005) because its many
                // optional/enum parameters do not marshal cleanly without the typed PIA. SaveAs takes a
                // single well-typed enum and is the reliable PPT->PDF route. Fall back to
                // ExportAsFixedFormat only if SaveAs itself fails.
                try
                {
                    InvokeMethod(presentation, "SaveAs", outputPdfPath, PpSaveAsPdf);
                }
                catch (Exception saveAsError)
                {
                    if (!TryExportPowerPointAsFixedFormat(presentation, outputPdfPath))
                    {
                        throw new InvalidOperationException(
                            "PowerPoint could not export the presentation to PDF via SaveAs or ExportAsFixedFormat.",
                            saveAsError);
                    }
                }
            });
        }
        finally
        {
            TryInvoke(presentation, "Close");
            if (quitInstance)
            {
                TryInvoke(app, "Quit");
            }

            ReleaseComObject(presentation);
            ReleaseComObject(presentations);
            ReleaseComObject(app);
        }
    }

    [SupportedOSPlatform("windows")]
    private static void ExportExcelWorkbook(string inputPath, string outputPdfPath)
    {
        object? app = null;
        object? workbooks = null;
        object? workbook = null;

        try
        {
            app = CreateComObject(OfficeComProgIds.ExcelApplication);
            SetPropertyIfAvailable(app, "Visible", false);
            SetPropertyIfAvailable(app, "DisplayAlerts", false);
            SetPropertyIfAvailable(app, "ScreenUpdating", false);

            // Macros never run during conversion; the pre-existing state is restored after the
            // open/export, even when it fails. Excel activation always spawns a fresh process, so
            // the Quit below only ever ends this call's own instance.
            OfficeAutomationSecurity.RunWithForceDisabled(app, null, () =>
            {
                workbooks = GetProperty(app, "Workbooks");
                workbook = InvokeMethod(workbooks, "Open", inputPath)
                    ?? throw new InvalidOperationException("Excel returned no workbook instance.");

                InvokeMethod(workbook, "ExportAsFixedFormat", XlTypePdf, outputPdfPath);
            });
        }
        finally
        {
            TryInvoke(workbook, "Close", false);
            TryInvoke(app, "Quit");
            ReleaseComObject(workbook);
            ReleaseComObject(workbooks);
            ReleaseComObject(app);
        }
    }

    private const string PowerPntProcessName = "POWERPNT";
    private static readonly TimeSpan SpawnPollInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan SpawnPollBudget = TimeSpan.FromSeconds(5);

    /// <summary>PIDs of all POWERPNT processes right now (empty on any failure → attach semantics).</summary>
    private static HashSet<int> SnapshotProcessIds(string processName)
    {
        var snapshot = new HashSet<int>();
        try
        {
            foreach (var process in System.Diagnostics.Process.GetProcessesByName(processName))
            {
                using (process)
                {
                    snapshot.Add(process.Id);
                }
            }
        }
        catch
        {
            // Enumeration failure: treat as if everything could be pre-existing (no kill/quit).
        }

        return snapshot;
    }

    /// <summary>
    /// Proves this activation SPAWNED a fresh POWERPNT that is exclusively ours. Requires BOTH a
    /// single new process after activation and a window of OUR Application object resolving to that
    /// pid. Attach (no new process) or unresolvable evidence returns false — Quit must then never
    /// run, because the instance may be the user's.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static bool ProveFreshPowerPointOwnership(object app, HashSet<int> beforePids)
    {
        var deadline = DateTime.UtcNow + SpawnPollBudget;
        while (true)
        {
            var appWindowPid = TryResolvePowerPointAppWindowPid(app);

            // Our COM reference demonstrably points at a pre-existing instance: attached.
            if (appWindowPid is { } attachedPid && beforePids.Contains(attachedPid))
            {
                return false;
            }

            var freshCandidates = new List<int>();
            foreach (var process in System.Diagnostics.Process.GetProcessesByName(PowerPntProcessName))
            {
                using (process)
                {
                    if (!beforePids.Contains(process.Id) && !process.HasExited)
                    {
                        freshCandidates.Add(process.Id);
                    }
                }
            }

            if (freshCandidates.Count == 1
                && appWindowPid == freshCandidates[0])
            {
                return true;
            }

            if (freshCandidates.Count > 1)
            {
                // Ambiguous race: never quit on ambiguity.
                return false;
            }

            if (DateTime.UtcNow >= deadline)
            {
                // No fresh process within budget: attached, or spawn still unproven. Fail closed.
                return false;
            }

            Thread.Sleep(SpawnPollInterval);
        }
    }

    /// <summary>
    /// Resolves a pid from OUR Application's window(s): COM Window.HWND first, then PowerPoint's own
    /// PPTFrameClass desktop scan (modern builds removed the late-bound HWND property). Null when
    /// no frame is found — callers must treat null as "no proof".
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static int? TryResolvePowerPointAppWindowPid(object app)
    {
        try
        {
            var activeWindow = TryGetPropertyQuiet(app, "ActiveWindow");
            if (activeWindow is not null)
            {
                var hwnd = TryGetIntProperty(activeWindow, "HWND") ?? TryGetIntProperty(activeWindow, "Hwnd");
                if (hwnd is > 0 && TryPidFromWindow(hwnd.Value) is { } windowPid)
                {
                    return windowPid;
                }
            }

            var windows = TryGetPropertyQuiet(app, "Windows");
            if (windows is not null)
            {
                var count = TryGetIntProperty(windows, "Count");
                for (var i = 1; count.HasValue && i <= count.Value; i++)
                {
                    var window = windows.GetType().InvokeMember(
                        "Item", BindingFlags.InvokeMethod, null, windows, new object[] { i });
                    if (window is null)
                    {
                        continue;
                    }

                    var hwnd = TryGetIntProperty(window, "HWND") ?? TryGetIntProperty(window, "Hwnd");
                    if (hwnd is > 0 && TryPidFromWindow(hwnd.Value) is { } collectionPid)
                    {
                        return collectionPid;
                    }
                }
            }

            return PowerPntFrameClassWindowPids().FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private const string PowerPntFrameClass = "PPTFrameClass";

    /// <summary>Pids (owning process) of all visible top-level windows of the given class name.</summary>
    private static HashSet<int> PowerPntFrameClassWindowPids()
    {
        var pids = new HashSet<int>();
        try
        {
            EnumWindows(
                (hWnd, _) =>
                {
                    if (!IsWindowVisible(hWnd))
                    {
                        return true;
                    }

                    var buffer = new System.Text.StringBuilder(256);
                    if (GetClassName(hWnd, buffer, buffer.Capacity) > 0
                        && string.Equals(buffer.ToString(), PowerPntFrameClass, StringComparison.OrdinalIgnoreCase)
                        && GetWindowThreadProcessId(hWnd, out var pid)
                        && pid > 0
                        && pid <= int.MaxValue)
                    {
                        pids.Add((int)pid);
                    }

                    return true;
                },
                0);
        }
        catch
        {
            // Desktop enumeration failure: no proof (fail closed).
        }

        return pids;
    }

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

    private delegate bool EnumWindowsProc(nint hWnd, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(nint hWnd, System.Text.StringBuilder text, int count);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    private static int? TryPidFromWindow(int hwnd)
    {
        if (hwnd == 0)
        {
            return null;
        }

        try
        {
            return GetWindowThreadProcessId(hwnd, out var pid) && pid > 0 && pid <= int.MaxValue
                ? (int)pid
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static object? TryGetPropertyQuiet(object target, string name)
    {
        try
        {
            return target.GetType().InvokeMember(name, BindingFlags.GetProperty, null, target, null);
        }
        catch
        {
            return null;
        }
    }

    private static int? TryGetIntProperty(object target, string name)
    {
        try
        {
            var raw = target.GetType().InvokeMember(name, BindingFlags.GetProperty, null, target, null);
            return raw is null ? null : Convert.ToInt32(raw, System.Globalization.CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }
    }

    [SupportedOSPlatform("windows")]
    private static bool TryExportPowerPointAsFixedFormat(object presentation, string outputPdfPath)
    {
        try
        {
            InvokeMethod(presentation, "ExportAsFixedFormat", outputPdfPath, PpFixedFormatTypePdf);
            return true;
        }
        catch
        {
            return false;
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
