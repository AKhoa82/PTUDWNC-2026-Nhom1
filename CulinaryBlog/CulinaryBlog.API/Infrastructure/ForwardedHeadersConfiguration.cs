using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace CulinaryBlog.API.Infrastructure;

public static class ForwardedHeadersConfiguration
{
    public static IServiceCollection AddTrustedForwardedHeaders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var proxyAddresses = configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [];
        var proxies = proxyAddresses.Select(value =>
        {
            if (!IPAddress.TryParse(value, out var address))
                throw new InvalidOperationException($"ForwardedHeaders:KnownProxies contains an invalid IP address: '{value}'.");
            return address;
        }).ToArray();

        var forwardLimit = configuration.GetValue<int?>("ForwardedHeaders:ForwardLimit") ?? 1;
        if (forwardLimit is < 1 or > 5)
            throw new InvalidOperationException("ForwardedHeaders:ForwardLimit must be between 1 and 5.");

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = forwardLimit;
            foreach (var proxy in proxies)
                options.KnownProxies.Add(proxy);
        });

        return services;
    }
}