namespace CulinaryBlog.Application.Contracts.Persistence;

public interface IRegistrationTransactionFactory
{
    Task<IRegistrationTransaction> BeginAsync(CancellationToken ct);
}

public interface IRegistrationTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct);
}
