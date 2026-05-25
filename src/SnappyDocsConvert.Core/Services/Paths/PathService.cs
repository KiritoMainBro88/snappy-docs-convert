using SnappyDocsConvert.Core.Services;

namespace SnappyDocsConvert.Core.Services.Paths;

public sealed class PathService : IPathService
{
    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public string GetFullPath(string path) => Path.GetFullPath(path);

    public string GetTempPath() => Path.GetTempPath();

    public string? GetDirectoryName(string path) => Path.GetDirectoryName(path);

    public string GetFileNameWithoutExtension(string path) => Path.GetFileNameWithoutExtension(path);

    public string GetExtension(string path) => Path.GetExtension(path);

    public string Combine(params string[] paths) => Path.Combine(paths);

    public void DeleteDirectory(string path, bool recursive) => Directory.Delete(path, recursive);

    public IEnumerable<string> EnumerateFiles(string path, string searchPattern)
        => Directory.EnumerateFiles(path, searchPattern);
}
