using System.Text;
using System.Xml;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Infrastructure.Jobs;

public class GenerateSitemapJob
{
    private readonly IApplicationDbContext _context;
    private readonly IDistributedCache _cache;
    private readonly ILogger<GenerateSitemapJob> _logger;
    private readonly string _frontendBaseUrl;

    public GenerateSitemapJob(
        IApplicationDbContext context, 
        IDistributedCache cache, 
        ILogger<GenerateSitemapJob> logger,
        IConfiguration configuration)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
        // Đọc URL Frontend từ appsettings.json, mặc định là localhost nếu không có
        _frontendBaseUrl = configuration["FrontendBaseUrl"] ?? "http://localhost:3000";
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Bắt đầu tạo Sitemap...");

        // 1. Lấy tất cả Slug Danh mục
        var categories = await _context.Categories
            .AsNoTracking()
            .Select(c => c.Slug)
            .ToListAsync(cancellationToken);

        // 2. Lấy tất cả Slug Công thức (Chỉ lấy bài đã Published)
        var recipes = await _context.Recipes
            .AsNoTracking()
            .Where(r => r.Status == RecipeStatus.Published)
            .Select(r => new { r.Slug, r.UpdatedAt, r.PublishedAt })
            .ToListAsync(cancellationToken);

        // 3. Xây dựng nội dung file XML
        using var stream = new MemoryStream();
        using var xmlWriter = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = Encoding.UTF8, Indent = true });

        xmlWriter.WriteStartDocument();
        xmlWriter.WriteStartElement("urlset", "http://www.sitemaps.org/schemas/sitemap/0.9");

        // Thêm trang chủ
        WriteUrl(xmlWriter, _frontendBaseUrl, DateTime.UtcNow, "daily", "1.0");

        // Thêm URL Danh mục
        foreach (var slug in categories)
        {
            var url = $"{_frontendBaseUrl}/categories/{slug}";
            WriteUrl(xmlWriter, url, DateTime.UtcNow, "weekly", "0.8");
        }

        // Thêm URL Công thức
        foreach (var recipe in recipes)
        {
            var url = $"{_frontendBaseUrl}/recipes/{recipe.Slug}";
            var lastMod = recipe.UpdatedAt ?? recipe.PublishedAt ?? DateTime.UtcNow;
            WriteUrl(xmlWriter, url, lastMod, "monthly", "0.9");
        }

        xmlWriter.WriteEndElement();
        xmlWriter.WriteEndDocument();
        xmlWriter.Flush();

        var xmlString = Encoding.UTF8.GetString(stream.ToArray());

        // 4. Lưu chuỗi XML vào Redis cache (thời gian sống 24 giờ)
        await _cache.SetStringAsync("sitemap_xml", xmlString, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
        }, cancellationToken);

        _logger.LogInformation("Tạo Sitemap thành công và đã lưu vào Redis Cache.");
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
}