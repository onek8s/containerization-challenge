using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Fabrikam.Housing.Common;
using Fabrikam.Housing.Data;
using Fabrikam.Housing.Jobs.Infrastructure;
using Fabrikam.Housing.Repairs;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fabrikam.Housing.Jobs.Jobs
{
    /// <summary>
    /// Tells contractors once when a job goes past its target date and chases emergencies that nobody has accepted.
    /// </summary>
    [AutomaticRetry(Attempts = 0)]
    [DisableConcurrentExecution(600)]
    public class RepairsSlaMonitorJob : IHousingJob
    {
        private readonly HousingDbContext _db;
        private readonly TenantMessenger _messenger;
        private readonly IClock _clock;
        private readonly JobsOptions _options;
        private readonly ILogger<RepairsSlaMonitorJob> _logger;

        public RepairsSlaMonitorJob(HousingDbContext db, TenantMessenger messenger, IClock clock, IOptions<JobsOptions> options, ILogger<RepairsSlaMonitorJob> logger)
        {
            _db = db;
            _messenger = messenger;
            _clock = clock;
            _options = options.Value;
            _logger = logger;
        }

        public async Task RunAsync()
        {
            var now = _clock.Now;
            var breached = await _db.WorkOrders.Include(w => w.Contractor).Include(w => w.Property)
                .Where(w => w.Status != WorkOrderStatus.Completed && w.Status != WorkOrderStatus.Cancelled && w.Status != WorkOrderStatus.OnHold
                    && w.TargetDate < now && !w.SlaBreachNotified && w.ContractorId != null)
                .ToListAsync();

            foreach (var group in breached.GroupBy(w => w.Contractor))
            {
                var rows = string.Join("", group.OrderBy(w => w.TargetDate).Select(w =>
                    $"<tr><td>{w.Reference}</td><td>{w.Priority}</td><td>{WebUtility.HtmlEncode(w.Property.AddressLine1)}, {w.Property.Postcode}</td><td>{w.TargetDate:dd/MM/yyyy HH:mm}</td><td>{w.Status}</td></tr>"));
                await _messenger.EmailAsync(null, group.Key.Id, group.Key.ContactEmail, "SlaBreach",
                    $"Fabrikam Homes: {group.Count()} job(s) past target",
                    $"<p>The following Fabrikam Homes jobs have passed their target date. Please complete them or update the appointment in the contractor portal.</p><table border=\"1\" cellpadding=\"4\"><tr><th>Job</th><th>Priority</th><th>Address</th><th>Target</th><th>Status</th></tr>{rows}</table>");
                foreach (var w in group)
                {
                    w.SlaBreachNotified = true;
                    w.Events.Add(new WorkOrderEvent { At = now, Actor = "housing-jobs", Text = "Target date passed; contractor notified" });
                }
            }

            var chaseBefore = now.AddHours(-2);
            var unaccepted = await _db.WorkOrders.Include(w => w.Contractor).Include(w => w.Property)
                .Where(w => w.Priority == RepairPriority.Emergency
                    && ((w.Status == WorkOrderStatus.Assigned && w.AssignedAt < chaseBefore)
                        || ((w.Status == WorkOrderStatus.Reported || w.Status == WorkOrderStatus.Triaged) && w.ReportedAt < now.AddHours(-1))))
                .ToListAsync();
            if (unaccepted.Count > 0)
            {
                var rows = string.Join("", unaccepted.Select(w =>
                    $"<tr><td>{w.Reference}</td><td>{WebUtility.HtmlEncode(w.Description)}</td><td>{WebUtility.HtmlEncode(w.Property.AddressLine1)}</td><td>{w.Contractor?.Code ?? "unassigned"}</td><td>{w.ReportedAt:dd/MM HH:mm}</td></tr>"));
                await _messenger.StaffEmailAsync(_options.RepairsDeskEmail, "EmergencyChase", $"{unaccepted.Count} emergency repair(s) not accepted",
                    $"<table border=\"1\" cellpadding=\"4\"><tr><th>Job</th><th>Repair</th><th>Address</th><th>Contractor</th><th>Reported</th></tr>{rows}</table>");
            }
            await _db.SaveChangesAsync();
            _logger.LogInformation("SLA monitor: {Breached} newly breached, {Unaccepted} emergencies not accepted", breached.Count, unaccepted.Count);
        }
    }
}
