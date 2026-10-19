using System.Reflection;
using System.Net;
using System.Net.Sockets;
using System.Text;
using CulinaryBlog.Infrastructure.Services;
using Microsoft.Extensions.Options;
using MimeKit;
using CulinaryBlog.Infrastructure.Configurations;
using CulinaryBlog.Infrastructure.Jobs;
using Hangfire;
using MailKit.Security;
using Xunit;

namespace CulinaryBlog.Application.Tests;

public sealed class WelcomeEmailTests
{
    [Fact]
    public void Template_encodes_user_input_and_preserves_plain_text()
    {
        var (html, text) = WelcomeEmailTemplate.Render("<script>alert(1)</script> & An", "https://example.com/?a=1&b=2");
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.Contains("?a=1&amp;b=2", html);
        Assert.Contains("<script>alert(1)</script> & An", text);
        Assert.Contains("https://example.com/?a=1&b=2", text);
        Assert.Contains("\n", text);
    }

    [Fact]
    public async Task Smtp_sender_delivers_multipart_message_to_local_smtp_server()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var captured = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var server = Task.Run(async () =>
        {
            using var socket = await listener.AcceptTcpClientAsync(timeout.Token);
            await using var stream = socket.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
            using var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true) { NewLine = "\r\n", AutoFlush = true };
            await writer.WriteLineAsync("220 localhost SMTP test");
            while (await reader.ReadLineAsync(timeout.Token) is { } line)
            {
                if (line.StartsWith("EHLO") || line.StartsWith("HELO")) await writer.WriteLineAsync("250 localhost");
                else if (line == "DATA")
                {
                    await writer.WriteLineAsync("354 Send message");
                    var body = new StringBuilder();
                    while (await reader.ReadLineAsync(timeout.Token) is { } data && data != ".")
                        body.AppendLine(data.StartsWith("..") ? data[1..] : data);
                    captured.TrySetResult(body.ToString());
                    await writer.WriteLineAsync("250 Accepted");
                }
                else if (line == "QUIT") { await writer.WriteLineAsync("221 Bye"); break; }
                else await writer.WriteLineAsync("250 OK");
            }
        }, timeout.Token);
        var sender = new SmtpEmailSender(Options.Create(new SmtpOptions
        {
            Host = "127.0.0.1", Port = port, Tls = SecureSocketOptions.None, FromEmail = "hello@example.com"
        }));
        var (html, text) = WelcomeEmailTemplate.Render("Nguy\u1ec5n An", "https://example.com");
        await sender.SendAsync("an@example.com", WelcomeEmailTemplate.Subject, html, text, timeout.Token);
        await server;
        using var messageStream = new MemoryStream(Encoding.ASCII.GetBytes(await captured.Task.WaitAsync(timeout.Token)));
        var message = await MimeMessage.LoadAsync(messageStream, timeout.Token);
        Assert.Equal("Ch\u00e0o m\u1eebng b\u1ea1n \u0111\u1ebfn v\u1edbi Culinary Blog!", message.Subject);
        Assert.Equal("an@example.com", Assert.IsType<MailboxAddress>(Assert.Single(message.To)).Address);
        Assert.NotNull(message.HtmlBody);
        Assert.Equal(html, message.HtmlBody.TrimEnd('\r', '\n'));
        Assert.Contains("Nguy\u1ec5n An", message.TextBody);
    }

    [Theory]
    [InlineData(SecureSocketOptions.None, true, "http://localhost:3000", true)]
    [InlineData(SecureSocketOptions.None, false, "https://example.com", false)]
    [InlineData(SecureSocketOptions.Auto, false, "https://example.com", false)]
    [InlineData(SecureSocketOptions.StartTlsWhenAvailable, false, "https://example.com", false)]
    [InlineData(SecureSocketOptions.StartTls, false, "https://example.com", true)]
    [InlineData(SecureSocketOptions.SslOnConnect, false, "https://example.com", true)]
    [InlineData(SecureSocketOptions.StartTls, false, "javascript:alert(1)", false)]
    [InlineData(SecureSocketOptions.StartTls, false, "http://example.com", false)]
    [InlineData(SecureSocketOptions.StartTls, false, "https://user:password@example.com", false)]
    public void Configuration_requires_secure_production_settings(SecureSocketOptions tls, bool development, string url, bool valid)
    {
        var options = new SmtpOptions { Host = "smtp.example.com", FromEmail = "hello@example.com", Tls = tls, AppUrl = url };
        Assert.Equal(valid, options.IsValid(development));
        options.Port = 0;
        Assert.False(options.IsValid(development));
    }

    [Fact]
    public void Configuration_rejects_partial_credentials_and_invalid_sender()
    {
        var options = new SmtpOptions { Host = "smtp.example.com", FromEmail = "hello@example.com", AppUrl = "https://example.com", Username = "user" };
        Assert.False(options.IsValid(false));
        options.Password = "secret";
        Assert.True(options.IsValid(false));
        options.FromEmail = "invalid";
        Assert.False(options.IsValid(false));
    }

    [Fact]
    public void Job_has_exact_retry_schedule_and_terminal_failed_state()
    {
        var retry = typeof(WelcomeEmailJob).GetMethod(nameof(WelcomeEmailJob.ExecuteAsync))!.GetCustomAttribute<AutomaticRetryAttribute>()!;
        Assert.Equal(3, retry.Attempts);
        Assert.Equal(new[] { 60, 300, 1800 }, retry.DelaysInSeconds);
        Assert.Equal(AttemptsExceededAction.Fail, retry.OnAttemptsExceeded);
    }
}
