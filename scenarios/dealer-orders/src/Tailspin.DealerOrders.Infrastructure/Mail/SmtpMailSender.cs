using System;
using System.Net;
using System.Net.Mail;
using System.Text;
using log4net;
using Polly;

namespace Tailspin.DealerOrders.Infrastructure.Mail
{
    public interface IMailSender
    {
        void Send(string to, string subject, string body, string replyTo = null, Attachment attachment = null);
    }

    /// <summary>
    /// Plain text mail through the SMTP relay (Mail.Host, port 25 or 587 with STARTTLS). In test environments
    /// Mail.PickupDirectory writes .eml files instead of sending.
    /// </summary>
    public class SmtpMailSender : IMailSender
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(SmtpMailSender));

        private readonly string _host;
        private readonly int _port;
        private readonly bool _ssl;
        private readonly string _user;
        private readonly string _password;
        private readonly string _from;
        private readonly string _pickupDirectory;

        public SmtpMailSender()
        {
            _host = Settings.Get("Mail.Host", "smtp.corp.local");
            _port = Settings.Int("Mail.Port", 25);
            _ssl = Settings.Bool("Mail.EnableSsl", false);
            _user = Settings.Get("Mail.User");
            _password = Settings.Get("Mail.Password");
            _from = Settings.Get("Mail.From", "Tailspin Machinery <dealer-orders@tailspin-machinery.example>");
            _pickupDirectory = Settings.Get("Mail.PickupDirectory");
        }

        public void Send(string to, string subject, string body, string replyTo = null, Attachment attachment = null)
        {
            using (var message = new MailMessage())
            {
                message.From = new MailAddress(_from);
                foreach (var address in to.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)) message.To.Add(address.Trim());
                if (!string.IsNullOrEmpty(replyTo)) message.ReplyToList.Add(replyTo);
                message.Subject = subject;
                message.Body = body;
                message.BodyEncoding = Encoding.UTF8;
                message.SubjectEncoding = Encoding.UTF8;
                if (attachment != null) message.Attachments.Add(attachment);

                Policy.Handle<SmtpException>(ex => ex.StatusCode != SmtpStatusCode.MailboxUnavailable)
                    .WaitAndRetry(2, i => TimeSpan.FromSeconds(5 * i), (ex, delay) => Log.Warn($"SMTP send failed, retrying in {delay}", ex))
                    .Execute(() =>
                    {
                        using (var client = Client())
                            client.Send(message);
                    });
            }
            Log.InfoFormat("Mail '{0}' sent to {1}", subject, to);
        }

        private SmtpClient Client()
        {
            if (!string.IsNullOrEmpty(_pickupDirectory))
                return new SmtpClient { DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory, PickupDirectoryLocation = _pickupDirectory };
            var client = new SmtpClient(_host, _port) { EnableSsl = _ssl, Timeout = 30000 };
            if (!string.IsNullOrEmpty(_user)) client.Credentials = new NetworkCredential(_user, _password);
            return client;
        }
    }
}
