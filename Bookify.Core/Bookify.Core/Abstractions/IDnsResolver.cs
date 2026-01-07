namespace Bookify.Core.Abstractions;

public interface IDnsResolver
{
    Task<System.Net.IPAddress[]> ResolveAsync(string host);
}

