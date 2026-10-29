using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Fabrikam.Housing.Common;
using Fabrikam.Housing.Communications;
using Fabrikam.Housing.Data;
using Fabrikam.Housing.Data.Queries;
using Fabrikam.Housing.Jobs.Infrastructure;
using Fabrikam.Housing.Rent;
using Fabrikam.Housing.Tenancies;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fabrikam.Housing.Jobs.Jobs
{
    /// <summary>
    /// Daily arrears review under policy IN-04: reminder texts, arrears letters for the post room and
    /// referrals to the housing officer for cases that need a person to look at them.
    /// </summary>
    [AutomaticRetry(Attempts = 1)]
    [DisableConcurrentExecution(1800)]
    public class ArrearsReviewJob : IHousingJob
    {
        private readonly HousingDbContext _db;
        private readonly RentAccountQueries _accounts;
        private readonly ArrearsPolicy _policy;
        private readonly TenantMessenger _messenger;
        private readonly IClock _clock;
        private readonly ILogger<ArrearsReviewJob> _logger;

        public ArrearsReviewJob(HousingDbContext db, RentAccountQueries accounts, ArrearsPolicy policy, TenantMessenger messenger, IClock clock, ILogger<ArrearsReviewJob> logger)
        {
            _db = db;
            _accounts = accounts;
            _policy = policy;
            _messenger = messenger;
            _clock = clock;
            _logger = logger;
        }

        public async Task RunAsync()
        {
            var today = _clock.Today;
            var balances = await _accounts.BalancesAsync();
            var cases = (await _db.ArrearsCases.Include(c => c.Notes).Where(c => c.ClosedOn == null).ToListAsync())
                .GroupBy(c => c.TenancyId).ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.Id).First());
            var candidates = balances.Where(b => b.Value > 0).Select(b => b.Key).Union(cases.Keys).ToHashSet();

            var tenancies = await _db.Tenancies.Include(t => t.Property).Include(t => t.Tenants)
                .Where(t => t.Status != TenancyStatus.Ended).ToListAsync();
            tenancies = tenancies.Where(t => candidates.Contains(t.Id)).ToList();

            var from = today.AddDays(-35);
            var recent = (await _db.RentTransactions.AsNoTracking().Where(t => t.PostedOn >= from).ToListAsync())
                .GroupBy(t => t.TenancyId).ToDictionary(g => g.Key, g => g.ToList());
            var arrangements = (await _db.Arrangements.Where(a => a.Status == ArrangementStatus.Active).ToListAsync())
                .GroupBy(a => a.TenancyId).ToDictionary(g => g.Key, g => g.First());
            var patches = await _db.Patches.ToDictionaryAsync(p => p.Code);

            var counts = new Dictionary<ArrearsAction, int>();
            var letters = 0;
            foreach (var tenancy in tenancies)
            {
                balances.TryGetValue(tenancy.Id, out var balance);
                cases.TryGetValue(tenancy.Id, out var current);
                recent.TryGetValue(tenancy.Id, out var lines);
                arrangements.TryGetValue(tenancy.Id, out var arrangement);
                var decision = _policy.Evaluate(tenancy, balance, lines ?? new List<RentTransaction>(), current, arrangement, today);

                if (decision.Stage == ArrearsStage.None && current != null)
                {
                    current.ClosedOn = today;
                    AddNote(current, $"Case closed: balance {balance:N2}");
                    continue;
                }

                counts[decision.Action] = counts.TryGetValue(decision.Action, out var n) ? n + 1 : 1;
                var lead = tenancy.Tenants.FirstOrDefault(t => t.IsLeadTenant) ?? tenancy.Tenants.FirstOrDefault();
                switch (decision.Action)
                {
                    case ArrearsAction.SendSms:
                        current = Escalate(tenancy, current, decision, balance);
                        if (lead != null && !string.IsNullOrEmpty(lead.MobilePhone))
                        {
                            await _messenger.SmsAsync(tenancy.Id, lead.MobilePhone, "ArrearsReminder", SmsTemplates.ArrearsReminder(lead.FirstName, balance));
                            AddNote(current, "Reminder text sent to " + lead.MobilePhone);
                        }
                        else if (lead != null && !string.IsNullOrEmpty(lead.Email))
                        {
                            await _messenger.EmailAsync(tenancy.Id, null, lead.Email, "ArrearsReminder", "Your rent account",
                                $"<p>Dear {lead.FirstName},</p><p>Your rent account is {balance:N2} GBP in arrears. Please pay at tenants.fabrikamhomes.co.uk or call 0300 123 4567 if you need help.</p>");
                            AddNote(current, "Reminder e-mail sent to " + lead.Email);
                        }
                        else
                        {
                            AddNote(current, "No mobile or e-mail: reminder not sent");
                        }
                        break;

                    case ArrearsAction.SendLetter:
                        var previous = current;
                        current = Escalate(tenancy, current, decision, balance);
                        var template = decision.Stage == ArrearsStage.FirstLetter ? "ArrearsStage2" : "ArrearsStage3";
                        var payment = (lines ?? new List<RentTransaction>()).Where(t => RentTransaction.IsPayment(t.Type)).OrderByDescending(t => t.PostedOn).FirstOrDefault();
                        patches.TryGetValue(tenancy.Property.PatchCode ?? "", out var patch);
                        await _messenger.LetterAsync(tenancy, template, new Dictionary<string, string>
                        {
                            ["Balance"] = balance.ToString("N2", CultureInfo.GetCultureInfo("en-GB")),
                            ["WeeksInArrears"] = decision.WeeksInArrears.ToString("0.0", CultureInfo.InvariantCulture),
                            ["WeeklyCharge"] = tenancy.WeeklyCharge.ToString("N2", CultureInfo.GetCultureInfo("en-GB")),
                            ["LastPaymentAmount"] = payment == null ? "none" : (-payment.Amount).ToString("N2", CultureInfo.GetCultureInfo("en-GB")),
                            ["LastPaymentDate"] = payment == null ? "in the last five weeks" : payment.PostedOn.ToString("d MMMM yyyy"),
                            ["PreviousLetterDate"] = previous?.StageReachedOn.ToString("d MMMM yyyy") ?? "",
                            ["RespondBy"] = today.AddDays(14).ToString("d MMMM yyyy"),
                            ["OfficerName"] = patch?.OfficerName ?? "Income Team",
                            ["PropertyAddress"] = tenancy.Property.SingleLineAddress
                        });
                        AddNote(current, template + " letter produced for printing");
                        letters++;
                        break;

                    case ArrearsAction.OfficerReview:
                        if (current != null && current.NextReviewOn.HasValue && current.NextReviewOn.Value > today) break;
                        if (current == null)
                        {
                            current = NewCase(tenancy, (ArrearsStage)Math.Max(1, (int)decision.Stage - 1), balance);
                        }
                        current.NextReviewOn = today.AddDays(7);
                        AddNote(current, "Referred to housing officer: " + decision.Reason);
                        if (patches.TryGetValue(tenancy.Property.PatchCode ?? "", out var officerPatch) && !string.IsNullOrEmpty(officerPatch.OfficerEmail))
                        {
                            await _messenger.StaffEmailAsync(officerPatch.OfficerEmail, "ArrearsReferral",
                                $"Arrears review: {tenancy.Reference} {tenancy.Property.AddressLine1}",
                                $"<p>{tenancy.Reference}, {tenancy.Property.SingleLineAddress}</p><p>Balance {balance:N2} ({decision.WeeksInArrears} weeks).</p><p>{decision.Reason}</p><p>Open the case in the housing system to decide the next step.</p>");
                        }
                        break;
                }
            }
            await _db.SaveChangesAsync();
            _logger.LogInformation("Arrears review: {Count} accounts reviewed, actions {Actions}, {Letters} letters", tenancies.Count,
                string.Join(", ", counts.Select(kv => kv.Key + "=" + kv.Value)), letters);
        }

        private ArrearsCase Escalate(Tenancy tenancy, ArrearsCase current, ArrearsDecision decision, decimal balance)
        {
            if (current == null) return NewCase(tenancy, decision.Stage, balance);
            current.Stage = decision.Stage;
            current.StageReachedOn = _clock.Today;
            current.BalanceAtStage = balance;
            current.ModifiedAt = _clock.Now;
            current.ModifiedBy = "housing-jobs";
            AddNote(current, $"Moved to stage {decision.Stage}: {decision.Reason}");
            return current;
        }

        private ArrearsCase NewCase(Tenancy tenancy, ArrearsStage stage, decimal balance)
        {
            var c = new ArrearsCase
            {
                TenancyId = tenancy.Id,
                Stage = stage,
                BalanceAtStage = balance,
                StageReachedOn = _clock.Today,
                AssignedTo = tenancy.Property.PatchCode,
                CreatedAt = _clock.Now,
                CreatedBy = "housing-jobs"
            };
            AddNote(c, $"Case opened at stage {stage}, balance {balance:N2}");
            _db.ArrearsCases.Add(c);
            return c;
        }

        private void AddNote(ArrearsCase c, string text)
        {
            c.Notes.Add(new ArrearsCaseNote { CreatedAt = _clock.Now, CreatedBy = "housing-jobs", Text = text });
        }
    }
}
