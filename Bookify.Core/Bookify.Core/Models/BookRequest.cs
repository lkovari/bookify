namespace Bookify.Core.Models;

public record BookRequest
{
    public required string Url { get; init; }
    public string? Title { get; init; }
}

