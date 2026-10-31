using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace CulinaryBlog.Application.Common.Telemetry;

public static class CulinaryBlogTelemetry
{
    public const string ServiceName = "CulinaryBlog.API";
    public const string ActivitySourceName = "CulinaryBlog.API";
    public const string MeterName = "CulinaryBlog.API";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, "1.0.0");
    public static readonly Meter Meter = new(MeterName, "1.0.0");

    // Custom Business Metrics (FR-OBS-003: recipe created/published count)
    public static readonly Counter<long> RecipesCreatedCounter = Meter.CreateCounter<long>(
        name: "culinaryblog.recipes.created",
        unit: "{recipe}",
        description: "Tong so cong thuc nau an duoc tao moi.");

    public static readonly Counter<long> RecipesPublishedCounter = Meter.CreateCounter<long>(
        name: "culinaryblog.recipes.published",
        unit: "{recipe}",
        description: "Tong so cong thuc nau an duoc xuat ban (Published).");

    // HTTP Metrics (FR-OBS-003: request count, duration histogram, error rate)
    public static readonly Counter<long> HttpRequestCounter = Meter.CreateCounter<long>(
        name: "culinaryblog.http.requests.total",
        unit: "{request}",
        description: "Tong so HTTP requests.");

    public static readonly Histogram<double> HttpRequestDurationHistogram = Meter.CreateHistogram<double>(
        name: "culinaryblog.http.request.duration",
        unit: "ms",
        description: "Thoi gian xu ly HTTP request (ms).");

    public static readonly Counter<long> HttpRequestErrorCounter = Meter.CreateCounter<long>(
        name: "culinaryblog.http.requests.errors",
        unit: "{error}",
        description: "Tong so HTTP requests bi loi (status >= 400 hoac unhandled exception).");

    public static void RecordRecipeCreated(Guid recipeId, Guid categoryId, Guid authorId)
    {
        RecipesCreatedCounter.Add(
            1,
            new KeyValuePair<string, object?>("category.id", categoryId.ToString()),
            new KeyValuePair<string, object?>("author.id", authorId.ToString()));

        Activity.Current?.SetTag("recipe.id", recipeId.ToString());
        Activity.Current?.SetTag("recipe.category_id", categoryId.ToString());
        Activity.Current?.AddEvent(new ActivityEvent("recipe.created"));
    }

    public static void RecordRecipePublished(Guid recipeId, Guid authorId)
    {
        RecipesPublishedCounter.Add(
            1,
            new KeyValuePair<string, object?>("author.id", authorId.ToString()));

        Activity.Current?.SetTag("recipe.id", recipeId.ToString());
        Activity.Current?.AddEvent(new ActivityEvent("recipe.published"));
    }
}