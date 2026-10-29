using System.Collections.Concurrent;
using System.Text;
using System.Text.Json.Nodes;
using Lucerne.Lending.CoreBanking;
using Lucerne.Lending.CoreBanking.Simulator;
using Lucerne.Lending.LabStubs;

// Stand-ins for the external services in the development and training environment:
// credit bureau (SOAP), e-signature (REST), payments hub (REST) and an operator page for the core simulator.
var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<CoreOptions>(builder.Configuration.GetSection(CoreOptions.Section));
builder.Services.AddCoreSimulator();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<ESignStub>();
var app = builder.Build();

app.MapGet("/", () => Results.Content(
    "<html><body style=\"font-family:sans-serif\"><h2>Consumer Loans lab services</h2><ul>" +
    "<li><a href=\"/core\">Core simulator</a></li><li>POST /bureau/ConsumerReportService.svc</li>" +
    "<li>/esign/api/v1/orders</li><li><a href=\"/payhub/payments\">Payments hub releases</a></li></ul></body></html>", "text/html"));

app.MapPost("/bureau/ConsumerReportService.svc", (HttpRequest r) => BureauStub.HandleAsync(r));

var esign = app.Services.GetRequiredService<ESignStub>();
app.MapPost("/esign/api/v1/orders", (HttpRequest r) => esign.CreateAsync(r));
app.MapGet("/esign/api/v1/orders/{id}", (string id) => esign.Get(id));
app.MapGet("/esign/api/v1/orders/{id}/documents/0/signed", (string id) => esign.Signed(id));
app.MapPost("/esign/api/v1/orders/{id}/cancel", (string id) => esign.CancelAsync(id));
app.MapGet("/esign/sign/{id}", (string id) => esign.Page(id));
app.MapPost("/esign/sign/{id}", (string id) => esign.SignAsync(id));

var releases = new ConcurrentQueue<JsonNode>();
app.MapPost("/payhub/payments/v2/release", async (HttpRequest r) =>
{
    var body = JsonNode.Parse(await new StreamReader(r.Body).ReadToEndAsync())!;
    body["paymentId"] = "PH" + DateTime.UtcNow.ToString("yyMMddHHmmssfff");
    releases.Enqueue(body);
    return Results.Json(new { paymentId = (string)body["paymentId"]!, status = (bool?)body["instant"] == true ? "SETTLED" : "ACCEPTED" });
});
app.MapGet("/payhub/payments", () => Results.Json(releases.ToArray()));

var ops = app.Services.GetRequiredService<SimulatorOperations>();
app.MapGet("/core", () =>
{
    var sb = new StringBuilder("<html><head><meta charset=\"utf-8\"><title>Core simulator</title>" +
        "<style>body{font-family:sans-serif} td,th{padding:3px 8px;border-bottom:1px solid #ddd}</style></head><body>" +
        $"<h2>Core simulator</h2><p>Business date <b>{ops.BusinessDate():yyyy-MM-dd}</b></p>" +
        "<form method=\"post\" action=\"/core/eod\"><input name=\"days\" value=\"1\" size=\"3\"> days <button>Run end of day</button></form>" +
        "<form method=\"post\" action=\"/core/payments\">Account <input name=\"account\" size=\"14\"> amount <input name=\"amount\" size=\"8\"> <button>Post payment</button></form>" +
        "<table><tr><th>Account</th><th>Customer</th><th>Balance</th><th>Arrears</th><th>DPD</th><th>Status</th><th>Next due</th></tr>");
    foreach (var l in ops.Loans())
        sb.Append($"<tr><td>{l.Account}</td><td>{l.Customer}</td><td>{l.Balance:0.00}</td><td>{l.Arrears:0.00}</td><td>{l.Dpd}</td><td>{l.Status}</td><td>{l.NextDue}</td></tr>");
    return Results.Content(sb.Append("</table></body></html>").ToString(), "text/html");
});
app.MapPost("/core/eod", (HttpRequest r) =>
{
    var days = int.TryParse(r.HasFormContentType ? r.Form["days"] : r.Query["days"], out var d) ? d : 1;
    var results = Enumerable.Range(0, Math.Clamp(days, 1, 120)).Select(_ => ops.RunEndOfDay()).ToList();
    return r.HasFormContentType ? Results.Redirect("/core") : Results.Json(new { businessDate = results.Last().BusinessDate.ToString("yyyy-MM-dd"), days = results.Count, dueLoans = results.Sum(x => x.DueLoans), missed = results.Sum(x => x.Missed), dpdUpdates = results.Sum(x => x.DpdUpdates) });
});
app.MapPost("/core/payments", (HttpRequest r) =>
{
    string account = r.HasFormContentType ? r.Form["account"]! : r.Query["account"]!;
    var amount = decimal.Parse(r.HasFormContentType ? r.Form["amount"]! : r.Query["amount"]!, System.Globalization.CultureInfo.InvariantCulture);
    var arrears = ops.PostPayment(account.Trim(), amount);
    return r.HasFormContentType ? Results.Redirect("/core") : Results.Json(new { account, amount, arrears });
});

app.Run();
