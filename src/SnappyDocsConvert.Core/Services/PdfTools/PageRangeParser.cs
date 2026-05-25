using SnappyDocsConvert.Core.Models;

namespace SnappyDocsConvert.Core.Services.PdfTools;

public sealed class PageRangeParser
{
    public PageRangeSelection Parse(string? value, int? maxPage = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return PageRangeSelection.Failed("Page range is required.");
        }

        var pages = new SortedSet<int>();
        foreach (var rawPart in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (rawPart.Contains('-', StringComparison.Ordinal))
            {
                var bounds = rawPart.Split('-', StringSplitOptions.TrimEntries);
                if (bounds.Length != 2 ||
                    !int.TryParse(bounds[0], out var start) ||
                    !int.TryParse(bounds[1], out var end))
                {
                    return PageRangeSelection.Failed($"Invalid page range segment: {rawPart}");
                }

                if (start <= 0 || end <= 0 || end < start)
                {
                    return PageRangeSelection.Failed($"Invalid page range segment: {rawPart}");
                }

                for (var page = start; page <= end; page++)
                {
                    pages.Add(page);
                }
            }
            else
            {
                if (!int.TryParse(rawPart, out var page) || page <= 0)
                {
                    return PageRangeSelection.Failed($"Invalid page number: {rawPart}");
                }

                pages.Add(page);
            }
        }

        if (pages.Count == 0)
        {
            return PageRangeSelection.Failed("Page range did not contain any pages.");
        }

        if (maxPage is not null && pages.Any(page => page > maxPage.Value))
        {
            return PageRangeSelection.Failed($"Page range exceeds document page count ({maxPage.Value}).");
        }

        return PageRangeSelection.Ok(pages.ToArray());
    }
}
