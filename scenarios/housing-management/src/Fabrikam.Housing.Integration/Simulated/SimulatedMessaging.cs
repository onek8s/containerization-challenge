using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Fabrikam.Housing.Communications;
using Fabrikam.Housing.Integration.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fabrikam.Housing.Integration.Simulated
{
    public class SimulatedSmsSender : ISmsSender
    {
        private readonly string _folder;
        private readonly ILogger<SimulatedSmsSender> _logger;

        public SimulatedSmsSender(IOptions<IntegrationOptions> options, ILogger<SimulatedSmsSender> logger)
        {
            _folder = SimulatorFolder.Ensure(options.Value.SimulatorFolder, "sms");
            _logger = logger;
        }

        public Task<string> SendAsync(string mobileNumber, string text)
        {
            var id = "SIM-" + SimulatorFolder.Stamp() + "-" + System.Guid.NewGuid().ToString("N").Substring(0, 6);
            var to = TextlineSmsSender.ToInternational(mobileNumber);
            File.WriteAllText(Path.Combine(_folder, id + ".txt"), "To: " + to + "\r\n\r\n" + SmsTemplates.Truncate(text));
            _logger.LogInformation("Simulated SMS {Id} to {To}", id, to);
            return Task.FromResult(id);
        }
    }

    public class SimulatedEmailSender : IEmailSender
    {
        private readonly string _folder;
        private readonly string _from;
        private readonly ILogger<SimulatedEmailSender> _logger;

        public SimulatedEmailSender(IOptions<IntegrationOptions> options, ILogger<SimulatedEmailSender> logger)
        {
            _folder = SimulatorFolder.Ensure(options.Value.SimulatorFolder, "mail");
            _from = options.Value.Smtp.From;
            _logger = logger;
        }

        public async Task SendAsync(string to, string subject, string htmlBody, IEnumerable<EmailAttachment> attachments = null)
        {
            var message = SmtpEmailSender.Build(_from, to, subject, htmlBody, attachments);
            var path = Path.Combine(_folder, SimulatorFolder.Stamp() + "-" + SimulatorFolder.Safe(to) + ".eml");
            using (var stream = File.Create(path))
                await message.WriteToAsync(stream);
            _logger.LogInformation("Simulated mail '{Subject}' to {To} written to {Path}", subject, to, path);
        }
    }
}
