using System.Collections.Concurrent;
using Bookify.Core.Models;

namespace Bookify.WebApi.Services;

public sealed class JobStateService
{
    private readonly ConcurrentDictionary<Guid, BookJobStatus> _jobs = new();

    public void SetStatus(Guid jobId, BookJobStatus status)
    {
        _jobs[jobId] = status;
    }

    public BookJobStatus? GetStatus(Guid jobId)
    {
        return _jobs.TryGetValue(jobId, out var status) ? status : null;
    }

    public void RemoveJob(Guid jobId)
    {
        _jobs.TryRemove(jobId, out _);
    }

    public List<BookJobStatus> GetAllJobs()
    {
        return _jobs.Values.OrderByDescending(j => j.JobId).ToList();
    }
}

