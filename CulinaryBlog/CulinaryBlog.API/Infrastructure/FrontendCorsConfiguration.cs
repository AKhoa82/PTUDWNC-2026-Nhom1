namespace CulinaryBlog.API.Infrastructure;

public static class FrontendCorsConfiguration
{
    public const string PolicyName = "Frontend";

    public static IServiceCollection AddFrontendCors(
        this IServiceCollection services, IConfiguration configuration)
    {
        var configuredOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (configuredOrigins.Length == 0)
            throw new InvalidOperationException("Configure Cors:AllowedOrigins with at least one frontend origin.");

        var origins = configuredOrigins.Select(origin => origin?.Trim()).ToArray();
        foreach (var origin in origins)
        {
            if (string.IsNullOrWhiteSpace(origin) ||
                !Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
                uri.GetLeftPart(UriPartial.Authority) != origin ||
                !string.IsNullOrEmpty(uri.UserInfo))
            {
                throw new InvalidOperationException("Cors:AllowedOrigins must contain only HTTP(S) origins without paths or wildcards.");
            }
        }

        services.AddCors(options => options.AddPolicy(PolicyName, policy =>
            policy.WithOrigins(origins.Distinct(StringComparer.Ordinal).Select(origin => origin!).ToArray())
                .AllowAnyHeader()
                .AllowAnyMethod()));

        return services;
    }
}
