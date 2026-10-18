using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using SnappyDocsConvert.Core.Models;
using SysProcess = System.Diagnostics.Process;

namespace SnappyDocsConvert.Core.Services.Office;

/// <summary>
/// Real COM-backed Office app factory. Creates a Word or PowerPoint Application that stays open across
/// many exports within a batch. All members must be called on the owning STA thread.
///
/// Not unit-tested (requires installed Office); exercised manually on a machine with Office. The
/// reuse/timeout/dispose policy that surrounds it lives in <see cref="OfficeComSession"/> and is
/// covered by tests through fakes.
/// </summary>
public sealed class ComOfficeAppFactory : IOfficeAppFactory
{
    public IOfficeAppHandle Create(OfficeAppKind kind)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Microsoft Office COM automation is only supported on Windows.");
        }

#pragma warning disable CA1416
        return new ComOfficeAppHandle(kind);
#pragma warning restore CA1416
    }
}

[SupportedOSPlatform("windows")]
internal sealed class ComOfficeAppHandle : IOfficeAppHandle
{
    private const int WdExportFormatPdf = 17;
    private const int PpFixedFormatTypePdf = 2;
    private const int PpSaveAsPdf = 32;
    private const int XlTypePdf = 0;
    private const int WdFormatDocumentDefault = 16; // .docx

    private object? _app;
    private OfficeProcessIdentity? _identity;
    private bool _ownedExclusively;

    /// <summary>AutomationSecurity value observed before this handle first changed it (restore fallback).</summary>
    private int? _automationSecurityAtLaunch;

    public ComOfficeAppHandle(OfficeAppKind kind)
    {
        Kind = kind;
        object app;
        if (kind == OfficeAppKind.Word)
        {
            app = CreateWordApp();
        }
        else if (kind == OfficeAppKind.PowerPoint)
        {
            app = CreatePowerPointApp();
        }
        else if (kind == OfficeAppKind.Excel)
        {
            app = CreateExcelApp();
        }
        else
        {
            throw new NotSupportedException($"Unsupported Office app kind: {kind}");
        }

        _app = app;
        ProcessId = _identity?.ProcessId ?? 0;
    }

    public OfficeAppKind Kind { get; }

    public int ProcessId { get; private set; }

    public OfficeProcessIdentity? Identity => _identity;

    public bool OwnedExclusively => _ownedExclusively && _identity is not null;

    public void Export(string inputPath, string outputPdfPath)
    {
        switch (Kind)
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
                throw new NotSupportedException($"Unsupported Office app kind: {Kind}");
        }
    }

    private object CreateWordApp()
    {
        const string processName = WordProcessName;
        var beforePids = SnapshotOfficePids(processName);

        var app = CreateComObject(OfficeComProgIds.WordApplication);
        SetPropertyIfAvailable(app, "Visible", false);
        SetPropertyIfAvailable(app, "DisplayAlerts", 0);
        _automationSecurityAtLaunch = TryGetIntValue(app, "AutomationSecurity");
        SetPropertyIfAvailable(app, "AutomationSecurity", 3);

        // Ownership proof: Word does not expose Application.Hwnd, but Word.Window does
        // (Microsoft docs: Word.Window.Hwnd). A hidden blank document creates that window; the
        // window's PID belongs to the Application object this process holds. Corroborate with the
        // spawn-diff below: window pid must equal the single new process, otherwise ownership stays
        // unproven (fail closed: no kill, no Quit).
        _identity = null;
        _ownedExclusively = false;
        try
        {
            object? documents = null;
            object? document = null;
            try
            {
                documents = GetProperty(app, "Documents");
                document = InvokeMethod(documents, "Add");
                if (document is not null)
                {
                    // Word without an open document can briefly have no ActiveWindow: missing
                    // values stay null and identity stays unproven (fail closed).
                    var window = TryGetPropertyValue(app, "ActiveWindow");
                    var hwnd = window is null ? null : TryGetIntValue(window, "Hwnd");
                    if (hwnd is > 0 && ResolvePidFromHwnd(hwnd.Value) is { } hwndPid)
                    {
                        var spawnedWord = TryFindSpawnedProcess(beforePids, processName);
                        if (spawnedWord is not null
                            && spawnedWord.ProcessId == hwndPid
                            && CaptureIdentity(hwndPid, WordProcessName) is { } wordIdentity)
                        {
                            _identity = wordIdentity;
                            _ownedExclusively = true;
                        }
                    }
                }
            }
            finally
            {
                TryInvoke(document, "Close", false);
                ReleaseComObject(document);
                ReleaseComObject(documents);
            }
        }
        catch
        {
            // Could not resolve the audit window. Fail closed: no identity, no watchdog kill.
            _identity = null;
            _ownedExclusively = false;
        }

        return app;
    }

    private object CreatePowerPointApp()
    {
        // PowerPoint semantics (documented by Microsoft): the PowerPoint.Application class object is
        // shared. Activation may ATTACH to a POWERPNT.EXE the user already has open (their window,
        // their unsaved documents) instead of spawning a fresh one.
        //
        // Ownership policy (fail closed): a kill requires EXCLUSIVE ownership, proven by BOTH
        //   (a) snapshot-diff: exactly one new POWERPNT process appeared after activation, and
        //   (b) window proof: a Window.HWND of OUR Application object resolving to that same pid.
        // Diff alone never authorizes a kill (the new process could be the user's concurrent
        // launch). When ownership is unproven: no kill, no Quit, and user-facing state
        // (DisplayAlerts / AutomationSecurity) is not modified.
        const string processName = PowerPntProcessName;
        var beforePids = SnapshotOfficePids(processName);

        var app = CreateComObject(OfficeComProgIds.PowerPointApplication);

        // Ownership proof must converge: exactly one spawned candidate AND one of OUR window handles
        // resolving to that same pid. Poll both together because a freshly starting POWERPNT.EXE may
        // register its windows slightly later than process creation. A window matching a PRE-EXISTING
        // pid is immediate attach proof (fail closed, no kill/quit).
        // A fresh automation instance exposes no windows, so when exactly ONE fresh candidate exists
        // this factory creates a MINIMIZED throwaway windowed presentation, reads its HWND, and
        // closes it. That brief minimized window is what makes a later watchdog kill provable;
        // ambiguity still disables kill/quit, and a possibly shared instance is never touched.
        _identity = null;
        _ownedExclusively = false;
        var attachDeadline = DateTime.UtcNow + AttachProbeBudget;
        var deadline = DateTime.UtcNow + SpawnPollBudget * 2;
        var attach = false;
        while (true)
        {
            var candidates = CollectSpawnedCandidates(beforePids, processName);
            if (candidates.Count > 1)
            {
                // Ambiguous race: never kill/quit on ambiguity.
                break;
            }

            if (candidates.Count == 0)
            {
                // Fast attach path: every PowerPoint frame on the desktop demonstrably predates
                // activation, so our reference must belong to a pre-existing instance.
                var frames = EnumFrameClassPids(PowerPntFrameClass);
                if (frames.Count > 0 && frames.All(beforePids.Contains))
                {
                    attach = true;
                    break;
                }

                if (DateTime.UtcNow >= attachDeadline)
                {
                    // No fresh process registered within the attach probe window: attached.
                    attach = true;
                    break;
                }

                Thread.Sleep(SpawnPollInterval);
                continue;
            }

            var spawned = candidates[0];
            var windowPid = TryGetPowerPointWindowPid(app);
            if (windowPid != spawned.ProcessId)
            {
                // Fresh instances are window-less: give it a throwaway minimized frame, then the
                // class scan must contain the candidate's own pid for the proof to count.
                ProveOwnershipWithTempWindow(app);
                windowPid = EnumFrameClassPids(PowerPntFrameClass).Contains(spawned.ProcessId)
                    ? spawned.ProcessId
                    : TryGetPowerPointWindowPid(app);
            }

            if (windowPid == spawned.ProcessId
                && CaptureIdentity(spawned.ProcessId, processName) is { } identity)
            {
                // Both evidence paths converge on the same fresh process: exclusivity proven.
                _ownedExclusively = true;
                _identity = identity;
                SetPropertyIfAvailable(app, "DisplayAlerts", 1); // ppAlertsNone
                _automationSecurityAtLaunch = TryGetIntValue(app, "AutomationSecurity");
                SetPropertyIfAvailable(app, "AutomationSecurity", 3);
                break;
            }

            if (DateTime.UtcNow >= deadline)
            {
                // Window proof never converged: fail closed, no kill/quit.
                break;
            }

            Thread.Sleep(SpawnPollInterval);
        }

        if (attach || !_ownedExclusively)
        {
            // Attached or unproven: never touch user-facing state on an instance we may share.
            _automationSecurityAtLaunch = null;
        }

        return app;
    }

    /// <summary>
    /// Creates a MINIMIZED throwaway windowed presentation, then resolves OUR pid from its window
    /// (COM window API first, PPTFrameClass desktop scan as fallback). Only called when exactly one
    /// fresh candidate exists (never on a possibly-shared instance). Returns the resolved pid, or
    /// null when no window/HWND is available.
    /// </summary>
    private static int? ProveOwnershipWithTempWindow(object app)
    {
        object? presentations = null;
        object? presentation = null;
        try
        {
            // Minimize first so the brief window never steals the user's focus.
            SetPropertyIfAvailable(app, "WindowState", 2); // ppWindowMinimized
            presentations = GetProperty(app, "Presentations");
            presentation = InvokeMethod(presentations, "Add", 1); // WithWindow = msoTrue

            // Frame registration can lag a moment behind the COM call; poll briefly.
            var settle = DateTime.UtcNow + TimeSpan.FromSeconds(3);
            int? pid;
            do
            {
                pid = TryGetPowerPointWindowPid(app);
                if (pid is not null)
                {
                    return pid;
                }

                Thread.Sleep(SpawnPollInterval);
            }
            while (DateTime.UtcNow < settle);

            return null;
        }
        catch
        {
            return null;
        }
        finally
        {
            TryInvoke(presentation, "Close");
            ReleaseComObject(presentation);
            ReleaseComObject(presentations);
        }
    }

    private object CreateExcelApp()
    {
        const string processName = ExcelProcessName;
        var beforePids = SnapshotOfficePids(processName);

        var app = CreateComObject(OfficeComProgIds.ExcelApplication);
        SetPropertyIfAvailable(app, "Visible", false);
        SetPropertyIfAvailable(app, "DisplayAlerts", false);
        SetPropertyIfAvailable(app, "ScreenUpdating", false);
        _automationSecurityAtLaunch = TryGetIntValue(app, "AutomationSecurity");
        SetPropertyIfAvailable(app, "AutomationSecurity", 3);

        // Ownership proof: Excel exposes Application.Hwnd whose thread belongs to the Application
        // object this process holds. Spawn-diff corroborates: window pid must equal the single new
        // process (a failed snapshot or a concurrent user launch yields no conclusive match and
        // disables kill/quit instead of guessing).
        _identity = null;
        _ownedExclusively = false;
        var excelHwnd = TryGetIntValue(app, "Hwnd");
        var spawnedExcel = TryFindSpawnedProcess(beforePids, processName);
        if (excelHwnd is > 0
            && ResolvePidFromHwnd(excelHwnd.Value) is { } excelPid
            && spawnedExcel is not null
            && spawnedExcel.ProcessId == excelPid
            && CaptureIdentity(excelPid, processName) is { } excelIdentity)
        {
            _ownedExclusively = true;
            _identity = excelIdentity;
        }

        return app;
    }

    private void ExportWordDocument(string inputPath, string outputPdfPath)
    {
        var app = _app ?? throw new InvalidOperationException("Word application is not available.");
        object? documents = null;
        object? document = null;
        try
        {
            OfficeAutomationSecurity.RunWithForceDisabled(app, _automationSecurityAtLaunch, () =>
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
            ReleaseComObject(document);
            ReleaseComObject(documents);
        }
    }

    private void ExportPowerPointPresentation(string inputPath, string outputPdfPath)
    {
        var app = _app ?? throw new InvalidOperationException("PowerPoint application is not available.");
        object? presentations = null;
        object? presentation = null;
        try
        {
            OfficeAutomationSecurity.RunWithForceDisabled(app, _automationSecurityAtLaunch, () =>
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

                // SaveAs(ppSaveAsPDF=32) avoids the DISP_E_TYPEMISMATCH that late-bound ExportAsFixedFormat
                // throws; fall back to ExportAsFixedFormat only if SaveAs fails.
                try
                {
                    InvokeMethod(presentation, "SaveAs", outputPdfPath, PpSaveAsPdf);
                }
                catch (Exception saveAsError)
                {
                    if (!TryExportPresentationAsFixedFormat(presentation, outputPdfPath))
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
            ReleaseComObject(presentation);
            ReleaseComObject(presentations);
        }
    }

    private void ExportExcelWorkbook(string inputPath, string outputPdfPath)
    {
        var app = _app ?? throw new InvalidOperationException("Excel application is not available.");
        object? workbooks = null;
        object? workbook = null;
        try
        {
            OfficeAutomationSecurity.RunWithForceDisabled(app, _automationSecurityAtLaunch, () =>
            {
                workbooks = GetProperty(app, "Workbooks");
                workbook = InvokeMethod(workbooks, "Open", inputPath)
                    ?? throw new InvalidOperationException("Excel returned no workbook instance.");

                // Workbook.ExportAsFixedFormat(xlTypePDF=0, path).
                InvokeMethod(workbook, "ExportAsFixedFormat", XlTypePdf, outputPdfPath);
            });
        }
        finally
        {
            TryInvoke(workbook, "Close", false);
            ReleaseComObject(workbook);
            ReleaseComObject(workbooks);
        }
    }

    private static bool TryExportPresentationAsFixedFormat(object presentation, string outputPdfPath)
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

    public void Quit()
    {
        if (_app is null)
        {
            return;
        }

        // Ownership policy: quit an instance only when exclusive ownership was proven. An attached
        // or unproven PowerPoint (or any unproven Office app) may be the user's — quitting would
        // close their window/package. Restore the user's security state first, then release only.
        if (!_ownedExclusively)
        {
            RestoreAutomationSecurity();
            ReleaseComObject(_app);
            _app = null;
            return;
        }

        RestoreAutomationSecurity();
        TryInvoke(_app, "Quit");
        ReleaseComObject(_app);
        _app = null;
    }

    /// <summary>Best-effort restore of the AutomationSecurity state changed at launch/per-file.</summary>
    private void RestoreAutomationSecurity()
    {
        if (_app is null || !_automationSecurityAtLaunch.HasValue)
        {
            return;
        }

        try
        {
            var current = TryGetIntValue(_app, "AutomationSecurity");
            if (current.HasValue && current.Value != _automationSecurityAtLaunch.Value)
            {
                SetPropertyIfAvailable(_app, "AutomationSecurity", _automationSecurityAtLaunch.Value);
            }
        }
        catch
        {
            // Property may be gone once Office is quitting; best-effort.
        }
    }

    public void ConvertFromPdf(string inputPdfPath, string outputPath)
    {
        if (Kind != OfficeAppKind.Word)
        {
            throw new NotSupportedException($"PDF import is only supported by Word, not {Kind}.");
        }

        var app = _app ?? throw new InvalidOperationException("Word application is not available.");
        object? documents = null;
        object? document = null;
        try
        {
            OfficeAutomationSecurity.RunWithForceDisabled(app, _automationSecurityAtLaunch, () =>
            {
                documents = GetProperty(app, "Documents");
                // Word 2013+ opens a PDF and reflows it into an editable document. Open with
                // ConfirmConversions=false so no dialog blocks automation.
                document = InvokeMethod(
                    documents,
                    "Open",
                    inputPdfPath,
                    false,   // ConfirmConversions
                    true,    // ReadOnly
                    false,   // AddToRecentFiles
                    Missing.Value, Missing.Value, Missing.Value, Missing.Value, Missing.Value,
                    Missing.Value, Missing.Value, false, Missing.Value, Missing.Value, Missing.Value)
                    ?? throw new InvalidOperationException("Word returned no document instance for the PDF.");

                InvokeMethod(document, "SaveAs2", outputPath, WdFormatDocumentDefault);
            });
        }
        finally
        {
            TryInvoke(document, "Close", false);
            ReleaseComObject(document);
            ReleaseComObject(documents);
        }
    }

    public void Dispose() => Quit();

    // ---- Process ownership --------------------------------------------------------------------------------

    private const string WordProcessName = "WINWORD";
    private const string ExcelProcessName = "EXCEL";
    private const string PowerPntProcessName = "POWERPNT";

    private static readonly TimeSpan SpawnPollInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan SpawnPollBudget = TimeSpan.FromSeconds(5);
    // Spawn lag after CoCreateInstance can exceed 2s under load; mis-classifying such a spawn as
    // "attach" leaves an orphan. Attach itself is still detected instantly via an OUR-window pid
    // matching a pre-existing instance, so a longer probe only affects window-less edge cases.
    private static readonly TimeSpan AttachProbeBudget = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Resolves a pid from OUR PowerPoint Application's window. Modern PowerPoint builds removed the
    /// late-bound Window.HWND property (DISP_E_MEMBERNOTFOUND), so after trying the COM window APIs
    /// the resolver falls back to enumerating the desktop for PowerPoint's own frame class
    /// PPTFrameClass. Only called on the spawn path (exactly one fresh candidate), where the frame
    /// pid must equal that candidate for the proof to count.
    /// </summary>
    private static int? TryGetPowerPointWindowPid(object app)
    {
        return TryGetComWindowPid(app) ?? EnumFrameClassPids(PowerPntFrameClass).FirstOrDefault();
    }

    private static int? TryGetComWindowPid(object app)
    {
        try
        {
            var windows = TryGetPropertyValue(app, "Windows");
            if (windows is null)
            {
                return null;
            }

            var count = TryGetIntValue(windows, "Count");
            if (count is not > 0)
            {
                return null;
            }

            for (var i = 1; i <= count.Value; i++)
            {
                var window = windows.GetType().InvokeMember(
                    "Item", System.Reflection.BindingFlags.InvokeMethod, null, windows, new object[] { i });
                if (window is null)
                {
                    continue;
                }

                var hwnd = TryGetIntValue(window, "HWND") ?? TryGetIntValue(window, "Hwnd");
                if (hwnd is > 0 && ResolvePidFromHwnd(hwnd.Value) is { } pid)
                {
                    return pid;
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private const string PowerPntFrameClass = "PPTFrameClass";

    /// <summary>Pids (owning process) of all visible top-level windows of the given class name.</summary>
    private static HashSet<int> EnumFrameClassPids(string className)
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
                        && string.Equals(buffer.ToString(), className, StringComparison.OrdinalIgnoreCase)
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

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

    private delegate bool EnumWindowsProc(nint hWnd, nint lParam);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint hWnd);

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int GetClassName(nint hWnd, System.Text.StringBuilder text, int count);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    /// <summary>Resolves the owning PID of a window, or null when unresolvable (fail closed).</summary>
    private static int? ResolvePidFromHwnd(int hwnd)
    {
        if (hwnd == 0 || !OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            if (GetWindowThreadProcessId(hwnd, out var pid)
                && pid > 0
                && pid <= int.MaxValue)
            {
                return (int)pid;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Builds the identity only when the live process actually matches the expected Office image
    /// name; any doubt (missing process, wrong name, unreadable start time) returns null so the
    /// watchdog later refuses to terminate instead of guessing.
    /// </summary>
    private static OfficeProcessIdentity? CaptureIdentity(int processId, string expectedProcessName)
    {
        try
        {
            using var process = SysProcess.GetProcessById(processId);
            if (process.HasExited
                || !string.Equals(process.ProcessName, expectedProcessName, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            try
            {
                return new OfficeProcessIdentity(
                    processId,
                    expectedProcessName,
                    process.StartTime.ToUniversalTime());
            }
            catch
            {
                // StartTime unreadable: cannot discriminate PID reuse later. Fail closed.
                return null;
            }
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Fresh set of PIDs for an Office image, or an empty set off Windows / on any failure.</summary>
    private static HashSet<int> SnapshotOfficePids(string processName)
    {
        var snapshot = new HashSet<int>();
        if (!OperatingSystem.IsWindows())
        {
            return snapshot;
        }

        try
        {
            foreach (var process in SysProcess.GetProcessesByName(processName))
            {
                using (process)
                {
                    snapshot.Add(process.Id);
                }
            }
        }
        catch
        {
            // Enumeration failure means the diff below cannot be trusted; the caller treats its
            // semantics as "attached" and never kills.
        }

        return snapshot;
    }

    /// <summary>
    /// Polls up to <see cref="SpawnPollBudget"/> for a newly created Office process of
    /// <paramref name="processName"/> that was absent before activation. Returns null when nothing
    /// appeared (attach) or when MORE THAN ONE candidate appeared (an ambiguous race, e.g. the user
    /// launching Office at the same moment) — ambiguity must never authorize a kill.
    /// </summary>
    // NOTE: an empty/untrusted beforePids merely means the poll below looks for exactly one
    // fresh candidate of the expected image; an ambiguous multi-candidate result still refuses
    // ownership, so a failed snapshot degrades to "no kill", never to "kill".
    private static OfficeProcessIdentity? TryFindSpawnedProcess(HashSet<int> beforePids, string processName)
    {
        var deadline = DateTime.UtcNow + SpawnPollBudget;
        while (true)
        {
            var candidate = CollectSpawnedCandidates(beforePids, processName) is { Count: 1 } found ? found[0] : null;
            if (candidate is not null)
            {
                return candidate;
            }

            if (DateTime.UtcNow >= deadline)
            {
                return null;
            }

            Thread.Sleep(SpawnPollInterval);
        }
    }

    /// <summary>
    /// Single (non-polling) ownership scan: returns ALL fresh (post-activation) candidates of the
    /// expected image that could be captured with a verifiable identity.
    /// </summary>
    private static List<OfficeProcessIdentity> CollectSpawnedCandidates(HashSet<int> beforePids, string processName)
    {
        var candidates = new List<OfficeProcessIdentity>();
        foreach (var process in SysProcess.GetProcessesByName(processName))
        {
            using (process)
            {
                if (beforePids.Contains(process.Id))
                {
                    continue;
                }

                if (CaptureIdentity(process.Id, processName) is { } identity)
                {
                    candidates.Add(identity);
                }
            }
        }

        return candidates;
    }

    /// <summary>Reads a COM property value, or null when missing/unreadable (fail closed).</summary>
    private static object? TryGetPropertyValue(object target, string name)
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

    /// <summary>Reads a COM int property, or null when missing or of another shape.</summary>
    private static int? TryGetIntValue(object target, string name)
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

    /// <summary>Reads a COM property as int; returns false when missing or of another shape.</summary>
    private static bool TryGetIntProperty(object target, string name, out int value)
    {
        value = 0;
        try
        {
            var raw = target.GetType().InvokeMember(name, BindingFlags.GetProperty, null, target, null);
            if (raw is null)
            {
                return false;
            }

            value = Convert.ToInt32(raw, System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static object CreateComObject(string progId)
    {
        var type = Type.GetTypeFromProgID(progId)
            ?? throw new InvalidOperationException($"{progId} ProgID not found.");
        return Activator.CreateInstance(type)
            ?? throw new InvalidOperationException($"Could not create {progId} COM instance.");
    }

    private static object GetProperty(object target, string name)
        => target.GetType().InvokeMember(name, BindingFlags.GetProperty, null, target, null)
            ?? throw new InvalidOperationException($"COM property '{name}' returned null.");

    private static object? InvokeMethod(object target, string name, params object?[] args)
        => target.GetType().InvokeMember(name, BindingFlags.InvokeMethod, null, target, args);

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
            // Best-effort COM cleanup.
        }
    }

    private static void SetPropertyIfAvailable(object target, string name, object value)
    {
        try
        {
            target.GetType().InvokeMember(name, BindingFlags.SetProperty, null, target, new[] { value });
        }
        catch
        {
            // Optional property missing on this Office version.
        }
    }

    private static void ReleaseComObject(object? comObject)
    {
        if (comObject is null || !Marshal.IsComObject(comObject))
        {
            return;
        }

        Marshal.FinalReleaseComObject(comObject);
    }
}
