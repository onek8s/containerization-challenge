using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace Proseware.ControlTower.Identity.Services;

public sealed class SmtpOptions
{
    public string Host { get; set; } = "smtp.corp.local";
    public int Port { get; set; } = 25;
    public bool EnableSsl { get; set; }
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string From { get; set; } = "Proseware Control Tower <no-reply@proseware-freight.example>";
    /// <summary>When set, mails are written as .eml files to this folder instead of being sent.</summary>
    public string? PickupDirectory { get; set; }
}

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
}

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        using var message = new MailMessage { From = new MailAddress(_options.From), Subject = subject, Body = htmlBody, IsBodyHtml = true };
        message.To.Add(to);
        using var client = new SmtpClient(_options.Host, _options.Port) { EnableSsl = _options.EnableSsl };
        if (!string.IsNullOrEmpty(_options.PickupDirectory))
        {
            Directory.CreateDirectory(_options.PickupDirectory);
            client.DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory;
            client.PickupDirectoryLocation = Path.GetFullPath(_options.PickupDirectory);
        }
        else if (!string.IsNullOrEmpty(_options.UserName))
        {
            client.Credentials = new NetworkCredential(_options.UserName, _options.Password);
        }
        await client.SendMailAsync(message, ct);
        _logger.LogInformation("Mail '{Subject}' sent to {To}", subject, to);
    }
}
