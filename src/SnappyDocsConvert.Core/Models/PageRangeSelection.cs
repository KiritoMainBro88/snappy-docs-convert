namespace SnappyDocsConvert.Core.Models;

public sealed record PageRangeSelection
{
    private PageRangeSelection(
        bool success,
        IReadOnlyList<int> pages,
        string? errorMessage)
    {
        Success = success;
        Pages = pages;
        ErrorMessage = errorMessage;
    }

    public bool Success { get; }

    public IReadOnlyList<int> Pages { get; }

    public string? ErrorMessage { get; }

    public static PageRangeSelection Ok(IReadOnlyList<int> pages)
        => new(true, pages, null);

    public static PageRangeSelection Failed(string errorMessage)
        => new(false, Array.Empty<int>(), errorMessage);
}
