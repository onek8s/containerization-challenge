using System.Net;
using System.Text;
using Quartz;
using Quartz.Impl.Matchers;

namespace ExpenseClaims.Jobs.Status;

public static class JobsStatusPage
{
    public static IEndpointRouteBuilder MapJobsStatus(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/", async (ISchedulerFactory factory, CancellationToken ct) =>
        {
            var scheduler = await factory.GetScheduler(ct);
            var html = new StringBuilder();
            html.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>Expense Claims jobs</title>")
                .Append("<style>body{font-family:Segoe UI,Arial,sans-serif;margin:2em}table{border-collapse:collapse}td,th{border:1px solid #ccc;padding:4px 10px;text-align:left}</style>")
                .Append("</head><body><h1>Expense Claims jobs</h1>")
                .Append($"<p>Scheduler <b>{Encode(scheduler.SchedulerName)}</b> / instance {Encode(scheduler.SchedulerInstanceId)}, ")
                .Append(scheduler.InStandbyMode ? "standby" : scheduler.IsStarted ? "running" : "stopped")
                .Append($", host {Encode(Environment.MachineName)}</p>")
                .Append("<table><tr><th>Job</th><th>Schedule</th><th>Previous run</th><th>Next run</th><th>State</th></tr>");

            foreach (var key in (await scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup(), ct)).OrderBy(k => k.Name))
            {
                foreach (var trigger in await scheduler.GetTriggersOfJob(key, ct))
                {
                    var state = await scheduler.GetTriggerState(trigger.Key, ct);
                    var schedule = trigger is ICronTrigger cron ? cron.CronExpressionString : trigger.Description;
                    html.Append("<tr>")
                        .Append($"<td>{Encode(key.Name)}</td>")
                        .Append($"<td>{Encode(schedule)}</td>")
                        .Append($"<td>{trigger.GetPreviousFireTimeUtc()?.ToLocalTime():yyyy-MM-dd HH:mm:ss}</td>")
                        .Append($"<td>{trigger.GetNextFireTimeUtc()?.ToLocalTime():yyyy-MM-dd HH:mm:ss}</td>")
                        .Append($"<td>{state}</td>")
                        .Append("</tr>");
                }
            }

            html.Append("</table><p>Run a job now from the server: <code>POST /jobs/{name}/run</code></p></body></html>");
            return Results.Content(html.ToString(), "text/html; charset=utf-8");
        });

        // Only accepted from the server itself (operators use it after fixing an interface problem).
        endpoints.MapPost("/jobs/{name}/run", async (string name, HttpContext http, ISchedulerFactory factory, CancellationToken ct) =>
        {
            var remote = http.Connection.RemoteIpAddress;
            if (remote is null || !IPAddress.IsLoopback(remote))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            var scheduler = await factory.GetScheduler(ct);
            var key = (await scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup(), ct))
                .FirstOrDefault(k => string.Equals(k.Name, name, StringComparison.OrdinalIgnoreCase));
            if (key is null)
                return Results.NotFound();
            await scheduler.TriggerJob(key, ct);
            return Results.Accepted(value: new { job = key.Name, triggered = DateTime.Now });
        });

        return endpoints;
    }

    private static string Encode(string? value) => WebUtility.HtmlEncode(value ?? "");
}
