using System.Collections.Concurrent;
using AngleSharp;
using AngleSharp.Html.Dom;

namespace Bookify.Core.Services;

public sealed class LinkDiscoveryService
{
    private readonly HttpClient _httpClient;
    private readonly int _maxPages;
    private readonly int _maxDepth;

    public LinkDiscoveryService(HttpClient httpClient, int maxPages = 300, int maxDepth = 10)
    {
        _httpClient = httpClient;
        _maxPages = maxPages;
        _maxDepth = maxDepth;
    }

    public async Task<List<Uri>> DiscoverAsync(Uri startUrl, CancellationToken cancellationToken = default)
    {
        var discovered = new ConcurrentDictionary<string, Uri>();
        var queue = new Queue<(Uri url, int depth)>();
        queue.Enqueue((startUrl, 0));

        var baseHost = startUrl.Host;

        while (queue.Count > 0 && discovered.Count < _maxPages)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var (currentUrl, depth) = queue.Dequeue();

            if (depth > _maxDepth)
            {
                continue;
            }

            var urlKey = NormalizeUrl(currentUrl);
            if (discovered.ContainsKey(urlKey))
            {
                continue;
            }

            if (currentUrl.Host != baseHost)
            {
                continue;
            }

            try
            {
                var html = await FetchHtmlAsync(currentUrl, cancellationToken);
                if (string.IsNullOrWhiteSpace(html))
                {
                    continue;
                }

                discovered[urlKey] = currentUrl;

                var links = ExtractLinks(html, currentUrl, baseHost);
                foreach (var link in links)
                {
                    var normalizedLink = NormalizeUrl(link);
                    if (!discovered.ContainsKey(normalizedLink) && link.Host == baseHost)
                    {
                        queue.Enqueue((link, depth + 1));
                    }
                }
            }
            catch
            {
                continue;
            }
        }

        var uniqueUrls = discovered.Values
            .GroupBy(u => NormalizeUrl(u))
            .Select(g => g.First())
            .OrderBy(u => u.ToString())
            .ToList();
        
        return uniqueUrls;
    }

    private async Task<string> FetchHtmlAsync(Uri url, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("Bookify/1.0");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private List<Uri> ExtractLinks(string html, Uri baseUrl, string baseHost)
    {
        var links = new List<Uri>();
        var context = BrowsingContext.New(Configuration.Default);
        var document = context.OpenAsync(req => req.Content(html)).Result;

        var anchorElements = document.QuerySelectorAll("a[href]")
            .OfType<IHtmlAnchorElement>();

        foreach (var anchor in anchorElements)
        {
            var href = anchor.GetAttribute("href");
            if (string.IsNullOrWhiteSpace(href))
            {
                continue;
            }

            if (href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
                href.StartsWith("tel:", StringComparison.OrdinalIgnoreCase) ||
                href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (Uri.TryCreate(baseUrl, href, out var absoluteUri))
            {
                var normalized = new UriBuilder(absoluteUri)
                {
                    Fragment = string.Empty
                }.Uri;

                if (normalized.Host == baseHost && normalized.Scheme is "http" or "https")
                {
                    links.Add(normalized);
                }
            }
        }

        return links;
    }

    private static string NormalizeUrl(Uri uri)
    {
        var builder = new UriBuilder(uri)
        {
            Fragment = string.Empty,
            Query = string.Empty,
            Port = uri.IsDefaultPort ? -1 : uri.Port
        };
        var normalized = builder.Uri.ToString().TrimEnd('/');
        if (normalized.EndsWith("/index", StringComparison.OrdinalIgnoreCase) || 
            normalized.EndsWith("/index.html", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized.Replace("/index", "", StringComparison.OrdinalIgnoreCase)
                                   .Replace("/index.html", "", StringComparison.OrdinalIgnoreCase);
        }
        return normalized;
    }
}

