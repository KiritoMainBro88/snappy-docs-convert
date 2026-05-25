using SnappyDocsConvert.Core.Models;
using SnappyDocsConvert.Core.Services;
using SnappyDocsConvert.Core.Services.LibreOffice;
using SnappyDocsConvert.Core.Services.Paths;

namespace SnappyDocsConvert.Tests;

public sealed class LibreOfficeEngineTests
{
    [Fact]
    public async Task Locator_ReturnsUnavailable_WhenExplicitPathMissing()
    {
        using var temp = TempDirectory.Create();
        var missingPath = Path.Combine(temp.Path, "missing-soffice.exe");
        var locator = new LibreOfficeLocator(new PathService(), new FakeProcessRunner());

        var result = await locator.LocateAsync(
            new LibreOfficeOptions { ExecutablePath = missingPath, ProbeVersion = false },
            CancellationToken.None);

        Assert.False(result.IsAvailable);
        Assert.Null(result.ExecutablePath);
        Assert.Contains("does not exist", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Locator_AcceptsExplicitFakeExecutable_WhenFileExists()
    {
        using var temp = TempDirectory.Create();
        var fakeExecutable = Path.Combine(temp.Path, "soffice.exe");
        File.WriteAllText(fakeExecutable, string.Empty);
        var locator = new LibreOfficeLocator(new PathService(), new FakeProcessRunner());

        var result = await locator.LocateAsync(
            new LibreOfficeOptions { ExecutablePath = fakeExecutable, ProbeVersion = false },
            CancellationToken.None);

        Assert.True(result.IsAvailable);
        Assert.Equal(Path.GetFullPath(fakeExecutable), result.ExecutablePath);
    }

    [Fact]
    public async Task Locator_AcceptsLibreOfficeRootDirectory_WhenProgramSofficeExists()
    {
        using var temp = TempDirectory.Create();
        var programDirectory = temp.CreateSubdirectory(Path.Combine("LibreOffice", "program"));
        var fakeExecutable = Path.Combine(programDirectory, "soffice.com");
        File.WriteAllText(fakeExecutable, string.Empty);
        var rootDirectory = Path.Combine(temp.Path, "LibreOffice");
        var locator = new LibreOfficeLocator(new PathService(), new FakeProcessRunner());

        var result = await locator.LocateAsync(
            new LibreOfficeOptions { ExecutablePath = rootDirectory, ProbeVersion = false },
            CancellationToken.None);

        Assert.True(result.IsAvailable);
        Assert.Equal(Path.GetFullPath(fakeExecutable), result.ExecutablePath);
    }

    [Fact]
    public async Task Locator_AcceptsLibreOfficeProgramDirectory_WhenSofficeExists()
    {
        using var temp = TempDirectory.Create();
        var programDirectory = temp.CreateSubdirectory("program");
        var fakeExecutable = Path.Combine(programDirectory, "soffice.exe");
        File.WriteAllText(fakeExecutable, string.Empty);
        var locator = new LibreOfficeLocator(new PathService(), new FakeProcessRunner());

        var result = await locator.LocateAsync(
            new LibreOfficeOptions { ExecutablePath = programDirectory, ProbeVersion = false },
            CancellationToken.None);

        Assert.True(result.IsAvailable);
        Assert.Equal(Path.GetFullPath(fakeExecutable), result.ExecutablePath);
    }

    [Fact]
    public async Task Locator_RejectsDirectoryWithoutSoffice()
    {
        using var temp = TempDirectory.Create();
        var selectedDirectory = temp.CreateSubdirectory("LibreOffice");
        var locator = new LibreOfficeLocator(new PathService(), new FakeProcessRunner());

        var result = await locator.LocateAsync(
            new LibreOfficeOptions { ExecutablePath = selectedDirectory, ProbeVersion = false },
            CancellationToken.None);

        Assert.False(result.IsAvailable);
        Assert.Contains("does not contain", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CommandBuilder_IncludesRequiredHeadlessArguments()
    {
        using var temp = TempDirectory.Create();
        var inputPath = Path.Combine(temp.Path, "input.docx");
        var outputDirectory = Path.Combine(temp.Path, "out");
        var profileDirectory = Path.Combine(temp.Path, "profile");
        var builder = new LibreOfficeCommandBuilder();

        var command = builder.BuildConvertToPdfCommand(
            "soffice.com",
            inputPath,
            outputDirectory,
            profileDirectory);

        Assert.Contains("--headless", command.Arguments);
        Assert.Contains("--nologo", command.Arguments);
        Assert.Contains("--nodefault", command.Arguments);
        Assert.Contains("--nofirststartwizard", command.Arguments);
        Assert.Contains("--norestore", command.Arguments);
        Assert.Contains("--convert-to", command.Arguments);
        Assert.Contains("pdf", command.Arguments);
        Assert.Contains("--outdir", command.Arguments);
        Assert.Contains(outputDirectory, command.Arguments);
        Assert.Contains(inputPath, command.Arguments);
    }

    [Fact]
    public void CommandBuilder_UsesOutputDirectoryAndInputPathSafely()
    {
        using var temp = TempDirectory.Create();
        var inputPath = Path.Combine(temp.Path, "input.docx");
        var outputDirectory = Path.Combine(temp.Path, "out");
        var profileDirectory = Path.Combine(temp.Path, "profile");
        var builder = new LibreOfficeCommandBuilder();

        var command = builder.BuildConvertToPdfCommand(
            "soffice.com",
            inputPath,
            outputDirectory,
            profileDirectory);

        var outDirIndex = Array.IndexOf(command.Arguments.ToArray(), "--outdir") + 1;
        Assert.Equal(outputDirectory, command.Arguments[outDirIndex]);
        Assert.Equal(inputPath, command.Arguments[^1]);
        Assert.Equal(Path.Combine(outputDirectory, "input.pdf"), command.ExpectedPdfPath);
    }

    [Fact]
    public void CommandBuilder_GeneratesTempProfileFileUri()
    {
        using var temp = TempDirectory.Create();
        var builder = new LibreOfficeCommandBuilder();

        var command = builder.BuildConvertToPdfCommand(
            "soffice.com",
            Path.Combine(temp.Path, "input.docx"),
            Path.Combine(temp.Path, "out"),
            Path.Combine(temp.Path, "profile"));

        var profileArgument = Assert.Single(
            command.Arguments,
            argument => argument.StartsWith("-env:UserInstallation=", StringComparison.Ordinal));
        Assert.Contains("file:///", profileArgument, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\\", profileArgument);
    }

    [Fact]
    public async Task ConvertToPdfAsync_ReturnsFailure_ForUnsupportedExtension()
    {
        using var temp = TempDirectory.Create();
        var inputPath = temp.WriteFile("note.txt", "not supported");
        var engine = CreateEngine();

        var result = await engine.ConvertToPdfAsync(
            new ConversionRequest(inputPath, temp.CreateSubdirectory("out")),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ConversionStatus.UnsupportedInput, result.Status);
        Assert.Contains("Unsupported", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConvertToPdfAsync_ReturnsFailure_ForPdfNoOp()
    {
        using var temp = TempDirectory.Create();
        var inputPath = temp.WriteFile("already.pdf", "%PDF");
        var engine = CreateEngine();

        var result = await engine.ConvertToPdfAsync(
            new ConversionRequest(inputPath, temp.CreateSubdirectory("out")),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ConversionStatus.UnsupportedInput, result.Status);
        Assert.Contains("already PDF", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConvertToPdfAsync_ReturnsFailure_ForMissingInput()
    {
        using var temp = TempDirectory.Create();
        var engine = CreateEngine();

        var result = await engine.ConvertToPdfAsync(
            new ConversionRequest(Path.Combine(temp.Path, "missing.docx"), temp.CreateSubdirectory("out")),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ConversionStatus.MissingInput, result.Status);
    }

    [Fact]
    public async Task ConvertToPdfAsync_ReturnsFailure_ForTimeout()
    {
        using var temp = TempDirectory.Create();
        var inputPath = temp.WriteFile("input.docx", "fake docx");
        var engine = CreateEngine(
            new FakeProcessRunner((_, _, _, _) =>
                Task.FromResult(new ProcessRunResult(null, true, "out", "err", TimeSpan.FromSeconds(10)))));

        var result = await engine.ConvertToPdfAsync(
            new ConversionRequest(inputPath, temp.CreateSubdirectory("out")),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ConversionStatus.TimedOut, result.Status);
        Assert.Contains("timed out", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConvertToPdfAsync_ReturnsFailure_ForNonZeroExit()
    {
        using var temp = TempDirectory.Create();
        var inputPath = temp.WriteFile("input.docx", "fake docx");
        var engine = CreateEngine(
            new FakeProcessRunner((_, _, _, _) =>
                Task.FromResult(new ProcessRunResult(1, false, "out", "err", TimeSpan.FromSeconds(1)))));

        var result = await engine.ConvertToPdfAsync(
            new ConversionRequest(inputPath, temp.CreateSubdirectory("out")),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ConversionStatus.ProcessFailed, result.Status);
        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public async Task ConvertToPdfAsync_ReturnsFailure_WhenPdfNotProduced()
    {
        using var temp = TempDirectory.Create();
        var inputPath = temp.WriteFile("input.docx", "fake docx");
        var engine = CreateEngine(
            new FakeProcessRunner((_, _, _, _) =>
                Task.FromResult(new ProcessRunResult(0, false, "ok", string.Empty, TimeSpan.FromSeconds(1)))));

        var result = await engine.ConvertToPdfAsync(
            new ConversionRequest(inputPath, temp.CreateSubdirectory("out")),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ConversionStatus.OutputMissing, result.Status);
    }

    [Fact]
    public async Task ConvertToPdfAsync_ReturnsSuccess_WhenFakeRunnerCreatesPdf()
    {
        using var temp = TempDirectory.Create();
        var inputPath = temp.WriteFile("input.docx", "fake docx");
        var outputDirectory = temp.CreateSubdirectory("out");
        var expectedPdf = Path.Combine(outputDirectory, "input.pdf");
        var engine = CreateEngine(
            new FakeProcessRunner((_, _, _, _) =>
            {
                File.WriteAllText(expectedPdf, "%PDF-1.7");
                return Task.FromResult(new ProcessRunResult(0, false, "ok", string.Empty, TimeSpan.FromSeconds(1)));
            }));

        var result = await engine.ConvertToPdfAsync(
            new ConversionRequest(inputPath, outputDirectory),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(ConversionStatus.Succeeded, result.Status);
        Assert.Equal(expectedPdf, result.OutputPdfPath);
        Assert.True(File.Exists(expectedPdf));
    }

    [Fact]
    public async Task ConvertToPdfAsync_HandlesPathsWithSpaces()
    {
        using var temp = TempDirectory.Create();
        var inputPath = temp.WriteFile("input with spaces.docx", "fake docx");
        var outputDirectory = temp.CreateSubdirectory("out with spaces");
        var expectedPdf = Path.Combine(outputDirectory, "input with spaces.pdf");
        var fakeRunner = new FakeProcessRunner((_, _, _, _) =>
        {
            File.WriteAllText(expectedPdf, "%PDF-1.7");
            return Task.FromResult(new ProcessRunResult(0, false, string.Empty, string.Empty, TimeSpan.FromSeconds(1)));
        });
        var engine = CreateEngine(fakeRunner);

        var result = await engine.ConvertToPdfAsync(
            new ConversionRequest(inputPath, outputDirectory),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains(inputPath, fakeRunner.LastArguments);
        Assert.Contains(outputDirectory, fakeRunner.LastArguments);
    }

    [Fact]
    public async Task ConvertToPdfAsync_CreatesOutputDirectorySafely()
    {
        using var temp = TempDirectory.Create();
        var inputPath = temp.WriteFile("input.docx", "fake docx");
        var outputDirectory = Path.Combine(temp.Path, "new-output");
        var expectedPdf = Path.Combine(outputDirectory, "input.pdf");
        var engine = CreateEngine(
            new FakeProcessRunner((_, _, _, _) =>
            {
                Assert.True(Directory.Exists(outputDirectory));
                File.WriteAllText(expectedPdf, "%PDF-1.7");
                return Task.FromResult(new ProcessRunResult(0, false, string.Empty, string.Empty, TimeSpan.FromSeconds(1)));
            }));

        var result = await engine.ConvertToPdfAsync(
            new ConversionRequest(inputPath, outputDirectory),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(Directory.Exists(outputDirectory));
    }

    [Fact]
    public async Task ConvertToPdfAsync_ReturnsFailure_WhenOutputExistsAndOverwriteDisabled()
    {
        using var temp = TempDirectory.Create();
        var inputPath = temp.WriteFile("input.docx", "fake docx");
        var outputDirectory = temp.CreateSubdirectory("out");
        File.WriteAllText(Path.Combine(outputDirectory, "input.pdf"), "existing");
        var engine = CreateEngine();

        var result = await engine.ConvertToPdfAsync(
            new ConversionRequest(inputPath, outputDirectory),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ConversionStatus.OutputCollision, result.Status);
    }

    private static LibreOfficeConversionEngine CreateEngine(FakeProcessRunner? processRunner = null)
    {
        processRunner ??= new FakeProcessRunner();
        return new LibreOfficeConversionEngine(
            new FakeLibreOfficeLocator(),
            processRunner,
            new PathService(),
            new LibreOfficeCommandBuilder());
    }

    private sealed class FakeLibreOfficeLocator : ILibreOfficeLocator
    {
        public Task<EngineAvailability> LocateAsync(
            LibreOfficeOptions options,
            CancellationToken cancellationToken)
            => Task.FromResult(EngineAvailability.Available("fake-soffice.com", "fake"));
    }

    private sealed class FakeProcessRunner : IProcessRunner
    {
        private readonly Func<string, IReadOnlyList<string>, TimeSpan, CancellationToken, Task<ProcessRunResult>> _handler;

        public FakeProcessRunner()
            : this((_, _, _, _) =>
                Task.FromResult(new ProcessRunResult(0, false, string.Empty, string.Empty, TimeSpan.Zero)))
        {
        }

        public FakeProcessRunner(
            Func<string, IReadOnlyList<string>, TimeSpan, CancellationToken, Task<ProcessRunResult>> handler)
        {
            _handler = handler;
        }

        public IReadOnlyList<string> LastArguments { get; private set; } = Array.Empty<string>();

        public Task<ProcessRunResult> RunAsync(
            string executablePath,
            IReadOnlyList<string> arguments,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            LastArguments = arguments.ToArray();
            return _handler(executablePath, arguments, timeout, cancellationToken);
        }
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
                "SnappyDocsConvertTests",
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

        public string WriteFile(string fileName, string content)
        {
            var path = System.IO.Path.Combine(Path, fileName);
            File.WriteAllText(path, content);
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
