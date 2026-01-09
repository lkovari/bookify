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

            progress?.Report(new BookJobStatus
            {
                JobId = jobId,
                State = JobState.Running,
                PagesTotal = 0,
                PagesRendered = 0
            });

            var html = await FetchHtmlForTocAsync(validatedUrl, cancellationToken);
            var toc = await _tocExtractor.ExtractAsync(validatedUrl, html, cancellationToken);

            IEnumerable<Uri> GetAllTocUrls(TocNode node)
            {
                yield return node.Url;
                foreach (var child in node.Children)
                {
                    foreach (var childUrl in GetAllTocUrls(child))
                    {
                        yield return childUrl;
                    }
                }
            }

            var initialTocUrls = GetAllTocUrls(toc).ToList();
            var initialPageCount = Math.Max(1, initialTocUrls.Count);

            progress?.Report(new BookJobStatus
            {
                JobId = jobId,
                State = JobState.Running,
                PagesTotal = initialPageCount,
                PagesRendered = 0
            });

            var pages = await _linkDiscovery.DiscoverAsync(validatedUrl, cancellationToken);

            string NormalizePageUrlForMerge(Uri uri)
            {
                var builder = new UriBuilder(uri)
                {
                    Query = string.Empty
                };

                // UriBuilder.Fragment does NOT contain '#'. For "#/route" URLs it will be "/route".
                if (string.IsNullOrEmpty(builder.Fragment) || !builder.Fragment.StartsWith("/", StringComparison.Ordinal))
                {
                    builder.Fragment = string.Empty;
                }

                return builder.Uri.ToString().TrimEnd('/').ToLowerInvariant();
            }

            // Merge TOC urls into discovered pages (this is what fixes SPAs where crawler sees only bootstrap HTML)
            var tocUrls = initialTocUrls;
            var discoveredUrlSet = new HashSet<string>(pages.Select(NormalizePageUrlForMerge));

            foreach (var tocUrl in tocUrls)
            {
                var normalized = NormalizePageUrlForMerge(tocUrl);
                if (!discoveredUrlSet.Contains(normalized) && tocUrl.Host == validatedUrl.Host)
                {
                    pages.Add(tocUrl);
                    discoveredUrlSet.Add(normalized);
                }
            }

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
            var failedPages = new ConcurrentBag<(int index, Uri url, string error)>();

            await Parallel.ForEachAsync(orderedPages.Select((pageUrl, index) => (pageUrl, index)), new ParallelOptions
            {
                MaxDegreeOfParallelism = 2,
                CancellationToken = cancellationToken
            }, async (item, ct) =>
            {
                var (pageUrl, index) = item;
                var pdfPath = Path.Combine(tempDir, $"page_{index:D4}.pdf");

                try
                {
                    await _pdfRenderer.RenderPageAsync(pageUrl, pdfPath, ct);
                    
                    if (File.Exists(pdfPath))
                    {
                        pdfFiles[index] = pdfPath;
                        var current = Interlocked.Increment(ref renderedCount);
                        progress?.Report(new BookJobStatus
                        {
                            JobId = jobId,
                            State = JobState.Running,
                            PagesTotal = totalPages,
                            PagesRendered = current
                        });
                    }
                    else
                    {
                        failedPages.Add((index, pageUrl, "PDF file was not created"));
                    }
                }
                catch (Exception ex)
                {
                    failedPages.Add((index, pageUrl, ex.Message));
                }
            });

            if (pdfFiles.Count == 0)
            {
                var errorDetails = failedPages.Any()
                    ? $"All pages failed to render. Failed pages: {string.Join(", ", failedPages.Select(f => $"{f.url} ({f.error})"))}"
                    : "No pages were successfully rendered";
                
                progress?.Report(new BookJobStatus
                {
                    JobId = jobId,
                    State = JobState.Failed,
                    PagesTotal = totalPages,
                    PagesRendered = 0,
                    ErrorMessage = errorDetails
                });
                
                throw new InvalidOperationException(errorDetails);
            }

            var fileName = GenerateFileName(title, jobId);
            var finalPdfPath = Path.Combine(tempDir, fileName);

            var orderedPdfFiles = pdfFiles
                .OrderBy(kvp => kvp.Key)
                .Select(kvp => kvp.Value)
                .Where(f => !string.IsNullOrEmpty(f) && File.Exists(f))
                .ToList();

            _pdfMerger.MergeFiles(orderedPdfFiles, finalPdfPath);

            var errorMessage = failedPages.Any()
                ? $"Some pages failed to render ({failedPages.Count} of {totalPages}): {string.Join("; ", failedPages.OrderBy(f => f.index).Select(f => $"{f.url} - {f.error}"))}"
                : null;

            progress?.Report(new BookJobStatus
            {
                JobId = jobId,
                State = JobState.Completed,
                PagesTotal = totalPages,
                PagesRendered = pdfFiles.Count,
                OutputFilePath = finalPdfPath,
                ErrorMessage = errorMessage
            });

            return finalPdfPath;
        }
        catch (OperationCanceledException)
        {
            throw;
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
            if (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(1000, cancellationToken);
            }
        }
    }

    public string MergePartialPdfs(string tempDir, string? title, Guid jobId)
    {
        var pdfFiles = Directory.GetFiles(tempDir, "page_*.pdf")
            .OrderBy(f => f)
            .Where(f => File.Exists(f))
            .ToList();

        if (pdfFiles.Count == 0)
        {
            throw new InvalidOperationException("No PDF files found to merge");
        }

        var fileName = GenerateFileName(title, jobId);
        var finalPdfPath = Path.Combine(tempDir, fileName);
        _pdfMerger.MergeFiles(pdfFiles, finalPdfPath);
        return finalPdfPath;
    }

    private async Task<string> FetchHtmlForTocAsync(Uri url, CancellationToken cancellationToken)
    {
        try
        {
            var rendered = await _pdfRenderer.GetRenderedHtmlAsync(url, cancellationToken);
            if (!string.IsNullOrWhiteSpace(rendered))
            {
                return rendered;
            }
        }
        catch
        {
            // fallback below
        }

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
        var pageSet = new HashSet<string>(pages.Select(p => NormalizePageUrlForOrdering(p)));

        string NormalizePageUrlForOrdering(Uri uri)
        {
            var builder = new UriBuilder(uri)
            {
                Query = string.Empty
            };

            // UriBuilder.Fragment does NOT contain '#'. For "#/route" URLs it will be "/route".
            if (string.IsNullOrEmpty(builder.Fragment) || !builder.Fragment.StartsWith("/", StringComparison.Ordinal))
            {
                builder.Fragment = string.Empty;
            }

            return builder.Uri.ToString().TrimEnd('/').ToLowerInvariant();
        }

        void TraverseToc(TocNode node)
        {
            var normalized = NormalizePageUrlForOrdering(node.Url);
            if (pageSet.Contains(normalized) && !seen.Contains(normalized))
            {
                var matchingPage = pages.FirstOrDefault(p => NormalizePageUrlForOrdering(p) == normalized);
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
            .Where(p => !seen.Contains(NormalizePageUrlForOrdering(p)))
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