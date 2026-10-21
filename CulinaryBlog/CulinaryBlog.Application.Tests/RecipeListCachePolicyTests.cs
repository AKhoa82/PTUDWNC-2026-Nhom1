using CulinaryBlog.API.Infrastructure;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;
using Xunit;

namespace CulinaryBlog.Application.Tests;

public sealed class RecipeListCachePolicyTests
{
    [Fact]
    public async Task Anonymous_list_uses_fifteen_minute_query_varying_tagged_cache()
    {
        var http = new DefaultHttpContext();
        http.Request.Method = "GET";
        http.Request.QueryString = new QueryString("?page=2&sort=title");
        var context = new OutputCacheContext { HttpContext = http };

        await new RecipeListCachePolicy().CacheRequestAsync(context, default);

        Assert.True(context.EnableOutputCaching);
        Assert.True(context.AllowCacheLookup);
        Assert.True(context.AllowCacheStorage);
        Assert.Equal(TimeSpan.FromMinutes(15), context.ResponseExpirationTimeSpan);
        Assert.Equal("*", context.CacheVaryByRules.QueryKeys.ToString());
        Assert.Contains("recipes", context.Tags);
    }

    [Fact]
    public async Task Authorization_header_bypasses_shared_list_cache()
    {
        var http = new DefaultHttpContext();
        http.Request.Method = "GET";
        http.Request.Headers.Authorization = "Bearer token";
        var context = new OutputCacheContext { HttpContext = http };

        await new RecipeListCachePolicy().CacheRequestAsync(context, default);

        Assert.False(context.AllowCacheLookup);
        Assert.False(context.AllowCacheStorage);
    }

    [Fact]
    public async Task Authenticated_identity_bypasses_shared_list_cache_without_authorization_header()
    {
        var http = new DefaultHttpContext();
        http.Request.Method = "GET";
        http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "author")], "cookie"));
        var context = new OutputCacheContext { HttpContext = http };

        await new RecipeListCachePolicy().CacheRequestAsync(context, default);

        Assert.False(context.AllowCacheLookup);
        Assert.False(context.AllowCacheStorage);
    }

    [Fact]
    public async Task Cookie_setting_response_is_not_stored()
    {
        var http = new DefaultHttpContext();
        http.Request.Method = "GET";
        var context = new OutputCacheContext { HttpContext = http };
        var policy = new RecipeListCachePolicy();
        await policy.CacheRequestAsync(context, default);
        http.Response.Headers.SetCookie = "session=private";

        await policy.ServeResponseAsync(context, default);

        Assert.False(context.AllowCacheStorage);
    }
}
