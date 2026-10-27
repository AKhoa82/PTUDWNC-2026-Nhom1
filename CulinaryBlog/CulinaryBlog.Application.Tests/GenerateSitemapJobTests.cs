using System.Net;
using System.Xml.Linq;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Jobs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CulinaryBlog.Application.Tests;

public sealed class GenerateSitemapJobTests
{
    [Fact]
    public async Task Generates_reachable_page_urls_and_only_published_recipes()
    {
        using var directory = new TemporaryDirectory();
        await using var context = new RecipeAcceptanceContext();
        var published = context.SeedRecipe();
        context.Recipes.Add(new Recipe
        {
            Slug = "ban-nhap", Status = RecipeStatus.Draft,
            Category = published.Category, CategoryId = published.CategoryId,
            Author = published.Author, AuthorId = published.AuthorId
        });
        await context.SaveChangesAsync();

        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(x => x.EnvironmentName).Returns("Development");
        environment.SetupGet(x => x.ContentRootPath).Returns(directory.FullName);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FrontendBaseUrl"] = "https://food.example",
            ["BackendBaseUrl"] = "https://api.food.example"
        }).Build();
        var handler = new SuccessfulHandler();
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler));

        var job = new GenerateSitemapJob(context, NullLogger<GenerateSitemapJob>.Instance,
            configuration, environment.Object, factory.Object);
        await job.ExecuteAsync(CancellationToken.None);

        var document = XDocument.Load(Path.Combine(directory.FullName, "wwwroot", "sitemap.xml"));
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var locations = document.Descendants(ns + "loc").Select(x => x.Value).ToArray();
        Assert.Contains("https://food.example/", locations);
        Assert.Contains("https://food.example/categories", locations);
        Assert.Contains("https://food.example/recipes", locations);
        Assert.Contains("https://food.example/categories/mon-chinh", locations);
        Assert.Contains("https://food.example/recipes/pho-bo", locations);
        Assert.DoesNotContain(locations, location => location.Contains("ban-nhap"));
        Assert.Equal("https://www.google.com/ping?sitemap=https%3A%2F%2Fapi.food.example%2Fsitemap.xml", handler.RequestUrl);
        Assert.All(document.Descendants(ns + "url"), url =>
        {
            Assert.NotNull(url.Element(ns + "lastmod"));
            Assert.NotNull(url.Element(ns + "changefreq"));
            Assert.NotNull(url.Element(ns + "priority"));
        });
    }

    [Fact]
    public void Rejects_localhost_urls_in_production()
    {
        using var context = new RecipeAcceptanceContext();
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(x => x.EnvironmentName).Returns("Production");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FrontendBaseUrl"] = "http://localhost:3000",
            ["BackendBaseUrl"] = "https://api.food.example"
        }).Build();

        Assert.Throws<InvalidOperationException>(() => new GenerateSitemapJob(context,
            NullLogger<GenerateSitemapJob>.Instance, configuration, environment.Object,
            Mock.Of<IHttpClientFactory>()));
    }

    [Fact]
    public async Task Deprecated_google_ping_does_not_fail_a_generated_sitemap()
    {
        using var directory = new TemporaryDirectory();
        await using var context = new RecipeAcceptanceContext();
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(x => x.EnvironmentName).Returns("Development");
        environment.SetupGet(x => x.ContentRootPath).Returns(directory.FullName);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FrontendBaseUrl"] = "https://food.example",
            ["BackendBaseUrl"] = "https://api.food.example"
        }).Build();
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(new SuccessfulHandler(HttpStatusCode.NotFound)));

        var job = new GenerateSitemapJob(context, NullLogger<GenerateSitemapJob>.Instance,
            configuration, environment.Object, factory.Object);
        await job.ExecuteAsync(CancellationToken.None);

        Assert.True(File.Exists(Path.Combine(directory.FullName, "wwwroot", "sitemap.xml")));
    }

    [Fact]
    public async Task Unavailable_google_ping_does_not_fail_a_generated_sitemap()
    {
        using var directory = new TemporaryDirectory();
        await using var context = new RecipeAcceptanceContext();
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(x => x.EnvironmentName).Returns("Development");
        environment.SetupGet(x => x.ContentRootPath).Returns(directory.FullName);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FrontendBaseUrl"] = "https://food.example",
            ["BackendBaseUrl"] = "https://api.food.example"
        }).Build();
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(new UnavailableHandler()));

        var job = new GenerateSitemapJob(context, NullLogger<GenerateSitemapJob>.Instance,
            configuration, environment.Object, factory.Object);
        await job.ExecuteAsync(CancellationToken.None);

        Assert.True(File.Exists(Path.Combine(directory.FullName, "wwwroot", "sitemap.xml")));
    }

    private sealed class SuccessfulHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        public SuccessfulHandler(HttpStatusCode status = HttpStatusCode.OK) => _status = status;
        public string? RequestUrl { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUrl = request.RequestUri?.ToString();
            return Task.FromResult(new HttpResponseMessage(_status));
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string FullName { get; } = Directory.CreateTempSubdirectory("culinary-sitemap-test-").FullName;

        public void Dispose() => Directory.Delete(FullName, recursive: true);
    }

    private sealed class UnavailableHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("Google is unreachable");
    }
}
