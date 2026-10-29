using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using Polly;
using Polly.Retry;

namespace Humongous.Documents.Mail;

public sealed class MailOptions
{
    public const string SectionName = "Mail";

    /// <summary><c>Smtp</c> or <c>Pickup</c> (writes .eml files to <see cref="PickupFolder"/>).</summary>
    public string Mode { get; set; } = "Smtp";

    public string Host { get; set; } = "smtp.corp.local";
    public int Port { get; set; } = 25;
    public bool StartTls { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string From { get; set; } = "Humongous Broker Documents <documents@humongousinsurance.example>";
    public string PickupFolder { get; set; } = "";
}

public sealed record OutgoingMail(IReadOnlyList<string> To, string Subject, string Body, string AttachmentName, byte[] Attachment);

public interface IMailSender
{
    Task SendAsync(OutgoingMail mail, CancellationToken ct);
}

public sealed class SmtpMailSender(IOptions<MailOptions> options, ILogger<SmtpMailSender> logger) : IMailSender
{
    private readonly ResiliencePipeline _retry = new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions
        {
            ShouldHandle = new PredicateBuilder().Handle<SmtpCommandException>().Handle<SmtpProtocolException>().Handle<IOException>()
                .Handle<System.Net.Sockets.SocketException>(),
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromSeconds(5),
            BackoffType = DelayBackoffType.Exponential
        })
        .Build();

    public async Task SendAsync(OutgoingMail mail, CancellationToken ct)
    {
        var o = options.Value;
        var message = MailBuilder.Build(o.From, mail);
        await _retry.ExecuteAsync(async token =>
        {
            using var client = new SmtpClient();
            await client.ConnectAsync(o.Host, o.Port, o.StartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None, token);
            if (!string.IsNullOrEmpty(o.Username))
                await client.AuthenticateAsync(o.Username, o.Password ?? "", token);
            await client.SendAsync(message, token);
            await client.DisconnectAsync(true, token);
        }, ct);
        logger.LogInformation("Sent {Subject} to {Recipients}", mail.Subject, string.Join(", ", mail.To));
    }
}

public sealed class PickupMailSender(IOptions<MailOptions> options, ILogger<PickupMailSender> logger) : IMailSender
{
    public async Task SendAsync(OutgoingMail mail, CancellationToken ct)
    {
        var o = options.Value;
        Directory.CreateDirectory(o.PickupFolder);
        var path = Path.Combine(o.PickupFolder, $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.eml");
        await MailBuilder.Build(o.From, mail).WriteToAsync(path, ct);
        logger.LogInformation("Wrote {Subject} for {Recipients} to {Path}", mail.Subject, string.Join(", ", mail.To), path);
    }
}

internal static class MailBuilder
{
    public static MimeMessage Build(string from, OutgoingMail mail)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(from));
        foreach (var to in mail.To) message.To.Add(MailboxAddress.Parse(to));
        message.Subject = mail.Subject;
        var body = new BodyBuilder { TextBody = mail.Body };
        body.Attachments.Add(mail.AttachmentName, mail.Attachment, new ContentType("application", "pdf"));
        message.Body = body.ToMessageBody();
        return message;
    }
}
