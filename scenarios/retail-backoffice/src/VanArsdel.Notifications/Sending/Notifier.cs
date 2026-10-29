using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VanArsdel.Notifications.Data;

namespace VanArsdel.Notifications.Sending;

/// <summary>Sends each notification at most once per (template, reference, channel, recipient).</summary>
public class Notifier
{
    private readonly NotificationsDbContext _db;
    private readonly IEmailSender _email;
    private readonly ISmsSender _sms;
    private readonly TemplateRenderer _templates;
    private readonly NotificationOptions _options;
    private readonly ILogger<Notifier> _logger;

    public Notifier(NotificationsDbContext db, IEmailSender email, ISmsSender sms, TemplateRenderer templates, IOptions<NotificationOptions> options, ILogger<Notifier> logger)
    {
        _db = db;
        _email = email;
        _sms = sms;
        _templates = templates;
        _options = options.Value;
        _logger = logger;
    }

    public async Task EmailAsync(string template, string reference, string to, string market, Dictionary<string, object> values, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(to)) return;
        var key = Key(template, reference, "Email", to);
        if (await AlreadySentAsync(key, cancellationToken)) return;

        values["WebShopUrl"] = _options.WebShopUrl;
        var mail = _templates.Render(template, TemplateRenderer.LanguageFor(market), values);
        var result = await _email.SendAsync(to, mail.Subject, mail.Html, template, cancellationToken);
        await RecordAsync(key, template, "Email", reference, to, result, cancellationToken);
    }

    public async Task SmsAsync(string template, string reference, string to, string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(to)) return;
        var key = Key(template, reference, "Sms", to);
        if (await AlreadySentAsync(key, cancellationToken)) return;
        var result = await _sms.SendAsync(NormalizePhone(to), text, cancellationToken);
        await RecordAsync(key, template, "Sms", reference, to, result, cancellationToken);
    }

    public static string Hash(string recipient)
    {
        using var sha = SHA256.Create();
        return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(recipient.Trim().ToLowerInvariant())));
    }

    /// <summary>Finnish numbers are often typed as 040 123 4567.</summary>
    public static string NormalizePhone(string phone)
    {
        var digits = new string(phone.Where(c => char.IsDigit(c) || c == '+').ToArray());
        if (digits.StartsWith("00")) return "+" + digits.Substring(2);
        if (digits.StartsWith("0")) return "+358" + digits.Substring(1);
        return digits;
    }

    private static string Key(string template, string reference, string channel, string to) =>
        $"{template}:{reference}:{channel}:{Hash(to).Substring(0, 16)}";

    private Task<bool> AlreadySentAsync(string key, CancellationToken cancellationToken) =>
        _db.Sent.AnyAsync(s => s.DedupeKey == key && s.Status == "Sent", cancellationToken);

    private async Task RecordAsync(string key, string template, string channel, string reference, string to, SendResult result, CancellationToken cancellationToken)
    {
        _db.Sent.Add(new SentNotification
        {
            DedupeKey = key, Template = template, Channel = channel, Reference = reference, RecipientHash = Hash(to),
            Provider = result.Provider, ProviderMessageId = result.MessageId, Status = "Sent", SentUtc = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("{Channel} {Template} for {Reference} sent via {Provider}", channel, template, reference, result.Provider);
    }
}
