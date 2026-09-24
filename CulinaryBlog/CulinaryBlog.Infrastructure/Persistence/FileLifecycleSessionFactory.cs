using CulinaryBlog.Application.Contracts.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace CulinaryBlog.Infrastructure.Persistence;

public sealed class FileLifecycleSessionFactory(IServiceScopeFactory scopes) : IFileLifecycleSessionFactory
{
    public IFileLifecycleSession Create() => new Session(scopes.CreateAsyncScope());
    private sealed class Session(AsyncServiceScope scope) : IFileLifecycleSession
    {
        private readonly ApplicationDbContext _db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        public IApplicationDbContext Db => _db;
        public async Task<IRecipeImageTransaction> BeginAsync(Guid? recipeId, Guid fileId, CancellationToken ct)
        {
            var transaction = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                if (recipeId.HasValue)
                    await _db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Recipes\" WHERE \"Id\" = {recipeId.Value} FOR UPDATE", ct);
                await _db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"StoredFiles\" WHERE \"Id\" = {fileId} FOR UPDATE", ct);
                return new Transaction(transaction);
            }
            catch { await transaction.DisposeAsync(); throw; }
        }
        public ValueTask DisposeAsync() => scope.DisposeAsync();
    }
    private sealed class Transaction(IDbContextTransaction transaction) : IRecipeImageTransaction
    {
        public Task CommitAsync(CancellationToken ct) => transaction.CommitAsync(ct);
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
