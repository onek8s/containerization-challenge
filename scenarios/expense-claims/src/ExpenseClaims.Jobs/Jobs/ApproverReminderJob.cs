using System.Text;
using ExpenseClaims.Core.Claims;
using ExpenseClaims.Data;
using ExpenseClaims.Data.Entities;
using ExpenseClaims.Jobs.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Quartz;

namespace ExpenseClaims.Jobs.Jobs;

/// <summary>Sends each approver one mail listing the claims that have waited for their decision too long.</summary>
[DisallowConcurrentExecution]
public sealed class ApproverReminderJob : IJob
{
    private readonly ExpenseClaimsDbContext _db;
    private readonly IMailSender _mail;
    private readonly SmtpOptions _smtp;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ApproverReminderJob> _logger;

    public ApproverReminderJob(ExpenseClaimsDbContext db, IMailSender mail, IOptions<SmtpOptions> smtp, IConfiguration configuration, ILogger<ApproverReminderJob> logger)
    {
        _db = db;
        _mail = mail;
        _smtp = smtp.Value;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        var waitDays = _configuration.GetValue("Reminders:AfterDays", 3);
        var now = DateTime.Now;
        var waitingSince = now.AddDays(-waitDays);
        var remindedBefore = now.AddHours(-20);

        var pending = await _db.Approvals
            .Include(a => a.Approver)
            .Include(a => a.Claim).ThenInclude(c => c.Employee)
            .Include(a => a.Claim).ThenInclude(c => c.Approvals)
            .Where(a => a.Decision == ApprovalDecision.Pending
                        && a.Claim.Status == ClaimStatus.Submitted
                        && a.Claim.SubmittedAt < waitingSince
                        && (a.RemindedAt == null || a.RemindedAt < remindedBefore))
            .ToListAsync(ct);

        // Only the approver whose turn it is gets a reminder.
        var current = pending
            .Where(a => a.Claim.Approvals.Where(x => x.Decision == ApprovalDecision.Pending).Min(x => x.Level) == a.Level)
            .GroupBy(a => a.Approver);

        var sent = 0;
        foreach (var group in current)
        {
            var approver = group.Key;
            var body = new StringBuilder();
            body.AppendLine($"Hello {approver.DisplayName},").AppendLine();
            body.AppendLine("The following expense claims are waiting for your approval:").AppendLine();
            foreach (var a in group.OrderBy(a => a.Claim.SubmittedAt))
                body.AppendLine($"  {a.Claim.Number}  {a.Claim.Employee.DisplayName,-28} {a.Claim.Total,10:N2} EUR  submitted {a.Claim.SubmittedAt:d.M.yyyy}");
            body.AppendLine().AppendLine($"Open the approvals view: {_smtp.PortalUrl}approvals").AppendLine();
            body.AppendLine("This is an automatic message from Expense Claims.");

            try
            {
                await _mail.SendAsync(approver.Email, approver.DisplayName, $"Expense claims waiting for your approval ({group.Count()})", body.ToString(), ct);
                foreach (var a in group)
                    a.RemindedAt = now;
                sent++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Reminder to {Approver} could not be sent", approver.Email);
            }
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Approval reminders sent to {Count} approver(s)", sent);
    }
}
