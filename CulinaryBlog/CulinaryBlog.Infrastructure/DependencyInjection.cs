using CulinaryBlog.Application.Contracts.Infrastructure;
using CulinaryBlog.Infrastructure.Configurations;
using CulinaryBlog.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Minio;

namespace CulinaryBlog.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. Cấu hình MinioOptions từ appsettings.json
        services.AddOptions<MinioOptions>()
            .Bind(configuration.GetSection(MinioOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.Endpoint) &&
                !string.IsNullOrWhiteSpace(o.AccessKey) && !string.IsNullOrWhiteSpace(o.SecretKey) &&
                !string.IsNullOrWhiteSpace(o.BucketName), "MinIO connection settings are required.")
            .Validate(o => string.IsNullOrEmpty(o.PublicBaseUrl) ||
                (Uri.TryCreate(o.PublicBaseUrl, UriKind.Absolute, out var uri) &&
                 (uri.Scheme == "https" || uri.Scheme == "http") &&
                 string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment) &&
                 string.IsNullOrEmpty(uri.UserInfo)), "MinIO PublicBaseUrl must be an HTTP(S) base URL.")
            .ValidateOnStart();

        // 2. Đăng ký IMinioClient
        services.AddSingleton<IMinioClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MinioOptions>>().Value;

            var client = new MinioClient()
                .WithEndpoint(options.Endpoint)
                .WithTimeout(15000)
                .WithCredentials(options.AccessKey, options.SecretKey);

            if (options.UseSsl)
            {
                client = client.WithSSL();
            }

            return client.Build();
        });

        // 3. Đăng ký IFileStorageService
        services.AddSingleton<IFileStorageService, MinioFileStorageService>();

        return services;
    }
}
