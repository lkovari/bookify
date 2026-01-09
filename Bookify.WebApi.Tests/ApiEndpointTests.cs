using System.Net;
using System.Net.Http.Json;
using Bookify.Core.Models;
using Bookify.WebApi.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bookify.WebApi.Tests;

public sealed class ApiEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ApiEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Get_Root_RedirectsToSwagger()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("swagger", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Post_CreateBook_ReturnsJobId()
    {
        using var client = _factory.CreateClient();
        var request = new BookRequest
        {
            Url = "https://example.com",
            Title = "Test Book"
        };

        using var response = await client.PostAsJsonAsync("/api/books", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("jobId"));
    }

    [Fact]
    public async Task Get_ListBooks_ReturnsJobsList()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/books");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("jobs"));
        Assert.True(result.ContainsKey("count"));
    }

    [Fact]
    public async Task Get_BookStatus_NonExistentJob_ReturnsNotFound()
    {
        using var client = _factory.CreateClient();
        var nonExistentJobId = Guid.NewGuid();

        using var response = await client.GetAsync($"/api/books/{nonExistentJobId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_BookStatus_ExistingJob_ReturnsStatus()
    {
        using var client = _factory.CreateClient();
        var request = new BookRequest
        {
            Url = "https://example.com",
            Title = "Test Book"
        };

        var createResponse = await client.PostAsJsonAsync("/api/books", request);
        var createResult = await createResponse.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        var jobId = Guid.Parse(createResult!["jobId"].ToString()!);

        await Task.Delay(100);

        using var response = await client.GetAsync($"/api/books/{jobId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("jobId"));
        Assert.True(result.ContainsKey("state"));
    }

    [Fact]
    public async Task Get_DownloadBook_NonExistentJob_ReturnsNotFound()
    {
        using var client = _factory.CreateClient();
        var nonExistentJobId = Guid.NewGuid();

        using var response = await client.GetAsync($"/api/books/{nonExistentJobId}/file");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_DownloadBook_JobNotCompleted_ReturnsBadRequest()
    {
        using var client = _factory.CreateClient();
        var request = new BookRequest
        {
            Url = "https://example.com",
            Title = "Test Book"
        };

        var createResponse = await client.PostAsJsonAsync("/api/books", request);
        var createResult = await createResponse.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        var jobId = Guid.Parse(createResult!["jobId"].ToString()!);

        await Task.Delay(100);

        using var response = await client.GetAsync($"/api/books/{jobId}/file");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("error"));
        Assert.True(result.ContainsKey("message"));
        Assert.True(result.ContainsKey("pagesTotal"));
        Assert.True(result.ContainsKey("pagesRendered"));
    }

    [Fact]
    public async Task Post_CancelBook_NonExistentJob_ReturnsNotFound()
    {
        using var client = _factory.CreateClient();
        var nonExistentJobId = Guid.NewGuid();

        using var response = await client.PostAsync($"/api/books/{nonExistentJobId}/cancel", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_CancelBook_ExistingPendingJob_ReturnsOk()
    {
        using var client = _factory.CreateClient();
        var request = new BookRequest
        {
            Url = "https://example.com",
            Title = "Test Book"
        };

        var createResponse = await client.PostAsJsonAsync("/api/books", request);
        var createResult = await createResponse.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        var jobId = Guid.Parse(createResult!["jobId"].ToString()!);

        await Task.Delay(100);

        using var response = await client.PostAsync($"/api/books/{jobId}/cancel", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("message"));
    }

    [Fact]
    public async Task Post_CancelAndSaveBook_NonExistentJob_ReturnsNotFound()
    {
        using var client = _factory.CreateClient();
        var nonExistentJobId = Guid.NewGuid();

        using var response = await client.PostAsync($"/api/books/{nonExistentJobId}/cancel-and-save", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_CancelAndSaveBook_NoPdfFiles_ReturnsBadRequest()
    {
        using var scope = _factory.Services.CreateScope();
        var jobStateService = scope.ServiceProvider.GetRequiredService<JobStateService>();
        var jobId = Guid.NewGuid();

        jobStateService.SetStatus(jobId, new BookJobStatus
        {
            JobId = jobId,
            State = JobState.Running,
            PagesTotal = 10,
            PagesRendered = 0
        });

        var tempDir = Path.Combine(Path.GetTempPath(), "bookify", jobId.ToString());
        Directory.CreateDirectory(tempDir);
        jobStateService.RegisterTempDirectory(jobId, tempDir);

        var cts = new CancellationTokenSource();
        jobStateService.RegisterCancellationToken(jobId, cts);

        using var client = _factory.CreateClient();

        using var response = await client.PostAsync($"/api/books/{jobId}/cancel-and-save", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        if (Directory.Exists(tempDir))
        {
            Directory.Delete(tempDir, true);
        }
    }
}

