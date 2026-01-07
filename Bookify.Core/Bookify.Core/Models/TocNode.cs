namespace Bookify.Core.Models;

public record TocNode
{
    public required string Title { get; init; }
    public required Uri Url { get; init; }
    public List<TocNode> Children { get; init; } = new();
}

