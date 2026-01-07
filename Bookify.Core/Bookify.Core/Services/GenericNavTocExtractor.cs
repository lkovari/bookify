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

        var allDocumentLinks = document.QuerySelectorAll("a[href]")
            .OfType<IHtmlAnchorElement>()
            .Where(a => IsInternalLink(a, url))
            .Select(a =>
            {
                var href = a.GetAttribute("href") ?? "";
                var resolvedUrl = ResolveUrl(href, url);
                var title = a.TextContent?.Trim();
                if (string.IsNullOrWhiteSpace(title))
                {
                    title = href;
                }
                return new TocNode
                {
                    Title = title,
                    Url = resolvedUrl,
                    Children = new List<TocNode>()
                };
            })
            .GroupBy(n => n.Url.ToString().ToLowerInvariant())
            .Select(g => g.First())
            .ToList();

        var navElements = document.QuerySelectorAll("nav, [role='navigation'], .nav, .navigation, .sidebar, aside nav, [class*='menu'], [class*='sidebar'], header, footer");

        foreach (var nav in navElements)
        {
            var navLinks = nav.QuerySelectorAll("a[href]")
                .OfType<IHtmlAnchorElement>()
                .ToList();
            var linkCount = navLinks.Count;

            if (linkCount >= 2)
            {
                var children = ExtractNavLinksHierarchical(nav, url);
                if (children.Count > 0)
                {
                    root.Children.AddRange(children);
                }
            }
        }

        if (root.Children.Count == 0)
        {
            root.Children.AddRange(allDocumentLinks.Take(100));
        }
        else
        {
            var tocUrls = new HashSet<string>(root.Children.SelectMany(GetAllUrls).Select(u => u.ToString().ToLowerInvariant()));
            var missingLinks = allDocumentLinks.Where(l => !tocUrls.Contains(l.Url.ToString().ToLowerInvariant())).Take(50);
            root.Children.AddRange(missingLinks);
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

    private static List<TocNode> ExtractNavLinksHierarchical(AngleSharp.Dom.IElement navElement, Uri baseUrl)
    {
        var nodes = new List<TocNode>();
        var processedUrls = new HashSet<string>();

        TocNode? ProcessListItem(AngleSharp.Dom.IElement listItem, TocNode? parentNode = null)
        {
            var link = listItem.QuerySelector("a[href]") as IHtmlAnchorElement;
            if (link == null)
            {
                return null;
            }

            var href = link.GetAttribute("href");
            if (string.IsNullOrWhiteSpace(href) || !IsInternalLink(link, baseUrl))
            {
                return null;
            }

            var resolvedUrl = ResolveUrl(href, baseUrl);
            var urlKey = resolvedUrl.ToString().ToLowerInvariant();
            
            if (processedUrls.Contains(urlKey))
            {
                return null;
            }
            processedUrls.Add(urlKey);

            var title = link.TextContent?.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                title = href;
            }

            var node = new TocNode
            {
                Title = title,
                Url = resolvedUrl,
                Children = new List<TocNode>()
            };

            var nestedLists = listItem.QuerySelectorAll("> ul, > ol, ul, ol, [class*='submenu'], [class*='dropdown'], [class*='menu']")
                .Where(ul => ul.ParentElement == listItem || listItem.Contains(ul))
                .ToList();

            foreach (var nestedList in nestedLists)
            {
                var nestedItems = nestedList.QuerySelectorAll("> li, li, [class*='menu-item'], [class*='nav-item']");
                foreach (var nestedItem in nestedItems)
                {
                    var childNode = ProcessListItem(nestedItem, node);
                    if (childNode != null)
                    {
                        node.Children.Add(childNode);
                    }
                }
            }

            return node;
        }

        var listItems = navElement.QuerySelectorAll("li, [class*='menu-item'], [class*='nav-item'], a[href]")
            .ToList();

        foreach (var item in listItems)
        {
            var node = ProcessListItem(item);
            if (node != null)
            {
                nodes.Add(node);
            }
        }

        if (nodes.Count == 0)
        {
            var allLinks = navElement.QuerySelectorAll("a[href]")
                .OfType<IHtmlAnchorElement>()
                .Where(a => IsInternalLink(a, baseUrl))
                .Select(a =>
                {
                    var href = a.GetAttribute("href") ?? "";
                    var resolvedUrl = ResolveUrl(href, baseUrl);
                    var urlKey = resolvedUrl.ToString().ToLowerInvariant();
                    
                    if (processedUrls.Contains(urlKey))
                    {
                        return null;
                    }
                    processedUrls.Add(urlKey);

                    var title = a.TextContent?.Trim() ?? href;
                    return new TocNode
                    {
                        Title = title,
                        Url = resolvedUrl,
                        Children = new List<TocNode>()
                    };
                })
                .Where(n => n != null)
                .Cast<TocNode>()
                .ToList();

            nodes.AddRange(allLinks);
        }

        return nodes;
    }

    private static string NormalizeHref(string href, Uri baseUrl)
    {
        if (Uri.TryCreate(baseUrl, href, out var absoluteUri))
        {
            var builder = new UriBuilder(absoluteUri);
            return builder.Uri.ToString().ToLowerInvariant();
        }
        return href.ToLowerInvariant();
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
            if (href.StartsWith("#", StringComparison.Ordinal))
            {
                return new UriBuilder(baseUrl) { Fragment = href.Substring(1) }.Uri;
            }
            return absoluteUri;
        }
        return baseUrl;
    }

    private static IEnumerable<Uri> GetAllUrls(TocNode node)
    {
        yield return node.Url;
        foreach (var child in node.Children)
        {
            foreach (var url in GetAllUrls(child))
            {
                yield return url;
            }
        }
    }
}

