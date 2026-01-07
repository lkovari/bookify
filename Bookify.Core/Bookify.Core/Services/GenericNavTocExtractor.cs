using AngleSharp;
using AngleSharp.Html.Dom;
using Bookify.Core.Abstractions;
using Bookify.Core.Models;

namespace Bookify.Core.Services;

public sealed class GenericNavTocExtractor : ITocExtractor
{
    public bool CanHandle(Uri url) => true;

    public Task<TocNode> ExtractAsync(Uri url, string html, CancellationToken cancellationToken = default)
    {
        var context = BrowsingContext.New(Configuration.Default);
        var document = context.OpenAsync(req => req.Content(html)).Result as IHtmlDocument;
        
        if (document == null)
        {
            return Task.FromResult(new TocNode
            {
                Title = url.ToString(),
                Url = url,
                Children = new List<TocNode>()
            });
        }

        var root = new TocNode
        {
            Title = ExtractTitle(document, url),
            Url = url,
            Children = new List<TocNode>()
        };

        var navElements = document.QuerySelectorAll("nav, [role='navigation'], .nav, .navigation, .sidebar, aside nav");

        foreach (var nav in navElements)
        {
            var navLinks = nav.QuerySelectorAll("a[href]")
                .OfType<IHtmlAnchorElement>()
                .ToList();
            var linkCount = navLinks.Count;

            if (linkCount > 3)
            {
                var children = ExtractNavLinks(navLinks, url);
                root.Children.AddRange(children);
            }
        }

        if (root.Children.Count == 0)
        {
            var allLinks = document.QuerySelectorAll("a[href]")
                .OfType<IHtmlAnchorElement>()
                .Where(a => IsInternalLink(a, url))
                .Take(50)
                .Select(a => new TocNode
                {
                    Title = a.TextContent?.Trim() ?? a.GetAttribute("href") ?? "Untitled",
                    Url = ResolveUrl(a.GetAttribute("href") ?? "", url),
                    Children = new List<TocNode>()
                })
                .ToList();

            root.Children.AddRange(allLinks);
        }

        return Task.FromResult(root);
    }

    private static string ExtractTitle(IHtmlDocument document, Uri baseUrl)
    {
        var title = document.Title;
        if (!string.IsNullOrWhiteSpace(title))
        {
            return title;
        }

        var h1 = document.QuerySelector("h1");
        if (h1 != null)
        {
            return h1.TextContent?.Trim() ?? baseUrl.ToString();
        }

        return baseUrl.ToString();
    }

    private static List<TocNode> ExtractNavLinks(IEnumerable<IHtmlAnchorElement> links, Uri baseUrl)
    {
        var nodes = new List<TocNode>();

        foreach (var link in links)
        {
            var href = link.GetAttribute("href");
            if (string.IsNullOrWhiteSpace(href) || !IsInternalLink(link, baseUrl))
            {
                continue;
            }

            var title = link.TextContent?.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                title = href;
            }

            nodes.Add(new TocNode
            {
                Title = title,
                Url = ResolveUrl(href, baseUrl),
                Children = new List<TocNode>()
            });
        }

        return nodes;
    }

    private static bool IsInternalLink(IHtmlAnchorElement link, Uri baseUrl)
    {
        var href = link.GetAttribute("href");
        if (string.IsNullOrWhiteSpace(href))
        {
            return false;
        }

        if (href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
            href.StartsWith("tel:", StringComparison.OrdinalIgnoreCase) ||
            href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (Uri.TryCreate(baseUrl, href, out var absoluteUri))
        {
            return absoluteUri.Host == baseUrl.Host;
        }

        return false;
    }

    private static Uri ResolveUrl(string href, Uri baseUrl)
    {
        if (Uri.TryCreate(baseUrl, href, out var absoluteUri))
        {
            return new UriBuilder(absoluteUri) { Fragment = string.Empty }.Uri;
        }
        return baseUrl;
    }
}

