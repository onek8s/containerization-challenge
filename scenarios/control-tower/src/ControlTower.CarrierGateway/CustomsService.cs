using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Proseware.ControlTower.Data;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Domain.Customs;
using Proseware.ControlTower.Domain.Events;
using Proseware.ControlTower.Integration.Customs;
using Proseware.ControlTower.Integration.Messaging;

namespace Proseware.ControlTower.CarrierGateway;

/// <summary>Submits customs declarations and follows them until release.</summary>
public sealed class CustomsService : BackgroundService
{
    public const string SubscriptionId = "gateway-customs";

    private readonly IServiceScopeFactory _scopes;
    private readonly ICustomsGateway _customs;
    private readonly IEventBus _bus;
    private readonly CustomsOptions _customsOptions;
    private readonly GatewayOptions _options;
    private readonly IClock _clock;
    private readonly ILogger<CustomsService> _logger;
    private IDisposable? _subscription;

    public CustomsService(IServiceScopeFactory scopes, ICustomsGateway customs, IEventBus bus, IOptions<CustomsOptions> customsOptions,
        IOptions<GatewayOptions> options, IClock clock, ILogger<CustomsService> logger)
    {
        _scopes = scopes;
        _customs = customs;
        _bus = bus;
        _customsOptions = customsOptions.Value;
        _options = options.Value;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.CustomsEnabled || string.Equals(_customsOptions.Mode, "Disabled", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Customs integration is disabled");
            return;
        }

        _subscription = await _bus.SubscribeAsync<CustomsDeclarationRequested>(SubscriptionId, (m, ct) => SubmitAsync(m.DeclarationId, m.RequestedBy, ct), stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_customsOptions.StatusPollSeconds));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await PollStatusAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Customs status poll failed");
            }
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _subscription?.Dispose();
        return base.StopAsync(cancellationToken);
    }

    internal async Task SubmitAsync(int declarationId, string requestedBy, CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ControlTowerDbContext>();
        var queries = scope.ServiceProvider.GetRequiredService<ShipmentQueries>();
        var declaration = await db.CustomsDeclarations.Include(d => d.Lines).FirstOrDefaultAsync(d => d.Id == declarationId, ct);
        if (declaration is null)
        {
            _logger.LogWarning("Declaration {Id} not found", declarationId);
            return;
        }
        if (declaration.Status is not (DeclarationStatus.Draft or DeclarationStatus.Rejected))
        {
            _logger.LogInformation("Declaration {Lrn} is already {Status}", declaration.Lrn, declaration.Status);
            return;
        }
        if (declaration.SubmitAttempts >= _customsOptions.MaxSubmitAttempts)
        {
            _logger.LogWarning("Declaration {Lrn} reached {Max} attempts", declaration.Lrn, _customsOptions.MaxSubmitAttempts);
            return;
        }

        var shipment = await queries.GetAsync(declaration.ShipmentId, ct);
        if (shipment is null) return;

        var now = _clock.UtcNow;
        declaration.SubmitAttempts++;
        // a rejected LRN may not be reused
        if (declaration.Status == DeclarationStatus.Rejected)
            declaration.Lrn = DeclarationValidator.NewLrn(shipment.Id, now, declaration.SubmitAttempts);

        var errors = DeclarationValidator.Validate(declaration);
        CustomsSubmitResult result;
        if (errors.Count > 0)
        {
            result = new CustomsSubmitResult(false, null, null, errors);
        }
        else
        {
            try
            {
                result = await _customs.SubmitAsync(declaration, shipment, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Submitting declaration {Lrn} failed; will be retried by the operator", declaration.Lrn);
                declaration.RejectionReason = "Transmission failed: " + ex.Message;
                await db.SaveChangesAsync(ct);
                return;
            }
        }

        declaration.SubmittedAt = now;
        if (result.Accepted)
        {
            declaration.Status = DeclarationStatus.Accepted;
            declaration.Mrn = result.Mrn;
            declaration.AcceptedAt = result.AcceptedAt ?? now;
            declaration.RejectionReason = null;
        }
        else
        {
            declaration.Status = DeclarationStatus.Rejected;
            declaration.RejectionReason = string.Join("; ", result.Errors);
            if (declaration.RejectionReason.Length > 500) declaration.RejectionReason = declaration.RejectionReason[..500];
        }
        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Declaration {Lrn} for {Number} submitted by {User}: {Status} {Mrn}", declaration.Lrn, shipment.Number, requestedBy, declaration.Status, declaration.Mrn);
        await PublishAsync(declaration, shipment.Number, ct);
    }

    private async Task PollStatusAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ControlTowerDbContext>();
        var open = await db.CustomsDeclarations
            .Where(d => d.Status == DeclarationStatus.Submitted || d.Status == DeclarationStatus.Accepted || d.Status == DeclarationStatus.UnderControl)
            .ToListAsync(ct);
        foreach (var declaration in open)
        {
            CustomsStatusResult status;
            try
            {
                status = await _customs.GetStatusAsync(declaration, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Status of {Lrn} could not be read", declaration.Lrn);
                continue;
            }
            if (status.Status == declaration.Status) continue;

            declaration.Status = status.Status;
            declaration.Mrn ??= status.Mrn;
            if (status.Status == DeclarationStatus.Released) declaration.ReleasedAt = status.StatusAt ?? _clock.UtcNow;
            if (status.Status == DeclarationStatus.Rejected) declaration.RejectionReason = status.Reason;
            await db.SaveChangesAsync(ct);

            var number = await db.Shipments.Where(s => s.Id == declaration.ShipmentId).Select(s => s.Number).FirstAsync(ct);
            _logger.LogInformation("Declaration {Lrn} ({Mrn}) is now {Status}", declaration.Lrn, declaration.Mrn, declaration.Status);
            await PublishAsync(declaration, number, ct, status.Reason);
        }
    }

    private async Task PublishAsync(CustomsDeclaration d, string shipmentNumber, CancellationToken ct, string? reason = null)
    {
        var now = _clock.UtcNow;
        await _bus.PublishAsync(new CustomsStatusChanged(IntegrationEvent.NewId(), now, d.Id, d.ShipmentId, d.Status, d.Mrn, reason ?? d.RejectionReason), ct);
        var milestone = DeclarationMapper.MilestoneFor(d.Status);
        if (milestone is not null)
        {
            await _bus.PublishAsync(new StatusReported(IntegrationEvent.NewId(), d.ReleasedAt ?? d.AcceptedAt ?? now, shipmentNumber, null, null, milestone,
                null, null, "customs", $"{d.Type} declaration {d.Status}{(d.Mrn is null ? "" : ", MRN " + d.Mrn)}"), ct);
        }
    }
}
