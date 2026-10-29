using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace Proseware.ControlTower.Jobs.Infrastructure;

public sealed class Mailer
{
    private readonly SmtpOptions _options;
    private readonly ILogger<Mailer> _logger;

    public Mailer(IOptions<SmtpOptions> options, ILogger<Mailer> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(string to, string subject, string body, IEnumerable<string> attachments, CancellationToken ct)
    {
        using var message = new MailMessage
        {
            From = new MailAddress(_options.From.Contains('<') ? _options.From[(_options.From.IndexOf('<') + 1)..].TrimEnd('>') : _options.From,
                _options.From.Contains('<') ? _options.From[.._options.From.IndexOf('<')].Trim() : null),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };
        foreach (var address in to.Split(';', ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            message.To.Add(address);
        if (!string.IsNullOrEmpty(_options.BlindCopy)) message.Bcc.Add(_options.BlindCopy);
        foreach (var file in attachments) message.Attachments.Add(new Attachment(file));

        using var client = new SmtpClient(_options.Host, _options.Port) { EnableSsl = _options.EnableSsl };
        if (!string.IsNullOrEmpty(_options.User)) client.Credentials = new NetworkCredential(_options.User, _options.Password);
        await client.SendMailAsync(message, ct);
        _logger.LogInformation("Mail '{Subject}' sent to {To}", subject, to);
    }
}
