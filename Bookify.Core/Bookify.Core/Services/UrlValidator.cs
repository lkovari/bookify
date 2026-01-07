using System.Net;
using System.Net.Http.Headers;
using Bookify.Core.Abstractions;

namespace Bookify.Core.Services;

public sealed class UrlValidator
{
    private readonly IDnsResolver _dnsResolver;
    private readonly HttpClient _httpClient;

    public UrlValidator(IDnsResolver dnsResolver, HttpClient httpClient)
    {
        _dnsResolver = dnsResolver;
        _httpClient = httpClient;
    }

    public async Task<Uri> ValidateAsync(string url, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("Invalid URL format", nameof(url));
        }

        if (uri.Scheme != "http" && uri.Scheme != "https")
        {
            throw new ArgumentException("Only HTTP and HTTPS URLs are allowed", nameof(url));
        }

        if (IsForbiddenHost(uri.Host))
        {
            throw new ArgumentException("Forbidden host: localhost, private IPs, or internal networks are not allowed", nameof(url));
        }

        var addresses = await _dnsResolver.ResolveAsync(uri.Host);
        foreach (var address in addresses)
        {
            if (IsForbiddenIp(address))
            {
                throw new ArgumentException("Forbidden IP address: private or internal network IPs are not allowed", nameof(url));
            }
        }

        var normalizedUri = await ValidateHttpAsync(uri, cancellationToken);
        return normalizedUri;
    }

    private static bool IsForbiddenHost(string host)
    {
        var lowerHost = host.ToLowerInvariant();
        return lowerHost == "localhost" ||
               lowerHost == "127.0.0.1" ||
               lowerHost == "::1" ||
               lowerHost.StartsWith("127.") ||
               lowerHost.StartsWith("192.168.") ||
               lowerHost.StartsWith("10.") ||
               lowerHost.StartsWith("172.16.") ||
               lowerHost.StartsWith("172.17.") ||
               lowerHost.StartsWith("172.18.") ||
               lowerHost.StartsWith("172.19.") ||
               lowerHost.StartsWith("172.20.") ||
               lowerHost.StartsWith("172.21.") ||
               lowerHost.StartsWith("172.22.") ||
               lowerHost.StartsWith("172.23.") ||
               lowerHost.StartsWith("172.24.") ||
               lowerHost.StartsWith("172.25.") ||
               lowerHost.StartsWith("172.26.") ||
               lowerHost.StartsWith("172.27.") ||
               lowerHost.StartsWith("172.28.") ||
               lowerHost.StartsWith("172.29.") ||
               lowerHost.StartsWith("172.30.") ||
               lowerHost.StartsWith("172.31.");
    }

    private static bool IsForbiddenIp(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            if (bytes.Length == 4)
            {
                if (bytes[0] == 10) return true;
                if (bytes[0] == 192 && bytes[1] == 168) return true;
                if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
                if (bytes[0] == 127) return true;
            }
        }

        return false;
    }

    private async Task<Uri> ValidateHttpAsync(Uri uri, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.ParseAdd("Bookify/1.0");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"HTTP request failed with status {response.StatusCode}");
        }

        var contentType = response.Content.Headers.ContentType;
        if (contentType == null || !contentType.MediaType?.Contains("text/html", StringComparison.OrdinalIgnoreCase) == true)
        {
            throw new ArgumentException("URL must return HTML content (Content-Type: text/html)", nameof(uri));
        }

        var finalUri = response.RequestMessage?.RequestUri ?? uri;
        var normalizedUri = new UriBuilder(finalUri)
        {
            Fragment = string.Empty
        }.Uri;

        return normalizedUri;
    }
}

