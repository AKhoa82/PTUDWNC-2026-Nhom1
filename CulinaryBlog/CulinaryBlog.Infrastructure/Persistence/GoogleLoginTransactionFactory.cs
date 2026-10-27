using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using CulinaryBlog.Application.Features.Auth.GoogleLogin;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CulinaryBlog.Infrastructure.Persistence;

public sealed class GoogleLoginTransactionFactory(ApplicationDbContext context) : IGoogleLoginTransactionFactory
{
    public async Task<IGoogleLoginTransaction> BeginAsync(
        string providerKey, string normalizedEmail, CancellationToken cancellationToken)
    {
        var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var keys = new[]
            {
                StableLockKey($"google:subject:{providerKey}"),
                StableLockKey($"google:email:{normalizedEmail}")
            };
            Array.Sort(keys);
            foreach (var key in keys.Distinct())
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT pg_advisory_xact_lock({key})", cancellationToken);

            return new GoogleLoginTransaction(transaction);
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private static long StableLockKey(string value) =>
        BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed class GoogleLoginTransaction(IDbContextTransaction transaction) : IGoogleLoginTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) =>
            transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
