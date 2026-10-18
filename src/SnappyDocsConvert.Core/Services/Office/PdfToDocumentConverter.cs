using SnappyDocsConvert.Core.Models;

namespace SnappyDocsConvert.Core.Services.Office;

/// <summary>
/// Reverse conversion: PDF -> editable document (currently PDF -> .docx via Word COM, which reflows
/// the PDF into editable text on Word 2013+). Local-only, Windows + Microsoft Word required.
/// </summary>
public sealed class PdfToDocumentConverter
{
    private readonly IOfficeComSession _session;
    private readonly bool _ownsSession;
    private readonly TimeSpan _timeout;

    public PdfToDocumentConverter(IOfficeComSession? session = null, TimeSpan? timeout = null)
    {
        _ownsSession = session is null;
        _session = session ?? new OfficeComSession(new ComOfficeAppFactory());
        _timeout = timeout ?? TimeSpan.FromSeconds(180);
    }

    /// <summary>Converts a single PDF to a .docx written next to <paramref name="outputDirectory"/>.</summary>
    public async Task<ConversionResult> ConvertToDocxAsync(
        string inputPdfPath,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(inputPdfPath) || !File.Exists(inputPdfPath))
        {
            return Failure(inputPdfPath, ConversionStatus.MissingInput, "Input PDF does not exist.");
        }

        if (!string.Equals(Path.GetExtension(inputPdfPath), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return Failure(inputPdfPath, ConversionStatus.UnsupportedInput, "PDF import requires a .pdf input.");
        }

        Directory.CreateDirectory(outputDirectory);
        var outputPath = Path.Combine(
            outputDirectory,
            $"{Path.GetFileNameWithoutExtension(inputPdfPath)}.docx");

        var result = await _session
            .ConvertFromPdfAsync(OfficeAppKind.Word, inputPdfPath, outputPath, _timeout, cancellationToken)
            .ConfigureAwait(false);

        if (result.TimedOut)
        {
            return Failure(inputPdfPath, ConversionStatus.TimedOut, result.ErrorMessage ?? "Word PDF import timed out.");
        }

        if (!result.Success)
        {
            return Failure(inputPdfPath, ConversionStatus.ProcessFailed,
                result.ErrorMessage ?? "Word could not convert the PDF to DOCX.", result.Warnings);
        }

        if (!File.Exists(outputPath) || new FileInfo(outputPath).Length <= 0)
        {
            return Failure(inputPdfPath, ConversionStatus.OutputMissing,
                "Word completed but produced no .docx output.", result.Warnings);
        }

        return new ConversionResult
        {
            Success = true,
            Status = ConversionStatus.Succeeded,
            InputPath = inputPdfPath,
            OutputPdfPath = outputPath, // reused field: the produced output path (.docx here)
            EngineKind = ConversionEngineKind.MicrosoftOffice,
            Warnings = result.Warnings
        };
    }

    public void DisposeSessionIfOwned()
    {
        if (_ownsSession)
        {
            _session.Dispose();
        }
    }

    private static ConversionResult Failure(
        string inputPath,
        ConversionStatus status,
        string message,
        IReadOnlyList<string>? warnings = null)
        => new()
        {
            Success = false,
            Status = status,
            InputPath = inputPath ?? string.Empty,
            EngineKind = ConversionEngineKind.MicrosoftOffice,
            ErrorMessage = message,
            Warnings = warnings ?? Array.Empty<string>()
        };
}
