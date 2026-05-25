using Microsoft.Win32;

namespace SnappyDocsConvert.App.Services;

public sealed class FilePickerService
{
    private const string InputFilter =
        "Supported documents|*.pdf;*.doc;*.docx;*.rtf;*.odt;*.ppt;*.pptx;*.odp|All files|*.*";

    public IReadOnlyList<string> PickInputFiles()
    {
        var dialog = new OpenFileDialog
        {
            Filter = InputFilter,
            Multiselect = true,
            Title = "Add files"
        };

        return dialog.ShowDialog() == true
            ? dialog.FileNames
            : Array.Empty<string>();
    }

    public string? PickLibreOfficeExecutable()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "LibreOffice executable|soffice.com;soffice.exe|All files|*.*",
            Multiselect = false,
            Title = "Choose soffice.com or soffice.exe"
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
