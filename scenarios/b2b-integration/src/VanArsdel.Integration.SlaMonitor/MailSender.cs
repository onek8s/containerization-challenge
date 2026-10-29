using System.Net.Mail;
using System.Text;
using VanArsdel.Integration.Monitoring;

namespace VanArsdel.Integration.SlaMonitor
{
    public interface IMailSender
    {
        void Send(AlertMail mail);
    }

    /// <summary>SMTP through the system.net/mailSettings section (relay smtp.corp.local, anonymous from the BizTalk VLAN).</summary>
    public class SmtpMailSender : IMailSender
    {
        public void Send(AlertMail mail)
        {
            using (var message = new MailMessage())
            using (var client = new SmtpClient())
            {
                foreach (var to in mail.To.Split(new[] { ';', ',' }, System.StringSplitOptions.RemoveEmptyEntries))
                {
                    message.To.Add(to.Trim());
                }

                message.Subject = mail.Subject;
                message.SubjectEncoding = Encoding.UTF8;
                message.Body = mail.TextBody;
                message.BodyEncoding = Encoding.UTF8;
                message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(mail.HtmlBody, Encoding.UTF8, "text/html"));
                message.Headers.Add("X-VanArsdel-Source", "B2B SLA Monitor");
                client.Send(message);
            }
        }
    }
}
