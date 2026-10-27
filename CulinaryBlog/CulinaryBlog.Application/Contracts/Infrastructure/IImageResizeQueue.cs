namespace CulinaryBlog.Application.Contracts.Infrastructure;

public interface IImageResizeQueue
{
    Task<string> EnqueueAsync(Guid imageId, CancellationToken ct = default);
}
