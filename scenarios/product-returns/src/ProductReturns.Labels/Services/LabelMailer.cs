using System;
using System.IO;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ProductReturns.Core;

namespace ProductReturns.Labels.Services
{
    /// <summary>Sends the return label to the customer through the internal SMTP relay.</summary>
    public class LabelMailer
    {
        private readonly MailOptions _options;
        private readonly IHostEnvironment _environment;

        public LabelMailer(IOptions<MailOptions> options, IHostEnvironment environment)
        {
            _options = options.Value;
            _environment = environment;
        }

        public async Task SendLabelAsync(ReturnRequest request, string trackingNumber, byte[] pdf)
        {
            var body = LoadTemplate(_options.LabelTemplate)
                .Replace("{{CustomerName}}", Html(request.CustomerName))
                .Replace("{{RmaNumber}}", Html(request.RmaNumber))
                .Replace("{{OrderNumber}}", Html(request.OrderNumber))
                .Replace("{{TrackingNumber}}", Html(trackingNumber));

            using (var message = new MailMessage())
            {
                message.From = new MailAddress(_options.From, _options.FromName);
                message.To.Add(new MailAddress(request.CustomerEmail, request.CustomerName));
                if (!string.IsNullOrEmpty(_options.Bcc))
                {
                    message.Bcc.Add(_options.Bcc);
                }

                message.Subject = "Your return label for " + request.RmaNumber;
                message.SubjectEncoding = Encoding.UTF8;
                message.Body = body;
                message.BodyEncoding = Encoding.UTF8;
                message.IsBodyHtml = true;
                message.Attachments.Add(new Attachment(new MemoryStream(pdf), request.RmaNumber + ".pdf", MediaTypeNames.Application.Pdf));

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

            return new SmtpClient(_options.SmtpHost, _options.SmtpPort)
            {
                UseDefaultCredentials = _options.UseDefaultCredentials,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };
        }

        private string LoadTemplate(string name)
        {
            var folder = Path.IsPathRooted(_options.TemplateFolder)
                ? _options.TemplateFolder
                : Path.Combine(_environment.ContentRootPath, _options.TemplateFolder);
            return File.ReadAllText(Path.Combine(folder, name));
        }

        private static string Html(string value) => System.Net.WebUtility.HtmlEncode(value ?? string.Empty);
    }
}
