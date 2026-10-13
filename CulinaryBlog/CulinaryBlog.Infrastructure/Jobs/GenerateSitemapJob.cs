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

// Cấu hình retry tối đa 2 lần khi tác vụ thất bại
[AutomaticRetry(Attempts = 2)]
public class GenerateSitemapJob
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GenerateSitemapJob> _logger;
    private readonly string _frontendBaseUrl;
    private readonly IWebHostEnvironment _env;

    public GenerateSitemapJob(
        IApplicationDbContext context, 
        ILogger<GenerateSitemapJob> logger,
        IConfiguration configuration,
        IWebHostEnvironment env) // Inject WebHostEnvironment để lấy đường dẫn wwwroot
    {
        _context = context;
        _logger = logger;
        _frontendBaseUrl = configuration["FrontendBaseUrl"] ?? "http://localhost:3000";
        _env = env;
    }

    // Distributed lock: Khóa tác vụ không cho phép chạy song song (Timeout 5 phút)
    [DisableConcurrentExecution(timeoutInSeconds: 300)]
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

        // 3. Xây dựng danh sách URL bao gồm cả các trang tĩnh
        var urls = new List<(string Loc, DateTime LastMod, string ChangeFreq, string Priority)>
        {
            ($"{_frontendBaseUrl}/", DateTime.UtcNow, "daily", "1.0"),
            ($"{_frontendBaseUrl}/about", DateTime.UtcNow, "monthly", "0.8"),
            ($"{_frontendBaseUrl}/contact", DateTime.UtcNow, "monthly", "0.8")
        };

        // Thêm URL Danh mục
        foreach (var slug in categories)
        {
            urls.Add(($"{_frontendBaseUrl}/categories/{slug}", DateTime.UtcNow, "weekly", "0.8"));
        }

        // Thêm URL Công thức
        foreach (var recipe in recipes)
        {
            var lastMod = recipe.UpdatedAt ?? recipe.PublishedAt ?? DateTime.UtcNow;
            urls.Add(($"{_frontendBaseUrl}/recipes/{recipe.Slug}", lastMod, "monthly", "0.9"));
        }

        // 4. Khởi tạo thư mục wwwroot nếu chưa có và lưu file vật lý
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

        // 5. Ghi log số lượng URL theo đúng yêu cầu
        _logger.LogInformation($"Tạo Sitemap thành công. Tổng cộng có {urls.Count} URLs được ghi nhận và lưu tại wwwroot/sitemap.xml.");

        // 6. Ping Google Search Console
        await PingGoogleAsync($"{_frontendBaseUrl}/sitemap.xml");
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

    private async Task PingGoogleAsync(string sitemapUrl)
    {
        try
        {
            using var client = new HttpClient();
            var pingUrl = $"https://www.google.com/ping?sitemap={Uri.EscapeDataString(sitemapUrl)}";
            var response = await client.GetAsync(pingUrl);
            
            if (response.IsSuccessStatusCode)
                _logger.LogInformation("Đã Ping sitemap thành công tới Google Search Console.");
            else
                _logger.LogWarning($"Ping Google thất bại. Status Code: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi thực hiện Ping Google Search Console.");
        }
    }
}