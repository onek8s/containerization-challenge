using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Southridge.Clinic.LabStubs;

// Stand-ins for the clinic's outside partners in the development and training environment:
// hospital and reference lab HL7 interfaces (MLLP), the claims clearinghouse drop folders,
// the Reporting Services execution endpoint and the SMS gateway.
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(builder.Configuration.GetSection("Partners").Get<PartnerOptions>() ?? new PartnerOptions());
builder.Services.AddSingleton(builder.Configuration.GetSection("Clearinghouse").Get<ClearinghouseOptions>() ?? new ClearinghouseOptions());
builder.Services.AddSingleton<TrafficLog>();
builder.Services.AddSingleton<ReportServerStub>();
builder.Services.AddSingleton<Hl7Partners>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<Hl7Partners>());
builder.Services.AddHostedService<Clearinghouse>();
var app = builder.Build();

var log = app.Services.GetRequiredService<TrafficLog>();
var sms = new ConcurrentQueue<(DateTime At, string Id, string To, string From, string Text)>();
var smsCounter = 0;

app.MapGet("/", () =>
{
    var sb = new StringBuilder("<html><head><meta charset=\"utf-8\"><title>Clinic lab services</title>" +
        "<style>body{font-family:sans-serif;font-size:13px} td,th{padding:3px 8px;border-bottom:1px solid #ddd;text-align:left;vertical-align:top}</style></head><body>" +
        "<h2>Southridge clinic lab services</h2>" +
        "<p>MLLP: hospital 7661, lab 7662, hospital billing 7663 &middot; ReportServer: POST /ReportServer/ReportExecution2005.asmx &middot; SMS: POST /sms/send, <a href=\"/sms\">sent messages</a></p>" +
        "<form method=\"post\" action=\"/hospital/adt\"><b>Hospital registration</b> " +
        "<select name=\"trigger\"><option>A04</option><option>A01</option><option>A08</option><option>A03</option></select> " +
        "MRN <input name=\"mrn\" size=\"9\"> hospital no. <input name=\"hospitalNumber\" size=\"9\" value=\"H" + Random.Shared.Next(100000, 999999) + "\"> " +
        "last <input name=\"last\" size=\"10\"> first <input name=\"first\" size=\"10\"> DOB <input name=\"dob\" size=\"8\" placeholder=\"yyyyMMdd\"> " +
        "sex <select name=\"sex\"><option>F</option><option>M</option><option>U</option></select> <button>Send ADT</button></form>" +
        "<table><tr><th>Time</th><th>Partner</th><th></th><th>Type</th><th>Control</th><th>Summary</th></tr>");
    foreach (var e in log.Recent())
    {
        sb.Append($"<tr><td>{e.At:HH:mm:ss}</td><td>{e.Partner}</td><td>{e.Direction}</td><td>{e.Type}</td><td>{WebUtility.HtmlEncode(e.ControlId)}</td>" +
                  $"<td title=\"{WebUtility.HtmlEncode(e.Raw.Replace('\r', '\n'))}\">{WebUtility.HtmlEncode(e.Summary)}</td></tr>");
    }

    return Results.Content(sb.Append("</table></body></html>").ToString(), "text/html");
});

app.MapGet("/traffic", () => Results.Json(log.Recent().Select(e => new { e.At, e.Partner, e.Direction, e.Type, e.ControlId, e.Summary })));

app.MapPost("/hospital/adt", async (HttpRequest r, Hl7Partners partners) =>
{
    var f = r.HasFormContentType ? r.Form : null;
    string V(string key) => f?[key].ToString() ?? r.Query[key].ToString();
    var result = await partners.SendAdtAsync(V("trigger") is { Length: > 0 } t ? t : "A04", V("mrn"), V("hospitalNumber"), V("last"), V("first"), V("dob"), V("sex"), r.HttpContext.RequestAborted);
    return f != null ? Results.Redirect("/") : Results.Text(result);
});

var reportServer = app.Services.GetRequiredService<ReportServerStub>();
app.MapPost("/ReportServer/ReportExecution2005.asmx", (HttpRequest r) => reportServer.HandleAsync(r));

app.MapPost("/sms/send", (HttpRequest r) =>
{
    var form = r.Form;
    if (string.IsNullOrEmpty(form["account"]) || string.IsNullOrEmpty(form["token"]))
    {
        return Results.Text("ERR:authentication");
    }

    var to = form["to"].ToString();
    if (!to.StartsWith('+') || to.Length < 11)
    {
        return Results.Text("ERR:invalid destination");
    }

    var id = "M" + DateTime.Now.ToString("yyMMdd") + Interlocked.Increment(ref smsCounter).ToString("D5");
    sms.Enqueue((DateTime.Now, id, to, form["from"].ToString(), form["text"].ToString()));
    log.Add("SMS", "in", "SMS", id, to + ": " + form["text"]);
    return Results.Text("OK:" + id);
});

app.MapGet("/sms", () => Results.Json(sms.Reverse().Select(m => new { m.At, m.Id, m.To, m.From, m.Text })));

app.Run();
