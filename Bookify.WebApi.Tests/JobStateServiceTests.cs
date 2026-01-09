using Bookify.Core.Models;
using Bookify.WebApi.Services;
using Xunit;

namespace Bookify.WebApi.Tests;

public sealed class JobStateServiceTests
{
    [Fact]
    public void SetStatus_NewJob_StoresStatus()
    {
        var service = new JobStateService();
        var jobId = Guid.NewGuid();
        var status = new BookJobStatus
        {
            JobId = jobId,
            State = JobState.Pending,
            PagesTotal = 0,
            PagesRendered = 0
        };

        service.SetStatus(jobId, status);

        var retrieved = service.GetStatus(jobId);
        Assert.NotNull(retrieved);
        Assert.Equal(jobId, retrieved.JobId);
        Assert.Equal(JobState.Pending, retrieved.State);
    }

    [Fact]
    public void SetStatus_ExistingJob_UpdatesStatus()
    {
        var service = new JobStateService();
        var jobId = Guid.NewGuid();
        var initialStatus = new BookJobStatus
        {
            JobId = jobId,
            State = JobState.Pending,
            PagesTotal = 0,
            PagesRendered = 0
        };

        service.SetStatus(jobId, initialStatus);

        var updatedStatus = new BookJobStatus
        {
            JobId = jobId,
            State = JobState.Running,
            PagesTotal = 10,
            PagesRendered = 5
        };

        service.SetStatus(jobId, updatedStatus);

        var retrieved = service.GetStatus(jobId);
        Assert.NotNull(retrieved);
        Assert.Equal(JobState.Running, retrieved.State);
        Assert.Equal(10, retrieved.PagesTotal);
        Assert.Equal(5, retrieved.PagesRendered);
    }

    [Fact]
    public void GetStatus_NonExistentJob_ReturnsNull()
    {
        var service = new JobStateService();
        var jobId = Guid.NewGuid();

        var result = service.GetStatus(jobId);

        Assert.Null(result);
    }

    [Fact]
    public void GetAllJobs_NoJobs_ReturnsEmptyList()
    {
        var service = new JobStateService();

        var result = service.GetAllJobs();

        Assert.Empty(result);
    }

    [Fact]
    public void GetAllJobs_MultipleJobs_ReturnsAllJobs()
    {
        var service = new JobStateService();
        var jobId1 = Guid.NewGuid();
        var jobId2 = Guid.NewGuid();

        service.SetStatus(jobId1, new BookJobStatus
        {
            JobId = jobId1,
            State = JobState.Pending,
            PagesTotal = 0,
            PagesRendered = 0
        });

        service.SetStatus(jobId2, new BookJobStatus
        {
            JobId = jobId2,
            State = JobState.Running,
            PagesTotal = 10,
            PagesRendered = 5
        });

        var result = service.GetAllJobs();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void GetAllJobs_ReturnsJobsOrderedByJobIdDescending()
    {
        var service = new JobStateService();
        var jobId1 = Guid.NewGuid();
        var jobId2 = Guid.NewGuid();

        service.SetStatus(jobId1, new BookJobStatus
        {
            JobId = jobId1,
            State = JobState.Pending,
            PagesTotal = 0,
            PagesRendered = 0
        });

        service.SetStatus(jobId2, new BookJobStatus
        {
            JobId = jobId2,
            State = JobState.Running,
            PagesTotal = 10,
            PagesRendered = 5
        });

        var result = service.GetAllJobs();

        Assert.True(result[0].JobId.CompareTo(result[1].JobId) >= 0);
    }

    [Fact]
    public void RegisterCancellationToken_NewJob_StoresToken()
    {
        var service = new JobStateService();
        var jobId = Guid.NewGuid();
        var cts = new CancellationTokenSource();

        service.RegisterCancellationToken(jobId, cts);

        var canceled = service.CancelJob(jobId);
        Assert.True(canceled);
        Assert.True(cts.Token.IsCancellationRequested);
    }

    [Fact]
    public void CancelJob_NonExistentJob_ReturnsFalse()
    {
        var service = new JobStateService();
        var jobId = Guid.NewGuid();

        var result = service.CancelJob(jobId);

        Assert.False(result);
    }

    [Fact]
    public void CancelJob_AlreadyCanceled_ReturnsFalse()
    {
        var service = new JobStateService();
        var jobId = Guid.NewGuid();
        var cts = new CancellationTokenSource();

        service.RegisterCancellationToken(jobId, cts);
        service.CancelJob(jobId);

        var result = service.CancelJob(jobId);

        Assert.False(result);
    }

    [Fact]
    public void RegisterTempDirectory_NewJob_StoresDirectory()
    {
        var service = new JobStateService();
        var jobId = Guid.NewGuid();
        var tempDir = "/tmp/test";

        service.RegisterTempDirectory(jobId, tempDir);

        var result = service.GetTempDirectory(jobId);
        Assert.Equal(tempDir, result);
    }

    [Fact]
    public void GetTempDirectory_NonExistentJob_ReturnsNull()
    {
        var service = new JobStateService();
        var jobId = Guid.NewGuid();

        var result = service.GetTempDirectory(jobId);

        Assert.Null(result);
    }

    [Fact]
    public void RemoveJob_ExistingJob_RemovesAllData()
    {
        var service = new JobStateService();
        var jobId = Guid.NewGuid();
        var status = new BookJobStatus
        {
            JobId = jobId,
            State = JobState.Pending,
            PagesTotal = 0,
            PagesRendered = 0
        };
        var cts = new CancellationTokenSource();
        var tempDir = "/tmp/test";

        service.SetStatus(jobId, status);
        service.RegisterCancellationToken(jobId, cts);
        service.RegisterTempDirectory(jobId, tempDir);

        service.RemoveJob(jobId);

        Assert.Null(service.GetStatus(jobId));
        Assert.Null(service.GetTempDirectory(jobId));
        Assert.False(service.CancelJob(jobId));
    }

    [Fact]
    public void RemoveJob_DisposesCancellationTokenSource()
    {
        var service = new JobStateService();
        var jobId = Guid.NewGuid();
        var cts = new CancellationTokenSource();

        service.RegisterCancellationToken(jobId, cts);
        service.RemoveJob(jobId);

        Assert.Throws<ObjectDisposedException>(() => cts.Token);
    }
}

