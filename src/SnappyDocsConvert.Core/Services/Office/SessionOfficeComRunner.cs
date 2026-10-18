using SnappyDocsConvert.Core.Models;

namespace SnappyDocsConvert.Core.Services.Office;

/// <summary>
/// <see cref="IOfficeComRunner"/> backed by a batch-scoped <see cref="IOfficeComSession"/>. Keeps the
/// same per-file interface the engine already uses, but routes every export through one reused Office
/// instance per app-kind instead of opening and quitting Office for each file. Dispose at batch end
/// quits/releases the underlying Office applications.
/// </summary>
public sealed class SessionOfficeComRunner : IOfficeComRunner, IDisposable
{
    private readonly IOfficeComSession _session;
    private readonly bool _ownsSession;

    public SessionOfficeComRunner(IOfficeComSession session, bool ownsSession = true)
    {
        _session = session;
        _ownsSession = ownsSession;
    }

    public SessionOfficeComRunner()
        : this(new OfficeComSession(new ComOfficeAppFactory()))
    {
    }

    public Task<OfficeComExportResult> ExportToPdfAsync(
        OfficeAppKind appKind,
        string inputPath,
        string outputPdfPath,
        TimeSpan timeout,
        CancellationToken cancellationToken)
        => _session.ExportToPdfAsync(appKind, inputPath, outputPdfPath, timeout, cancellationToken);

    public void Dispose()
    {
        if (_ownsSession)
        {
            _session.Dispose();
        }
    }
}
