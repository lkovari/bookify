using System.Net;
using System.Net.Http;
using Bookify.Core.Services;
using Xunit;

namespace Bookify.Core.Tests;

public sealed class LinkDiscoveryServiceTests
{
    [Fact]
    public async Task DiscoverAsync_SimpleHtmlWithLinks_ReturnsDiscoveredLinks()
    {
        var html = @"
<html>
<body>
    <a href=""/page1"">Page 1</a>
    <a href=""/page2"">Page 2</a>
</body>
</html>";

        var httpClient = new HttpClient(new TestHttpMessageHandler
        {
            Response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(html, System.Text.Encoding.UTF8, "text/html")
            }
        });

        var service = new LinkDiscoveryService(httpClient, maxPages: 10, maxDepth: 2);
        var startUrl = new Uri("https://example.com");

        var result = await service.DiscoverAsync(startUrl);

        Assert.NotEmpty(result);
        Assert.All(result, uri => Assert.Equal("example.com", uri.Host));
    }

    [Fact]
    public async Task DiscoverAsync_ExternalLinks_ExcludesExternalLinks()
    {
        var html = @"
<html>
<body>
    <a href=""/internal"">Internal</a>
    <a href=""https://external.com/page"">External</a>
</body>
</html>";

        var httpClient = new HttpClient(new TestHttpMessageHandler
        {
            Response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(html, System.Text.Encoding.UTF8, "text/html")
            }
        });

        var service = new LinkDiscoveryService(httpClient, maxPages: 10, maxDepth: 2);
        var startUrl = new Uri("https://example.com");

        var result = await service.DiscoverAsync(startUrl);

        Assert.All(result, uri => Assert.Equal("example.com", uri.Host));
    }

    [Fact]
    public async Task DiscoverAsync_MaxDepthExceeded_StopsAtMaxDepth()
    {
        var callCount = 0;
        var httpClient = new HttpClient(new TestHttpMessageHandler
        {
            ResponseFactory = (request) =>
            {
                callCount++;
                var html = callCount == 1
                    ? @"<html><body><a href=""/page2"">Page 2</a></body></html>"
                    : @"<html><body><a href=""/page3"">Page 3</a></body></html>";

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(html, System.Text.Encoding.UTF8, "text/html")
                };
            }
        });

        var service = new LinkDiscoveryService(httpClient, maxPages: 10, maxDepth: 1);
        var startUrl = new Uri("https://example.com");

        var result = await service.DiscoverAsync(startUrl);

        Assert.True(callCount <= 2);
    }

    [Fact]
    public async Task DiscoverAsync_MaxPagesExceeded_StopsAtMaxPages()
    {
        var callCount = 0;
        var httpClient = new HttpClient(new TestHttpMessageHandler
        {
            ResponseFactory = (request) =>
            {
                callCount++;
                var pageNum = callCount;
                var html = $@"<html><body><a href=""/page{pageNum + 1}"">Page {pageNum + 1}</a></body></html>";

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(html, System.Text.Encoding.UTF8, "text/html")
                };
            }
        });

        var service = new LinkDiscoveryService(httpClient, maxPages: 3, maxDepth: 10);
        var startUrl = new Uri("https://example.com");

        var result = await service.DiscoverAsync(startUrl);

        Assert.True(result.Count <= 3);
    }

    [Fact]
    public async Task DiscoverAsync_InvalidLinks_IgnoresInvalidLinks()
    {
        var html = @"
<html>
<body>
    <a href=""mailto:test@example.com"">Email</a>
    <a href=""tel:+1234567890"">Phone</a>
    <a href=""javascript:void(0)"">JS Link</a>
    <a href=""/valid"">Valid</a>
</body>
</html>";

        var httpClient = new HttpClient(new TestHttpMessageHandler
        {
            Response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(html, System.Text.Encoding.UTF8, "text/html")
            }
        });

        var service = new LinkDiscoveryService(httpClient, maxPages: 10, maxDepth: 2);
        var startUrl = new Uri("https://example.com");

        var result = await service.DiscoverAsync(startUrl);

        Assert.All(result, uri => Assert.DoesNotContain("mailto:", uri.ToString()));
        Assert.All(result, uri => Assert.DoesNotContain("tel:", uri.ToString()));
        Assert.All(result, uri => Assert.DoesNotContain("javascript:", uri.ToString()));
    }

    [Fact]
    public async Task DiscoverAsync_CancellationRequested_StopsDiscovery()
    {
        var httpClient = new HttpClient(new TestHttpMessageHandler
        {
            Response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html><body><a href=\"/page\">Page</a></body></html>", System.Text.Encoding.UTF8, "text/html")
            }
        });

        var service = new LinkDiscoveryService(httpClient, maxPages: 100, maxDepth: 10);
        var startUrl = new Uri("https://example.com");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await service.DiscoverAsync(startUrl, cts.Token);

        Assert.Empty(result);
    }

    [Fact]
    public async Task DiscoverAsync_HttpError_ContinuesWithOtherLinks()
    {
        var callCount = 0;
        var httpClient = new HttpClient(new TestHttpMessageHandler
        {
            ResponseFactory = (request) =>
            {
                callCount++;
                if (callCount == 1)
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("<html><body><a href=\"/page1\">Page 1</a><a href=\"/page2\">Page 2</a></body></html>", System.Text.Encoding.UTF8, "text/html")
                    };
                }
                throw new HttpRequestException("Connection failed");
            }
        });

        var service = new LinkDiscoveryService(httpClient, maxPages: 10, maxDepth: 2);
        var startUrl = new Uri("https://example.com");

        var result = await service.DiscoverAsync(startUrl);

        Assert.NotEmpty(result);
    }

    private sealed class TestHttpMessageHandler : HttpMessageHandler
    {
        public HttpResponseMessage? Response { get; set; }
        public Func<HttpRequestMessage, HttpResponseMessage>? ResponseFactory { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (ResponseFactory != null)
            {
                try
                {
                    return Task.FromResult(ResponseFactory(request));
                }
                catch (Exception ex)
                {
                    var tcs = new TaskCompletionSource<HttpResponseMessage>();
                    tcs.SetException(ex);
                    return tcs.Task;
                }
            }
            return Task.FromResult(Response ?? new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}

