using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace ExpenseClaims.Jobs.Mail;

public interface IMailSender
{
    Task SendAsync(string toAddress, string toName, string subject, string textBody, CancellationToken ct);
}

public sealed class MailSender : IMailSender
{
    private readonly SmtpOptions _options;
    private readonly ILogger<MailSender> _logger;

    public MailSender(IOptions<SmtpOptions> options, ILogger<MailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(string toAddress, string toName, string subject, string textBody, CancellationToken ct)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(_options.From));
        message.To.Add(new MailboxAddress(toName, toAddress));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = textBody };

        if (!string.IsNullOrEmpty(_options.PickupFolder))
        {
            Directory.CreateDirectory(_options.PickupFolder);
            var path = Path.Combine(_options.PickupFolder, $"{DateTime.Now:yyyyMMddHHmmss}-{Guid.NewGuid():N}.eml");
            await message.WriteToAsync(path, ct);
            _logger.LogInformation("Mail to {To} written to {Path}", toAddress, path);
            return;
        }

        using var client = new SmtpClient();
        await client.ConnectAsync(_options.Host, _options.Port, _options.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None, ct);
        if (!string.IsNullOrEmpty(_options.UserName))
            await client.AuthenticateAsync(_options.UserName, _options.Password, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
        _logger.LogInformation("Mail {Subject} sent to {To}", subject, toAddress);
    }
}
