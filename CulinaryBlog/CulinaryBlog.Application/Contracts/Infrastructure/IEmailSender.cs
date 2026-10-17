namespace CulinaryBlog.Application.Contracts.Infrastructure;

public interface IEmailSender
{
    Task SendAsync(string recipient, string subject, string html, string text, CancellationToken ct);
}

public interface IWelcomeEmailQueue
{
    Task EnqueueAsync(Guid outboxId, CancellationToken ct);
}
