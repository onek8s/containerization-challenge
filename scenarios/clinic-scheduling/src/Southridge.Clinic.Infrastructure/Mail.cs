using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Mail;
using System.Text;

namespace Southridge.Clinic.Infrastructure
{
    public interface IMailer
    {
        void Send(string to, string subject, string body, bool isHtml);

        void Send(string to, string subject, string body, bool isHtml, IEnumerable<Attachment> attachments);
    }

    /// <summary>
    /// Sends through system.net/mailSettings (SMTP relay in production, a pickup folder in test environments).
    /// </summary>
    public class SmtpMailer : IMailer
    {
        public void Send(string to, string subject, string body, bool isHtml)
        {
            Send(to, subject, body, isHtml, null);
        }

        public void Send(string to, string subject, string body, bool isHtml, IEnumerable<Attachment> attachments)
        {
            using (var message = new MailMessage())
            {
                message.From = new MailAddress(ClinicSettings.Get("MailFrom", "no-reply@southridgemedical.example"), ClinicSettings.Get("MailFromName", "Southridge Medical Group"));
                message.To.Add(to);
                message.Subject = subject;
                message.Body = body;
                message.IsBodyHtml = isHtml;
                message.BodyEncoding = Encoding.UTF8;
                if (attachments != null)
                {
                    foreach (Attachment a in attachments)
                    {
                        message.Attachments.Add(a);
                    }
                }

                using (var client = new SmtpClient())
                {
                    // SmtpClient only accepts an absolute pickup folder; the shipped configs use one relative to the application.
                    if (client.DeliveryMethod == SmtpDeliveryMethod.SpecifiedPickupDirectory && !string.IsNullOrEmpty(client.PickupDirectoryLocation)
                        && !Path.IsPathRooted(client.PickupDirectoryLocation))
                    {
                        client.PickupDirectoryLocation = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, client.PickupDirectoryLocation));
                        Directory.CreateDirectory(client.PickupDirectoryLocation);
                    }

                    client.Send(message);
                }
            }
        }
    }
}
