using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HandlebarsDotNet;
using Litware.OrderHub.Integration.Configuration;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Litware.OrderHub.Integration.Mail
{
    /// <summary>
    /// Customer e-mails rendered from Handlebars templates (&lt;name&gt;.&lt;lang&gt;.hbs; the first line is the subject)
    /// and sent through the company SMTP relay.
    /// </summary>
    public class CustomerMailer
    {
        private readonly string _templateFolder;
        private readonly MailSettings _settings;
        private readonly ConcurrentDictionary<string, Func<object, string>> _templates = new ConcurrentDictionary<string, Func<object, string>>();

        public CustomerMailer(string templateFolder, MailSettings settings)
        {
            _templateFolder = templateFolder;
            _settings = settings;
        }

        public static string LanguageFor(string countryCode)
        {
            switch ((countryCode ?? "").ToUpperInvariant())
            {
                case "FI": return "fi";
                case "SE": return "sv";
                default: return "en";
            }
        }

        public MimeMessage Render(string template, string language, string to, object model)
        {
            var render = _templates.GetOrAdd(template + "." + language, key =>
            {
                string path = Path.Combine(_templateFolder, key + ".hbs");
                if (!File.Exists(path)) path = Path.Combine(_templateFolder, template + ".en.hbs");
                return Handlebars.Compile(File.ReadAllText(path));
            });

            string text = render(model).Replace("\r\n", "\n");
            int newline = text.IndexOf('\n');
            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(_settings.From));
            message.To.Add(MailboxAddress.Parse(to));
            if (!string.IsNullOrEmpty(_settings.Bcc)) message.Bcc.Add(MailboxAddress.Parse(_settings.Bcc));
            message.Subject = text.Substring(0, newline).Trim();
            message.Body = new TextPart("html") { Text = text.Substring(newline + 1) };
            return message;
        }

        public async Task SendAsync(MimeMessage message, CancellationToken ct)
        {
            using (var client = new SmtpClient())
            {
                await client.ConnectAsync(_settings.Host, _settings.Port, _settings.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None, ct);
                if (!string.IsNullOrEmpty(_settings.Username))
                {
                    await client.AuthenticateAsync(_settings.Username, _settings.Password, ct);
                }
                await client.SendAsync(message, ct);
                await client.DisconnectAsync(true, ct);
            }
        }
    }

    public class MailSettings
    {
        public string Host { get; set; }
        public int Port { get; set; } = 25;
        public bool UseStartTls { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string From { get; set; }
        public string Bcc { get; set; }

        public static MailSettings Read(SettingsReader settings)
        {
            return new MailSettings
            {
                Host = settings.Get("Mail.SmtpHost", "smtp.corp.local"),
                Port = settings.GetInt("Mail.SmtpPort", 25),
                UseStartTls = settings.GetBool("Mail.StartTls", false),
                Username = settings.Get("Mail.Username"),
                Password = settings.Get("Mail.Password"),
                From = settings.Get("Mail.From", "Litware Outdoor <noreply@litware-outdoor.example>"),
                Bcc = settings.Get("Mail.Bcc")
            };
        }
    }
}
