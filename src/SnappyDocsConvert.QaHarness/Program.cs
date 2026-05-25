using System.Text;
using SnappyDocsConvert.Core.Models;
using SnappyDocsConvert.Core.Services;
using SnappyDocsConvert.Core.Services.Batch;
using SnappyDocsConvert.Core.Services.LibreOffice;

var qaRoot = GetOption(args, "--qa-root")
    ?? Path.Combine(Path.GetTempPath(), "SnappyDocsConvertQa", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(qaRoot);

var inputsRoot = Path.Combine(qaRoot, "inputs");
var outputsRoot = Path.Combine(qaRoot, "outputs");
Directory.CreateDirectory(inputsRoot);
Directory.CreateDirectory(outputsRoot);

var failures = 0;
var pipeline = new BatchConversionPipeline();
var officeAvailability = await new MicrosoftOfficeAvailabilityProvider()
    .GetOfficeAvailabilityAsync(CancellationToken.None)
    .ConfigureAwait(false);
var libreOfficeAvailability = await new LibreOfficeLocator()
    .LocateAsync(new LibreOfficeOptions { ProbeVersion = false }, CancellationToken.None)
    .ConfigureAwait(false);

Row(
    "Office availability",
    "Report",
    "Pass",
    $"Word={officeAvailability.WordAvailable}; PowerPoint={officeAvailability.PowerPointAvailable}");

await PdfToImageAsync(ImageOutputFormat.Png, "PDF -> PNG", "page-001.png").ConfigureAwait(false);
await PdfToImageAsync(ImageOutputFormat.Jpeg, "PDF -> JPEG", "page-001.jpg").ConfigureAwait(false);
await RtfToPdfAsync().ConfigureAwait(false);
await RtfToPdfAndImagesAsync().ConfigureAwait(false);
await BatchPartialFailureAsync().ConfigureAwait(false);
await LibreOfficeReportingAsync().ConfigureAwait(false);

return failures == 0 ? 0 : 1;

async Task PdfToImageAsync(
    ImageOutputFormat format,
    string caseName,
    string expectedFileName)
{
    var inputPath = WriteTinyPdf(Path.Combine(inputsRoot, $"{Sanitize(caseName)}.pdf"));
    var outputRoot = Path.Combine(outputsRoot, Sanitize(caseName));
    var result = await pipeline.RunAsync(
        new BatchConversionJob(
            new[] { new BatchConversionItem(inputPath) },
            new BatchConversionOptions
            {
                OutputRoot = outputRoot,
                Target = BatchConversionTarget.Images,
                ImageFormat = format,
                Dpi = 72
            }),
        null,
        CancellationToken.None).ConfigureAwait(false);

    var item = result.Items.SingleOrDefault();
    var imagePath = item?.ImageFiles.SingleOrDefault();
    if (item?.Status == BatchConversionItemStatus.Succeeded &&
        imagePath is not null &&
        File.Exists(imagePath) &&
        new FileInfo(imagePath).Length > 0 &&
        string.Equals(Path.GetFileName(imagePath), expectedFileName, StringComparison.OrdinalIgnoreCase))
    {
        Row(caseName, "Image exists, size > 0", "Pass", Evidence(imagePath));
        return;
    }

    Fail(caseName, "Image exists, size > 0", item?.ErrorMessage ?? "Expected image missing.");
}

async Task RtfToPdfAsync()
{
    const string caseName = "RTF -> PDF via Word";
    if (!officeAvailability.WordAvailable)
    {
        Row(caseName, "Real PDF if Word available", "Skip", "Word.Application ProgID missing.");
        return;
    }

    var inputPath = WriteTinyRtf(Path.Combine(inputsRoot, "word-smoke.rtf"));
    var outputRoot = Path.Combine(outputsRoot, "rtf-pdf");
    var result = await pipeline.RunAsync(
        new BatchConversionJob(
            new[] { new BatchConversionItem(inputPath) },
            new BatchConversionOptions
            {
                OutputRoot = outputRoot,
                Target = BatchConversionTarget.Pdf,
                EnginePreference = BatchConversionEnginePreference.MicrosoftOffice
            }),
        null,
        CancellationToken.None).ConfigureAwait(false);

    var item = result.Items.SingleOrDefault();
    if (item?.Status == BatchConversionItemStatus.Succeeded &&
        item.OutputPdfPath is not null &&
        File.Exists(item.OutputPdfPath) &&
        new FileInfo(item.OutputPdfPath).Length > 0)
    {
        Row(caseName, "Real PDF exists, size > 0", "Pass", Evidence(item.OutputPdfPath));
        return;
    }

    Fail(caseName, "Real PDF exists, size > 0", item?.ErrorMessage ?? "PDF missing.");
}

async Task RtfToPdfAndImagesAsync()
{
    const string caseName = "RTF -> PDF+Images";
    if (!officeAvailability.WordAvailable)
    {
        Row(caseName, "Real PDF and image if Word available", "Skip", "Word.Application ProgID missing.");
        return;
    }

    var inputPath = WriteTinyRtf(Path.Combine(inputsRoot, "word-pdf-images.rtf"));
    var outputRoot = Path.Combine(outputsRoot, "rtf-pdf-images");
    var result = await pipeline.RunAsync(
        new BatchConversionJob(
            new[] { new BatchConversionItem(inputPath) },
            new BatchConversionOptions
            {
                OutputRoot = outputRoot,
                Target = BatchConversionTarget.PdfAndImages,
                EnginePreference = BatchConversionEnginePreference.MicrosoftOffice,
                ImageFormat = ImageOutputFormat.Png,
                Dpi = 72
            }),
        null,
        CancellationToken.None).ConfigureAwait(false);

    var item = result.Items.SingleOrDefault();
    var imagePath = item?.ImageFiles.SingleOrDefault();
    if (item?.Status == BatchConversionItemStatus.Succeeded &&
        item.OutputPdfPath is not null &&
        imagePath is not null &&
        File.Exists(item.OutputPdfPath) &&
        File.Exists(imagePath) &&
        new FileInfo(item.OutputPdfPath).Length > 0 &&
        new FileInfo(imagePath).Length > 0)
    {
        Row(caseName, "Real PDF and image exist, size > 0", "Pass", $"{Evidence(item.OutputPdfPath)}; {Evidence(imagePath)}");
        return;
    }

    Fail(caseName, "Real PDF and image exist, size > 0", item?.ErrorMessage ?? "PDF/image missing.");
}

async Task BatchPartialFailureAsync()
{
    const string caseName = "Batch partial failure";
    var validPdf = WriteTinyPdf(Path.Combine(inputsRoot, "partial-valid.pdf"));
    var missing = Path.Combine(inputsRoot, "missing-file.pdf");
    var result = await pipeline.RunAsync(
        new BatchConversionJob(
            new[] { new BatchConversionItem(validPdf), new BatchConversionItem(missing) },
            new BatchConversionOptions
            {
                OutputRoot = Path.Combine(outputsRoot, "partial"),
                Target = BatchConversionTarget.Images,
                ImageFormat = ImageOutputFormat.Png,
                Dpi = 72
            }),
        null,
        CancellationToken.None).ConfigureAwait(false);

    if (result.TotalItems == 2 &&
        result.SucceededCount == 1 &&
        result.FailedCount == 1)
    {
        Row(caseName, "One success, one failed", "Pass", $"total={result.TotalItems}; succeeded={result.SucceededCount}; failed={result.FailedCount}");
        return;
    }

    Fail(caseName, "One success, one failed", $"total={result.TotalItems}; succeeded={result.SucceededCount}; failed={result.FailedCount}");
}

async Task LibreOfficeReportingAsync()
{
    const string caseName = "LibreOffice";
    if (libreOfficeAvailability.IsAvailable)
    {
        Row(caseName, "Report availability", "Pass", $"available: {libreOfficeAvailability.ExecutablePath}");
        return;
    }

    var advisor = new EngineSetupAdvisor();
    var status = await advisor
        .GetStatusAsync(EngineSetupMode.IDoNotHaveMicrosoftOffice, null, CancellationToken.None)
        .ConfigureAwait(false);
    if (!status.LibreOfficeAvailable &&
        !string.IsNullOrWhiteSpace(status.BlockingReason))
    {
        Row(caseName, "Missing reported honestly", "Skip", status.BlockingReason);
        return;
    }

    Fail(caseName, "Missing reported honestly", "LibreOffice missing but setup guidance was unclear.");
}

void Row(string caseName, string expected, string result, string evidence)
{
    Console.WriteLine($"QA_ROW|{Clean(caseName)}|{Clean(expected)}|{Clean(result)}|{Clean(evidence)}");
}

void Fail(string caseName, string expected, string evidence)
{
    failures++;
    Row(caseName, expected, "Fail", evidence);
}

static string? GetOption(string[] args, string name)
{
    for (var index = 0; index < args.Length - 1; index++)
    {
        if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
        {
            return args[index + 1];
        }
    }

    return null;
}

static string WriteTinyPdf(string path)
{
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllBytes(path, TinyPdf.Create());
    return path;
}

static string WriteTinyRtf(string path)
{
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(
        path,
        @"{\rtf1\ansi\deff0{\fonttbl{\f0 Arial;}}\f0\fs24 kmb file tools QA.\par}",
        Encoding.ASCII);
    return path;
}

static string Evidence(string path)
    => $"{path} ({new FileInfo(path).Length} bytes)";

static string Sanitize(string value)
{
    var invalid = Path.GetInvalidFileNameChars();
    return new string(value.Select(ch => invalid.Contains(ch) || char.IsWhiteSpace(ch) ? '-' : ch).ToArray())
        .Trim('-')
        .ToLowerInvariant();
}

static string Clean(string value)
    => value.Replace("|", "/").Replace("\r", " ").Replace("\n", " ").Trim();

internal static class TinyPdf
{
    public static byte[] Create()
    {
        const string pageText = "BT /F1 18 Tf 36 90 Td (kmb file tools QA) Tj ET\n";
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 240 140] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(pageText)} >>\nstream\n{pageText}endstream"
        };

        using var stream = new MemoryStream();
        WriteAscii(stream, "%PDF-1.4\n");
        var offsets = new List<long>();

        for (var index = 0; index < objects.Length; index++)
        {
            offsets.Add(stream.Position);
            WriteAscii(stream, $"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
        }

        var xrefOffset = stream.Position;
        WriteAscii(stream, $"xref\n0 {objects.Length + 1}\n");
        WriteAscii(stream, "0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            WriteAscii(stream, $"{offset:0000000000} 00000 n \n");
        }

        WriteAscii(stream, $"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\n");
        WriteAscii(stream, $"startxref\n{xrefOffset}\n%%EOF\n");
        return stream.ToArray();
    }

    private static void WriteAscii(Stream stream, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        stream.Write(bytes, 0, bytes.Length);
    }
}
