using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Fabrikam.Housing.Assets;
using Fabrikam.Housing.Common;
using Fabrikam.Housing.Compliance;
using Fabrikam.Housing.Contractors;
using Fabrikam.Housing.Rent;
using Fabrikam.Housing.Repairs;
using Fabrikam.Housing.Tenancies;
using Microsoft.EntityFrameworkCore;

namespace Fabrikam.Housing.Data.Seed
{
    /// <summary>
    /// Creates a realistic development data set: six estates, ~320 homes, tenancies with six months of rent
    /// history, direct debits, arrears, repairs, contractor invoices and compliance certificates.
    /// The data is generated from a fixed seed so every developer gets the same references.
    /// </summary>
    public class DevelopmentSeeder
    {
        public const string DevelopmentContractorSecretPrefix = "dev-secret-";
        private readonly HousingDbContext _db;
        private readonly Random _rnd = new Random(20210412);
        private readonly DateTime _today;
        private readonly WorkingCalendar _calendar = new WorkingCalendar();

        public DevelopmentSeeder(HousingDbContext db, DateTime today)
        {
            _db = db;
            _today = today.Date;
        }

        public static string PortalSubjectFor(int tenantId) => "tn-" + tenantId.ToString("00000");

        public static string HashSecret(string secret)
        {
            using (var sha = SHA256.Create())
                return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(secret)));
        }

        private static readonly (string Code, string Name, string Patch, string Postcode, string Town, double Lat, double Lon, PropertyType Type, bool Lift, string[] Streets, int Count)[] EstateDefs =
        {
            ("BARF", "Barton Fields", "EAST", "BS5", "Bristol", 51.4652, -2.5634, PropertyType.House, false, new[] { "Barton Fields Road", "Cherrywood Close", "Hawthorn Way" }, 70),
            ("KING", "Kingsmead", "SOUTH", "BS13", "Bristol", 51.4153, -2.6170, PropertyType.House, false, new[] { "Kingsmead Avenue", "Lark Rise", "Mead Lane" }, 64),
            ("MILL", "Millbrook Court", "NORTH", "BS16", "Bristol", 51.4870, -2.5170, PropertyType.Flat, true, new[] { "Millbrook Court" }, 56),
            ("ORCH", "Orchard Rise", "SOUTH", "BS14", "Bristol", 51.4140, -2.5600, PropertyType.House, false, new[] { "Orchard Rise", "Pippin Close" }, 48),
            ("RIVW", "Riverside Walk", "EAST", "BS3", "Bristol", 51.4420, -2.6010, PropertyType.Flat, false, new[] { "Riverside Walk", "Towpath Mews" }, 52),
            ("STAN", "St Anne's Close", "NORTH", "BS4", "Bristol", 51.4470, -2.5480, PropertyType.Bungalow, false, new[] { "St Anne's Close" }, 30)
        };

        private static readonly string[] FirstNames = { "Olivia", "Amelia", "Isla", "Ava", "Mia", "Sophia", "Grace", "Lily", "Freya", "Emily", "Ella", "Chloe", "Zainab", "Aisha", "Maja", "Oliver", "George", "Harry", "Noah", "Jack", "Leo", "Arthur", "Muhammad", "Oscar", "Charlie", "Jacob", "Thomas", "Ibrahim", "Kacper", "Tomasz", "Dylan", "Kofi", "Ama", "Priya", "Ravi", "Sean", "Niamh", "Rhys", "Megan", "Carys" };
        private static readonly string[] LastNames = { "Smith", "Jones", "Williams", "Taylor", "Brown", "Davies", "Evans", "Wilson", "Thomas", "Johnson", "Roberts", "Robinson", "Thompson", "Wright", "Walker", "White", "Edwards", "Hughes", "Green", "Hall", "Khan", "Ali", "Hussain", "Ahmed", "Nowak", "Kowalski", "Mensah", "Patel", "Begum", "O'Brien", "Murphy", "Price", "Morgan", "Lewis", "Clarke", "Cooper", "Ward", "Harris", "Mitchell", "Bennett" };

        public static bool IsSeeded(HousingDbContext db) => db.Properties.Any();

        public void Seed()
        {
            if (IsSeeded(_db)) return;
            var started = _db.Database.BeginTransaction();

            _db.BankHolidays.AddRange(WorkingCalendar.DefaultBankHolidays.Select(d => new BankHoliday { Date = d, Name = "Bank holiday" }));
            _db.Patches.AddRange(
                new Patch { Code = "NORTH", Name = "North patch", OfficerLogin = @"CORP\j.okafor", OfficerName = "Joy Okafor", OfficerEmail = "j.okafor@fabrikamhomes.co.uk" },
                new Patch { Code = "SOUTH", Name = "South patch", OfficerLogin = @"CORP\d.price", OfficerName = "Dafydd Price", OfficerEmail = "d.price@fabrikamhomes.co.uk" },
                new Patch { Code = "EAST", Name = "East patch", OfficerLogin = @"CORP\s.kowalska", OfficerName = "Sylwia Kowalska", OfficerEmail = "s.kowalska@fabrikamhomes.co.uk" });

            var sor = ReferenceData.ScheduleOfRates().ToList();
            _db.ScheduleOfRates.AddRange(sor);
            var contractors = ReferenceData.Contractors().ToList();
            foreach (var c in contractors)
            {
                c.ApiClientSecretHash = HashSecret(DevelopmentContractorSecretPrefix + c.Code.ToLowerInvariant());
                c.ApiSecretIssuedAt = _today.AddMonths(-6);
                Stamp(c, _today.AddYears(-2));
            }
            _db.Contractors.AddRange(contractors);
            _db.SaveChanges();

            var properties = SeedProperties();
            var tenancies = SeedTenancies(properties);
            SeedRentHistory(tenancies);
            SeedCertificates(properties, contractors);
            var orders = SeedRepairs(properties, tenancies, contractors, sor);
            SeedInvoices(orders, contractors);
            SeedEnquiries(tenancies);

            _db.Counters.Add(new Counter { Name = "WorkOrder" + _today.Year, Value = orders.Count(o => o.ReportedAt.Year == _today.Year) });
            _db.Counters.Add(new Counter { Name = "BacsFile", Value = 41 });
            _db.SaveChanges();
            started.Commit();
        }

        private void Stamp(AuditedEntity e, DateTime at)
        {
            e.CreatedAt = at;
            e.CreatedBy = "dataload";
        }

        private List<Property> SeedProperties()
        {
            var list = new List<Property>();
            var refNo = 1000;
            long uprn = 100121034000;
            foreach (var d in EstateDefs)
            {
                var estate = new Estate { Code = d.Code, Name = d.Name, PatchCode = d.Patch, HasCommunalAreas = d.Type == PropertyType.Flat, HasLift = d.Lift };
                _db.Estates.Add(estate);
                for (var i = 0; i < d.Count; i++)
                {
                    var street = d.Streets[i % d.Streets.Length];
                    var number = i / d.Streets.Length + 1;
                    var type = d.Type;
                    if (type == PropertyType.House && _rnd.NextDouble() < 0.12) type = PropertyType.Maisonette;
                    if (type == PropertyType.Flat && _rnd.NextDouble() < 0.08) type = PropertyType.Bedsit;
                    var bedrooms = type == PropertyType.Bedsit ? 0 : type == PropertyType.Bungalow ? 1 + _rnd.Next(2) : type == PropertyType.Flat ? 1 + _rnd.Next(2) : 2 + _rnd.Next(3);
                    var p = new Property
                    {
                        Uprn = uprn++,
                        Reference = "P" + (refNo++).ToString("00000"),
                        Estate = estate,
                        AddressLine1 = d.Type == PropertyType.Flat ? $"Flat {number}, {street}" : $"{number} {street}",
                        Town = d.Town,
                        Postcode = $"{d.Postcode} {1 + _rnd.Next(9)}{(char)('A' + _rnd.Next(20))}{(char)('A' + _rnd.Next(20))}".Replace('I', 'J').Replace('O', 'P'),
                        Latitude = Math.Round(d.Lat + (_rnd.NextDouble() - 0.5) * 0.006, 6),
                        Longitude = Math.Round(d.Lon + (_rnd.NextDouble() - 0.5) * 0.009, 6),
                        PatchCode = d.Patch,
                        Type = type,
                        Bedrooms = bedrooms,
                        YearBuilt = d.Code == "STAN" ? 1972 : d.Code == "MILL" ? 1968 : 1950 + _rnd.Next(55),
                        Status = PropertyStatus.Let,
                        HasGasSupply = d.Type != PropertyType.Flat || d.Code == "RIVW",
                        HasSmokeAlarms = true,
                        AsbestosPresent = _rnd.NextDouble() < 0.3,
                        WeeklyNetRent = Math.Round(78m + bedrooms * 11.35m + (decimal)_rnd.Next(0, 900) / 100m, 2),
                        WeeklyServiceCharge = d.Type == PropertyType.Flat ? 9.85m + (d.Lift ? 4.20m : 0) : d.Code == "STAN" ? 14.60m : 0m,
                        WeeklyWaterCharge = _rnd.NextDouble() < 0.4 ? 7.42m : 0m
                    };
                    if (_rnd.NextDouble() < 0.4) p.Latitude = p.Longitude = null; // not yet geocoded
                    Stamp(p, new DateTime(2016, 4, 1));
                    _db.Properties.Add(p);
                    list.Add(p);
                }
            }
            _db.SaveChanges();
            return list;
        }

        private List<Tenancy> SeedTenancies(List<Property> properties)
        {
            var list = new List<Tenancy>();
            var tref = 20000;
            foreach (var p in properties)
            {
                if (_rnd.NextDouble() < 0.05)
                {
                    p.Status = PropertyStatus.Void;
                    continue;
                }
                var start = _today.AddDays(-_rnd.Next(30, 365 * 15));
                var type = (_today - start).TotalDays < 365 ? TenancyType.Starter : p.Estate.Code == "RIVW" && _rnd.NextDouble() < 0.3 ? TenancyType.SharedOwnership : TenancyType.Assured;
                var t = new Tenancy
                {
                    Reference = "T" + (tref++).ToString("000000"),
                    Property = p,
                    Type = type,
                    Status = TenancyStatus.Current,
                    StartDate = start,
                    WeeklyCharge = p.WeeklyTotalCharge
                };
                var hb = _rnd.NextDouble();
                t.HousingBenefitClaimed = hb < 0.22;
                t.HousingBenefitClaimRef = t.HousingBenefitClaimed ? "HB" + _rnd.Next(100000, 999999) : null;
                t.UniversalCredit = !t.HousingBenefitClaimed && hb < 0.38;
                Stamp(t, start);

                var lead = NewTenant(t, true, start);
                t.Tenants.Add(lead);
                if (p.Bedrooms >= 2 && _rnd.NextDouble() < 0.4)
                {
                    var joint = NewTenant(t, false, start);
                    joint.LastName = lead.LastName;
                    t.Tenants.Add(joint);
                }
                _db.Tenancies.Add(t);
                list.Add(t);
            }
            _db.SaveChanges();

            // The first five lead tenants have portal accounts in the development identity server.
            foreach (var tenant in list.Take(5).Select(t => t.Tenants.First(x => x.IsLeadTenant)))
                tenant.PortalSubjectId = PortalSubjectFor(tenant.Id);
            _db.SaveChanges();
            return list;
        }

        private Tenant NewTenant(Tenancy t, bool lead, DateTime start)
        {
            var first = FirstNames[_rnd.Next(FirstNames.Length)];
            var last = LastNames[_rnd.Next(LastNames.Length)];
            var female = Array.IndexOf(FirstNames, first) < 15 || first == "Ama" || first == "Priya" || first == "Niamh" || first == "Megan" || first == "Carys";
            var tenant = new Tenant
            {
                IsLeadTenant = lead,
                Title = female ? (_rnd.NextDouble() < 0.5 ? "Ms" : "Mrs") : "Mr",
                FirstName = first,
                LastName = last,
                DateOfBirth = new DateTime(1940 + _rnd.Next(62), 1 + _rnd.Next(12), 1 + _rnd.Next(28)),
                Email = _rnd.NextDouble() < 0.75 ? $"{first}.{last}{_rnd.Next(10, 99)}@example.com".ToLowerInvariant().Replace("'", "") : null,
                MobilePhone = _rnd.NextDouble() < 0.85 ? "07700 9" + _rnd.Next(10000, 99999) : null,
                NeedsLargePrint = t.Property.Estate.Code == "STAN" && _rnd.NextDouble() < 0.3,
                Vulnerable = _rnd.NextDouble() < 0.08
            };
            tenant.ContactPreference = tenant.MobilePhone != null ? ContactPreference.Sms : tenant.Email != null ? ContactPreference.Email : ContactPreference.Letter;
            if (tenant.Vulnerable) tenant.VulnerabilityNotes = "See support plan; contact by phone before visits.";
            Stamp(tenant, start);
            return tenant;
        }

        private void SeedRentHistory(List<Tenancy> tenancies)
        {
            var calendar = new RentCalendar();
            var run = new RentChargeRun(calendar);
            var schedule = new CollectionSchedule(_calendar);
            var from = RentCalendar.WeekStart(_today.AddDays(-26 * 7));
            var mandateNo = 1;

            foreach (var t in tenancies)
            {
                var opening = (decimal)Math.Round(_rnd.NextDouble() < 0.7 ? 0 : (_rnd.NextDouble() * 350 - 60), 2);
                var historyFrom = t.StartDate > from ? t.StartDate : from;
                if (opening != 0 && t.StartDate < from)
                    _db.RentTransactions.Add(new RentTransaction { TenancyId = t.Id, PostedOn = from.AddDays(-1), RentWeek = RentCalendar.WeekStart(from.AddDays(-1)), Type = TransactionType.Adjustment, Amount = opening, Reference = "B/F", Description = "Balance brought forward from previous system", Source = "DataLoad" });

                // Payment profile: 0 = direct debit, 1 = housing benefit (+ top-up), 2 = card payer, 3 = struggling card payer.
                var profile = t.HousingBenefitClaimed ? 1 : _rnd.NextDouble() < 0.6 ? 0 : _rnd.NextDouble() < 0.75 ? 2 : 3;
                DirectDebitMandate mandate = null;
                if (profile == 0)
                {
                    var freq = _rnd.NextDouble() < 0.55 ? CollectionFrequency.Monthly : CollectionFrequency.Weekly;
                    var day = freq == CollectionFrequency.Monthly ? new[] { 1, 8, 15, 22, 28 }[_rnd.Next(5)] : 1 + _rnd.Next(5);
                    mandate = new DirectDebitMandate
                    {
                        TenancyId = t.Id,
                        ProviderCustomerId = "CU" + (1000000 + mandateNo).ToString(),
                        ProviderMandateId = "MD" + (5000000 + mandateNo).ToString(),
                        BankReference = "FABRIKAM" + t.Reference,
                        AccountHolderName = t.Tenants.First().FullName,
                        AccountNumberEnding = _rnd.Next(1000, 9999).ToString(),
                        Status = MandateStatus.Active,
                        Frequency = freq,
                        CollectionDay = day,
                        Amount = CollectionSchedule.SuggestedAmount(t.WeeklyCharge, freq),
                        FirstCollectionDate = historyFrom
                    };
                    mandateNo++;
                    Stamp(mandate, historyFrom.AddDays(-10));
                    _db.Mandates.Add(mandate);
                }

                var nextMonthly = mandate != null && mandate.Frequency == CollectionFrequency.Monthly ? schedule.FirstCollection(mandate.Frequency, mandate.CollectionDay, historyFrom.AddDays(-7)) : DateTime.MaxValue;
                for (var week = RentCalendar.WeekStart(historyFrom); week < RentCalendar.WeekStart(_today).AddDays(1); week = week.AddDays(7))
                {
                    foreach (var line in run.ChargesFor(t, week, week.AddHours(2)))
                        _db.RentTransactions.Add(line);
                    var rentFree = calendar.IsRentFreeWeek(week);
                    var payDay = week.AddDays(_rnd.Next(0, 5));
                    if (payDay > _today) continue;

                    switch (profile)
                    {
                        case 0 when mandate.Frequency == CollectionFrequency.Weekly && !rentFree:
                            var charge = week.AddDays(mandate.CollectionDay - 1);
                            if (charge <= _today) AddDd(t, mandate, charge, mandate.Amount);
                            break;
                        case 1:
                            if (!rentFree)
                            {
                                var hbShare = Math.Round(t.WeeklyCharge * (decimal)(0.6 + _rnd.NextDouble() * 0.4), 2);
                                _db.RentTransactions.Add(new RentTransaction { TenancyId = t.Id, PostedOn = week.AddDays(3), RentWeek = week, Type = TransactionType.HousingBenefit, Amount = -hbShare, Reference = t.HousingBenefitClaimRef, Description = "Housing benefit", Source = "HBImport" });
                                if (_rnd.NextDouble() < 0.7)
                                    _db.RentTransactions.Add(Payment(t, payDay, TransactionType.CardPayment, t.WeeklyCharge - hbShare));
                            }
                            break;
                        case 2:
                            if (_rnd.NextDouble() < 0.5)
                                _db.RentTransactions.Add(Payment(t, payDay, TransactionType.CardPayment, t.WeeklyCharge * 2));
                            break;
                        case 3:
                            if (_rnd.NextDouble() < 0.3)
                                _db.RentTransactions.Add(Payment(t, payDay, TransactionType.CardPayment, Math.Round(t.WeeklyCharge * (decimal)(0.5 + _rnd.NextDouble()), 2)));
                            break;
                    }
                }
                if (mandate != null && mandate.Frequency == CollectionFrequency.Monthly)
                {
                    for (var d = nextMonthly; d <= _today; d = schedule.Next(mandate, d))
                        AddDd(t, mandate, d, mandate.Amount);
                }
                if (mandate != null)
                {
                    var last = mandate.Frequency == CollectionFrequency.Monthly ? nextMonthly : RentCalendar.WeekStart(_today).AddDays(mandate.CollectionDay - 1);
                    while (last <= _today) last = schedule.Next(mandate, last);
                    mandate.NextCollectionDate = last;
                }
            }
            _db.SaveChanges();

            // Arrears cases for the worst accounts so the income team has a caseload.
            var balances = _db.RentTransactions.AsEnumerable().GroupBy(r => r.TenancyId).ToDictionary(g => g.Key, g => g.Sum(r => r.Amount));
            foreach (var t in tenancies)
            {
                if (!balances.TryGetValue(t.Id, out var bal) || bal < t.WeeklyCharge) continue;
                var weeks = bal / Math.Max(1, t.WeeklyCharge);
                var stage = weeks >= 8 ? ArrearsStage.NoticeOfSeekingPossession : weeks >= 4 ? ArrearsStage.SecondLetter : weeks >= 2 ? ArrearsStage.FirstLetter : ArrearsStage.Reminder;
                var c = new ArrearsCase { TenancyId = t.Id, Stage = stage, BalanceAtStage = bal, StageReachedOn = _today.AddDays(-_rnd.Next(1, 40)), AssignedTo = t.Property.PatchCode, NextReviewOn = _today.AddDays(_rnd.Next(1, 14)) };
                Stamp(c, c.StageReachedOn);
                c.Notes.Add(new ArrearsCaseNote { CreatedAt = c.StageReachedOn, CreatedBy = "system", Text = $"Case opened at stage {stage}, balance {bal:N2}" });
                if (_rnd.NextDouble() < 0.25)
                {
                    var arrangement = new PaymentArrangement { TenancyId = t.Id, WeeklyAmountTowardsArrears = Math.Round(Math.Max(3.70m, bal / 40), 2), StartDate = _today.AddDays(-_rnd.Next(7, 60)), Status = ArrangementStatus.Active, Notes = "Agreed by phone" };
                    Stamp(arrangement, arrangement.StartDate);
                    _db.Arrangements.Add(arrangement);
                }
                _db.ArrearsCases.Add(c);
            }
            _db.SaveChanges();
        }

        private void AddDd(Tenancy t, DirectDebitMandate mandate, DateTime chargeDate, decimal amount)
        {
            var failed = _rnd.NextDouble() < 0.03;
            var payment = new DirectDebitPayment
            {
                Mandate = mandate,
                ProviderPaymentId = "PM" + _rnd.Next(10000000, 99999999),
                ChargeDate = chargeDate,
                Amount = amount,
                Status = failed ? PaymentStatus.Failed : chargeDate < _today.AddDays(-3) ? PaymentStatus.PaidOut : PaymentStatus.Confirmed,
                FailureReason = failed ? "insufficient_funds" : null,
                CreatedAt = chargeDate.AddDays(-4)
            };
            _db.DirectDebitPayments.Add(payment);
            _db.RentTransactions.Add(Payment(t, chargeDate, TransactionType.DirectDebit, amount, payment.ProviderPaymentId));
            if (failed)
                _db.RentTransactions.Add(new RentTransaction { TenancyId = t.Id, PostedOn = chargeDate.AddDays(2), RentWeek = RentCalendar.WeekStart(chargeDate), Type = TransactionType.DirectDebitReturned, Amount = amount, Reference = payment.ProviderPaymentId, Description = "Direct debit returned unpaid", Source = "Paystream" });
        }

        private RentTransaction Payment(Tenancy t, DateTime on, TransactionType type, decimal amount, string reference = null)
        {
            return new RentTransaction
            {
                TenancyId = t.Id,
                PostedOn = on.AddHours(9 + _rnd.Next(8)),
                RentWeek = RentCalendar.WeekStart(on),
                Type = type,
                Amount = -Math.Round(amount, 2),
                Reference = reference ?? "CP" + _rnd.Next(1000000, 9999999),
                Description = type == TransactionType.DirectDebit ? "Direct debit" : "Card payment",
                Source = "Paystream"
            };
        }

        private void SeedCertificates(List<Property> properties, List<Contractor> contractors)
        {
            var gas = contractors.Where(c => c.GasSafeRegistered).ToList();
            var elec = contractors.First(c => c.Code == "BRISEL");
            var certNo = 880000;
            foreach (var p in properties)
            {
                if (p.HasGasSupply)
                {
                    var c = gas[_rnd.Next(gas.Count)];
                    // Spread so that a few are overdue and a good number are due within 60 days.
                    var inspected = _today.AddDays(-_rnd.Next(5, 380));
                    _db.Certificates.Add(Cert(p, ComplianceType.GasSafety, inspected, c, "CP12-" + certNo++, _rnd.NextDouble() < 0.04 ? CertificateOutcome.RemedialRequired : CertificateOutcome.Satisfactory));
                }
                var eicr = _today.AddDays(-_rnd.Next(30, 365 * 5 + 60));
                _db.Certificates.Add(Cert(p, ComplianceType.Electrical, eicr, elec, "EICR-" + certNo++, _rnd.NextDouble() < 0.1 ? CertificateOutcome.RemedialRequired : CertificateOutcome.Satisfactory));
                if (p.AsbestosPresent)
                    _db.Certificates.Add(Cert(p, ComplianceType.Asbestos, _today.AddDays(-_rnd.Next(30, 400)), null, "ASB-" + certNo++, CertificateOutcome.Satisfactory));
            }
            foreach (var e in _db.Estates.Local.Where(e => e.HasCommunalAreas || e.HasLift))
            {
                var fra = new ComplianceCertificate { EstateId = e.Id, Type = ComplianceType.FireRiskAssessment, InspectedOn = _today.AddDays(-200), ExpiresOn = ComplianceRules.ExpiryFor(ComplianceType.FireRiskAssessment, _today.AddDays(-200)), Outcome = CertificateOutcome.Satisfactory, CertificateNumber = "FRA-" + e.Code + "-2021", EngineerName = "Severn Fire Consultants" };
                Stamp(fra, fra.InspectedOn);
                _db.Certificates.Add(fra);
                if (e.HasLift)
                {
                    var lift = new ComplianceCertificate { EstateId = e.Id, Type = ComplianceType.LiftInspection, InspectedOn = _today.AddDays(-170), ExpiresOn = ComplianceRules.ExpiryFor(ComplianceType.LiftInspection, _today.AddDays(-170)), Outcome = CertificateOutcome.Satisfactory, CertificateNumber = "LOLER-" + e.Code, EngineerName = "Zurich Engineering" };
                    Stamp(lift, lift.InspectedOn);
                    _db.Certificates.Add(lift);
                }
            }
            _db.SaveChanges();
        }

        private ComplianceCertificate Cert(Property p, ComplianceType type, DateTime inspected, Contractor c, string number, CertificateOutcome outcome)
        {
            var cert = new ComplianceCertificate
            {
                Property = p,
                Type = type,
                InspectedOn = inspected,
                ExpiresOn = ComplianceRules.ExpiryFor(type, inspected),
                Outcome = outcome,
                CertificateNumber = number,
                ContractorId = c?.Id,
                EngineerName = c == null ? "Avon Asbestos Surveys" : FirstNames[_rnd.Next(FirstNames.Length)] + " " + LastNames[_rnd.Next(LastNames.Length)],
                EngineerRegistration = c?.GasSafeNumber ?? (type == ComplianceType.Electrical ? "NICEIC-" + _rnd.Next(100000, 999999) : null),
                Observations = outcome == CertificateOutcome.RemedialRequired ? (type == ComplianceType.Electrical ? "C2: no RCD protection on socket circuits" : "Flue at risk (AR), appliance turned off with permission") : null
            };
            Stamp(cert, inspected);
            return cert;
        }

        private List<WorkOrder> SeedRepairs(List<Property> properties, List<Tenancy> tenancies, List<Contractor> contractors, List<ScheduleOfRatesItem> sor)
        {
            var rules = new RepairRules(_calendar);
            var byProperty = tenancies.ToDictionary(t => t.PropertyId);
            var orders = new List<WorkOrder>();
            var seqByYear = new Dictionary<int, int>();
            var open = contractors.ToDictionary(c => c.Id, c => 0);
            var reportable = sor.Where(s => s.Code != "HT0301" && s.Code != "EL0901" && s.Code != "HT0310").ToList();

            for (var i = 0; i < 460; i++)
            {
                var reported = _today.AddDays(-Math.Pow(_rnd.NextDouble(), 1.6) * 180).AddHours(8 + _rnd.Next(10)).AddMinutes(_rnd.Next(60));
                var p = properties[_rnd.Next(properties.Count)];
                var item = reportable[_rnd.Next(reportable.Count)];
                var option = RepairFinder.Options.FirstOrDefault(o => o.SorCode == item.Code);
                seqByYear.TryGetValue(reported.Year, out var seq);
                seqByYear[reported.Year] = ++seq;
                var o = new WorkOrder
                {
                    Reference = RepairRules.NextReference(reported.Year, seq),
                    PropertyId = p.Id,
                    Property = p,
                    TenancyId = byProperty.TryGetValue(p.Id, out var t) ? t.Id : (int?)null,
                    Channel = (ReportChannel)(1 + _rnd.Next(3)),
                    ReportedBy = t != null ? t.Tenants.First().FullName : "Voids team",
                    Location = option?.Area ?? item.Trade.ToString(),
                    Description = option?.Text ?? item.Description,
                    Trade = item.Trade,
                    Priority = item.DefaultPriority,
                    ReportedAt = reported,
                    TargetDate = rules.TargetDate(item.DefaultPriority, reported),
                    Status = WorkOrderStatus.Reported
                };
                Stamp(o, reported);
                o.Events.Add(new WorkOrderEvent { At = reported, Actor = o.ReportedBy, NewStatus = WorkOrderStatus.Reported, Text = "Reported via " + o.Channel });
                o.Lines.Add(new WorkOrderLine { SorCode = item.Code, Description = item.Description, Quantity = item.Unit == "IT" ? 1 : 1 + _rnd.Next(4), UnitPrice = item.UnitPrice });

                var age = (_today - reported.Date).TotalDays;
                var contractor = RepairRules.ChooseContractor(contractors, o.Trade, p.PatchCode, o.Priority, open);
                if (contractor != null && (age > 1 || _rnd.NextDouble() < 0.5))
                {
                    o.ContractorId = contractor.Id;
                    o.AssignedAt = reported.AddMinutes(20 + _rnd.Next(240));
                    o.Status = WorkOrderStatus.Assigned;
                    o.Events.Add(new WorkOrderEvent { At = o.AssignedAt.Value, Actor = "CORP\\repairs.desk", NewStatus = WorkOrderStatus.Assigned, Text = "Assigned to " + contractor.Code });
                    if (age > 2 || _rnd.NextDouble() < 0.5)
                    {
                        o.AcceptedAt = o.AssignedAt.Value.AddHours(1 + _rnd.Next(20));
                        o.Status = WorkOrderStatus.Accepted;
                        o.Events.Add(new WorkOrderEvent { At = o.AcceptedAt.Value, Actor = contractor.ApiClientId, NewStatus = WorkOrderStatus.Accepted });
                        var apptDay = _calendar.AddWorkingDays(reported.Date, o.Priority == RepairPriority.Emergency ? 0 : 1 + _rnd.Next(o.Priority == RepairPriority.Urgent ? 4 : 18));
                        o.Appointments.Add(new Appointment { Date = apptDay, Slot = (AppointmentSlot)_rnd.Next(4), BookedBy = contractor.ApiClientId, BookedAt = o.AcceptedAt.Value });
                        o.Status = WorkOrderStatus.Appointed;
                        if (apptDay < _today)
                        {
                            if (_rnd.NextDouble() < 0.06)
                            {
                                o.Status = WorkOrderStatus.NoAccess;
                                o.NoAccessCount = 1;
                                o.Appointments[0].Outcome = "No access - card left";
                            }
                            else
                            {
                                var late = _rnd.NextDouble() < 0.12 ? _rnd.Next(1, 15) : 0;
                                o.CompletedAt = apptDay.AddDays(late).AddHours(10 + _rnd.Next(7));
                                o.Status = WorkOrderStatus.Completed;
                                o.LabourHours = Math.Round((decimal)(0.5 + _rnd.NextDouble() * 4), 1);
                                o.CompletionNotes = "Work completed, tested and left clean.";
                                o.Appointments[0].Outcome = "Completed";
                                o.SatisfactionScore = _rnd.NextDouble() < 0.4 ? 3 + _rnd.Next(3) : (int?)null;
                                o.Events.Add(new WorkOrderEvent { At = o.CompletedAt.Value, Actor = contractor.ApiClientId, NewStatus = WorkOrderStatus.Completed, Text = o.CompletionNotes });
                            }
                        }
                    }
                }
                else if (age > 0.5 && _rnd.NextDouble() < 0.5)
                {
                    o.Status = WorkOrderStatus.Triaged;
                }
                if (o.Status != WorkOrderStatus.Completed && _rnd.NextDouble() < 0.03)
                {
                    o.Status = WorkOrderStatus.Cancelled;
                    o.CancellationReason = "Duplicate report";
                }
                if (o.IsOpen && o.ContractorId.HasValue) open[o.ContractorId.Value]++;
                _db.WorkOrders.Add(o);
                orders.Add(o);
            }
            _db.SaveChanges();
            return orders;
        }

        private void SeedInvoices(List<WorkOrder> orders, List<Contractor> contractors)
        {
            var byId = contractors.ToDictionary(c => c.Id);
            var invoiceNo = new Dictionary<int, int>();
            foreach (var o in orders.Where(o => o.Status == WorkOrderStatus.Completed && o.ContractorId.HasValue).OrderBy(o => o.CompletedAt))
            {
                var c = byId[o.ContractorId.Value];
                if ((_today - o.CompletedAt.Value).TotalDays < 3 && _rnd.NextDouble() < 0.6) continue;
                invoiceNo.TryGetValue(c.Id, out var n);
                invoiceNo[c.Id] = ++n;
                var net = o.Lines.Sum(l => l.LineTotal) + (_rnd.NextDouble() < 0.08 ? 75m : 0m);
                var age = (_today - o.CompletedAt.Value).TotalDays;
                var inv = new ContractorInvoice
                {
                    ContractorId = c.Id,
                    InvoiceNumber = c.Code.Substring(0, 3) + "-" + (4000 + n),
                    InvoiceDate = o.CompletedAt.Value.Date.AddDays(1),
                    WorkOrderId = o.Id,
                    NetAmount = net,
                    VatAmount = c.VatRegistered ? Math.Round(net * InvoiceMatching.VatRate, 2, MidpointRounding.AwayFromZero) : 0m,
                    Status = age > 21 ? InvoiceStatus.Paid : age > 7 ? InvoiceStatus.Approved : InvoiceStatus.Received
                };
                var problem = InvoiceMatching.Check(inv, o, c);
                if (problem != null)
                {
                    inv.Status = InvoiceStatus.Queried;
                    inv.QueryReason = problem;
                }
                else if (inv.Status != InvoiceStatus.Received)
                {
                    inv.ApprovedAt = inv.InvoiceDate.AddDays(3);
                    inv.ApprovedBy = @"CORP\m.hughes";
                }
                Stamp(inv, inv.InvoiceDate);
                _db.ContractorInvoices.Add(inv);
            }
            _db.SaveChanges();
        }

        private void SeedEnquiries(List<Tenancy> tenancies)
        {
            var topics = new[]
            {
                (EnquiryTopic.Rent, "Rent statement query", "Hi, I think my housing benefit payment from last month is missing from my statement. Can you check?"),
                (EnquiryTopic.AntiSocialBehaviour, "Noise from neighbours", "There is loud music most nights after midnight from the flat above. What can be done?"),
                (EnquiryTopic.TenancyChange, "Adding my partner to the tenancy", "My partner has lived with me for two years. How do we make it a joint tenancy?"),
                (EnquiryTopic.Complaint, "Repair not done", "The plumber did not turn up for the appointment and nobody called me."),
                (EnquiryTopic.Repairs, "Communal light not working", "The light on the second floor landing has been out for a week."),
                (EnquiryTopic.Other, "Parking permit", "Can I get a parking permit for the estate car park?")
            };
            for (var i = 0; i < 24; i++)
            {
                var t = tenancies[_rnd.Next(tenancies.Count)];
                var (topic, subject, message) = topics[_rnd.Next(topics.Length)];
                var created = _today.AddDays(-_rnd.Next(0, 30)).AddHours(8 + _rnd.Next(12));
                var e = new TenantEnquiry { TenancyId = t.Id, Topic = topic, Subject = subject, Message = message, CreatedAt = created, CreatedBy = t.Tenants.First().FullName, AssignedTo = t.Property.PatchCode };
                if ((_today - created).TotalDays > 6)
                {
                    e.Response = "Thank you for getting in touch. Your housing officer will call you this week.";
                    e.RespondedAt = created.AddDays(2);
                    e.RespondedBy = @"CORP\j.okafor";
                }
                _db.Enquiries.Add(e);
            }
            _db.SaveChanges();
        }
    }
}
