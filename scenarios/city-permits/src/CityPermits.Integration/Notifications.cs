using System.Net.Mail;

namespace CityPermits.Integration;

public sealed class MailOptions
{
    /// <summary>Smtp or Pickup (writes .eml files).</summary>
    public string Mode { get; set; } = "Smtp";
    public string Host { get; set; } = "smtp.corp.local";
    public int Port { get; set; } = 25;
    public string From { get; set; } = "Ashford Permits <permits@ashford.gov.uk>";
    public string PickupFolder { get; set; } = "App_Data/mail";
}

public sealed class Mailer
{
    private readonly MailOptions _options;

    public Mailer(MailOptions options) => _options = options;

    public void Send(string to, string subject, string body, IEnumerable<(string Name, byte[] Content)>? attachments = null)
    {
        using var msg = new MailMessage(_options.From, to, subject, body);
        var streams = new List<MemoryStream>();
        foreach (var (name, content) in attachments ?? Enumerable.Empty<(string, byte[])>())
        {
            var ms = new MemoryStream(content);
            streams.Add(ms);
            msg.Attachments.Add(new Attachment(ms, name));
        }
        using var client = new SmtpClient(_options.Host, _options.Port);
        if (_options.Mode == "Pickup")
        {
            var dir = Path.GetFullPath(_options.PickupFolder);
            Directory.CreateDirectory(dir);
            client.DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory;
            client.PickupDirectoryLocation = dir;
        }
        client.Send(msg);
        streams.ForEach(s => s.Dispose());
    }
}
