using System.Net.Mail;
using System.Text;
using FurnaceRecords.Data;
using FurnaceRecords.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FurnaceRecords.Worker;

/// <summary>Daily mail listing sensors, surveys and accuracy tests that are due soon or overdue.</summary>
internal sealed class CalibrationReminder : BackgroundService
{
    private const string JobName = "calibration-reminder";

    private readonly IServiceScopeFactory _scopes;
    private readonly WorkerOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger<CalibrationReminder> _logger;

    public CalibrationReminder(IServiceScopeFactory scopes, WorkerOptions options, TimeProvider time, ILogger<CalibrationReminder> logger)
    {
        _scopes = scopes;
        _options = options;
        _time = time;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunIfDueAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Calibration reminder failed");
            }
            await Task.Delay(TimeSpan.FromMinutes(5), _time, stoppingToken);
        }
    }

    internal async Task<bool> RunIfDueAsync(CancellationToken ct)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(_options.TimeZone);
        var nowUtc = _time.GetUtcNow().UtcDateTime;
        var local = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, zone);
        if (TimeOnly.FromDateTime(local) < _options.ReminderTime)
        {
            return false;
        }

        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FurnaceRecordsContext>();
        var run = await db.JobRuns.FindAsync(new object[] { JobName }, ct);
        if (run is not null && TimeZoneInfo.ConvertTimeFromUtc(run.LastRunUtc, zone).Date == local.Date)
        {
            return false;
        }

        var today = DateOnly.FromDateTime(local);
        var limit = today.AddDays(_options.ReminderDays);
        var furnaces = await db.Furnaces.AsNoTracking().Include(f => f.Channels).Where(f => f.Active).OrderBy(f => f.Code).ToListAsync(ct);
        var lines = new List<string>();
        foreach (var f in furnaces)
        {
            if (f.UniformitySurveyDue <= limit) lines.Add($"{f.Code} uniformity survey due {f.UniformitySurveyDue:yyyy-MM-dd}{Overdue(f.UniformitySurveyDue, today)}");
            if (f.AccuracyTestDue <= limit) lines.Add($"{f.Code} system accuracy test due {f.AccuracyTestDue:yyyy-MM-dd}{Overdue(f.AccuracyTestDue, today)}");
            foreach (var c in f.Channels.Where(c => c.Enabled && c.CalibrationDue <= limit).OrderBy(c => c.CalibrationDue))
            {
                lines.Add($"{f.Code} channel {c.Channel} {c.Name} ({c.SensorSerial}) calibration due {c.CalibrationDue:yyyy-MM-dd}{Overdue(c.CalibrationDue, today)}");
            }
        }

        if (lines.Count > 0)
        {
            var body = new StringBuilder();
            body.AppendLine($"Due within {_options.ReminderDays} days or overdue on {today:yyyy-MM-dd}:");
            body.AppendLine();
            foreach (var line in lines) body.AppendLine(" - " + line);
            await SendAsync($"Furnace Records: {lines.Count} calibration items due", body.ToString(), ct);
        }

        if (run is null)
        {
            db.JobRuns.Add(run = new JobRun { Job = JobName });
        }
        run.LastRunUtc = nowUtc;
        run.Result = $"{lines.Count} items";
        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Calibration reminder: {Count} items", lines.Count);
        return true;
    }

    private static string Overdue(DateOnly? due, DateOnly today) => due < today ? " (OVERDUE)" : "";

    private async Task SendAsync(string subject, string body, CancellationToken ct)
    {
        using var message = new MailMessage
        {
            From = new MailAddress(_options.MailFrom),
            Subject = subject,
            Body = body,
            BodyEncoding = Encoding.UTF8,
            BodyTransferEncoding = System.Net.Mime.TransferEncoding.EightBit,
        };
        foreach (var to in _options.ReminderRecipients) message.To.Add(to);
        using var smtp = new SmtpClient(_options.SmtpHost, _options.SmtpPort) { UseDefaultCredentials = true };
        if (!string.IsNullOrEmpty(_options.MailPickupFolder))
        {
            Directory.CreateDirectory(_options.MailPickupFolder);
            smtp.DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory;
            smtp.PickupDirectoryLocation = Path.GetFullPath(_options.MailPickupFolder);
        }
        await smtp.SendMailAsync(message, ct);
    }
}
