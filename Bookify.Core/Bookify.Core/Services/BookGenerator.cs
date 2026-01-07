using System.Collections.Concurrent;
using Bookify.Core.Abstractions;
using Bookify.Core.Models;

namespace Bookify.Core.Services;

public sealed class BookGenerator
{
    private readonly UrlValidator _urlValidator;
    private readonly LinkDiscoveryService _linkDiscovery;
    private readonly ITocExtractor _tocExtractor;
    private readonly PlaywrightPdfRenderer _pdfRenderer;
    private readonly PdfMerger _pdfMerger;
    private readonly HttpClient _httpClient;

    public BookGenerator(
        UrlValidator urlValidator,
        LinkDiscoveryService linkDiscovery,
        ITocExtractor tocExtractor,
        PlaywrightPdfRenderer pdfRenderer,
        PdfMerger pdfMerger,
        HttpClient httpClient)
    {
        _urlValidator = urlValidator;
        _linkDiscovery = linkDiscovery;
        _tocExtractor = tocExtractor;
        _pdfRenderer = pdfRenderer;
        _pdfMerger = pdfMerger;
        _httpClient = httpClient;
    }

    public async Task<string> GenerateAsync(
        string url,
        string? title,
        Guid jobId,
        IProgress<BookJobStatus>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "bookify", jobId.ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            progress?.Report(new BookJobStatus
            {
                JobId = jobId,
                State = JobState.Running,
                PagesTotal = 0,
                PagesRendered = 0
            });

            var validatedUrl = await _urlValidator.ValidateAsync(url, cancellationToken);

            var html = await FetchHtmlAsync(validatedUrl, cancellationToken);
            var toc = await _tocExtractor.ExtractAsync(validatedUrl, html, cancellationToken);

            var pages = await _linkDiscovery.DiscoverAsync(validatedUrl, cancellationToken);
            if (pages.Count == 0)
            {
                pages = new List<Uri> { validatedUrl };
            }

            var orderedPages = OrderPagesByToc(pages, toc);
            var totalPages = orderedPages.Count;

            progress?.Report(new BookJobStatus
            {
                JobId = jobId,
                State = JobState.Running,
                PagesTotal = totalPages,
                PagesRendered = 0
            });

            var pdfFiles = new ConcurrentDictionary<int, string>();
            var renderedCount = 0;

            await Parallel.ForEachAsync(orderedPages.Select((url, index) => (url, index)), new ParallelOptions
            {
                MaxDegreeOfParallelism = 2,
                CancellationToken = cancellationToken
            }, async (item, ct) =>
            {
                var (pageUrl, index) = item;
                var pdfPath = Path.Combine(tempDir, $"page_{index:D4}.pdf");
                await _pdfRenderer.RenderPageAsync(pageUrl, pdfPath, ct);
                pdfFiles[index] = pdfPath;

                var current = Interlocked.Increment(ref renderedCount);
                progress?.Report(new BookJobStatus
                {
                    JobId = jobId,
                    State = JobState.Running,
                    PagesTotal = totalPages,
                    PagesRendered = current
                });
            });

            var fileName = GenerateFileName(title, jobId);
            var finalPdfPath = Path.Combine(tempDir, fileName);
            var orderedPdfFiles = pdfFiles
                .OrderBy(kvp => kvp.Key)
                .Select(kvp => kvp.Value)
                .Where(f => !string.IsNullOrEmpty(f) && File.Exists(f))
                .ToList();

            _pdfMerger.MergeFiles(orderedPdfFiles, finalPdfPath);

            progress?.Report(new BookJobStatus
            {
                JobId = jobId,
                State = JobState.Completed,
                PagesTotal = totalPages,
                PagesRendered = totalPages,
                OutputFilePath = finalPdfPath
            });

            return finalPdfPath;
        }
        catch (Exception ex)
        {
            progress?.Report(new BookJobStatus
            {
                JobId = jobId,
                State = JobState.Failed,
                PagesTotal = 0,
                PagesRendered = 0,
                ErrorMessage = ex.Message
            });
            throw;
        }
        finally
        {
            await Task.Delay(1000, cancellationToken);
        }
    }

    private async Task<string> FetchHtmlAsync(Uri url, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("Bookify/1.0");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private List<Uri> OrderPagesByToc(List<Uri> pages, TocNode toc)
    {
        var ordered = new List<Uri>();
        var seen = new HashSet<string>();
        var pageSet = new HashSet<string>(pages.Select(p => NormalizePageUrl(p)));

        string NormalizePageUrl(Uri uri)
        {
            var builder = new UriBuilder(uri)
            {
                Fragment = string.Empty,
                Query = string.Empty
            };
            return builder.Uri.ToString().TrimEnd('/').ToLowerInvariant();
        }

        void TraverseToc(TocNode node)
        {
            var normalized = NormalizePageUrl(node.Url);
            if (pageSet.Contains(normalized) && !seen.Contains(normalized))
            {
                var matchingPage = pages.FirstOrDefault(p => NormalizePageUrl(p) == normalized);
                if (matchingPage != null)
                {
                    ordered.Add(matchingPage);
                    seen.Add(normalized);
                }
            }

            foreach (var child in node.Children)
            {
                TraverseToc(child);
            }
        }

        TraverseToc(toc);

        var remaining = pages
            .Where(p => !seen.Contains(NormalizePageUrl(p)))
            .ToList();
        ordered.AddRange(remaining);

        return ordered;
    }

    private static string GenerateFileName(string? title, Guid jobId)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return $"book{jobId}.pdf";
        }

        var parts = title.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        var fileName = string.Join("", parts.Select(part =>
        {
            if (string.IsNullOrEmpty(part))
            {
                return string.Empty;
            }

            if (part.Length == 1)
            {
                return part.ToUpperInvariant();
            }

            return char.ToUpperInvariant(part[0]) + part.Substring(1);
        }));

        return $"{fileName}{jobId}.pdf";
    }
}

