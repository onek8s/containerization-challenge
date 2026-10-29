using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fabrikam.Housing.Assets;
using Fabrikam.Housing.Common;
using Fabrikam.Housing.Communications;
using Fabrikam.Housing.Compliance;
using Fabrikam.Housing.Contractors;
using Fabrikam.Housing.Data;
using Fabrikam.Housing.Jobs.Infrastructure;
using Fabrikam.Housing.Repairs;
using Fabrikam.Housing.Tenancies;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fabrikam.Housing.Jobs.Jobs
{
    /// <summary>
    /// Gas safety access process: reminders at 56, 28 and 14 days before the certificate expires, and the
    /// annual service raised as a planned job for the gas contractor with the first reminder.
    /// </summary>
    [AutomaticRetry(Attempts = 1)]
    [DisableConcurrentExecution(1800)]
    public class ComplianceRemindersJob : IHousingJob
    {
        private const string ServiceSorCode = "HT0301";

        private readonly HousingDbContext _db;
        private readonly TenantMessenger _messenger;
        private readonly RepairRules _rules;
        private readonly CounterService _counters;
        private readonly IClock _clock;
        private readonly JobsOptions _options;
        private readonly ILogger<ComplianceRemindersJob> _logger;

        public ComplianceRemindersJob(HousingDbContext db, TenantMessenger messenger, RepairRules rules, CounterService counters, IClock clock,
            IOptions<JobsOptions> options, ILogger<ComplianceRemindersJob> logger)
        {
            _db = db;
            _messenger = messenger;
            _rules = rules;
            _counters = counters;
            _clock = clock;
            _options = options.Value;
            _logger = logger;
        }

        public async Task RunAsync()
        {
            var today = _clock.Today;
            var horizon = today.AddDays(56);
            var certificates = await _db.Certificates.Include(c => c.Property)
                .Where(c => c.Type == ComplianceType.GasSafety && !c.Superseded && c.PropertyId != null && c.ExpiresOn <= horizon)
                .ToListAsync();
            var propertyIds = certificates.Select(c => c.PropertyId.Value).ToList();
            var tenancies = (await _db.Tenancies.Include(t => t.Tenants)
                    .Where(t => t.Status != TenancyStatus.Ended).ToListAsync())
                .Where(t => propertyIds.Contains(t.PropertyId)).ToDictionary(t => t.PropertyId);
            var reminders = (await _db.ComplianceReminders.Where(r => r.Type == ComplianceType.GasSafety).ToListAsync())
                .Where(r => propertyIds.Contains(r.PropertyId)).ToList();
            var gasContractors = await _db.Contractors.Where(c => c.Active && c.GasSafeRegistered).ToListAsync();
            var patches = await _db.Patches.ToDictionaryAsync(p => p.Code);

            int sent = 0, raised = 0;
            var overdue = new List<string>();
            foreach (var certificate in certificates)
            {
                var property = certificate.Property;
                if (property.Status != PropertyStatus.Let || !tenancies.TryGetValue(property.Id, out var tenancy)) continue;
                if (certificate.ExpiresOn < today) overdue.Add($"{property.Reference} {property.AddressLine1} (expired {certificate.ExpiresOn:dd/MM/yyyy})");

                var due = ComplianceRules.ReminderStageDue(certificate.ExpiresOn, today);
                var last = reminders.Where(r => r.PropertyId == property.Id && r.DueDate == certificate.ExpiresOn).Select(r => r.Stage).DefaultIfEmpty(0).Max();
                if (due <= last) continue;

                var service = await ServiceOrder(property, tenancy, certificate, gasContractors, due);
                if (service.Raised) raised++;
                var appointment = service.Order?.Appointments.Where(a => !a.Cancelled).OrderByDescending(a => a.Date).FirstOrDefault();
                var lead = tenancy.Tenants.FirstOrDefault(t => t.IsLeadTenant) ?? tenancy.Tenants.First();
                var channel = await Remind(tenancy, lead, certificate, due, appointment, service.Order?.Contractor ?? gasContractors.FirstOrDefault());

                if (due == 3 && patches.TryGetValue(property.PatchCode ?? "", out var patch) && !string.IsNullOrEmpty(patch.OfficerEmail))
                {
                    await _messenger.StaffEmailAsync(patch.OfficerEmail, "GasAccessFinal",
                        $"Gas safety check due {certificate.ExpiresOn:dd/MM/yyyy}: {property.AddressLine1}",
                        $"<p>The final gas access letter has been sent to {tenancy.Reference}, {property.SingleLineAddress}.</p><p>The certificate expires on {certificate.ExpiresOn:dd/MM/yyyy}. Please contact the tenant to arrange access.</p>");
                }

                var reminder = new ComplianceReminder { PropertyId = property.Id, Type = ComplianceType.GasSafety, DueDate = certificate.ExpiresOn, Stage = due, SentAt = _clock.Now, Channel = channel };
                _db.ComplianceReminders.Add(reminder);
                reminders.Add(reminder);
                sent++;
                await _db.SaveChangesAsync();
            }

            if (overdue.Count > 0)
            {
                await _messenger.StaffEmailAsync(_options.ComplianceTeamEmail, "GasOverdue", $"{overdue.Count} gas safety certificates have expired",
                    "<p>The following homes do not have a current gas safety record:</p><ul>" + string.Join("", overdue.Select(o => "<li>" + System.Net.WebUtility.HtmlEncode(o) + "</li>")) + "</ul>");
            }
            await _db.SaveChangesAsync();
            _logger.LogInformation("Gas reminders: {Sent} reminders, {Raised} service jobs raised, {Overdue} overdue", sent, raised, overdue.Count);
        }

        private async Task<string> Remind(Tenancy tenancy, Tenant lead, ComplianceCertificate certificate, int stage, Appointment appointment, Contractor contractor)
        {
            if (stage == 1 && lead.ContactPreference != ContactPreference.Letter)
            {
                if (!string.IsNullOrEmpty(lead.MobilePhone) && lead.ContactPreference == ContactPreference.Sms)
                {
                    await _messenger.SmsAsync(tenancy.Id, lead.MobilePhone, "GasAccess1", SmsTemplates.GasServiceReminder(lead.FirstName, certificate.ExpiresOn));
                    return "Sms";
                }
                if (!string.IsNullOrEmpty(lead.Email))
                {
                    await _messenger.EmailAsync(tenancy.Id, null, lead.Email, "GasAccess1", "Your annual gas safety check",
                        $"<p>Dear {lead.FirstName},</p><p>Your annual gas safety check is due by {certificate.ExpiresOn:d MMMM yyyy}. {contractor?.Name} will contact you to arrange a visit, or you can book a time at tenants.fabrikamhomes.co.uk.</p>");
                    return "Email";
                }
            }
            await _messenger.LetterAsync(tenancy, "GasAccess" + stage, new Dictionary<string, string>
            {
                ["ExpiryDate"] = certificate.ExpiresOn.ToString("d MMMM yyyy"),
                ["AppointmentDate"] = appointment?.Date.ToString("dddd d MMMM yyyy") ?? "to be arranged",
                ["AppointmentSlot"] = appointment == null ? "" : appointment.Slot == AppointmentSlot.Morning ? "8am to 12pm" : appointment.Slot == AppointmentSlot.Afternoon ? "12pm to 5pm" : "8am to 5pm",
                ["ContractorName"] = contractor?.Name ?? "our gas contractor",
                ["PropertyAddress"] = tenancy.Property.SingleLineAddress
            });
            return "Letter";
        }

        private async Task<(WorkOrder Order, bool Raised)> ServiceOrder(Property property, Tenancy tenancy, ComplianceCertificate certificate, List<Contractor> gasContractors, int stage)
        {
            var open = await _db.WorkOrders.Include(w => w.Appointments).Include(w => w.Contractor)
                .Where(w => w.PropertyId == property.Id && w.Status != WorkOrderStatus.Completed && w.Status != WorkOrderStatus.Cancelled
                    && w.Lines.Any(l => l.SorCode == ServiceSorCode))
                .FirstOrDefaultAsync();
            if (open != null || stage != 1) return (open, false);

            var contractor = gasContractors.FirstOrDefault(c => c.Id == certificate.ContractorId)
                ?? gasContractors.FirstOrDefault(c => c.CoversPatch(property.PatchCode));
            var item = await _db.ScheduleOfRates.SingleAsync(s => s.Code == ServiceSorCode);
            var now = _clock.Now;
            var sequence = await _counters.NextAsync("WorkOrder" + now.Year);
            var order = new WorkOrder
            {
                Reference = RepairRules.NextReference(now.Year, sequence),
                PropertyId = property.Id,
                TenancyId = tenancy.Id,
                Channel = ReportChannel.Inspection,
                ReportedBy = "Compliance",
                Location = "Heating and hot water",
                Description = $"Annual gas safety check and service. Current certificate {certificate.CertificateNumber} expires {certificate.ExpiresOn:dd/MM/yyyy}.",
                Trade = Trade.Heating,
                Priority = RepairPriority.Planned,
                Status = contractor == null ? WorkOrderStatus.Triaged : WorkOrderStatus.Assigned,
                ReportedAt = now,
                // The service must be done before the certificate runs out, not within the normal 90 days.
                TargetDate = certificate.ExpiresOn.Date.AddHours(17).AddMinutes(30),
                ContractorId = contractor?.Id,
                Contractor = contractor,
                AssignedAt = contractor == null ? (DateTime?)null : now,
                CreatedAt = now,
                CreatedBy = "housing-jobs"
            };
            order.Lines.Add(new WorkOrderLine { SorCode = item.Code, Description = item.Description, Quantity = 1, UnitPrice = item.UnitPrice });
            order.Events.Add(new WorkOrderEvent { At = now, Actor = "housing-jobs", NewStatus = order.Status, Text = "Raised by gas safety reminders" });
            _db.WorkOrders.Add(order);
            return (order, true);
        }
    }
}
