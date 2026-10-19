using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.EntityFrameworkCore;
using Xunit;
using CulinaryBlog.API.Endpoints;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Domain.Entities;

namespace CulinaryBlog.Application.Tests
{
    public class RecipeImageCacheInvalidatorTests
    {
        [Fact]
        public async Task ProcessAsync_Evicts_Recipes_And_DynamicTags_And_Redis()
        {
            // Arrange
            var pendingRecords = new List<RecipeCacheInvalidation>
            {
                new RecipeCacheInvalidation { RecipeSlug = "slug-a", ProcessedAt = null, CreatedAt = DateTime.UtcNow },
                new RecipeCacheInvalidation { RecipeSlug = "slug-a", ProcessedAt = null, CreatedAt = DateTime.UtcNow },
                new RecipeCacheInvalidation { RecipeSlug = "slug-b", ProcessedAt = null, CreatedAt = DateTime.UtcNow },
                new RecipeCacheInvalidation { RecipeSlug = null, ProcessedAt = null, CreatedAt = DateTime.UtcNow }
            };

            var dbMock = new Mock<IApplicationDbContext>();
            dbMock.Setup(db => db.RecipeCacheInvalidations).ReturnsDbSet(pendingRecords);

            var outputCacheMock = new Mock<IOutputCacheStore>();
            var distributedCacheMock = new Mock<IDistributedCache>();

            var invalidator = new RecipeImageCacheInvalidator(
                dbMock.Object, 
                outputCacheMock.Object, 
                distributedCacheMock.Object, 
                NullLogger<RecipeImageCacheInvalidator>.Instance);

            // Act
            await invalidator.ProcessAsync(CancellationToken.None);

            // Assert
            outputCacheMock.Verify(x => x.EvictByTagAsync("recipes", It.IsAny<CancellationToken>()), Times.Once);
            outputCacheMock.Verify(x => x.EvictByTagAsync("recipe:slug-a", It.IsAny<CancellationToken>()), Times.Once);
            outputCacheMock.Verify(x => x.EvictByTagAsync("recipe:slug-b", It.IsAny<CancellationToken>()), Times.Once);
            outputCacheMock.Verify(x => x.EvictByTagAsync("recipe:", It.IsAny<CancellationToken>()), Times.Never);
            outputCacheMock.Verify(x => x.EvictByTagAsync(It.IsRegex("^recipe:null$"), It.IsAny<CancellationToken>()), Times.Never);

            distributedCacheMock.Verify(x => x.SetAsync(
                "recipes:list:version", 
                It.IsAny<byte[]>(), 
                It.IsAny<DistributedCacheEntryOptions>(), 
                It.IsAny<CancellationToken>()), Times.Once);

            Assert.All(pendingRecords, r => Assert.NotNull(r.ProcessedAt));
            dbMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ProcessAsync_DoesNot_MarkProcessed_When_EvictionFails()
        {
            var pendingRecords = new List<RecipeCacheInvalidation>
            {
                new RecipeCacheInvalidation { RecipeSlug = "slug-a", ProcessedAt = null }
            };

            var dbMock = new Mock<IApplicationDbContext>();
            dbMock.Setup(db => db.RecipeCacheInvalidations).ReturnsDbSet(pendingRecords);

            var outputCacheMock = new Mock<IOutputCacheStore>();
            outputCacheMock.Setup(x => x.EvictByTagAsync("recipes", It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Redis failure"));

            var distributedCacheMock = new Mock<IDistributedCache>();

            var invalidator = new RecipeImageCacheInvalidator(
                dbMock.Object, 
                outputCacheMock.Object, 
                distributedCacheMock.Object, 
                NullLogger<RecipeImageCacheInvalidator>.Instance);

            await Assert.ThrowsAsync<Exception>(() => invalidator.ProcessAsync(CancellationToken.None));

            Assert.Null(pendingRecords[0].ProcessedAt);
            dbMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
