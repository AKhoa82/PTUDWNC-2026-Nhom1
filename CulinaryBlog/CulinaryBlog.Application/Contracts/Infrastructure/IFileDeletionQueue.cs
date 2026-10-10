namespace CulinaryBlog.Application.Contracts.Infrastructure;

public interface IFileDeletionQueue
{
    string Enqueue(Guid storedFileId);
}
