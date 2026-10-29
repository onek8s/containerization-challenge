using System;
using System.Linq;
using System.Threading.Tasks;
using Fabrikam.Housing.Common;
using Fabrikam.Housing.Data;
using Fabrikam.Housing.Integration.Payments;
using Fabrikam.Housing.Rent;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fabrikam.Housing.Jobs.Jobs
{
    /// <summary>
    /// Submits direct debit collections to Paystream three working days before the charge date and
    /// moves each mandate on to its next collection date.
    /// </summary>
    [AutomaticRetry(Attempts = 3)]
    [DisableConcurrentExecution(600)]
    public class DirectDebitCollectionsJob : IHousingJob
    {
        private readonly HousingDbContext _db;
        private readonly IPaymentsGateway _payments;
        private readonly CollectionSchedule _schedule;
        private readonly WorkingCalendar _calendar;
        private readonly IClock _clock;
        private readonly ILogger<DirectDebitCollectionsJob> _logger;

        public DirectDebitCollectionsJob(HousingDbContext db, IPaymentsGateway payments, CollectionSchedule schedule, WorkingCalendar calendar,
            IClock clock, ILogger<DirectDebitCollectionsJob> logger)
        {
            _db = db;
            _payments = payments;
            _schedule = schedule;
            _calendar = calendar;
            _clock = clock;
            _logger = logger;
        }

        public async Task RunAsync()
        {
            var today = _clock.Today;

            foreach (var pending in await _db.Mandates.Where(m => m.Status == MandateStatus.PendingSubmission).ToListAsync())
            {
                pending.Status = MandateStatus.Submitted;
                pending.ModifiedAt = _clock.Now;
                pending.ModifiedBy = "housing-jobs";
            }
            await _db.SaveChangesAsync();

            var latestAllowed = _calendar.AddWorkingDays(today, CollectionSchedule.SubmissionLeadWorkingDays).Date;
            var mandates = await _db.Mandates.Include(m => m.Tenancy)
                .Where(m => m.Status == MandateStatus.Active && m.NextCollectionDate != null && m.NextCollectionDate <= latestAllowed)
                .ToListAsync();

            int submitted = 0, missed = 0, failed = 0;
            foreach (var mandate in mandates)
            {
                var chargeDate = mandate.NextCollectionDate.Value.Date;
                // A collection date already inside the Bacs lead time cannot be collected any more.
                while (chargeDate < latestAllowed)
                {
                    _logger.LogWarning("Mandate {Mandate}: collection on {Date:dd/MM/yyyy} missed the submission deadline", mandate.ProviderMandateId, chargeDate);
                    chargeDate = _schedule.Next(mandate, chargeDate);
                    missed++;
                }
                if (!_schedule.IsDueForSubmission(chargeDate, today))
                {
                    mandate.NextCollectionDate = chargeDate;
                    continue;
                }

                var exists = await _db.DirectDebitPayments.AnyAsync(p => p.MandateId == mandate.Id && p.ChargeDate == chargeDate);
                if (!exists)
                {
                    try
                    {
                        var key = mandate.ProviderMandateId + "-" + chargeDate.ToString("yyyyMMdd");
                        var paymentId = await _payments.CreatePaymentAsync(mandate.ProviderMandateId, mandate.Amount, chargeDate,
                            "RENT " + mandate.Tenancy.Reference, key);
                        _db.DirectDebitPayments.Add(new DirectDebitPayment
                        {
                            MandateId = mandate.Id,
                            ProviderPaymentId = paymentId,
                            ChargeDate = chargeDate,
                            Amount = mandate.Amount,
                            Status = PaymentStatus.Submitted,
                            CreatedAt = _clock.Now
                        });
                        submitted++;
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        _logger.LogError(ex, "Could not submit collection for mandate {Mandate} on {Date:dd/MM/yyyy}", mandate.ProviderMandateId, chargeDate);
                        continue;
                    }
                }
                mandate.NextCollectionDate = _schedule.Next(mandate, chargeDate);
                mandate.ModifiedAt = _clock.Now;
                mandate.ModifiedBy = "housing-jobs";
                await _db.SaveChangesAsync();
            }
            await _db.SaveChangesAsync();
            _logger.LogInformation("Direct debit collections: {Submitted} submitted, {Missed} missed, {Failed} failed", submitted, missed, failed);
            if (failed > 0) throw new InvalidOperationException(failed + " collections could not be submitted; the job will retry.");
        }
    }
}
