using System.Text;
using System.Text.RegularExpressions;
using SnappyDocsConvert.Core.Models;
using SnappyDocsConvert.Core.Services;

namespace SnappyDocsConvert.Core.Services.Ocr;

/// <summary>
/// OCR engine backed by a locally-installed PaddleOCR CLI (no bundled model, no cloud download by
/// this app). The executable path is supplied/located externally; this class only builds the command,
/// invokes it through <see cref="IProcessRunner"/>, and parses recognised text out of stdout.
///
/// PaddleOCR is NOT bundled or auto-installed; it must be present on the user's machine. The command
/// build + output parsing are unit-tested with a fake process runner (no PaddleOCR required).
/// </summary>
public sealed class PaddleOcrEngine : IOcrEngine
{
    private readonly IProcessRunner _processRunner;
    private readonly string? _executablePath;
    private readonly TimeSpan _timeout;

    public PaddleOcrEngine(IProcessRunner processRunner, string? executablePath, TimeSpan? timeout = null)
    {
        _processRunner = processRunner;
        _executablePath = executablePath;
        _timeout = timeout ?? TimeSpan.FromSeconds(120);
    }

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
        => Task.FromResult(!string.IsNullOrWhiteSpace(_executablePath) && File.Exists(_executablePath));

    public async Task<OcrResult> RecognizeImageAsync(string imagePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_executablePath) || !File.Exists(_executablePath))
        {
            return OcrResult.Failed("PaddleOCR executable not found.");
        }

        if (!File.Exists(imagePath))
        {
            return OcrResult.Failed($"Image not found: {imagePath}");
        }

        var arguments = BuildArguments(imagePath);
        var run = await _processRunner
            .RunAsync(_executablePath, arguments, _timeout, cancellationToken)
            .ConfigureAwait(false);

        if (run.TimedOut)
        {
            return OcrResult.Failed("PaddleOCR timed out.");
        }

        if (run.ExitCode is not 0)
        {
            var reason = string.IsNullOrWhiteSpace(run.Stderr) ? run.Stdout : run.Stderr;
            return OcrResult.Failed($"PaddleOCR failed (exit {run.ExitCode}): {Truncate(reason, 300)}");
        }

        return OcrResult.Ok(ParseRecognizedText(run.Stdout));
    }

    public static IReadOnlyList<string> BuildArguments(string imagePath)
        => new[]
        {
            "--image_dir",
            imagePath,
            "--use_angle_cls",
            "true",
            "--use_gpu",
            "false"
        };

    /// <summary>
    /// Extracts recognised text from PaddleOCR stdout. PaddleOCR prints lines like:
    ///   [2024/...] ppocr INFO: ('recognised text', 0.99)
    /// or tuples ('text', confidence). This pulls the quoted text out of each result line and joins
    /// them, ignoring log/noise lines. Tolerant of single or double quotes.
    /// </summary>
    public static string ParseRecognizedText(string stdout)
    {
        if (string.IsNullOrWhiteSpace(stdout))
        {
            return string.Empty;
        }

        var matches = Regex.Matches(
            stdout,
            @"\(\s*(?:'(?<t>(?:[^'\\]|\\.)*)'|""(?<t>(?:[^""\\]|\\.)*)"")\s*,\s*[0-9]*\.?[0-9]+\s*\)");

        var builder = new StringBuilder();
        foreach (Match match in matches)
        {
            var text = match.Groups["t"].Value
                .Replace("\\'", "'")
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\");
            if (!string.IsNullOrWhiteSpace(text))
            {
                builder.AppendLine(text);
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static string Truncate(string value, int max)
        => string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max];
}
