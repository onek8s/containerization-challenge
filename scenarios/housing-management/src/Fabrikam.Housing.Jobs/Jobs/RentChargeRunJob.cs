using System.Linq;
using System.Threading.Tasks;
using Fabrikam.Housing.Common;
using Fabrikam.Housing.Data;
using Fabrikam.Housing.Rent;
using Fabrikam.Housing.Tenancies;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fabrikam.Housing.Jobs.Jobs
{
    /// <summary>Posts the weekly rent, service and water charges every Monday morning.</summary>
    [AutomaticRetry(Attempts = 3)]
    [DisableConcurrentExecution(600)]
    public class RentChargeRunJob : IHousingJob
    {
        private readonly HousingDbContext _db;
        private readonly RentChargeRun _run;
        private readonly IClock _clock;
        private readonly ILogger<RentChargeRunJob> _logger;

        public RentChargeRunJob(HousingDbContext db, RentChargeRun run, IClock clock, ILogger<RentChargeRunJob> logger)
        {
            _db = db;
            _run = run;
            _clock = clock;
            _logger = logger;
        }

        public async Task RunAsync()
        {
            var week = RentCalendar.WeekStart(_clock.Today);
            var alreadyCharged = await _db.RentTransactions
                .Where(t => t.RentWeek == week && t.Type == TransactionType.RentCharge)
                .Select(t => t.TenancyId).Distinct().ToListAsync();
            var charged = alreadyCharged.ToHashSet();

            var tenancies = await _db.Tenancies.Include(t => t.Property)
                .Where(t => t.Status != TenancyStatus.Ended || t.EndDate >= week)
                .ToListAsync();

            int posted = 0, lines = 0;
            foreach (var tenancy in tenancies.Where(t => !charged.Contains(t.Id)))
            {
                var charges = _run.ChargesFor(tenancy, week, _clock.Now).ToList();
                if (charges.Count == 0) continue;
                _db.RentTransactions.AddRange(charges);
                lines += charges.Count;
                if (++posted % 200 == 0) await _db.SaveChangesAsync();
            }
            await _db.SaveChangesAsync();
            _logger.LogInformation("Rent run for week commencing {Week:dd/MM/yyyy}: {Tenancies} tenancies charged, {Lines} lines, {Skipped} already charged",
                week, posted, lines, charged.Count);
        }
    }
}
