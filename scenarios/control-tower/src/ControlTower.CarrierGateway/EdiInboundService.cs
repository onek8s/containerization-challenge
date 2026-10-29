using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Proseware.ControlTower.Data;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Domain.Parties;
using Proseware.ControlTower.Integration.Carriers;
using Proseware.ControlTower.Integration.Edifact;
using Proseware.ControlTower.Integration.Messaging;
using Proseware.ControlTower.Integration.Sftp;

namespace Proseware.ControlTower.CarrierGateway;

/// <summary>Polls the carriers' SFTP accounts for IFTSTA status reports and CONTRL acknowledgements.</summary>
public sealed class EdiInboundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IPartnerFileChannelFactory _channels;
    private readonly IEventBus _bus;
    private readonly ControlReferences _references;
    private readonly IftminBuilder _iftmin;
    private readonly CarrierIntegrationOptions _carrierOptions;
    private readonly GatewayOptions _options;
    private readonly IClock _clock;
    private readonly ILogger<EdiInboundService> _logger;

    public EdiInboundService(
        IServiceScopeFactory scopes,
        IPartnerFileChannelFactory channels,
        IEventBus bus,
        ControlReferences references,
        IftminBuilder iftmin,
        IOptions<CarrierIntegrationOptions> carrierOptions,
        IOptions<GatewayOptions> options,
        IClock clock,
        ILogger<EdiInboundService> logger)
    {
        _scopes = scopes;
        _channels = channels;
        _bus = bus;
        _references = references;
        _iftmin = iftmin;
        _carrierOptions = carrierOptions.Value;
        _options = options.Value;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EdiEnabled)
        {
            _logger.LogInformation("EDI polling is disabled");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.EdiPollSeconds));
        do
        {
            try
            {
                await PollAllAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "EDI poll failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    internal async Task PollAllAsync(CancellationToken ct)
    {
        List<Carrier> carriers;
        using (var scope = _scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ControlTowerDbContext>();
            carriers = await db.Carriers.AsNoTracking()
                .Where(c => c.Active && c.Integration == CarrierIntegration.EdifactSftp)
                .ToListAsync(ct);
        }

        foreach (var carrier in carriers)
        {
            if (!_carrierOptions.Sftp.TryGetValue(carrier.Code, out var sftp)) continue;
            try
            {
                await PollCarrierAsync(carrier, sftp, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Polling {Carrier} at {Host} failed", carrier.Code, sftp.LocalFolder ?? sftp.Host);
            }
        }
    }

    private async Task PollCarrierAsync(Carrier carrier, SftpOptions sftp, CancellationToken ct)
    {
        using var channel = _channels.Open(sftp);
        var files = channel.ListInbound();
        if (files.Count == 0) return;
        _logger.LogInformation("{Count} files waiting from {Carrier}", files.Count, carrier.Code);

        _carrierOptions.StatusCodes.TryGetValue(carrier.Code, out var overrides);
        var parser = new IftstaParser(overrides);
        var inboundFolder = _options.Folder("inbound", carrier.Code);

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            var bytes = channel.Download(file);
            await File.WriteAllBytesAsync(Path.Combine(inboundFolder, file.Name), bytes, ct);
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ControlTowerDbContext>();
            var log = new EdiInterchange
            {
                CarrierId = carrier.Id,
                Direction = InterchangeDirection.Inbound,
                FileName = file.Name,
                CreatedAt = _clock.UtcNow,
                Status = InterchangeStatus.Received
            };
            db.EdiInterchanges.Add(log);

            EdifactInterchange? interchange = null;
            var results = new List<(EdifactMessage, string?)>();
            try
            {
                var text = Decode(bytes);
                log.Payload = text;
                interchange = EdifactReader.Parse(text);
                log.ControlReference = interchange.ControlReference;
                log.MessageCount = interchange.Messages.Count;
                log.MessageType = interchange.Messages.FirstOrDefault()?.Type ?? "";

                if (!string.Equals(interchange.SenderId, carrier.EdiPartnerId, StringComparison.OrdinalIgnoreCase))
                    throw new EdifactException($"Sender {interchange.SenderId} is not the partner id of {carrier.Code}.");

                var duplicate = await db.EdiInterchanges.AnyAsync(e => e.CarrierId == carrier.Id
                    && e.Direction == InterchangeDirection.Inbound
                    && e.ControlReference == interchange.ControlReference
                    && e.Status == InterchangeStatus.Processed, ct);
                if (duplicate)
                {
                    log.Status = InterchangeStatus.Processed;
                    log.Error = "Duplicate interchange, ignored";
                    _logger.LogWarning("Duplicate interchange {ControlRef} from {Carrier} ignored", interchange.ControlReference, carrier.Code);
                }
                else
                {
                    foreach (var message in interchange.Messages)
                    {
                        try
                        {
                            await HandleMessageAsync(db, carrier, parser, message, ct);
                            results.Add((message, null));
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Message {Ref} in {File} from {Carrier} failed", message.ReferenceNumber, file.Name, carrier.Code);
                            results.Add((message, ex.Message));
                        }
                    }
                    var failed = results.Count(r => r.Item2 is not null);
                    log.Status = failed == 0 ? InterchangeStatus.Processed : failed == results.Count ? InterchangeStatus.Failed : InterchangeStatus.PartiallyProcessed;
                    log.Error = failed == 0 ? null : string.Join("; ", results.Where(r => r.Item2 is not null).Select(r => $"{r.Item1.ReferenceNumber}: {r.Item2}"));
                }
            }
            catch (Exception ex)
            {
                log.Status = InterchangeStatus.Failed;
                log.Error = ex.Message;
                _logger.LogError(ex, "Interchange {File} from {Carrier} rejected", file.Name, carrier.Code);
            }
            log.ProcessedAt = _clock.UtcNow;
            await db.SaveChangesAsync(ct);

            if (interchange is not null && log.MessageType != "CONTRL")
                await SendContrlAsync(channel, carrier, interchange, results, ct);
            channel.Archive(file);
        }
    }

    private async Task HandleMessageAsync(ControlTowerDbContext db, Carrier carrier, IftstaParser parser, EdifactMessage message, CancellationToken ct)
    {
        switch (message.Type)
        {
            case IftstaParser.MessageType:
                var statuses = parser.Read(message);
                foreach (var status in statuses)
                {
                    if (status.MilestoneCode is null)
                        _logger.LogWarning("Unmapped status code {Code} from {Carrier} for {Ref}", status.CarrierStatusCode, carrier.Code, status.ShipmentNumber ?? status.CarrierReference);
                    await _bus.PublishAsync(IftstaParser.ToEvent(status, carrier.EdiPartnerId ?? carrier.Code, _clock.UtcNow), ct);
                }
                break;
            case "CONTRL":
                var (reference, accepted) = ContrlBuilder.Read(message);
                var sent = await db.EdiInterchanges.FirstOrDefaultAsync(e => e.CarrierId == carrier.Id
                    && e.Direction == InterchangeDirection.Outbound && e.ControlReference == reference, ct);
                if (sent is null)
                {
                    _logger.LogWarning("CONTRL from {Carrier} refers to unknown interchange {ControlRef}", carrier.Code, reference);
                    break;
                }
                sent.Status = accepted ? InterchangeStatus.Acknowledged : InterchangeStatus.Failed;
                if (!accepted) sent.Error = "Rejected by partner (CONTRL)";
                break;
            case "APERAK":
                // application error report: keep the text for the EDI desk
                var text = string.Join(" ", message.All("FTX").Select(f => string.Join(" ", f[3].Components)));
                _logger.LogWarning("APERAK from {Carrier}: {Text}", carrier.Code, text);
                break;
            default:
                throw new EdifactException($"Message type {message.Type} is not expected from {carrier.Code}.");
        }
    }

    private async Task SendContrlAsync(IPartnerFileChannel channel, Carrier carrier, EdifactInterchange received, List<(EdifactMessage, string?)> results, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var controlRef = _references.Next(now);
        var ack = new EdifactInterchange
        {
            SyntaxIdentifier = received.SyntaxIdentifier,
            SyntaxVersion = received.SyntaxVersion,
            SenderId = _iftmin.OurPartyId,
            SenderQualifier = _iftmin.OurPartyQualifier,
            RecipientId = received.SenderId,
            RecipientQualifier = received.SenderQualifier,
            Prepared = now.UtcDateTime,
            ControlReference = controlRef
        };
        ack.Messages.Add(ContrlBuilder.Acknowledge(received, "1", results));
        var text = EdifactWriter.Write(ack);
        var fileName = ControlReferences.FileName(carrier.EdiPartnerId ?? carrier.Code, "CONTRL", controlRef);
        var bytes = EdifactInterchange.EncodingFor(ack.SyntaxIdentifier).GetBytes(text);
        await File.WriteAllBytesAsync(Path.Combine(_options.Folder("outbound", carrier.Code), fileName), bytes, ct);
        channel.Upload(fileName, bytes);
    }

    /// <summary>Partners send UNOC (Latin-1); a few send UTF-8 with a BOM.</summary>
    private static string Decode(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        var latin = Encoding.Latin1.GetString(bytes);
        var syntax = latin.IndexOf("UNB+", StringComparison.Ordinal) is var i and >= 0 && latin.Length > i + 8 ? latin.Substring(i + 4, 4) : "UNOC";
        return syntax is "UNOW" or "UNOY" ? Encoding.UTF8.GetString(bytes) : latin;
    }
}
