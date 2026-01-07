using System.Net;
using Bookify.Core.Abstractions;

namespace Bookify.Core.Services;

public sealed class SystemDnsResolver : IDnsResolver
{
    public async Task<IPAddress[]> ResolveAsync(string host)
    {
        var addresses = await Dns.GetHostAddressesAsync(host);
        return addresses;
    }
}

