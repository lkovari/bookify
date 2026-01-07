using Bookify.Core.Abstractions;
using Bookify.Core.Models;
using Bookify.Core.Services;
using Bookify.WebApi.Services;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Bookify API",
        Version = "v1",
        Description = "API for converting documentation websites to PDF books"
    });
});

builder.Services.AddSingleton<IDnsResolver, SystemDnsResolver>();
builder.Services.AddSingleton<JobStateService>();
builder.Services.AddSingleton<ITocExtractor, GenericNavTocExtractor>();
builder.Services.AddSingleton<PdfMerger>();

var httpClientBuilder = builder.Services.AddHttpClient();
httpClientBuilder.AddHttpClient<UrlValidator>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(10);
});
httpClientBuilder.AddHttpClient<LinkDiscoveryService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(10);
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Bookify API v1");
    c.RoutePrefix = "swagger";
});

app.UseHttpsRedirection();

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapPost("/api/books", async (BookRequest request, JobStateService jobState, CancellationToken cancellationToken) =>
{
    var jobId = Guid.NewGuid();
    
    jobState.SetStatus(jobId, new BookJobStatus
    {
        JobId = jobId,
        State = JobState.Pending,
        PagesTotal = 0,
        PagesRendered = 0
    });

    var serviceProvider = app.Services;
    
    _ = Task.Run(async () =>
    {
        PlaywrightPdfRenderer? pdfRenderer = null;
        using var jobCts = new CancellationTokenSource();
        var jobToken = jobCts.Token;
        
        try
        {
            jobState.SetStatus(jobId, new BookJobStatus
            {
                JobId = jobId,
                State = JobState.Running,
                PagesTotal = 0,
                PagesRendered = 0
            });

            var dnsResolver = serviceProvider.GetRequiredService<IDnsResolver>();
            var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            
            var urlValidatorHttpClient = httpClientFactory.CreateClient();
            urlValidatorHttpClient.Timeout = TimeSpan.FromMinutes(10);
            var urlValidator = new UrlValidator(dnsResolver, urlValidatorHttpClient);
            
            var linkDiscoveryHttpClient = httpClientFactory.CreateClient();
            linkDiscoveryHttpClient.Timeout = TimeSpan.FromMinutes(10);
            var linkDiscovery = new LinkDiscoveryService(linkDiscoveryHttpClient);
            
            var tocExtractor = serviceProvider.GetRequiredService<ITocExtractor>();
            pdfRenderer = new PlaywrightPdfRenderer();
            var pdfMerger = serviceProvider.GetRequiredService<PdfMerger>();
            
            var bookGeneratorHttpClient = httpClientFactory.CreateClient();
            bookGeneratorHttpClient.Timeout = TimeSpan.FromMinutes(10);
            
            var jobGenerator = new BookGenerator(urlValidator, linkDiscovery, tocExtractor, pdfRenderer, pdfMerger, bookGeneratorHttpClient);

            var progress = new Progress<BookJobStatus>(status =>
            {
                jobState.SetStatus(jobId, status);
            });

            var outputPath = await jobGenerator.GenerateAsync(
                request.Url,
                request.Title,
                jobId,
                progress,
                jobToken);

            var finalStatus = jobState.GetStatus(jobId);
            jobState.SetStatus(jobId, finalStatus! with 
            { 
                State = JobState.Completed,
                OutputFilePath = outputPath
            });
        }
        catch (OperationCanceledException)
        {
            var currentStatus = jobState.GetStatus(jobId);
            jobState.SetStatus(jobId, (currentStatus ?? new BookJobStatus
            {
                JobId = jobId,
                State = JobState.Pending,
                PagesTotal = 0,
                PagesRendered = 0
            }) with 
            { 
                State = JobState.Failed,
                ErrorMessage = "Job was canceled or timed out"
            });
        }
        catch (Exception ex)
        {
            var currentStatus = jobState.GetStatus(jobId);
            jobState.SetStatus(jobId, (currentStatus ?? new BookJobStatus
            {
                JobId = jobId,
                State = JobState.Pending,
                PagesTotal = 0,
                PagesRendered = 0
            }) with 
            { 
                State = JobState.Failed,
                ErrorMessage = ex.Message
            });
        }
        finally
        {
            if (pdfRenderer != null)
            {
                await pdfRenderer.DisposeAsync();
            }
        }
    });

    return Results.Ok(new { jobId });
})
.WithName("CreateBook")
.Produces<object>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest);

app.MapGet("/api/books", (JobStateService jobState) =>
{
    var jobs = jobState.GetAllJobs();
    return Results.Ok(new { jobs, count = jobs.Count });
})
.WithName("ListBooks")
.Produces<object>(StatusCodes.Status200OK);

app.MapGet("/api/books/{jobId:guid}", (Guid jobId, JobStateService jobState) =>
{
    var status = jobState.GetStatus(jobId);
    if (status == null)
    {
        return Results.NotFound(new { error = "Job not found", jobId });
    }
    
    var response = new
    {
        status.JobId,
        status.State,
        status.PagesTotal,
        status.PagesRendered,
        status.ErrorMessage,
        Progress = status.PagesTotal > 0 
            ? Math.Round((double)status.PagesRendered / status.PagesTotal * 100, 2) 
            : 0,
        IsCompleted = status.State == JobState.Completed,
        IsFailed = status.State == JobState.Failed,
        CanDownload = status.State == JobState.Completed && !string.IsNullOrEmpty(status.OutputFilePath)
    };
    
    return Results.Ok(response);
})
.WithName("GetBookStatus")
.Produces<object>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound);

app.MapGet("/api/books/{jobId:guid}/file", (Guid jobId, JobStateService jobState) =>
{
    var status = jobState.GetStatus(jobId);
    if (status == null)
    {
        return Results.NotFound(new { error = "Job not found", jobId });
    }

    if (status.State == JobState.Failed)
    {
        return Results.BadRequest(new 
        { 
            error = "Job failed", 
            jobId,
            message = status.ErrorMessage ?? "Unknown error occurred"
        });
    }

    if (status.State != JobState.Completed)
    {
        var progress = status.PagesTotal > 0 
            ? Math.Round((double)status.PagesRendered / status.PagesTotal * 100, 2) 
            : 0;
        return Results.BadRequest(new 
        { 
            error = "Job is not completed yet", 
            jobId,
            state = status.State.ToString(),
            progress = $"{progress}%",
            pagesRendered = status.PagesRendered,
            pagesTotal = status.PagesTotal,
            message = $"Processing: {status.PagesRendered} of {status.PagesTotal} pages rendered"
        });
    }

    if (string.IsNullOrEmpty(status.OutputFilePath))
    {
        return Results.BadRequest(new { error = "Output file path is missing", jobId });
    }

    if (!File.Exists(status.OutputFilePath))
    {
        return Results.NotFound(new { error = "PDF file not found", jobId, filePath = status.OutputFilePath });
    }

    var fileBytes = File.ReadAllBytes(status.OutputFilePath);
    var fileName = Path.GetFileName(status.OutputFilePath);
    return Results.File(fileBytes, "application/pdf", fileName);
})
.WithName("DownloadBook")
.Produces(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status404NotFound);

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

public partial class Program { }

