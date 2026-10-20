using CulinaryBlog.Application.Contracts.Infrastructure;
using CulinaryBlog.Infrastructure.Configurations;
using CulinaryBlog.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Minio;
using Microsoft.Extensions.Hosting;
using Hangfire;
using Hangfire.PostgreSql;
using CulinaryBlog.Infrastructure.Jobs;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Application.Features.Auth.GoogleLogin;

namespace CulinaryBlog.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<SmtpOptions>()
            .Bind(configuration.GetSection("Smtp"))
            .Validate<IHostEnvironment>((options, environment) => options.IsValid(environment.IsDevelopment()),
                "Configure valid Smtp settings and AppUrl; production requires TLS and HTTPS.")
            .ValidateOnStart();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddScoped<IWelcomeEmailQueue, HangfireWelcomeEmailQueue>();
        services.AddScoped<IRegistrationTransactionFactory, RegistrationTransactionFactory>();
        services.AddScoped<WelcomeEmailJob>();
        services.AddScoped<WelcomeEmailReconciliationJob>();
        services.AddSingleton<WelcomeEmailFailureFilter>();

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
        services.AddScoped<IFileDeletionQueue, HangfireFileDeletionQueue>();
        services.AddScoped<IRecipeImageTransactionFactory, RecipeImageTransactionFactory>();
        services.AddScoped<IFileLifecycleSessionFactory, FileLifecycleSessionFactory>();
        services.AddScoped<IGoogleLoginTransactionFactory, GoogleLoginTransactionFactory>();
        services.AddScoped<StorageMaintenanceService>();
        services.AddScoped<CulinaryBlog.Application.Features.Recipes.SearchRecipes.IRecipeSearchService, RecipeSearchService>();
        services.AddScoped<DeleteStoredFileJob>();
        services.AddScoped<FileDeletionReconciliationJob>();
        services.AddScoped<IImageResizeQueue, HangfireImageResizeQueue>();
        services.AddScoped<ImageResizeJob>();
        services.AddScoped<ImageResizeReconciliationJob>();

        var databaseConnection = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is required for Hangfire.");
        services.AddHangfire((provider, config) => config
            .UseFilter(provider.GetRequiredService<WelcomeEmailFailureFilter>())
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(databaseConnection)));
        services.AddHangfireServer();

        return services;
    }
}
