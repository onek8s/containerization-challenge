using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Fabrikam.Housing.Common;
using Fabrikam.Housing.Data;
using Fabrikam.Housing.Documents;
using Fabrikam.Housing.Jobs.Infrastructure;
using Fabrikam.Housing.Rent;
using Fabrikam.Housing.Tenancies;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fabrikam.Housing.Jobs.Jobs
{
    /// <summary>
    /// Imports the council's weekly housing benefit landlord schedules. Bristol City Council e-mails the
    /// schedule to the income team, who save it into the inbound folder.
    /// </summary>
    [AutomaticRetry(Attempts = 0)]
    [DisableConcurrentExecution(900)]
    public class HousingBenefitImportJob : IHousingJob
    {
        private readonly HousingDbContext _db;
        private readonly IHousingBenefitScheduleReader _reader;
        private readonly TenantMessenger _messenger;
        private readonly IClock _clock;
        private readonly JobsOptions _options;
        private readonly ILogger<HousingBenefitImportJob> _logger;

        public HousingBenefitImportJob(HousingDbContext db, IHousingBenefitScheduleReader reader, TenantMessenger messenger, IClock clock,
            IOptions<JobsOptions> options, ILogger<HousingBenefitImportJob> logger)
        {
            _db = db;
            _reader = reader;
            _messenger = messenger;
            _clock = clock;
            _options = options.Value;
            _logger = logger;
        }

        public async Task RunAsync()
        {
            var inbound = _options.HousingBenefitInbound;
            if (!Directory.Exists(inbound))
            {
                _logger.LogWarning("Housing benefit inbound folder {Folder} does not exist", inbound);
                return;
            }
            var files = Directory.GetFiles(inbound, "*.xls").Concat(Directory.GetFiles(inbound, "*.xlsx")).Distinct().OrderBy(f => File.GetLastWriteTime(f)).ToList();
            foreach (var file in files)
            {
                try
                {
                    await ImportAsync(file);
                    Move(file, "Processed");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Housing benefit schedule {File} could not be imported", file);
                    Move(file, "Failed");
                    await _messenger.StaffEmailAsync(_options.IncomeTeamEmail, "HbImportFailed", "Housing benefit schedule not imported: " + Path.GetFileName(file),
                        "<p>The schedule could not be read and has been moved to the Failed folder.</p><p>" + WebUtility.HtmlEncode(ex.Message) + "</p>");
                    await _db.SaveChangesAsync();
                }
            }
        }

        private async Task ImportAsync(string file)
        {
            var name = Path.GetFileName(file);
            if (await _db.HousingBenefitImports.AnyAsync(i => i.FileName == name))
                throw new InvalidOperationException("A schedule called " + name + " has already been imported.");

            IList<HousingBenefitLine> lines;
            using (var stream = File.OpenRead(file))
                lines = _reader.Read(stream, name);

            var tenancies = await _db.Tenancies.Include(t => t.Property).Where(t => t.Status != TenancyStatus.Ended).ToListAsync();
            var byClaim = tenancies.Where(t => !string.IsNullOrEmpty(t.HousingBenefitClaimRef))
                .GroupBy(t => t.HousingBenefitClaimRef.Trim().ToUpperInvariant()).ToDictionary(g => g.Key, g => g.First());

            var unmatched = new List<HousingBenefitLine>();
            var matched = 0;
            decimal total = 0;
            foreach (var line in lines)
            {
                var tenancy = Match(line, byClaim, tenancies);
                if (tenancy == null)
                {
                    unmatched.Add(line);
                    continue;
                }
                if (string.IsNullOrEmpty(tenancy.HousingBenefitClaimRef) && !string.IsNullOrEmpty(line.ClaimReference))
                {
                    tenancy.HousingBenefitClaimRef = line.ClaimReference.Trim();
                    tenancy.HousingBenefitClaimed = true;
                }
                _db.RentTransactions.Add(new RentTransaction
                {
                    TenancyId = tenancy.Id,
                    PostedOn = _clock.Now,
                    RentWeek = RentCalendar.WeekStart(line.PeriodFrom),
                    Type = TransactionType.HousingBenefit,
                    Amount = -Money.Round(line.Amount),
                    Reference = line.ClaimReference,
                    Description = $"Housing benefit {line.PeriodFrom:dd/MM/yy} to {line.PeriodTo:dd/MM/yy}",
                    Source = "HBImport"
                });
                matched++;
                total += line.Amount;
            }

            _db.HousingBenefitImports.Add(new HousingBenefitImport
            {
                FileName = name,
                ImportedAt = _clock.Now,
                Lines = lines.Count,
                Matched = matched,
                Total = Money.Round(total),
                Unmatched = unmatched.Count == 0 ? null : string.Join("; ", unmatched.Select(u => $"row {u.RowNumber} {u.ClaimReference} {u.AddressLine1} {u.Postcode} {u.Amount:N2}"))
            });
            await _db.SaveChangesAsync();

            if (unmatched.Count > 0)
            {
                var rows = string.Join("", unmatched.Select(u =>
                    $"<tr><td>{u.RowNumber}</td><td>{WebUtility.HtmlEncode(u.ClaimReference)}</td><td>{WebUtility.HtmlEncode(u.ClaimantName)}</td><td>{WebUtility.HtmlEncode(u.AddressLine1)}, {WebUtility.HtmlEncode(u.Postcode)}</td><td>{u.Amount:N2}</td></tr>"));
                await _messenger.StaffEmailAsync(_options.IncomeTeamEmail, "HbUnmatched", $"Housing benefit schedule {name}: {unmatched.Count} lines not matched",
                    $"<p>{matched} of {lines.Count} lines were posted. Please post these by hand:</p><table border=\"1\" cellpadding=\"4\"><tr><th>Row</th><th>Claim</th><th>Claimant</th><th>Address</th><th>Amount</th></tr>{rows}</table>");
                await _db.SaveChangesAsync();
            }
            _logger.LogInformation("Housing benefit schedule {File}: {Matched}/{Lines} lines posted, total {Total:N2}", name, matched, lines.Count, total);
        }

        private static Tenancy Match(HousingBenefitLine line, IDictionary<string, Tenancy> byClaim, IList<Tenancy> tenancies)
        {
            if (!string.IsNullOrWhiteSpace(line.ClaimReference) && byClaim.TryGetValue(line.ClaimReference.Trim().ToUpperInvariant(), out var t))
                return t;
            if (!Postcode.TryNormalise(line.Postcode, out var postcode)) return null;
            var address = Normalise(line.AddressLine1);
            var candidates = tenancies.Where(x => Postcode.TryNormalise(x.Property.Postcode, out var p) && p == postcode
                && Normalise(x.Property.AddressLine1) == address).ToList();
            return candidates.Count == 1 ? candidates[0] : null;
        }

        private static string Normalise(string s) =>
            new string((s ?? "").ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());

        private void Move(string file, string folder)
        {
            var target = Path.Combine(_options.HousingBenefitInbound, folder, _clock.Today.ToString("yyyy"));
            Directory.CreateDirectory(target);
            var destination = Path.Combine(target, _clock.Now.ToString("yyyyMMdd_HHmmss_") + Path.GetFileName(file));
            File.Move(file, destination);
        }
    }
}
