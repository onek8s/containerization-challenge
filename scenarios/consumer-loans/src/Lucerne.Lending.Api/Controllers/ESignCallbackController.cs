using System.Security.Cryptography;
using System.Text;
using Lucerne.Lending.Contracts;
using Lucerne.Lending.Data;
using Lucerne.Lending.Integration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Lucerne.Lending.Api.Controllers;

/// <summary>Webhook of the e-signature provider; processed asynchronously by the documents service.</summary>
[ApiController]
[Route("api/esign/callback")]
[AllowAnonymous]
public sealed class ESignCallbackController : ControllerBase
{
    private readonly OutboxRepository _outbox;
    private readonly ESignOptions _options;
    private readonly ILogger<ESignCallbackController> _log;

    public ESignCallbackController(OutboxRepository outbox, IOptions<ESignOptions> options, ILogger<ESignCallbackController> log)
    {
        _outbox = outbox;
        _options = options.Value;
        _log = log;
    }

    [HttpPost]
    public async Task<IActionResult> Post(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        var signature = Request.Headers["X-SignHub-Signature"].ToString();
        var expected = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(_options.CallbackSecret), Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(signature.ToLowerInvariant()), Encoding.ASCII.GetBytes(expected)))
        {
            _log.LogWarning("Rejected e-sign callback with invalid signature");
            return Unauthorized();
        }
        var callback = System.Text.Json.JsonSerializer.Deserialize<SigningCallback>(body, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        if (callback == null || string.IsNullOrEmpty(callback.OrderId)) return BadRequest();
        await _outbox.EnqueueAsync(Topics.SigningCallback, callback);
        return Ok();
    }
}
