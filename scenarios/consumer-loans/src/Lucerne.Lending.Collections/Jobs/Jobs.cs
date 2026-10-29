using System.Globalization;
using System.Text;
using Lucerne.Lending.CoreBanking;
using Lucerne.Lending.Data;
using Lucerne.Lending.Domain;
using Lucerne.Lending.Integration;
using Lucerne.Lending.Workflow;

namespace Lucerne.Lending.Collections.Jobs;

public sealed class DailyStagesJob : ICollectionsJob
{
    private readonly CollectionsService _collections;

    public DailyStagesJob(CollectionsService collections) => _collections = collections;

    public string Name => "daily-stages";

    public async Task<string> RunAsync(CancellationToken ct)
    {
        var r = await _collections.RunDailyStagesAsync(ct);
        return $"{r.Cases} open cases, {r.Escalated} escalated, {r.FeesCharged} fees, {r.Letters} letters, {r.HandedOver} handed over, {r.PromisesBroken} promises broken";
    }
}

public sealed class PaymentPlanJob : ICollectionsJob
{
    private readonly CollectionsService _collections;

    public PaymentPlanJob(CollectionsService collections) => _collections = collections;

    public string Name => "payment-plans";

    public async Task<string> RunAsync(CancellationToken ct) => $"{await _collections.CheckPlansAsync(ct)} plans completed or broken";
}

/// <summary>Daily journal of lending postings (disbursements, fees) to the general ledger.</summary>
public sealed class GeneralLedgerExportJob : ICollectionsJob
{
    private readonly LedgerRepository _ledger;
    private readonly GeneralLedgerFileWriter _writer;
    private readonly ICoreReferenceData _referenceData;

    public GeneralLedgerExportJob(LedgerRepository ledger, GeneralLedgerFileWriter writer, ICoreReferenceData referenceData)
    {
        _ledger = ledger;
        _writer = writer;
        _referenceData = referenceData;
    }

    public string Name => "gl-export";

    public async Task<string> RunAsync(CancellationToken ct)
    {
        var date = await _referenceData.GetBusinessDateAsync(ct);
        var entries = await _ledger.UnexportedAsync(date, ct);
        if (entries.Count == 0) return "Nothing to export";
        var batch = $"{date:yyyyMMdd}{DateTime.Now:HHmmss}";
        var lines = entries.Select(e => new JournalLine(e.BusinessDate, e.GlAccount, e.CostCenter, e.Debit, e.Credit, e.Text, e.SourceRef)).ToList();
        var file = _writer.Write(batch, date, lines);
        await _ledger.MarkExportedAsync(batch, entries.Select(e => e.Id));
        return $"{entries.Count} lines to {Path.GetFileName(file)}";
    }
}

/// <summary>Weekly hand-over file of terminated loans to the debt collection agency (picked up by the file transfer agent).</summary>
public sealed class AgencyHandoverJob : ICollectionsJob
{
    private readonly CollectionRepository _cases;
    private readonly ICoreCustomerService _customers;
    private readonly ICoreLoanService _loans;
    private readonly IConfiguration _configuration;

    public AgencyHandoverJob(CollectionRepository cases, ICoreCustomerService customers, ICoreLoanService loans, IConfiguration configuration)
    {
        _cases = cases;
        _customers = customers;
        _loans = loans;
        _configuration = configuration;
    }

    public string Name => "agency-handover";

    public async Task<string> RunAsync(CancellationToken ct)
    {
        var folder = _configuration["Collections:AgencyOutboundFolder"] ?? @"D:\Interfaces\Agency\Outbound";
        var cases = (await _cases.ListAsync(CollectionStatus.HandedOver, null, ct))
            .Where(c => c.LastActionUtc >= DateTime.UtcNow.AddDays(-7)).ToList();
        if (cases.Count == 0) return "No cases to hand over";
        Directory.CreateDirectory(folder);
        var name = Path.Combine(folder, $"LUCSB_HANDOVER_{DateTime.Now:yyyyMMdd}.csv");
        var sb = new StringBuilder("CaseReference;LoanAccount;CustomerNumber;PersonId;Name;Street;PostalCode;City;Arrears;Balance;DaysPastDue;Reference\r\n");
        foreach (var c in cases)
        {
            var customer = await _customers.GetAsync(c.CustomerNumber, ct);
            var loan = await _loans.GetLoanAsync(c.AccountNumber, ct);
            sb.Append(string.Join(';', c.Reference, c.AccountNumber, c.CustomerNumber, customer?.PersonalIdentityCode, customer?.Name, customer?.Street,
                customer?.PostalCode, customer?.City, c.Arrears.ToString("0.00", CultureInfo.InvariantCulture),
                (loan?.Balance ?? 0).ToString("0.00", CultureInfo.InvariantCulture), c.DaysPastDue, PaymentReference.ForLoanAccount(c.AccountNumber)));
            sb.Append("\r\n");
        }
        await File.WriteAllTextAsync(name, sb.ToString(), new UTF8Encoding(false), ct);
        return $"{cases.Count} cases to {Path.GetFileName(name)}";
    }
}
