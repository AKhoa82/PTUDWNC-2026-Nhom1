using CulinaryBlog.Application.Contracts.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CulinaryBlog.Infrastructure.Persistence;

public sealed class RegistrationTransactionFactory(ApplicationDbContext db) : IRegistrationTransactionFactory
{
    public async Task<IRegistrationTransaction> BeginAsync(CancellationToken ct)
        => new Transaction(await db.Database.BeginTransactionAsync(ct));

    private sealed class Transaction(IDbContextTransaction transaction) : IRegistrationTransaction
    {
        public Task CommitAsync(CancellationToken ct) => transaction.CommitAsync(ct);
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
