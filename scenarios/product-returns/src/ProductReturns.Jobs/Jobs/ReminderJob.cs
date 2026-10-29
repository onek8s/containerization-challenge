using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProductReturns.Core;
using ProductReturns.Data;
using ProductReturns.Jobs.Infrastructure;

namespace ProductReturns.Jobs.Jobs
{
    /// <summary>Reminds customers once when the parcel has not arrived some days after the label was issued.</summary>
    public class ReminderJob : IJob
    {
        private readonly ReturnsRepository _repository;
        private readonly ReturnPolicyOptions _policy;
        private readonly JobMailer _mailer;
        private readonly ILogger<ReminderJob> _logger;

        public ReminderJob(ReturnsRepository repository, ReturnPolicyOptions policy, JobMailer mailer, ILogger<ReminderJob> logger)
        {
            _repository = repository;
            _policy = policy;
            _mailer = mailer;
            _logger = logger;
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            var before = DateTime.UtcNow.AddDays(-_policy.ReminderAfterDays);
            var waiting = await _repository.AwaitingReceiptAsync(before, cancellationToken);
            var template = _mailer.Template("reminder.html");
            var sent = 0;

            foreach (var request in waiting)
            {
                if (request.ReminderSentUtc.HasValue || string.IsNullOrEmpty(request.CustomerEmail))
                {
                    continue;
                }

                var body = template
                    .Replace("{{CustomerName}}", WebUtility.HtmlEncode(request.CustomerName))
                    .Replace("{{RmaNumber}}", WebUtility.HtmlEncode(request.RmaNumber))
                    .Replace("{{ExpireDays}}", _policy.ExpireAfterDays.ToString());

                try
                {
                    await _mailer.SendAsync(request.CustomerEmail, request.CustomerName, "Reminder: your return " + request.RmaNumber, body);
                    request.ReminderSentUtc = DateTime.UtcNow;
                    sent++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Reminder for {Rma} failed", request.RmaNumber);
                }
            }

            await _repository.SaveAsync(cancellationToken);
            _logger.LogInformation("{Sent} reminders sent", sent);
        }
    }
}
