namespace Bookify.Core.Models;

public enum JobState
{
    Pending,
    Running,
    Completed,
    Failed
}

public record BookJobStatus
{
    public required Guid JobId { get; init; }
    public required JobState State { get; init; }
    public int PagesTotal { get; init; }
    public int PagesRendered { get; init; }
    public string? ErrorMessage { get; init; }
    public string? OutputFilePath { get; init; }
}

