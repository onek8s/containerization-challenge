using System.Collections.Concurrent;
using VanArsdel.Integration.Messaging;

namespace VanArsdel.LabStubs.Stubs;

/// <summary>Mail and SMS sink in place of Postiq. Messages are listed at /postiq/messages.</summary>
public class PostiqStub
{
    private readonly ConcurrentQueue<object> _messages = new();
    private readonly ILogger<PostiqStub> _logger;
    private int _id;

    public PostiqStub(ILogger<PostiqStub> logger)
    {
        _logger = logger;
    }

    public void Map(WebApplication app)
    {
        app.MapPost("/postiq/v3/mail/send", (EmailSendRequest r) =>
        {
            var id = "m" + Interlocked.Increment(ref _id);
            _messages.Enqueue(new { id, channel = "email", to = string.Join(",", r.To.Select(t => t.Email)), subject = r.Subject, tags = r.Tags, html = r.Html, at = DateTime.UtcNow });
            _logger.LogInformation("Mail {Id} to {To}: {Subject}", id, string.Join(",", r.To.Select(t => t.Email)), r.Subject);
            return Results.Json(new EmailSendResponse { MessageId = id, Status = "queued" });
        });
        app.MapPost("/postiq/v1/sms", (SmsSendRequest r) =>
        {
            var id = "s" + Interlocked.Increment(ref _id);
            _messages.Enqueue(new { id, channel = "sms", to = r.To, subject = r.Text, at = DateTime.UtcNow });
            _logger.LogInformation("SMS {Id} to {To}: {Text}", id, r.To, r.Text);
            return Results.Json(new SmsSendResponse { Id = id, Status = "sent", Parts = 1 + r.Text.Length / 160 });
        });
        app.MapGet("/postiq/messages", () => Results.Json(_messages.ToArray()));
    }
}
