using CulinaryBlog.Application.Contracts.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CulinaryBlog.Infrastructure.Persistence;

public sealed class RecipeImageTransactionFactory(ApplicationDbContext db) : IRecipeImageTransactionFactory
{
    public async Task<IRecipeImageTransaction> BeginAsync(Guid recipeId, CancellationToken ct)
    {
        var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"Recipes\" WHERE \"Id\" = {recipeId} FOR UPDATE", ct);
            return new Transaction(transaction);
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private sealed class Transaction(IDbContextTransaction transaction) : IRecipeImageTransaction
    {
        public Task CommitAsync(CancellationToken ct) => transaction.CommitAsync(ct);
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
