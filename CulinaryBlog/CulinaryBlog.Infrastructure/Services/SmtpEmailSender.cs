using CulinaryBlog.Application.Contracts.Infrastructure;
using CulinaryBlog.Infrastructure.Configurations;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;

namespace CulinaryBlog.Infrastructure.Services;

public sealed class SmtpEmailSender(IOptions<SmtpOptions> settings) : IEmailSender
{
    public async Task SendAsync(string recipient, string subject, string html, string text, CancellationToken ct)
    {
        var options = settings.Value;
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(options.FromName, options.FromEmail));
        message.To.Add(MailboxAddress.Parse(recipient));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = html, TextBody = text }.ToMessageBody();
        using var client = new SmtpClient { Timeout = 30000 };
        await client.ConnectAsync(options.Host, options.Port, options.Tls, ct);
        if (!string.IsNullOrEmpty(options.Username))
            await client.AuthenticateAsync(options.Username, options.Password, ct);
        await client.SendAsync(message, ct);
        // Once SMTP accepted the message, a disconnect failure must not trigger a resend.
        try { await client.DisconnectAsync(true, ct); }
        catch (Exception) { }
    }
}
