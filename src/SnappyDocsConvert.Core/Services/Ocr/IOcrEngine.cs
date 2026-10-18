namespace SnappyDocsConvert.Core.Services.Ocr;

/// <summary>Result of an OCR run over a single image.</summary>
public sealed record OcrResult(bool Success, string Text, string? ErrorMessage)
{
    public static OcrResult Ok(string text) => new(true, text, null);
    public static OcrResult Failed(string error) => new(false, string.Empty, error);
}

/// <summary>
/// OCR engine abstraction. The real implementation shells out to a locally-installed PaddleOCR
/// (no bundled model, no cloud). Fakes implement this for unit tests with no engine installed.
/// </summary>
public interface IOcrEngine
{
    /// <summary>True when a usable OCR engine was located on this machine.</summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken);

    /// <summary>Runs OCR on a single image file and returns recognised text.</summary>
    Task<OcrResult> RecognizeImageAsync(string imagePath, CancellationToken cancellationToken);
}
