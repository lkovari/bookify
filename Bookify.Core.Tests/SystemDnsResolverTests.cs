using System.Net;
using Bookify.Core.Services;
using Xunit;

namespace Bookify.Core.Tests;

public sealed class SystemDnsResolverTests
{
    [Fact]
    public async Task ResolveAsync_ValidHost_ReturnsIpAddresses()
    {
        var resolver = new SystemDnsResolver();

        var addresses = await resolver.ResolveAsync("google.com");

        Assert.NotEmpty(addresses);
        Assert.All(addresses, addr => Assert.NotNull(addr));
    }

    [Fact]
    public async Task ResolveAsync_InvalidHost_ThrowsException()
    {
        var resolver = new SystemDnsResolver();

        await Assert.ThrowsAnyAsync<System.Net.Sockets.SocketException>(() => resolver.ResolveAsync("this-domain-definitely-does-not-exist-12345.com"));
    }

    [Fact]
    public async Task ResolveAsync_Localhost_ReturnsLoopbackAddress()
    {
        var resolver = new SystemDnsResolver();

        var addresses = await resolver.ResolveAsync("localhost");

        Assert.NotEmpty(addresses);
        Assert.Contains(addresses, addr => IPAddress.IsLoopback(addr));
    }
}

