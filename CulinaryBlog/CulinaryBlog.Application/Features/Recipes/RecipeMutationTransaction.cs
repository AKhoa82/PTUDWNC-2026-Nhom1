using CulinaryBlog.Application.Contracts.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CulinaryBlog.Application.Features.Recipes;

internal static class RecipeMutationTransaction
{
    public static async Task<IDbContextTransaction?> BeginAsync(
        IApplicationDbContext context, Guid recipeId, CancellationToken cancellationToken)
    {
        if (context is not DbContext dbContext || !dbContext.Database.IsRelational())
            return null;

        var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"Recipes\" WHERE \"Id\" = {recipeId} FOR UPDATE", cancellationToken);
            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }
}
