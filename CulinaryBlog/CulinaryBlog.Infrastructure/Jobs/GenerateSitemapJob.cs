using System.Text;
using System.Xml;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Domain.Entities;
using Hangfire;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Infrastructure.Jobs;

[AutomaticRetry(Attempts = 2)]
public class GenerateSitemapJob
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GenerateSitemapJob> _logger;
    private readonly string _frontendBaseUrl;
    private readonly string _sitemapPublicBaseUrl;
    private readonly IWebHostEnvironment _env;

    public GenerateSitemapJob(
        IApplicationDbContext context, 
        ILogger<GenerateSitemapJob> logger,
        IConfiguration configuration,
        IWebHostEnvironment env)
    {
        _context = context;
        _logger = logger;
        _env = env;

        // Thống nhất đọc đúng khóa FrontendBaseUrl từ appsettings.json
        _frontendBaseUrl = (configuration["FrontendBaseUrl"] 
            ?? configuration["FrontendUrl"] 
            ?? "http://localhost:3000").TrimEnd('/');

        // Đọc URL công khai nơi phục vụ sitemap.xml (ưu tiên cấu hình BackendBaseUrl / PublicBaseUrl)
        _sitemapPublicBaseUrl = (configuration["BackendBaseUrl"] 
            ?? configuration["PublicBaseUrl"] 
            ?? configuration["ASPNETCORE_URLS"]?.Split(';', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() 
            ?? _frontendBaseUrl).TrimEnd('/');
    }

    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Bắt đầu tạo Sitemap...");

        var categories = await _context.Categories
            .AsNoTracking()
            .Select(c => c.Slug)
            .ToListAsync(cancellationToken);

        var recipes = await _context.Recipes
            .AsNoTracking()
            .Where(r => r.Status == RecipeStatus.Published)
            .Select(r => new { r.Slug, r.UpdatedAt, r.PublishedAt })
            .ToListAsync(cancellationToken);

        // Chỉ đưa trang chủ vào danh sách tĩnh (bỏ /about và /contact do frontend chưa có)
        var urls = new List<(string Loc, DateTime LastMod, string ChangeFreq, string Priority)>
        {
            ($"{_frontendBaseUrl}/", DateTime.UtcNow, "daily", "1.0")
        };

        foreach (var slug in categories)
        {
            urls.Add(($"{_frontendBaseUrl}/categories/{slug}", DateTime.UtcNow, "weekly", "0.8"));
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

        _logger.LogInformation($"Tạo Sitemap thành công. Tổng cộng có {urls.Count} URLs được lưu tại wwwroot/sitemap.xml.");

        // Ping Google bằng URL công khai của sitemap
        await PingGoogleAsync($"{_sitemapPublicBaseUrl}/sitemap.xml", cancellationToken);
    }

    private void WriteUrl(XmlWriter writer, string loc, DateTime lastmod, string changefreq, string priority)
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
        using var client = new HttpClient();
        var pingUrl = $"https://www.google.com/ping?sitemap={Uri.EscapeDataString(sitemapUrl)}";
        var response = await client.GetAsync(pingUrl, cancellationToken);
        
        if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation("Đã Ping sitemap thành công tới Google Search Console: {SitemapUrl}", sitemapUrl);
        }
        else
        {
            var errorMsg = $"Ping Google thất bại ({sitemapUrl}). Status Code: {response.StatusCode}";
            _logger.LogWarning(errorMsg);
            throw new HttpRequestException(errorMsg);
        }
    }
}