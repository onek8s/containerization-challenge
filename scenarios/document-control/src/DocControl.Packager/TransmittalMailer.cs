using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using Litware.DocControl.Data;
using Litware.DocControl.Packaging;
using Litware.DocControl.Platform;
using Litware.DocControl.Transmittals;
using Microsoft.Extensions.Options;

namespace Litware.DocControl.Packager;

public sealed class TransmittalMailer
{
    private readonly PackagerOptions _options;
    private readonly ILogger<TransmittalMailer> _log;
    private readonly Lazy<string> _template;

    public TransmittalMailer(IOptions<PackagerOptions> options, ILogger<TransmittalMailer> log)
    {
        _options = options.Value;
        _log = log;
        _template = new Lazy<string>(() =>
            File.ReadAllText(Path.Combine(InstallLocation.InstallPath("Packager"), "Templates", "transmittal-issued.html")));
    }

    public void SendIssued(TransmittalRecord t, BuiltPackage package)
    {
        var attachPackage = package.Size <= _options.Mail.MaxAttachmentBytes;
        var rows = string.Concat(package.Files.Select(f =>
            $"<tr><td>{WebUtility.HtmlEncode(f.DocumentNumber)}</td><td>{f.Revision}</td><td>{WebUtility.HtmlEncode(f.Title)}</td></tr>"));

        var body = _template.Value
            .Replace("{{Number}}", t.Number)
            .Replace("{{Purpose}}", WebUtility.HtmlEncode(t.Purpose.Description()))
            .Replace("{{Recipient}}", WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(t.RecipientName) ? t.RecipientCompany : t.RecipientName))
            .Replace("{{Rows}}", rows)
            .Replace("{{Notes}}", WebUtility.HtmlEncode(t.Notes ?? ""))
            .Replace("{{Delivery}}", attachPackage
                ? "The documents are attached as a zip package."
                : $"The package ({CoverSheet.FormatSize(package.Size)}) is too large for e-mail; it will be shared through the project file exchange by {WebUtility.HtmlEncode(t.CreatedBy)}.")
            .Replace("{{Sender}}", WebUtility.HtmlEncode(t.CreatedBy));

        using var message = new MailMessage
        {
            From = new MailAddress(_options.Mail.From, _options.Mail.FromName),
            Subject = $"{t.Number} {t.Purpose.Code()} {t.ProjectCode}",
            Body = body,
            IsBodyHtml = true,
        };
        message.To.Add(new MailAddress(t.RecipientEmail, t.RecipientName));
        if (MailAddress.TryCreate(t.CreatedByEmail, out var creator))
        {
            message.CC.Add(creator);
            message.ReplyToList.Add(creator);
        }

        message.Attachments.Add(new Attachment(new MemoryStream(package.CoverSheet), t.Number + ".pdf", MediaTypeNames.Application.Pdf));
        if (attachPackage)
            message.Attachments.Add(new Attachment(package.FullPath, MediaTypeNames.Application.Zip) { Name = t.Number + ".zip" });

        Send(message);
        _log.LogInformation("{Transmittal}: mailed to {Recipient}", t.Number, t.RecipientEmail);
    }

    public void SendFailed(TransmittalRecord t, string error)
    {
        if (!MailAddress.TryCreate(t.CreatedByEmail, out var creator)) return;
        using var message = new MailMessage
        {
            From = new MailAddress(_options.Mail.From, _options.Mail.FromName),
            Subject = $"{t.Number} could not be issued",
            Body = $"Transmittal {t.Number} failed after {t.Attempts} attempts:\r\n\r\n{error}\r\n\r\n{_options.PortalUrl.TrimEnd('/')}/Transmittals/Details/{t.Id}",
        };
        message.To.Add(creator);
        Send(message);
    }

    private void Send(MailMessage message)
    {
        using var client = new SmtpClient(_options.Mail.Host, _options.Mail.Port) { EnableSsl = _options.Mail.EnableSsl };
        if (!string.IsNullOrWhiteSpace(_options.Mail.PickupDirectory))
        {
            Directory.CreateDirectory(_options.Mail.PickupDirectory);
            client.DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory;
            client.PickupDirectoryLocation = Path.GetFullPath(_options.Mail.PickupDirectory);
        }
        else
        {
            client.UseDefaultCredentials = _options.Mail.UseDefaultCredentials;
        }
        client.Send(message);
    }
}
