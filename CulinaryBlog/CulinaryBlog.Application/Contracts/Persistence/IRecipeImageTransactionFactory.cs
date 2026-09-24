namespace CulinaryBlog.Application.Contracts.Persistence;

// Serializes changes to a recipe's gallery across API instances.
public interface IRecipeImageTransactionFactory
{
    Task<IRecipeImageTransaction> BeginAsync(Guid recipeId, CancellationToken ct);
}

public interface IRecipeImageTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct);
}
