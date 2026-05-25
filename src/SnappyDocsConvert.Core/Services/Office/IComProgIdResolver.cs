namespace SnappyDocsConvert.Core.Services.Office;

public interface IComProgIdResolver
{
    Type? GetTypeFromProgId(string progId);
}
