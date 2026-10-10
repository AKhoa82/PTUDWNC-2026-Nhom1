namespace CulinaryBlog.Application.Features.Auth.GoogleLogin;

public interface IGoogleLoginTransactionFactory
{
    Task<IGoogleLoginTransaction> BeginAsync(
        string providerKey, string normalizedEmail, CancellationToken cancellationToken);
}

public interface IGoogleLoginTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}
