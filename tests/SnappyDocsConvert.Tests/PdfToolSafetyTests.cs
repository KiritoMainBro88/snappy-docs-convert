using System.Text.RegularExpressions;
using PdfSharp.Pdf;
using SnappyDocsConvert.Core.Models;
using SnappyDocsConvert.Core.Services.PdfTools;

namespace SnappyDocsConvert.Tests;

/// <summary>
/// Regression tests for PDF tool safety behaviors: encrypted-input messaging (never leaks the
/// password) and output-file collision avoidance (never silently overwrites an existing file).
/// </summary>
public sealed class PdfToolSafetyTests
{
    private const string SecretPassword = "s3cret!42";

    [Fact]
    public async Task Watermark_OnEncryptedPdf_FailsWithClearMessage_WithoutLeakingPassword()
    {
        using var temp = TempDirectory.Create();
        var encrypted = WriteEncryptedPdf(temp, "locked.pdf", SecretPassword);
        var output = temp.CreateSubdirectory("out");

        var result = await Service().RunAsync(new PdfToolRequest
        {
            Operation = PdfToolOperation.Watermark,
            InputPaths = new[] { encrypted },
            OutputDirectory = output,
            OutputFileName = "watermarked.pdf",
            WatermarkText = "CONFIDENTIAL"
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("password", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(SecretPassword, result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Encrypt_OnAlreadyEncryptedPdf_FailsWithClearMessage_WithoutLeakingPassword()
    {
        using var temp = TempDirectory.Create();
        var encrypted = WriteEncryptedPdf(temp, "locked.pdf", SecretPassword);
        var output = temp.CreateSubdirectory("out");

        var result = await Service().RunAsync(new PdfToolRequest
        {
            Operation = PdfToolOperation.Encrypt,
            InputPaths = new[] { encrypted },
            OutputDirectory = output,
            OutputFileName = "encrypted.pdf",
            UserPassword = "another-password-999"
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("password", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(SecretPassword, result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Merge_RepeatedSameOutputName_DoesNotOverwrite_UsesHashSuffixedName()
    {
        using var temp = TempDirectory.Create();
        var first = temp.WritePdf("one.pdf", 1);
        var second = temp.WritePdf("two.pdf", 1);
        var output = temp.CreateSubdirectory("out");

        var request = new PdfToolRequest
        {
            Operation = PdfToolOperation.Merge,
            InputPaths = new[] { first, second },
            OutputDirectory = output,
            OutputFileName = "merged.pdf"
        };

        var firstRun = await Service().RunAsync(request, CancellationToken.None);
        var protectedPath = firstRun.OutputFiles.Single();
        Assert.EndsWith("merged.pdf", protectedPath, StringComparison.OrdinalIgnoreCase);
        var protectedBytes = await File.ReadAllBytesAsync(protectedPath);

        var secondRun = await Service().RunAsync(request, CancellationToken.None);

        Assert.True(firstRun.Success, firstRun.ErrorMessage);
        Assert.True(secondRun.Success, secondRun.ErrorMessage);
        var secondOutput = secondRun.OutputFiles.Single();
        Assert.NotEqual(protectedPath, secondOutput);
        Assert.Matches(
            new Regex(@"^merged__[0-9A-F]{8}\.pdf$", RegexOptions.IgnoreCase),
            Path.GetFileName(secondOutput));
        Assert.True(File.Exists(secondOutput));

        // The pre-existing merged.pdf is byte-identical: no silent overwrite.
        var afterBytes = await File.ReadAllBytesAsync(protectedPath);
        Assert.Equal(protectedBytes, afterBytes);
    }

    private static PdfToolService Service() => new();

    private static string WriteEncryptedPdf(TempDirectory temp, string fileName, string password)
    {
        var document = new PdfDocument();
        document.AddPage();
        document.SecuritySettings.UserPassword = password;
        var path = Path.Combine(temp.Path, fileName);
        document.Save(path);
        return path;
    }

    private sealed class TempDirectory : IDisposable
    {
        private TempDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TempDirectory Create()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "SnappyDocsConvertPdfToolSafetyTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TempDirectory(path);
        }

        public string CreateSubdirectory(string name)
        {
            var path = System.IO.Path.Combine(Path, name);
            Directory.CreateDirectory(path);
            return path;
        }

        public string WritePdf(string fileName, int pageCount)
        {
            var path = System.IO.Path.Combine(Path, fileName);
            using var document = new PdfDocument();
            for (var index = 0; index < pageCount; index++)
            {
                document.AddPage();
            }

            document.Save(path);
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
