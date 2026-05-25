namespace SnappyDocsConvert.Core.Services.LibreOffice;

public sealed record LibreOfficeCommand(
    string ExecutablePath,
    IReadOnlyList<string> Arguments,
    string TempProfileDirectory,
    string ExpectedPdfPath);
