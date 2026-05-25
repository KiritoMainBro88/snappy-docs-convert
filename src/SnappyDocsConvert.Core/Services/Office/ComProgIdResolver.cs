namespace SnappyDocsConvert.Core.Services.Office;

public sealed class ComProgIdResolver : IComProgIdResolver
{
    public Type? GetTypeFromProgId(string progId)
        => OperatingSystem.IsWindows()
            ? Type.GetTypeFromProgID(progId)
            : null;
}
