namespace SnappyDocsConvert.Core.Models;

public sealed record ExternalToolLink(
    string Name,
    string OfficialUrl,
    string Notes);

public static class KnownExternalToolLinks
{
    public const string LibreOfficeDownloadUrl =
        "https://www.libreoffice.org/download/download-libreoffice/";

    public const string LibreOfficeStartParametersUrl =
        "https://help.libreoffice.org/latest/en-US/text/shared/guide/start_parameters.html";

    public static readonly ExternalToolLink LibreOfficeDownload = new(
        "LibreOffice official download",
        LibreOfficeDownloadUrl,
        "Use the official LibreOffice download page. The app does not bundle LibreOffice in the MVP.");

    public static readonly ExternalToolLink LibreOfficeStartParameters = new(
        "LibreOffice command-line start parameters",
        LibreOfficeStartParametersUrl,
        "Reference for soffice headless and startup options.");
}
