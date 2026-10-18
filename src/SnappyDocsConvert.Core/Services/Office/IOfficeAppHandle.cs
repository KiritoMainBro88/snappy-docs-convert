using SnappyDocsConvert.Core.Models;

namespace SnappyDocsConvert.Core.Services.Office;

/// <summary>
/// A live Office Application instance (Word or PowerPoint) that can export one or more documents to
/// PDF while staying open between files. All members are expected to be invoked on the owning STA
/// thread (see <see cref="IStaDispatcher"/>).
/// </summary>
public interface IOfficeAppHandle : IDisposable
{
    OfficeAppKind Kind { get; }

    /// <summary>OS process id of the underlying Office application, or 0 when unknown.</summary>
    int ProcessId { get; }

    /// <summary>
    /// Verified process identity of this instance, or null when no identity was proven. Without an
    /// identity the watchdog must never terminate the process (fail closed).
    /// </summary>
    OfficeProcessIdentity? Identity { get; }

    /// <summary>
    /// True only when this instance was started exclusively for this process AND its process identity
    /// was verified at creation. Terminating such an instance cannot close an Office app that the
    /// user, or any other client, is using. PowerPoint also reports false when activation attached to
    /// a pre-existing shared instance, or when the spawned process could not be identified uniquely.
    /// </summary>
    bool OwnedExclusively { get; }

    /// <summary>Opens <paramref name="inputPath"/>, exports it to <paramref name="outputPdfPath"/>, and closes the document. The Application stays open for reuse.</summary>
    void Export(string inputPath, string outputPdfPath);

    /// <summary>
    /// Reverse conversion: opens a PDF and saves it as an editable document in <paramref name="outputPath"/>
    /// (e.g. Word opening a PDF and saving as .docx). Only meaningful for app-kinds that support it
    /// (Word). The Application stays open for reuse.
    /// </summary>
    void ConvertFromPdf(string inputPdfPath, string outputPath);

    /// <summary>Quits the Application cleanly (releases COM). Called once at session end.</summary>
    void Quit();
}

/// <summary>Creates <see cref="IOfficeAppHandle"/> instances. Real impl talks COM; fakes enable unit tests with no Office installed.</summary>
public interface IOfficeAppFactory
{
    IOfficeAppHandle Create(OfficeAppKind kind);
}
