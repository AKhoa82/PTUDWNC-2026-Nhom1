using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Xml.Linq;

namespace CulinaryBlog.Application.Features.Sitemap.Queries;

public class GetSitemapQueryHandler : IRequestHandler<GetSitemapQuery, string>
{
    private readonly IApplicationDbContext _context;

    public GetSitemapQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<string> Handle(GetSitemapQuery request, CancellationToken cancellationToken)
    {
        XNamespace xmlns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var urlset = new XElement(xmlns + "urlset");

        // 1. Thêm trang chủ
        urlset.Add(CreateUrlElement(xmlns, request.BaseUrl, DateTime.UtcNow, "1.0", "daily"));

        // 2. Thêm các danh mục (Categories)
        var categories = await _context.Categories.AsNoTracking().ToListAsync(cancellationToken);
        foreach (var category in categories)
        {
            var categoryUrl = $"{request.BaseUrl}/categories/{category.Slug}";
            urlset.Add(CreateUrlElement(xmlns, categoryUrl, DateTime.UtcNow, "0.8", "weekly"));
        }

        // 3. Thêm các công thức đã xuất bản (Published Recipes)
        var recipes = await _context.Recipes
            .AsNoTracking()
            .Where(r => r.Status == RecipeStatus.Published)
            .Select(r => new { r.Slug, r.UpdatedAt, r.PublishedAt })
            .ToListAsync(cancellationToken);

        foreach (var recipe in recipes)
        {
            var recipeUrl = $"{request.BaseUrl}/recipes/{recipe.Slug}";
            var lastMod = recipe.UpdatedAt ?? recipe.PublishedAt ?? DateTime.UtcNow;
            urlset.Add(CreateUrlElement(xmlns, recipeUrl, lastMod, "0.9", "monthly"));
        }

        // Xuất ra chuỗi XML
        var xmlDoc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), urlset);
        
        var stringBuilder = new StringBuilder();
        using (var writer = new Utf8StringWriter(stringBuilder))
        {
            xmlDoc.Save(writer);
        }

        return stringBuilder.ToString();
    }

    private static XElement CreateUrlElement(XNamespace xmlns, string loc, DateTime lastMod, string priority, string changeFreq)
    {
        return new XElement(xmlns + "url",
            new XElement(xmlns + "loc", loc),
            new XElement(xmlns + "lastmod", lastMod.ToString("yyyy-MM-dd")),
            new XElement(xmlns + "changefreq", changeFreq),
            new XElement(xmlns + "priority", priority)
        );
    }
}

public class Utf8StringWriter : StringWriter
{
    public Utf8StringWriter(StringBuilder sb) : base(sb) { }
    public override Encoding Encoding => Encoding.UTF8;
}