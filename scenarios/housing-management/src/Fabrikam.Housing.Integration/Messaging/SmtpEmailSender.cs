using System.Collections.Generic;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Fabrikam.Housing.Integration.Messaging
{
    /// <summary>Sends through the internal relay smtp.corp.local (anonymous from the application servers).</summary>
    public class SmtpEmailSender : IEmailSender
    {
        private readonly SmtpOptions _options;
        private readonly ILogger<SmtpEmailSender> _logger;

        public SmtpEmailSender(IOptions<IntegrationOptions> options, ILogger<SmtpEmailSender> logger)
        {
            _options = options.Value.Smtp;
            _logger = logger;
        }

        public static MimeMessage Build(string from, string to, string subject, string htmlBody, IEnumerable<EmailAttachment> attachments)
        {
            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(from));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;
            var builder = new BodyBuilder { HtmlBody = htmlBody };
            if (attachments != null)
            {
                foreach (var a in attachments)
                    builder.Attachments.Add(a.FileName, a.Content, ContentType.Parse(a.ContentType ?? "application/octet-stream"));
            }
            message.Body = builder.ToMessageBody();
            return message;
        }

        public async Task SendAsync(string to, string subject, string htmlBody, IEnumerable<EmailAttachment> attachments = null)
        {
            var message = Build(_options.From, to, subject, htmlBody, attachments);
            using (var client = new SmtpClient())
            {
                await client.ConnectAsync(_options.Host, _options.Port, _options.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None);
                if (!string.IsNullOrEmpty(_options.UserName))
                    await client.AuthenticateAsync(_options.UserName, _options.Password);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);
            }
            _logger.LogInformation("Mail '{Subject}' sent to {To}", subject, to);
        }
    }
}
