using System.Text;
using System.Xml;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Domain.Entities;
using Hangfire;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

namespace CulinaryBlog.Infrastructure.Jobs;

[AutomaticRetry(Attempts = 2)]
public class GenerateSitemapJob
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GenerateSitemapJob> _logger;
    private readonly string _frontendBaseUrl;
    private readonly string _sitemapPublicBaseUrl;
    private readonly IWebHostEnvironment _env;
    private readonly IHttpClientFactory _httpClientFactory;

    public GenerateSitemapJob(
        IApplicationDbContext context, 
        ILogger<GenerateSitemapJob> logger,
        IConfiguration configuration,
        IWebHostEnvironment env,
        IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _logger = logger;
        _env = env;
        _httpClientFactory = httpClientFactory;

        _frontendBaseUrl = GetPublicBaseUrl(configuration, "FrontendBaseUrl", env);
        _sitemapPublicBaseUrl = GetPublicBaseUrl(configuration, "BackendBaseUrl", env);
    }

    private static string GetPublicBaseUrl(IConfiguration configuration, string key, IWebHostEnvironment env)
    {
        var value = configuration[key]?.Trim();
        if (string.IsNullOrWhiteSpace(value) ||
            !Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") ||
            (!env.IsDevelopment() && uri.IsLoopback))
        {
            throw new InvalidOperationException($"Configure {key} as a public HTTP(S) URL for sitemap generation.");
        }

        return uri.ToString().TrimEnd('/');
    }

    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Bắt đầu tạo Sitemap...");

        var categories = await _context.Categories
            .AsNoTracking()
            .Select(c => new { c.Slug, c.UpdatedAt, c.CreatedAt })
            .ToListAsync(cancellationToken);

        var recipes = await _context.Recipes
            .AsNoTracking()
            .Where(r => r.Status == RecipeStatus.Published)
            .Select(r => new { r.Slug, r.UpdatedAt, r.PublishedAt })
            .ToListAsync(cancellationToken);

        var urls = new List<(string Loc, DateTime LastMod, string ChangeFreq, string Priority)>
        {
            ($"{_frontendBaseUrl}/", DateTime.UtcNow, "daily", "1.0"),
            ($"{_frontendBaseUrl}/categories", DateTime.UtcNow, "weekly", "0.8"),
            ($"{_frontendBaseUrl}/recipes", DateTime.UtcNow, "daily", "0.8")
        };

        foreach (var category in categories)
        {
            urls.Add(($"{_frontendBaseUrl}/categories/{category.Slug}", category.UpdatedAt ?? category.CreatedAt, "weekly", "0.8"));
        }

        foreach (var recipe in recipes)
        {
            var lastMod = recipe.UpdatedAt ?? recipe.PublishedAt ?? DateTime.UtcNow;
            urls.Add(($"{_frontendBaseUrl}/recipes/{recipe.Slug}", lastMod, "monthly", "0.9"));
        }

        var wwwrootPath = Path.Combine(_env.ContentRootPath, "wwwroot");
        if (!Directory.Exists(wwwrootPath))
        {
            Directory.CreateDirectory(wwwrootPath);
        }
        
        var sitemapPath = Path.Combine(wwwrootPath, "sitemap.xml");

        using (var stream = new FileStream(sitemapPath, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var xmlWriter = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = Encoding.UTF8, Indent = true }))
        {
            xmlWriter.WriteStartDocument();
            xmlWriter.WriteStartElement("urlset", "http://www.sitemaps.org/schemas/sitemap/0.9");

            foreach (var url in urls)
            {
                WriteUrl(xmlWriter, url.Loc, url.LastMod, url.ChangeFreq, url.Priority);
            }

            xmlWriter.WriteEndElement();
            xmlWriter.WriteEndDocument();
        }

        _logger.LogInformation("Tạo sitemap thành công: {UrlCount} URL tại {SitemapPath}", urls.Count, sitemapPath);

        // Google đã ngừng ping endpoint; lỗi gửi thông báo không làm hỏng sitemap đã tạo.
        var sitemapUrl = $"{_sitemapPublicBaseUrl}/sitemap.xml";
        try
        {
            await PingGoogleAsync(sitemapUrl, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Không thể ping Google cho {SitemapUrl}; sitemap vẫn được công bố qua robots.txt", sitemapUrl);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Ping Google hết thời gian chờ cho {SitemapUrl}; sitemap vẫn được công bố qua robots.txt", sitemapUrl);
        }
    }

    private static void WriteUrl(XmlWriter writer, string loc, DateTime lastmod, string changefreq, string priority)
    {
        writer.WriteStartElement("url");
        writer.WriteElementString("loc", loc);
        writer.WriteElementString("lastmod", lastmod.ToString("yyyy-MM-ddTHH:mm:sszzz"));
        writer.WriteElementString("changefreq", changefreq);
        writer.WriteElementString("priority", priority);
        writer.WriteEndElement();
    }

    private async Task PingGoogleAsync(string sitemapUrl, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        var pingUrl = $"https://www.google.com/ping?sitemap={Uri.EscapeDataString(sitemapUrl)}";
        using var response = await client.GetAsync(pingUrl, cancellationToken);
        
        if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation("Đã Ping sitemap thành công tới Google Search Console: {SitemapUrl}", sitemapUrl);
        }
        else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Google đã ngừng ping endpoint; robots.txt vẫn công bố URL sitemap.
            _logger.LogWarning("Google sitemap ping đã ngừng hỗ trợ (HTTP 404); sitemap vẫn được công bố tại {SitemapUrl}", sitemapUrl);
        }
        else
        {
            throw new HttpRequestException($"Ping Google thất bại: HTTP {(int)response.StatusCode}");
        }
    }
}
