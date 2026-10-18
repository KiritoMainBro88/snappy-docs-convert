using SnappyDocsConvert.App.Services;
using SnappyDocsConvert.App.ViewModels;
using SnappyDocsConvert.Core.Models;

namespace SnappyDocsConvert.Tests;

// Regression lock for the real-drag verification (slice S1 step 1): the S0 duplicate
// guard used to swallow every freshly accepted path, so drops queued nothing. These
// tests build the view model against the real (registry-only) availability provider
// and never touch Office COM, dialogs, or any saved settings.
public sealed class MainWindowViewModelQueueTests : IDisposable
{
    private readonly string _directory;

    public MainWindowViewModelQueueTests()
    {
        _directory = Path.Combine(
            Path.GetTempPath(),
            "kmb-vm-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private MainWindowViewModel CreateViewModel() => new(new AppServiceFactory());

    private string WriteFile(string name, string contents)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, contents);
        return path;
    }

    [Fact]
    public void AddPaths_QueuesAcceptedFiles()
    {
        var rtf = WriteFile("a.rtf", "{\\rtf1}");
        var csv = WriteFile("b.csv", "name,size");
        var pdf = WriteFile("c.pdf", "%PDF-1.4");
        var viewModel = CreateViewModel();

        viewModel.AddPaths(new[] { rtf, csv, pdf });

        Assert.Equal(3, viewModel.Queue.Count);
        Assert.Equal(3, int.Parse(viewModel.SummaryText.Split(' ')[0], System.Globalization.CultureInfo.InvariantCulture));
        Assert.Empty(viewModel.RejectedPaths);
        Assert.Equal("", viewModel.AlertText);
    }

    [Fact]
    public void AddPaths_SecondDropOfSameFile_AddsNothingAndLogsOnly()
    {
        var rtf = WriteFile("a.rtf", "{\\rtf1}");
        var viewModel = CreateViewModel();

        viewModel.AddPaths(new[] { rtf });
        var queuedAfterFirst = viewModel.Queue.Count;
        viewModel.AddPaths(new[] { rtf });

        Assert.Equal(queuedAfterFirst, viewModel.Queue.Count);
        Assert.Single(viewModel.Queue);
        Assert.Empty(viewModel.RejectedPaths);
        Assert.Equal("", viewModel.AlertText);
        Assert.Contains(viewModel.LogLines, line => line.Contains("Duplicate re-add ignored", StringComparison.Ordinal));
    }

    [Fact]
    public void AddPaths_SingleDropWithDuplicateInsidePayload_AndAlreadyQueued()
    {
        var rtf = WriteFile("a.rtf", "{\\rtf1}");
        var csv = WriteFile("b.csv", "name,size");
        var viewModel = CreateViewModel();

        viewModel.AddPaths(new[] { rtf });
        // Second drop re-sends the queued file plus one new file: the queued one is skipped
        // silently, the new one is still added exactly once.
        viewModel.AddPaths(new[] { rtf, csv });

        Assert.Equal(2, viewModel.Queue.Count);
        Assert.Empty(viewModel.RejectedPaths);
        Assert.Equal("", viewModel.AlertText);
    }

    [Fact]
    public void AddPaths_UnsupportedExtension_IsRejectedWithAlert()
    {
        var txt = WriteFile("n.txt", "not supported");
        var viewModel = CreateViewModel();

        viewModel.AddPaths(new[] { txt });

        Assert.Empty(viewModel.Queue);
        Assert.Single(viewModel.RejectedPaths);
        Assert.NotEqual("", viewModel.AlertText);
        Assert.Contains(viewModel.LogLines, line => line.Contains("Unsupported or missing path skipped", StringComparison.Ordinal));
    }

    [Fact]
    public void AddPaths_MixedPayload_KeepsAcceptedAndFlagsUnsupportedOnly()
    {
        var docx = WriteFile("doc.docx", "docx");
        var txt = WriteFile("note.txt", "txt");
        var viewModel = CreateViewModel();

        viewModel.AddPaths(new[] { docx, txt });

        Assert.Single(viewModel.Queue);
        Assert.Single(viewModel.RejectedPaths);
    }

    [Fact]
    public void AddPdfToolPaths_QueuesPdfOnlyAndIgnoresDuplicateReaddsLogOnly()
    {
        var pdf = WriteFile("doc.pdf", "%PDF-1.4");
        var png = WriteFile("img.png", "png");
        var viewModel = CreateViewModel();

        viewModel.AddPdfToolPaths(new[] { pdf, png });
        viewModel.AddPdfToolPaths(new[] { pdf });

        // Merge only accepts PDF inputs: the PNG is rejected for the operation, and the
        // repeated PDF is dropped with a log line instead of being queued twice.
        Assert.Equal(PdfToolOperation.Merge, viewModel.SelectedPdfToolOperation);
        Assert.Equal(1, viewModel.PdfToolInputs.Count);
        Assert.Equal(1, viewModel.RejectedPaths.Count);
        Assert.Equal("1 PDF tool input(s) ready.", viewModel.PdfToolStatus);
    }
}
