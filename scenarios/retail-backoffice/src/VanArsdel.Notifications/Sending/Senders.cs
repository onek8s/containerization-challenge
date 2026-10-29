using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using VanArsdel.Integration.Messaging;

namespace VanArsdel.Notifications.Sending;

public class NotificationOptions
{
    public string FromEmail { get; set; } = "noreply@vanarsdel.example";
    public string FromName { get; set; } = "VanArsdel";
    public string SmsSender { get; set; } = "VanArsdel";
    public string EmailProvider { get; set; } = "Postiq";
    public bool SmtpFallback { get; set; } = true;
    public PostiqOptions Postiq { get; set; } = new();
    public SmtpOptions Smtp { get; set; } = new();
    public string WebShopUrl { get; set; } = "https://www.vanarsdel.example";
}

public class PostiqOptions
{
    public string BaseUrl { get; set; } = "https://api.postiq.example";
    public string ApiKey { get; set; } = "";
}

public class SmtpOptions
{
    public string Host { get; set; } = "smtp.corp.local";
    public int Port { get; set; } = 25;
    public bool StartTls { get; set; }
    public string UserName { get; set; }
    public string Password { get; set; }
}

public record SendResult(string Provider, string MessageId);

public interface IEmailSender
{
    Task<SendResult> SendAsync(string to, string subject, string html, string tag, CancellationToken cancellationToken);
}

public interface ISmsSender
{
    Task<SendResult> SendAsync(string to, string text, CancellationToken cancellationToken);
}

public class PostiqEmailSender : IEmailSender
{
    private readonly IEmailProviderApi _api;
    private readonly NotificationOptions _options;

    public PostiqEmailSender(IEmailProviderApi api, IOptions<NotificationOptions> options)
    {
        _api = api;
        _options = options.Value;
    }

    public async Task<SendResult> SendAsync(string to, string subject, string html, string tag, CancellationToken cancellationToken)
    {
        var response = await _api.SendAsync(new EmailSendRequest
        {
            From = new EmailAddress { Email = _options.FromEmail, Name = _options.FromName },
            To = { new EmailAddress { Email = to } },
            Subject = subject,
            Html = html,
            Tags = { tag },
        }, "Bearer " + _options.Postiq.ApiKey);
        return new SendResult("Postiq", response.MessageId);
    }
}

/// <summary>The on-premises relay. Used for internal mail and when Postiq is down.</summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly NotificationOptions _options;

    public SmtpEmailSender(IOptions<NotificationOptions> options)
    {
        _options = options.Value;
    }

    public async Task<SendResult> SendAsync(string to, string subject, string html, string tag, CancellationToken cancellationToken)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromEmail));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Headers.Add("X-VanArsdel-Tag", tag);
        message.Body = new BodyBuilder { HtmlBody = html }.ToMessageBody();

        using var client = new SmtpClient();
        var smtp = _options.Smtp;
        await client.ConnectAsync(smtp.Host, smtp.Port, smtp.StartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None, cancellationToken);
        if (!string.IsNullOrEmpty(smtp.UserName))
        {
            await client.AuthenticateAsync(smtp.UserName, smtp.Password, cancellationToken);
        }
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
        return new SendResult("Smtp", message.MessageId);
    }
}

/// <summary>Postiq first, SMTP relay if Postiq fails (customer mail must still go out).</summary>
public class FallbackEmailSender : IEmailSender
{
    private readonly PostiqEmailSender _primary;
    private readonly SmtpEmailSender _fallback;
    private readonly NotificationOptions _options;
    private readonly ILogger<FallbackEmailSender> _logger;

    public FallbackEmailSender(PostiqEmailSender primary, SmtpEmailSender fallback, IOptions<NotificationOptions> options, ILogger<FallbackEmailSender> logger)
    {
        _primary = primary;
        _fallback = fallback;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<SendResult> SendAsync(string to, string subject, string html, string tag, CancellationToken cancellationToken)
    {
        if (string.Equals(_options.EmailProvider, "Smtp", StringComparison.OrdinalIgnoreCase))
        {
            return await _fallback.SendAsync(to, subject, html, tag, cancellationToken);
        }
        try
        {
            return await _primary.SendAsync(to, subject, html, tag, cancellationToken);
        }
        catch (Exception ex) when (_options.SmtpFallback && ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Postiq failed, sending {Tag} through SMTP", tag);
            return await _fallback.SendAsync(to, subject, html, tag, cancellationToken);
        }
    }
}

public class PostiqSmsSender : ISmsSender
{
    private readonly ISmsProviderApi _api;
    private readonly NotificationOptions _options;

    public PostiqSmsSender(ISmsProviderApi api, IOptions<NotificationOptions> options)
    {
        _api = api;
        _options = options.Value;
    }

    public async Task<SendResult> SendAsync(string to, string text, CancellationToken cancellationToken)
    {
        var response = await _api.SendAsync(new SmsSendRequest { From = _options.SmsSender, To = to, Text = text }, "Bearer " + _options.Postiq.ApiKey);
        return new SendResult("Postiq", response.Id);
    }
}
