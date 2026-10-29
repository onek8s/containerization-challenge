using Humongous.PolicyService.Bordereau;
using Humongous.Quoting.Data;
using Humongous.Quoting.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Humongous.PolicyService.Jobs;

/// <summary>Builds and delivers the premium bordereau for the previous calendar month.</summary>
public sealed class BordereauJob(
    QuotingDbContext db,
    QuotingQueries queries,
    IBordereauDelivery delivery,
    IOptions<BordereauOptions> options,
    TimeProvider clock,
    ILogger<BordereauJob> logger) : IScheduledJob
{
    public string Name => "bordereau";

    /// <summary>Period to run, yyyy-MM. Set by the run-job command; the schedule runs the previous month.</summary>
    public string? Period { get; set; }

    public async Task RunAsync(CancellationToken ct)
    {
        var now = clock.GetLocalNow();
        var month = Period is not null
            ? DateTime.ParseExact(Period, "yyyy-MM", System.Globalization.CultureInfo.InvariantCulture)
            : new DateTime(now.Year, now.Month, 1).AddMonths(-1);
        var period = month.ToString("yyyy-MM");

        if (await db.BordereauRuns.AnyAsync(r => r.Period == period && r.Status == "Delivered", ct) && Period is null)
        {
            logger.LogInformation("Bordereau {Period} already delivered", period);
            return;
        }

        // Transactions are reported in the month they were processed (UK time).
        var from = TimeZoneInfo.ConvertTimeToUtc(month, TimeZoneInfo.Local);
        var to = TimeZoneInfo.ConvertTimeToUtc(month.AddMonths(1), TimeZoneInfo.Local);
        var lines = await queries.BordereauLinesAsync(from, to);

        var umr = options.Value.Umr;
        var fileName = BordereauWriter.FileName(umr, period);
        var content = BordereauWriter.Write(period, umr, lines);
        var run = new BordereauRun
        {
            Id = Guid.NewGuid(),
            Period = period,
            CreatedUtc = DateTime.UtcNow,
            Rows = lines.Count,
            GrossPremium = lines.Sum(l => l.GrossPremium),
            Ipt = lines.Sum(l => l.Ipt),
            Commission = lines.Sum(l => l.Commission),
            FileName = fileName,
            Content = content,
            Status = "Created"
        };
        db.BordereauRuns.Add(run);
        await db.SaveChangesAsync(ct);

        try
        {
            await delivery.DeliverAsync(fileName, content, ct);
            run.Status = "Delivered";
            run.DeliveredUtc = DateTime.UtcNow;
            var ids = lines.Select(l => l.TransactionId).ToList();
            await db.PolicyTransactions.Where(t => ids.Contains(t.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.BordereauRunId, run.Id), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            run.Status = "Failed";
            run.Error = ex.Message;
            logger.LogError(ex, "Bordereau {Period} could not be delivered", period);
        }
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Bordereau {Period}: {Rows} rows, gross {Gross}, {Status}", period, run.Rows, run.GrossPremium, run.Status);
    }
}
