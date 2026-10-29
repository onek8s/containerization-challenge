using System.Globalization;
using CityPermits.Domain;
using CityPermits.Domain.Fees;
using CityPermits.Domain.Inspections;
using CityPermits.Domain.Model;
using CityPermits.Domain.Rules;
using CityPermits.Domain.Workflow;
using Microsoft.EntityFrameworkCore;

namespace CityPermits.Data.Seed;

public sealed record AddressRow(string Uprn, string Address, string Postcode, double Easting, double Northing, string ParcelId, string Ward);

/// <summary>
/// Reference data and a deterministic set of sample cases for training and development databases.
/// </summary>
public static class SampleData
{
    public static readonly ZoneTariff[] Tariffs =
    {
        new() { ZoneCode = "A", Name = "Town Centre", ResidentAnnual = 96m, BusinessAnnual = 420m, VisitorBookOf20 = 30m, MaxPermitsPerHousehold = 2 },
        new() { ZoneCode = "B", Name = "Beaver", ResidentAnnual = 72m, BusinessAnnual = 310m, VisitorBookOf20 = 22m, MaxPermitsPerHousehold = 3 },
        new() { ZoneCode = "C", Name = "Station", ResidentAnnual = 84m, BusinessAnnual = 365m, VisitorBookOf20 = 26m, MaxPermitsPerHousehold = 2 },
    };

    public static readonly StandardCondition[] Conditions =
    {
        new() { Code = "C01", AppliesTo = "BuildingPermit", Text = "The development hereby permitted shall be begun before the expiration of three years from the date of this permission." },
        new() { Code = "C02", AppliesTo = "BuildingPermit", Text = "The development shall be carried out in accordance with the approved drawings listed in this notice." },
        new() { Code = "C03", AppliesTo = "BuildingPermit", Text = "Materials for external surfaces shall match the existing building." },
        new() { Code = "C04", AppliesTo = "BuildingPermit", Text = "No construction work shall take place outside 08:00-18:00 Monday to Friday and 08:00-13:00 Saturday." },
        new() { Code = "C05", AppliesTo = "BuildingPermit", Text = "Finished floor levels shall be set no lower than 300 mm above the 1 in 100 year flood level." },
        new() { Code = "L01", AppliesTo = "BusinessLicence", Text = "The licence holder shall display this licence where it can be seen by customers." },
        new() { Code = "L02", AppliesTo = "BusinessLicence", Text = "CCTV shall be maintained and recordings kept for 31 days." },
        new() { Code = "L03", AppliesTo = "BusinessLicence", Text = "No customers shall be admitted after 23:00 on Sunday to Thursday." },
    };

    private static readonly string[] FirstNames = { "Amelia", "Oliver", "Isla", "George", "Ava", "Noah", "Mia", "Arthur", "Grace", "Leo", "Freya", "Harry", "Lily", "Oscar", "Ella", "Jack", "Priya", "Tomasz", "Aisha", "Daniel", "Chloe", "Samuel", "Zara", "Mohammed" };
    private static readonly string[] LastNames = { "Smith", "Jones", "Taylor", "Brown", "Williams", "Wilson", "Johnson", "Davies", "Patel", "Wright", "Walker", "Kowalski", "Hughes", "Green", "Khan", "Edwards", "Turner", "Baker" };
    private static readonly string[] Companies = { "Elwick Bakery Ltd", "Castle Street Cafe Ltd", "Ashford Cabs Ltd", "Kennel Lane Boarding", "Tufton Wines Ltd", "Bank Street Barbers", "Station Road Kebabs", "The Old Cinema Ltd", "Godinton Builders Ltd", "Weald Homes Ltd" };
    public static readonly string[] Officers = { "CORP\\j.hart", "CORP\\s.ahmed", "CORP\\r.lowe", "CORP\\k.osei", "CORP\\m.fenwick" };
    public static readonly string[] Inspectors = { "CORP\\p.doyle", "CORP\\l.chandler", "CORP\\t.nwosu" };

    public static IReadOnlyList<AddressRow> ReadAddresses(string csvPath)
    {
        var rows = new List<AddressRow>();
        foreach (var line in File.ReadLines(csvPath).Skip(1))
        {
            var parts = SplitCsv(line);
            rows.Add(new AddressRow(parts[0], parts[1], parts[2],
                double.Parse(parts[3], CultureInfo.InvariantCulture), double.Parse(parts[4], CultureInfo.InvariantCulture), parts[5], parts[6]));
        }
        return rows;
    }

    private static List<string> SplitCsv(string line)
    {
        var result = new List<string>();
        var sb = new System.Text.StringBuilder();
        var quoted = false;
        foreach (var ch in line)
        {
            if (ch == '"') { quoted = !quoted; continue; }
            if (ch == ',' && !quoted) { result.Add(sb.ToString()); sb.Clear(); continue; }
            sb.Append(ch);
        }
        result.Add(sb.ToString());
        return result;
    }

    public static IEnumerable<Holiday> Holidays()
    {
        (int y, int m, int d, string n)[] days =
        {
            (2024,1,1,"New Year's Day"),(2024,3,29,"Good Friday"),(2024,4,1,"Easter Monday"),(2024,5,6,"Early May bank holiday"),(2024,5,27,"Spring bank holiday"),
            (2024,8,26,"Summer bank holiday"),(2024,12,25,"Christmas Day"),(2024,12,26,"Boxing Day"),
            (2025,1,1,"New Year's Day"),(2025,4,18,"Good Friday"),(2025,4,21,"Easter Monday"),(2025,5,5,"Early May bank holiday"),(2025,5,26,"Spring bank holiday"),
            (2025,8,25,"Summer bank holiday"),(2025,12,25,"Christmas Day"),(2025,12,26,"Boxing Day"),
            (2026,1,1,"New Year's Day"),(2026,4,3,"Good Friday"),(2026,4,6,"Easter Monday"),(2026,5,4,"Early May bank holiday"),(2026,5,25,"Spring bank holiday"),
            (2026,8,31,"Summer bank holiday"),(2026,12,25,"Christmas Day"),(2026,12,28,"Boxing Day (substitute)"),
        };
        return days.Select(x => new Holiday { Date = new DateOnly(x.y, x.m, x.d), Name = x.n });
    }

    /// <summary>Creates reference data and, when <paramref name="cases"/> is above zero, sample applicants and cases.</summary>
    public static async Task SeedAsync(PermitsDbContext db, IReadOnlyList<AddressRow> addresses, int cases, DateTime nowUtc, Func<PermitCase, SiteConstraints?>? siteCheck = null)
    {
        if (!await db.ZoneTariffs.AnyAsync()) db.ZoneTariffs.AddRange(Tariffs);
        if (!await db.StandardConditions.AnyAsync()) db.StandardConditions.AddRange(Conditions);
        if (!await db.Holidays.AnyAsync()) db.Holidays.AddRange(Holidays());
        await db.SaveChangesAsync();
        if (cases <= 0 || await db.Cases.AnyAsync()) return;

        var rnd = new Random(20240417);
        var holidays = (await db.Holidays.Select(h => h.Date).ToListAsync()).ToHashSet();
        var calc = new FeeCalculator(new FeeSchedule());

        var applicants = new List<Applicant>();
        for (var i = 0; i < 40; i++)
        {
            var org = i % 4 == 3;
            var a = addresses[rnd.Next(addresses.Count)];
            var name = org ? Companies[i / 4 % Companies.Length] : $"{FirstNames[rnd.Next(FirstNames.Length)]} {LastNames[rnd.Next(LastNames.Length)]}";
            applicants.Add(new Applicant
            {
                Type = org ? ApplicantType.Organisation : ApplicantType.Person,
                EidSubject = i % 5 == 4 ? null : $"eid-{(org ? "org" : "p")}-{1000 + i}",
                CompanyNumber = org ? $"{rnd.Next(10000000, 15999999):00000000}" : null,
                DisplayName = name,
                Email = name.ToLowerInvariant().Replace(" ", ".").Replace("ltd", "").Trim('.') + "@example.org",
                Phone = $"07700 900{rnd.Next(100, 999)}",
                PostalAddress = a.Address,
                Postcode = a.Postcode,
                PrefersPaper = i % 5 == 4,
                CreatedUtc = nowUtc.AddDays(-400 + i)
            });
        }
        db.Applicants.AddRange(applicants);
        await db.SaveChangesAsync();

        var seq = new Dictionary<(PermitKind, int), int>();
        var statuses = new[]
        {
            CaseStatus.Draft, CaseStatus.AwaitingPayment, CaseStatus.Submitted, CaseStatus.IntakeCheck, CaseStatus.UnderReview,
            CaseStatus.InformationRequested, CaseStatus.Consultation, CaseStatus.DecisionPending, CaseStatus.Issued, CaseStatus.Issued,
            CaseStatus.Issued, CaseStatus.Refused, CaseStatus.Withdrawn, CaseStatus.Expired, CaseStatus.UnderReview
        };

        for (var i = 0; i < cases; i++)
        {
            var kind = (PermitKind)(i % 3 + 1);
            var applicant = applicants[rnd.Next(applicants.Count)];
            var addr = addresses[rnd.Next(addresses.Count)];
            var status = statuses[i % statuses.Length];
            var created = nowUtc.AddDays(-rnd.Next(5, 380)).Date.AddHours(9 + rnd.Next(8));
            var year = created.Year;
            seq[(kind, year)] = seq.GetValueOrDefault((kind, year)) + 1;

            var c = new PermitCase
            {
                Reference = ReferenceNumbers.Format(kind, year, seq[(kind, year)]),
                Kind = kind,
                Status = CaseStatus.Draft,
                Channel = applicant.PrefersPaper ? Channel.Paper : (i % 11 == 0 ? Channel.Counter : Channel.Online),
                ApplicantId = applicant.Id,
                Applicant = applicant,
                Uprn = addr.Uprn,
                SiteAddress = addr.Address,
                ParcelId = addr.ParcelId,
                Easting = addr.Easting,
                Northing = addr.Northing,
                Ward = addr.Ward,
                CreatedUtc = created,
                Priority = 3
            };
            switch (kind)
            {
                case PermitKind.BuildingPermit:
                    var wt = (WorkType)(rnd.Next(7) + 1);
                    c.Building = new BuildingDetails
                    {
                        WorkType = wt,
                        EstimatedCost = wt == WorkType.NewBuilding ? rnd.Next(80, 900) * 1000m : rnd.Next(5, 60) * 1000m,
                        GrossFloorAreaM2 = wt is WorkType.NewBuilding ? rnd.Next(90, 400) : rnd.Next(8, 80),
                        FootprintM2 = wt is WorkType.NewBuilding ? rnd.Next(60, 300) : rnd.Next(6, 45),
                        HeightM = Math.Round(3 + rnd.NextDouble() * (wt == WorkType.NewBuilding ? 12 : 5), 1),
                        Storeys = wt == WorkType.NewBuilding ? rnd.Next(1, 4) : 1,
                        UseClass = wt == WorkType.ChangeOfUse ? new[] { "E(b)", "C4", "Sui Generis" }[rnd.Next(3)] : "C3",
                        ListedBuilding = rnd.Next(20) == 0,
                        AgentName = rnd.Next(3) == 0 ? "Weald Architecture LLP" : null,
                    };
                    c.Title = $"{Describe(wt)} at {addr.Address.Replace(", Ashford", "")}";
                    c.Team = "Building Control";
                    break;
                case PermitKind.BusinessLicence:
                    var cat = (BusinessCategory)(rnd.Next(9) + 1);
                    c.Business = new BusinessDetails
                    {
                        Category = cat,
                        TradingName = applicant.Type == ApplicantType.Organisation ? applicant.DisplayName.Replace(" Ltd", "") : $"{applicant.DisplayName.Split(' ')[1]}'s",
                        OpeningHours = "Mon-Sat 08:00-22:00",
                        Capacity = cat is BusinessCategory.Entertainment or BusinessCategory.AlcoholOnSale ? rnd.Next(40, 650) : null,
                        ResponsiblePerson = cat is BusinessCategory.AlcoholOnSale or BusinessCategory.AlcoholOffSale ? applicant.DisplayName : null,
                        Seasonal = cat == BusinessCategory.StreetTrading && rnd.Next(2) == 0,
                        UseClass = cat switch { BusinessCategory.FoodPremises => "E(b)", BusinessCategory.AlcoholOnSale => "Sui Generis", BusinessCategory.HairAndBeauty => "E(a)", _ => null }
                    };
                    c.Title = $"{FeeCalculator.Display(cat)} licence: {c.Business.TradingName}";
                    c.Team = "Licensing";
                    break;
                default:
                    var pt = rnd.Next(10) switch { < 6 => ParkingPermitType.Resident, < 8 => ParkingPermitType.Visitor, 8 => ParkingPermitType.Business, _ => ParkingPermitType.Carer };
                    var zone = Tariffs[rnd.Next(Tariffs.Length)].ZoneCode;
                    c.Parking = new ParkingDetails
                    {
                        PermitType = pt,
                        Registration = pt is ParkingPermitType.Resident or ParkingPermitType.Business ? RandomRegistration(rnd) : null,
                        EmissionBand = (VehicleEmissionBand)rnd.Next(4),
                        ZoneCode = zone,
                        PermitNumberInHousehold = rnd.Next(10) < 8 ? 1 : 2,
                        DurationMonths = new[] { 3, 6, 12, 12, 12 }[rnd.Next(5)]
                    };
                    c.Title = $"{pt} parking permit zone {zone}";
                    c.Team = "Parking Services";
                    break;
            }

            var site = siteCheck?.Invoke(c);
            if (site is not null)
            {
                c.SpatialCheckJson = System.Text.Json.JsonSerializer.Serialize(site);
                c.SpatialCheckedUtc = created.AddMinutes(5);
                if (c.Parking is not null && site.ParkingZone is not null) c.Parking.ZoneCode = site.ParkingZone;
            }
            var tariff = Tariffs.FirstOrDefault(t => t.ZoneCode == c.Parking?.ZoneCode) ?? Tariffs[0];
            c.Fees.AddRange(calc.Calculate(c, site, tariff, nowUtc: created).Lines);

            Advance(c, status, created, rnd, holidays, nowUtc);
            db.Cases.Add(c);
        }
        foreach (var (key, value) in seq)
            db.Sequences.Add(new SequenceCounter { Name = ReferenceNumbers.Prefix(key.Item1), Year = key.Item2, Value = value });
        await db.SaveChangesAsync();
    }

    private static void Advance(PermitCase c, CaseStatus target, DateTime created, Random rnd, ISet<DateOnly> holidays, DateTime nowUtc)
    {
        var t = created;
        var officer = Officers[rnd.Next(Officers.Length)];
        var supervisor = Officers[0];
        string[] officerRoles = { Roles.CaseOfficer, Roles.Supervisor };
        string[] system = { Roles.System };
        string applicantActor = "applicant:" + c.ApplicantId;
        string[] applicantRoles = { Roles.Applicant };

        bool Step(string action, string actor, string[] roles, string? note = null, int hours = 20)
        {
            t = t.AddHours(hours + rnd.Next(40));
            if (t > nowUtc) t = nowUtc.AddMinutes(-rnd.Next(30, 600));
            CaseWorkflow.Apply(c, action, actor, roles, note, t);
            return c.Status == target;
        }

        if (target == CaseStatus.Draft) return;
        if (Step(CaseWorkflow.Submit, applicantActor, applicantRoles, hours: 0)) return;
        if (target == CaseStatus.Withdrawn) { Step(CaseWorkflow.Withdraw, applicantActor, applicantRoles, "Applicant no longer proceeding."); return; }

        c.Payments.Add(new Payment
        {
            ProviderReference = $"PS-{rnd.Next(10000000, 99999999)}",
            SessionId = Guid.NewGuid().ToString("N"),
            Amount = c.TotalFees,
            Status = c.TotalFees == 0 ? PaymentStatus.Settled : PaymentStatus.Settled,
            CardBrand = rnd.Next(3) == 0 ? "mastercard" : "visa",
            Last4 = rnd.Next(1000, 9999).ToString(),
            CreatedUtc = t.AddMinutes(4),
            SettledUtc = t.AddDays(1),
            SettlementBatch = $"STL{t:yyyyMMdd}"
        });
        if (Step(CaseWorkflow.PaymentReceived, "paystream", system, hours: 0)) return;
        c.TargetDecisionDate = TargetDates.AddWorkingDays(DateOnly.FromDateTime(t), TargetDates.WorkingDaysFor(c), holidays).ToDateTime(TimeOnly.MinValue);
        c.AssignedOfficer = officer;
        if (Step(CaseWorkflow.StartIntake, officer, officerRoles)) return;
        if (c.Kind == PermitKind.BuildingPermit) c.SpatialCheckedUtc ??= t;
        if (target == CaseStatus.InformationRequested)
        {
            Step(CaseWorkflow.RequestInformation, officer, officerRoles, "Please provide floor plans showing existing and proposed layouts.");
            return;
        }
        if (Step(CaseWorkflow.Validate, officer, officerRoles)) return;
        if (target == CaseStatus.Consultation || (c.Kind == PermitKind.BuildingPermit && rnd.Next(3) == 0))
        {
            c.Consultations.Add(new Consultation
            {
                Consultee = c.Kind == PermitKind.BusinessLicence ? "Police Licensing" : "Conservation Officer",
                Reason = "Standard consultation",
                SentUtc = t,
                DueDate = DateOnly.FromDateTime(t.AddDays(21)),
            });
            if (Step(CaseWorkflow.Consult, officer, officerRoles)) return;
            var cons = c.Consultations.Last();
            cons.RespondedUtc = t.AddDays(10);
            cons.Response = "No objection subject to standard conditions.";
            cons.Objection = false;
            if (Step(CaseWorkflow.ConsultationClosed, officer, officerRoles, hours: 240)) return;
        }
        if (target == CaseStatus.Refused)
        {
            Step(CaseWorkflow.Recommend, officer, officerRoles, "Recommend refusal: harm to the character of the conservation area.");
            Step(CaseWorkflow.Refuse, supervisor, officerRoles, "Refused: the proposal would harm the character and appearance of the area (policy HE3).");
            return;
        }
        if (Step(CaseWorkflow.Recommend, officer, officerRoles, "Recommend approval with standard conditions.")) return;
        if (c.Kind == PermitKind.BuildingPermit)
        {
            c.Conditions.Add(new Condition { Code = "C01", Text = Conditions[0].Text, Order = 1 });
            c.Conditions.Add(new Condition { Code = "C02", Text = Conditions[1].Text, Order = 2 });
        }
        if (Step(CaseWorkflow.Approve, supervisor, officerRoles)) return;
        if (Step(CaseWorkflow.Issue, "system", system, hours: 1)) { SetValidity(c, target, rnd); AddInspections(c, rnd, nowUtc); return; }
        SetValidity(c, target, rnd);
        AddInspections(c, rnd, nowUtc);
        if (target == CaseStatus.Expired) Step(CaseWorkflow.Expire, "system", system);
    }

    private static void SetValidity(PermitCase c, CaseStatus target, Random rnd)
    {
        var issued = DateOnly.FromDateTime(c.IssuedUtc ?? DateTime.UtcNow);
        var (from, to) = RenewalPolicy.Validity(c, issued, null);
        if (target == CaseStatus.Expired) { from = from.AddYears(-1); to = to.AddYears(-1); }
        c.ValidFrom = from;
        c.ValidTo = to;
        if (c.Parking is not null) c.Parking.PermitSerial = $"{c.Parking.ZoneCode}{issued:yy}-{rnd.Next(10000, 99999)}";
    }

    private static void AddInspections(PermitCase c, Random rnd, DateTime nowUtc)
    {
        var stages = InspectionStages.For(c);
        var day = DateOnly.FromDateTime(nowUtc);
        for (var i = 0; i < stages.Count; i++)
        {
            var done = i < stages.Count / 2;
            var date = done ? day.AddDays(-rnd.Next(5, 60)) : day.AddDays(rnd.Next(0, 4));
            if (date.DayOfWeek == DayOfWeek.Saturday) date = date.AddDays(2);
            if (date.DayOfWeek == DayOfWeek.Sunday) date = date.AddDays(1);
            c.Inspections.Add(new Inspection
            {
                Stage = stages[i],
                Status = done ? InspectionStatus.Passed : InspectionStatus.Scheduled,
                ScheduledDate = date,
                Slot = rnd.Next(2) == 0 ? "AM" : "PM",
                Inspector = Inspectors[rnd.Next(Inspectors.Length)],
                CompletedUtc = done ? date.ToDateTime(new TimeOnly(11, 30)) : null,
                ResultNotes = done ? "Inspected, satisfactory." : null
            });
        }
    }

    private static string Describe(WorkType wt) => wt switch
    {
        WorkType.NewBuilding => "New dwelling",
        WorkType.Extension => "Single storey rear extension",
        WorkType.Alteration => "Removal of load-bearing wall",
        WorkType.Demolition => "Demolition of garage",
        WorkType.ChangeOfUse => "Change of use of ground floor",
        WorkType.Outbuilding => "Garden room",
        WorkType.Signage => "Illuminated fascia sign",
        _ => wt.ToString()
    };

    private static string RandomRegistration(Random rnd)
    {
        const string letters = "ABCDEFGHJKLMNOPRSTUVWXY";
        return $"{letters[rnd.Next(letters.Length)]}{letters[rnd.Next(letters.Length)]}{rnd.Next(15, 74)} {letters[rnd.Next(letters.Length)]}{letters[rnd.Next(letters.Length)]}{letters[rnd.Next(letters.Length)]}";
    }
}
