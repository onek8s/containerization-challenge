using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using Woodgrove.Statements.Core.Delivery;

namespace Woodgrove.Statements.Distribution.Delivery
{
    public class MailAttachment
    {
        public string FileName { get; set; }
        public string MimeType { get; set; }
        public byte[] Content { get; set; }
    }

    /// <summary>Sends through the internal relay with the service account's Windows credentials (system.net/mailSettings).</summary>
    public class MailSender
    {
        readonly DistributionSettings _settings;

        public MailSender(DistributionSettings settings)
        {
            _settings = settings;
        }

        public void Send(IList<string> to, string subject, string body, IEnumerable<MailAttachment> attachments, bool customerMail)
        {
            if (to == null || to.Count == 0) throw new ArgumentException("No recipients.");
            var invalid = RecipientList.Invalid(to);
            if (invalid.Count > 0) throw new FormatException("Invalid e-mail address: " + string.Join(", ", invalid));

            using (var message = new MailMessage())
            {
                message.From = new MailAddress(_settings.MailFrom);
                if (!string.IsNullOrWhiteSpace(_settings.ReplyTo)) message.ReplyToList.Add(_settings.ReplyTo);
                var recipients = customerMail && !string.IsNullOrWhiteSpace(_settings.RedirectCustomerMailTo)
                    ? RecipientList.Parse(_settings.RedirectCustomerMailTo) : to;
                foreach (var address in recipients) message.To.Add(address);
                if (!ReferenceEquals(recipients, to)) subject = "[TEST to " + string.Join(", ", to) + "] " + subject;
                message.Subject = subject;
                message.SubjectEncoding = Encoding.UTF8;
                message.Body = body;
                message.BodyEncoding = Encoding.UTF8;
                message.IsBodyHtml = false;
                message.Headers.Add("X-Woodgrove-Source", "AccountStatements");
                if (attachments != null)
                    foreach (var a in attachments)
                        message.Attachments.Add(new Attachment(new MemoryStream(a.Content), a.FileName, a.MimeType ?? MediaTypeNames.Application.Octet));

                using (var client = new SmtpClient(_settings.SmtpHost, _settings.SmtpPort))
                {
                    client.EnableSsl = _settings.SmtpEnableSsl;
                    client.UseDefaultCredentials = true;
                    client.Timeout = 120000;
                    client.Send(message);
                }
            }
        }
    }
}
