namespace SnappyDocsConvert.Core.Services;

public interface IPathService
{
    bool FileExists(string path);

    bool DirectoryExists(string path);

    void CreateDirectory(string path);

    string GetFullPath(string path);

    string GetTempPath();

    string? GetDirectoryName(string path);

    string GetFileNameWithoutExtension(string path);

    string GetExtension(string path);

    string Combine(params string[] paths);

    void DeleteDirectory(string path, bool recursive);

    IEnumerable<string> EnumerateFiles(string path, string searchPattern);
}
