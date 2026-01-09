using System.Net;
using System.Net.Http;
using Bookify.Core.Abstractions;
using Bookify.Core.Services;
using NSubstitute;
using Xunit;

namespace Bookify.Core.Tests;

public sealed class UrlValidatorTests
{
    [Fact]
    public async Task ValidateAsync_ValidHttpsUrl_ReturnsNormalizedUri()
    {
        var dnsResolver = Substitute.For<IDnsResolver>();
        dnsResolver.ResolveAsync(Arg.Any<string>()).Returns(new[] { IPAddress.Parse("8.8.8.8") });

        var httpClient = new HttpClient(new TestHttpMessageHandler
        {
            Response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html></html>", System.Text.Encoding.UTF8, "text/html")
            }
        });

        var validator = new UrlValidator(dnsResolver, httpClient);

        var result = await validator.ValidateAsync("https://example.com");

        Assert.NotNull(result);
        Assert.Equal("https", result.Scheme);
        Assert.Equal("example.com", result.Host);
        Assert.Empty(result.Fragment);
    }

    [Fact]
    public async Task ValidateAsync_InvalidUrlFormat_ThrowsArgumentException()
    {
        var dnsResolver = Substitute.For<IDnsResolver>();
        var httpClient = new HttpClient();

        var validator = new UrlValidator(dnsResolver, httpClient);

        await Assert.ThrowsAsync<ArgumentException>(() => validator.ValidateAsync("not-a-url"));
    }

    [Fact]
    public async Task ValidateAsync_NonHttpScheme_ThrowsArgumentException()
    {
        var dnsResolver = Substitute.For<IDnsResolver>();
        var httpClient = new HttpClient();

        var validator = new UrlValidator(dnsResolver, httpClient);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => validator.ValidateAsync("ftp://example.com"));
        Assert.Contains("HTTP and HTTPS", ex.Message);
    }

    [Fact]
    public async Task ValidateAsync_LocalhostHost_ThrowsArgumentException()
    {
        var dnsResolver = Substitute.For<IDnsResolver>();
        var httpClient = new HttpClient();

        var validator = new UrlValidator(dnsResolver, httpClient);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => validator.ValidateAsync("http://localhost"));
        Assert.Contains("Forbidden host", ex.Message);
    }

    [Fact]
    public async Task ValidateAsync_PrivateIpAddress_ThrowsArgumentException()
    {
        var dnsResolver = Substitute.For<IDnsResolver>();
        dnsResolver.ResolveAsync(Arg.Any<string>()).Returns(new[] { IPAddress.Parse("192.168.1.1") });

        var httpClient = new HttpClient();

        var validator = new UrlValidator(dnsResolver, httpClient);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => validator.ValidateAsync("http://example.com"));
        Assert.Contains("Forbidden IP", ex.Message);
    }

    [Fact]
    public async Task ValidateAsync_NonHtmlContent_ThrowsArgumentException()
    {
        var dnsResolver = Substitute.For<IDnsResolver>();
        dnsResolver.ResolveAsync(Arg.Any<string>()).Returns(new[] { IPAddress.Parse("8.8.8.8") });

        var httpClient = new HttpClient(new TestHttpMessageHandler
        {
            Response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("json data", System.Text.Encoding.UTF8, "application/json")
            }
        });

        var validator = new UrlValidator(dnsResolver, httpClient);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => validator.ValidateAsync("https://example.com"));
        Assert.Contains("HTML content", ex.Message);
    }

    [Fact]
    public async Task ValidateAsync_HttpError_ThrowsHttpRequestException()
    {
        var dnsResolver = Substitute.For<IDnsResolver>();
        dnsResolver.ResolveAsync(Arg.Any<string>()).Returns(new[] { IPAddress.Parse("8.8.8.8") });

        var httpClient = new HttpClient(new TestHttpMessageHandler
        {
            Response = new HttpResponseMessage(HttpStatusCode.NotFound)
        });

        var validator = new UrlValidator(dnsResolver, httpClient);

        await Assert.ThrowsAsync<HttpRequestException>(() => validator.ValidateAsync("https://example.com"));
    }

    [Fact]
    public async Task ValidateAsync_RemovesFragment_ReturnsUriWithoutFragment()
    {
        var dnsResolver = Substitute.For<IDnsResolver>();
        dnsResolver.ResolveAsync(Arg.Any<string>()).Returns(new[] { IPAddress.Parse("8.8.8.8") });

        var httpClient = new HttpClient(new TestHttpMessageHandler
        {
            Response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html></html>", System.Text.Encoding.UTF8, "text/html")
            }
        });

        var validator = new UrlValidator(dnsResolver, httpClient);

        var result = await validator.ValidateAsync("https://example.com/page#section");

        Assert.Empty(result.Fragment);
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("192.168.1.1")]
    [InlineData("10.0.0.1")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    public async Task IsForbiddenIp_PrivateIpRanges_ReturnsTrue(string ipString)
    {
        var dnsResolver = Substitute.For<IDnsResolver>();
        dnsResolver.ResolveAsync(Arg.Any<string>()).Returns(new[] { IPAddress.Parse(ipString) });

        var httpClient = new HttpClient(new TestHttpMessageHandler
        {
            Response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html></html>", System.Text.Encoding.UTF8, "text/html")
            }
        });

        var validator = new UrlValidator(dnsResolver, httpClient);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => validator.ValidateAsync("http://example.com"));
        Assert.Contains("Forbidden IP", ex.Message);
    }

    private sealed class TestHttpMessageHandler : HttpMessageHandler
    {
        public HttpResponseMessage? Response { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(Response ?? new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}

