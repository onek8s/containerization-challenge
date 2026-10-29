using ExpenseClaims.Core.Currency;
using ExpenseClaims.Data;
using ExpenseClaims.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace ExpenseClaims.Jobs.Jobs;

/// <summary>Loads the ECB euro reference rates (last 90 days) and stores the ones that are missing.</summary>
[DisallowConcurrentExecution]
public sealed class ExchangeRateJob : IJob
{
    public const string HttpClientName = "ecb";

    private readonly IHttpClientFactory _httpFactory;
    private readonly ExpenseClaimsDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ExchangeRateJob> _logger;

    public ExchangeRateJob(IHttpClientFactory httpFactory, ExpenseClaimsDbContext db, IConfiguration configuration, ILogger<ExchangeRateJob> logger)
    {
        _httpFactory = httpFactory;
        _db = db;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var url = _configuration["ExchangeRates:Url"] ?? "https://www.ecb.europa.eu/stats/eurofxref/eurofxref-hist-90d.xml";
        var http = _httpFactory.CreateClient(HttpClientName);
        await using var stream = await http.GetStreamAsync(url, context.CancellationToken);
        var rates = EcbRatesParser.Parse(stream);
        var added = await StoreAsync(_db, rates, context.CancellationToken);
        _logger.LogInformation("Exchange rates: {Count} rates read, {Added} added", rates.Count, added);
    }

    public static async Task<int> StoreAsync(ExpenseClaimsDbContext db, IReadOnlyList<ReferenceRate> rates, CancellationToken ct)
    {
        if (rates.Count == 0)
            return 0;
        var from = rates.Min(r => r.Date);
        var existing = (await db.ExchangeRates.AsNoTracking().Where(r => r.Date >= from).Select(r => new { r.Date, r.Currency }).ToListAsync(ct))
            .Select(r => (r.Date, r.Currency))
            .ToHashSet();
        var added = 0;
        foreach (var rate in rates.Where(r => !existing.Contains((r.Date, r.Currency))))
        {
            db.ExchangeRates.Add(new ExchangeRate { Date = rate.Date, Currency = rate.Currency, UnitsPerEuro = rate.UnitsPerEuro });
            added++;
        }
        await db.SaveChangesAsync(ct);
        return added;
    }
}
