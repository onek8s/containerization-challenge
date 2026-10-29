using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace StudyData.Integration.Mail;

public class Mailer
{
    private readonly MailOptions _options;
    private readonly ILogger<Mailer> _log;

    public Mailer(IOptions<MailOptions> options, ILogger<Mailer> log)
    {
        _options = options.Value;
        _log = log;
    }

    public async Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        using var message = new MailMessage(_options.From, to, subject, body);
        using var client = new SmtpClient(_options.Host, _options.Port);
        if (!string.IsNullOrEmpty(_options.PickupFolder))
        {
            Directory.CreateDirectory(_options.PickupFolder);
            client.DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory;
            client.PickupDirectoryLocation = Path.GetFullPath(_options.PickupFolder);
        }
        else
        {
            client.UseDefaultCredentials = _options.UseDefaultCredentials;
        }

        await client.SendMailAsync(message, ct);
        _log.LogInformation("Mail '{Subject}' sent to {To}", subject, to);
    }
}
