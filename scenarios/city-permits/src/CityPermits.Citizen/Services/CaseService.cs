using System.Text.Json;
using CityPermits.Data;
using CityPermits.Domain.Fees;
using CityPermits.Domain.Model;
using CityPermits.Domain.Workflow;
using CityPermits.Integration;
using Microsoft.EntityFrameworkCore;

namespace CityPermits.Citizen.Services;

public sealed class CaseService
{
    private readonly PermitsDbContext _db;
    private readonly ReferenceAllocator _refs;
    private readonly MapServiceClient _maps;
    private readonly IAddressRegister _addresses;
    private readonly OutboxWriter _outbox;

    public CaseService(PermitsDbContext db, ReferenceAllocator refs, MapServiceClient maps, IAddressRegister addresses, OutboxWriter outbox)
    {
        _db = db; _refs = refs; _maps = maps; _addresses = addresses; _outbox = outbox;
    }

    public async Task<PermitCase> CreateDraftAsync(Applicant applicant, PermitCase c, string? uprn)
    {
        c.ApplicantId = applicant.Id;
        c.Status = CaseStatus.Draft;
        c.Channel = Channel.Online;
        c.CreatedUtc = DateTime.UtcNow;
        if (uprn is not null && _addresses.Get(uprn) is { } a)
        {
            c.Uprn = a.Uprn; c.SiteAddress = a.Address; c.ParcelId = a.ParcelId; c.Easting = a.Easting; c.Northing = a.Northing; c.Ward = a.Ward;
        }
        c.Reference = await _refs.NextAsync(c.Kind, DateTime.UtcNow.Year);
        _db.Cases.Add(c);
        await _db.SaveChangesAsync();
        return c;
    }

    /// <summary>Runs site checks, prices the application and submits it for payment.</summary>
    public async Task SubmitAsync(PermitCase c, Applicant applicant)
    {
        var site = c.ParcelId is null ? null : await _maps.CheckSiteAsync(c.ParcelId, c.Building?.FootprintWkt);
        if (site is not null)
        {
            c.SpatialCheckJson = JsonSerializer.Serialize(site);
            c.SpatialCheckedUtc = DateTime.UtcNow;
            if (c.Parking is not null) c.Parking.ZoneCode = site.ParkingZone;
        }
        c.Parking?.Let(p => p.PermitNumberInHousehold = 1 + _db.Cases.Count(x => x.Uprn == c.Uprn && x.Kind == PermitKind.ParkingPermit
            && x.Status == CaseStatus.Issued && x.Parking!.PermitType == ParkingPermitType.Resident));
        var tariff = c.Parking?.ZoneCode is { } z ? await _db.ZoneTariffs.FindAsync(z) : null;
        if (c.Parking is not null && c.Parking.PermitType != ParkingPermitType.Carer && tariff is null)
            throw new WorkflowException("This address is not in a controlled parking zone.");
        c.Fees.Clear();
        c.Fees.AddRange(new FeeCalculator(new FeeSchedule()).Calculate(c, site, tariff).Lines);
        CaseWorkflow.Apply(c, CaseWorkflow.Submit, "applicant:" + applicant.Id, new[] { Roles.Applicant }, null, DateTime.UtcNow);
        if (c.TotalFees == 0)
            CaseWorkflow.Apply(c, CaseWorkflow.PaymentReceived, "system", new[] { Roles.System }, "No fee payable.", DateTime.UtcNow);
        await _db.SaveChangesAsync();
    }

    public async Task MarkPaidAsync(Payment payment, PermitCase c)
    {
        payment.Status = PaymentStatus.Authorised;
        if (c.Status == CaseStatus.AwaitingPayment && c.Balance <= 0)
        {
            var holidays = (await _db.Holidays.Select(h => h.Date).ToListAsync()).ToHashSet();
            CaseWorkflow.Apply(c, CaseWorkflow.PaymentReceived, "paystream", new[] { Roles.System }, null, DateTime.UtcNow);
            c.TargetDecisionDate = TargetDates.AddWorkingDays(DateOnly.FromDateTime(DateTime.UtcNow), TargetDates.WorkingDaysFor(c), holidays).ToDateTime(TimeOnly.MinValue);
            _outbox.Add(OutboxTypes.OfficerNotification, new { c.Id, c.Reference, c.Team });
            _outbox.Add(OutboxTypes.ApplicantNotification, new { c.Id, Template = "submitted" });
        }
        await _db.SaveChangesAsync();
    }
}

internal static class LetExtensions
{
    public static void Let<T>(this T value, Action<T> action) => action(value);
}
