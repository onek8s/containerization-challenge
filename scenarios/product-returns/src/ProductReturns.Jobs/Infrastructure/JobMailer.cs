using System.IO;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ProductReturns.Jobs.Infrastructure
{
    public class JobMailer
    {
        private readonly JobMailOptions _options;
        private readonly IHostEnvironment _environment;

        public JobMailer(IOptions<JobMailOptions> options, IHostEnvironment environment)
        {
            _options = options.Value;
            _environment = environment;
        }

        public JobMailOptions Options => _options;

        public string Template(string name) =>
            File.ReadAllText(Path.Combine(_environment.ContentRootPath, "Templates", name));

        public async Task SendAsync(string to, string toName, string subject, string html, string attachmentPath = null)
        {
            using (var message = new MailMessage())
            {
                message.From = new MailAddress(_options.From, _options.FromName);
                message.To.Add(new MailAddress(to, toName));
                message.Subject = subject;
                message.SubjectEncoding = Encoding.UTF8;
                message.Body = html;
                message.BodyEncoding = Encoding.UTF8;
                message.IsBodyHtml = true;
                if (attachmentPath != null)
                {
                    message.Attachments.Add(new Attachment(attachmentPath, MediaTypeNames.Text.Plain));
                }

                using (var client = CreateClient())
                {
                    await client.SendMailAsync(message);
                }
            }
        }

        private SmtpClient CreateClient()
        {
            if (!string.IsNullOrEmpty(_options.PickupDirectory))
            {
                Directory.CreateDirectory(_options.PickupDirectory);
                return new SmtpClient
                {
                    DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory,
                    PickupDirectoryLocation = Path.GetFullPath(_options.PickupDirectory)
                };
            }

            return new SmtpClient(_options.SmtpHost, _options.SmtpPort) { UseDefaultCredentials = _options.UseDefaultCredentials };
        }
    }
}
