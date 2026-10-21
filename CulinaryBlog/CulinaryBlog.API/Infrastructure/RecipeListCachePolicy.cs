using Microsoft.AspNetCore.OutputCaching;

namespace CulinaryBlog.API.Infrastructure;

public sealed class RecipeListCachePolicy : IOutputCachePolicy
{
    public ValueTask CacheRequestAsync(OutputCacheContext context, CancellationToken cancellationToken)
    {
        var request = context.HttpContext.Request;
        var isAnonymousGet = (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method)) &&
            request.Headers.Authorization.Count == 0 &&
            context.HttpContext.User?.Identity?.IsAuthenticated != true;

        context.EnableOutputCaching = true;
        context.AllowCacheLookup = isAnonymousGet;
        context.AllowCacheStorage = isAnonymousGet;
        context.AllowLocking = true;
        context.ResponseExpirationTimeSpan = TimeSpan.FromMinutes(15);
        context.CacheVaryByRules.QueryKeys = "*";
        context.Tags.Add("recipes");

        return ValueTask.CompletedTask;
    }

    public ValueTask ServeFromCacheAsync(OutputCacheContext context, CancellationToken cancellationToken)
        => ValueTask.CompletedTask;

    public ValueTask ServeResponseAsync(OutputCacheContext context, CancellationToken cancellationToken)
    {
        if (context.HttpContext.Response.StatusCode != StatusCodes.Status200OK ||
            context.HttpContext.Response.Headers.SetCookie.Count > 0)
            context.AllowCacheStorage = false;

        return ValueTask.CompletedTask;
    }
}
