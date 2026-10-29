using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace Lucerne.Lending.Integration;

public sealed class MailOptions
{
    public const string Section = "Mail";
    public string Host { get; set; } = "smtp.corp.local";
    public int Port { get; set; } = 25;
    public bool EnableSsl { get; set; }
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string From { get; set; } = "Lucerne Savings Bank <noreply@lucernebank.example>";
    /// <summary>When set, mails are written as .eml files to this folder instead of being sent.</summary>
    public string? PickupFolder { get; set; }
}

public interface IMailSender
{
    Task SendAsync(string to, string subject, string body, (string Name, byte[] Content)? attachment = null, CancellationToken ct = default);
}

public sealed class SmtpMailSender : IMailSender
{
    private readonly MailOptions _options;

    public SmtpMailSender(IOptions<MailOptions> options) => _options = options.Value;

    public async Task SendAsync(string to, string subject, string body, (string Name, byte[] Content)? attachment = null, CancellationToken ct = default)
    {
        using var message = new MailMessage { From = new MailAddress(_options.From), Subject = subject, Body = body };
        message.To.Add(to);
        if (attachment is { } a) message.Attachments.Add(new Attachment(new MemoryStream(a.Content), a.Name, "application/pdf"));
        using var client = new SmtpClient(_options.Host, _options.Port) { EnableSsl = _options.EnableSsl };
        if (!string.IsNullOrEmpty(_options.PickupFolder))
        {
            Directory.CreateDirectory(_options.PickupFolder);
            client.DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory;
            client.PickupDirectoryLocation = Path.GetFullPath(_options.PickupFolder);
        }
        else if (!string.IsNullOrEmpty(_options.UserName))
        {
            client.Credentials = new NetworkCredential(_options.UserName, _options.Password);
        }
        await client.SendMailAsync(message, ct);
    }
}
