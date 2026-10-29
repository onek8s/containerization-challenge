using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Mail;
using Tailspin.CustomerCare.Data;

namespace Tailspin.CustomerCare.Web.Services
{
    /// <summary>Sends e-mail through the relay configured in system.net/mailSettings.</summary>
    public static class Mailer
    {
        public static void Send(IEnumerable<string> to, string subject, string htmlBody)
        {
            var recipients = (to ?? Enumerable.Empty<string>()).Where(a => !string.IsNullOrWhiteSpace(a)).Distinct().ToList();
            if (recipients.Count == 0)
            {
                Trace.TraceWarning("Mail '{0}' has no recipients and was not sent.", subject);
                return;
            }

            using (var message = new MailMessage { From = new MailAddress(CareSettings.MailFrom, "Customer Care"), Subject = subject, Body = htmlBody, IsBodyHtml = true })
            using (var client = new SmtpClient())
            {
                foreach (var r in recipients) message.To.Add(r);
                client.Send(message);
            }
        }
    }
}
