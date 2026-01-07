using Bookify.Core.Models;

namespace Bookify.Core.Abstractions;

public interface ITocExtractor
{
    bool CanHandle(Uri url);
    Task<TocNode> ExtractAsync(Uri url, string html, CancellationToken cancellationToken = default);
}

