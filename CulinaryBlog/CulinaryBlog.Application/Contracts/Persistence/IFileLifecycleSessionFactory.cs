namespace CulinaryBlog.Application.Contracts.Persistence;

// Each session owns a fresh context, including recovery after a failed transaction.
public interface IFileLifecycleSessionFactory
{
    IFileLifecycleSession Create();
}

public interface IFileLifecycleSession : IAsyncDisposable
{
    IApplicationDbContext Db { get; }
    Task<IRecipeImageTransaction> BeginAsync(Guid? recipeId, Guid fileId, CancellationToken ct);
}
