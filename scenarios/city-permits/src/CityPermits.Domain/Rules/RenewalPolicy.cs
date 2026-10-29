using CityPermits.Domain.Model;

namespace CityPermits.Domain.Rules;

public sealed record ReminderDue(int CaseId, int ReminderNumber, int DaysBeforeExpiry);

public static class RenewalPolicy
{
    /// <summary>Licences: reminders 60, 30 and 7 days before expiry. Parking: 21 and 7 days.</summary>
    public static int[] ReminderDays(PermitKind kind) => kind switch
    {
        PermitKind.BusinessLicence => new[] { 60, 30, 7 },
        PermitKind.ParkingPermit => new[] { 21, 7 },
        _ => Array.Empty<int>()
    };

    public static ReminderDue? Due(PermitCase c, DateOnly today)
    {
        if (c.Status != CaseStatus.Issued || c.ValidTo is null) return null;
        var days = ReminderDays(c.Kind);
        if (c.RenewalReminderCount >= days.Length) return null;
        var next = days[c.RenewalReminderCount];
        var left = c.ValidTo.Value.DayNumber - today.DayNumber;
        return left <= next ? new ReminderDue(c.Id, c.RenewalReminderCount + 1, next) : null;
    }

    public static bool IsExpired(PermitCase c, DateOnly today) =>
        c.Status == CaseStatus.Issued && c.ValidTo is { } to && to < today;

    public static (DateOnly From, DateOnly To) Validity(PermitCase c, DateOnly issueDate, PermitCase? previous)
    {
        // Renewals continue from the previous expiry if issued before it, so no days are lost.
        var from = previous?.ValidTo is { } prevTo && prevTo >= issueDate ? prevTo.AddDays(1) : issueDate;
        var months = c.Kind switch
        {
            PermitKind.ParkingPermit => c.Parking?.DurationMonths ?? 12,
            PermitKind.BusinessLicence => c.Business?.Seasonal == true ? 6 : 12,
            PermitKind.BuildingPermit => 36,
            _ => 12
        };
        return (from, from.AddMonths(months).AddDays(-1));
    }

    public static PermitCase CreateRenewal(PermitCase previous, DateTime nowUtc)
    {
        if (previous.Kind == PermitKind.BuildingPermit) throw new InvalidOperationException("Building permits are not renewed.");
        return new PermitCase
        {
            Kind = previous.Kind,
            Status = CaseStatus.Draft,
            Channel = previous.Channel,
            ApplicantId = previous.ApplicantId,
            Uprn = previous.Uprn,
            SiteAddress = previous.SiteAddress,
            ParcelId = previous.ParcelId,
            Easting = previous.Easting,
            Northing = previous.Northing,
            Ward = previous.Ward,
            Title = "Renewal: " + previous.Title.Replace("Renewal: ", ""),
            Business = previous.Business is null ? null : new BusinessDetails
            {
                Category = previous.Business.Category, TradingName = previous.Business.TradingName, OpeningHours = previous.Business.OpeningHours,
                Capacity = previous.Business.Capacity, ResponsiblePerson = previous.Business.ResponsiblePerson, Seasonal = previous.Business.Seasonal,
                UseClass = previous.Business.UseClass
            },
            Parking = previous.Parking is null ? null : new ParkingDetails
            {
                PermitType = previous.Parking.PermitType, Registration = previous.Parking.Registration, EmissionBand = previous.Parking.EmissionBand,
                ZoneCode = previous.Parking.ZoneCode, PermitNumberInHousehold = previous.Parking.PermitNumberInHousehold, DurationMonths = previous.Parking.DurationMonths
            },
            RenewalOfCaseId = previous.Id,
            CreatedUtc = nowUtc
        };
    }
}
