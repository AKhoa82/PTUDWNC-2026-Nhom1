using MailKit.Security;
using MimeKit;

namespace CulinaryBlog.Infrastructure.Configurations;

public sealed class SmtpOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromEmail { get; set; } = "";
    public string FromName { get; set; } = "Culinary Blog";
    public SecureSocketOptions Tls { get; set; } = SecureSocketOptions.StartTls;
    public string AppUrl { get; set; } = "";

    public bool IsValid(bool development)
        => !string.IsNullOrWhiteSpace(Host) && Port is > 0 and <= 65535
        && MailboxAddress.TryParse(FromEmail, out var mailbox) && mailbox.Address == FromEmail && FromEmail.Contains('@')
        && !FromName.Contains('\r') && !FromName.Contains('\n')
        && (string.IsNullOrEmpty(Username) == string.IsNullOrEmpty(Password))
        && (Tls is SecureSocketOptions.StartTls or SecureSocketOptions.SslOnConnect
            || (development && Tls == SecureSocketOptions.None))
        && Uri.TryCreate(AppUrl, UriKind.Absolute, out var uri)
        && (uri.Scheme == "https" || (development && uri.Scheme == "http"))
        && string.IsNullOrEmpty(uri.UserInfo);
}
