using Microsoft.Extensions.Options;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Integration.Carriers;
using Proseware.ControlTower.Integration.Messaging;

namespace Proseware.ControlTower.CarrierGateway;

public static class WebhookEndpoints
{
    public static void MapWebhooks(this WebApplication app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "carrier-gateway" }));

        app.MapPost("/webhooks/parcelnord", async (HttpRequest request, IEventBus bus, IOptions<CarrierIntegrationOptions> options, IClock clock, ILoggerFactory loggers, CancellationToken ct) =>
        {
            var logger = loggers.CreateLogger("Webhooks.ParcelNord");
            using var reader = new StreamReader(request.Body);
            var body = await reader.ReadToEndAsync();
            var secret = options.Value.ApiFor("PRCL").WebhookSecret ?? "";
            var signature = request.Headers["X-ParcelNord-Signature"].ToString();
            if (!ParcelNordClient.VerifySignature(signature, body, secret, clock.UtcNow, TimeSpan.FromMinutes(5)))
            {
                logger.LogWarning("Rejected webhook with invalid signature from {Remote}", request.HttpContext.Connection.RemoteIpAddress);
                return Results.Unauthorized();
            }

            var status = ParcelNordClient.ParseWebhook(body);
            if (status is null)
            {
                logger.LogInformation("Webhook event ignored: {Body}", body.Length > 300 ? body[..300] : body);
                return Results.Accepted();
            }
            await bus.PublishAsync(status, ct);
            return Results.Accepted();
        });
    }
}
