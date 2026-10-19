using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Primitives;
using Xunit;
using CulinaryBlog.API.Infrastructure;

namespace CulinaryBlog.Application.Tests
{
    public class RecipeDetailCachePolicyTests
    {
        [Fact]
        public async Task CacheRequestAsync_Adds_Dynamic_Slug_Tag()
        {
            var policy = new RecipeDetailCachePolicy();
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Method = "GET";
            httpContext.Request.RouteValues["slug"] = "pho-bo";

            var context = new OutputCacheContext { HttpContext = httpContext };

            await policy.CacheRequestAsync(context, CancellationToken.None);

            Assert.True(context.EnableOutputCaching);
            Assert.True(context.AllowCacheLookup);
            Assert.True(context.AllowCacheStorage);
            Assert.Equal(TimeSpan.FromMinutes(60), context.ResponseExpirationTimeSpan);
            Assert.Contains("recipes", context.Tags);
            Assert.Contains("recipe:pho-bo", context.Tags);
        }

        [Fact]
        public async Task CacheRequestAsync_DoesNotCache_AuthenticatedUser()
        {
            var policy = new RecipeDetailCachePolicy();
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Method = "GET";
            httpContext.Request.Headers.Authorization = new StringValues("Bearer dummy");

            var context = new OutputCacheContext { HttpContext = httpContext };

            await policy.CacheRequestAsync(context, CancellationToken.None);

            Assert.False(context.AllowCacheLookup);
            Assert.False(context.AllowCacheStorage);
        }
    }
}
