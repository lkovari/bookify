using System.Collections.Concurrent;
using Bookify.Core.Models;

namespace Bookify.WebApi.Services;

public sealed class JobStateService
{
    private readonly ConcurrentDictionary<Guid, BookJobStatus> _jobs = new();
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _cancellationTokens = new();
    private readonly ConcurrentDictionary<Guid, string> _tempDirectories = new();

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
        if (_cancellationTokens.TryRemove(jobId, out var cts))
        {
            cts.Dispose();
        }
        _tempDirectories.TryRemove(jobId, out _);
    }

    public List<BookJobStatus> GetAllJobs()
    {
        return _jobs.Values.OrderByDescending(j => j.JobId).ToList();
    }

    public void RegisterCancellationToken(Guid jobId, CancellationTokenSource cts)
    {
        _cancellationTokens[jobId] = cts;
    }

    public bool CancelJob(Guid jobId)
    {
        if (_cancellationTokens.TryGetValue(jobId, out var cts))
        {
            if (!cts.Token.IsCancellationRequested)
            {
                cts.Cancel();
                return true;
            }
        }
        return false;
    }

    public void RegisterTempDirectory(Guid jobId, string tempDir)
    {
        _tempDirectories[jobId] = tempDir;
    }

    public string? GetTempDirectory(Guid jobId)
    {
        return _tempDirectories.TryGetValue(jobId, out var tempDir) ? tempDir : null;
    }
}

