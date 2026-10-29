using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;

namespace StaffOnboarding.Workflows.Services
{
    /// <summary>
    /// Mail templates are maintained by HR on the onboarding share (Templates\&lt;name&gt;.txt). First line is
    /// "Subject: ...", the rest is the body. Placeholders look like {RequestNumber}.
    /// </summary>
    public class MailNotifier : INotifier
    {
        private static readonly Regex Placeholder = new Regex(@"\{([A-Za-z]+)\}", RegexOptions.CultureInvariant);

        private readonly WorkflowSettings _settings;
        private readonly IProvisioningClient _directory;

        public MailNotifier(WorkflowSettings settings, IProvisioningClient directory)
        {
            _settings = settings;
            _directory = directory;
        }

        public void Send(string template, IEnumerable<string> recipients, IDictionary<string, string> values)
        {
            var addresses = recipients
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(Resolve)
                .Where(a => a != null)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (addresses.Count == 0) return;

            string subject, body;
            Render(template, values, out subject, out body);

            using (var message = new MailMessage())
            using (var smtp = new SmtpClient(_settings.SmtpHost, _settings.SmtpPort))
            {
                message.From = new MailAddress(_settings.MailFrom, "Staff Onboarding");
                foreach (var a in addresses) message.To.Add(a);
                message.Subject = subject;
                message.Body = body;
                message.BodyEncoding = Encoding.UTF8;
                smtp.UseDefaultCredentials = true;
                smtp.Send(message);
            }
        }

        public string WriteWelcomePack(string requestNumber, IDictionary<string, string> values)
        {
            var folder = Path.Combine(_settings.OnboardingShare, "Packs",
                DateTime.Today.Year.ToString(CultureInfo.InvariantCulture), requestNumber);
            Directory.CreateDirectory(folder);

            string subject, body;
            Render("welcome-pack", values, out subject, out body);
            File.WriteAllText(Path.Combine(folder, "welcome.txt"), subject + Environment.NewLine + Environment.NewLine + body, Encoding.UTF8);

            var checklist = Path.Combine(_settings.OnboardingShare, "Templates", "first-day-checklist.docx");
            if (File.Exists(checklist))
                File.Copy(checklist, Path.Combine(folder, "first-day-checklist.docx"), true);

            return folder;
        }

        private void Render(string template, IDictionary<string, string> values, out string subject, out string body)
        {
            var path = Path.Combine(_settings.OnboardingShare, "Templates", template + ".txt");
            var lines = File.ReadAllLines(path, Encoding.UTF8);
            if (lines.Length == 0 || !lines[0].StartsWith("Subject:", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Template " + path + " must start with a Subject: line.");

            Func<string, string> fill = text => Placeholder.Replace(text, m =>
            {
                string v;
                return values != null && values.TryGetValue(m.Groups[1].Value, out v) ? v : m.Value;
            });

            subject = fill(lines[0].Substring("Subject:".Length).Trim());
            body = fill(string.Join(Environment.NewLine, lines.Skip(1)).Trim());
        }

        private string Resolve(string recipient)
        {
            if (recipient.StartsWith("group:", StringComparison.OrdinalIgnoreCase))
                return recipient.Substring(6) + "@" + _settings.MailDomain;
            if (recipient.Contains("@"))
                return recipient;

            var account = _directory.GetAccountAsync(recipient).GetAwaiter().GetResult();
            return account == null || string.IsNullOrEmpty(account.Mail) ? null : account.Mail;
        }
    }
}
