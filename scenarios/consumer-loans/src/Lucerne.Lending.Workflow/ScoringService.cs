using System.Text.Json;
using Lucerne.Lending.CoreBanking;
using Lucerne.Lending.Data;
using Lucerne.Lending.Decisioning;
using Lucerne.Lending.Domain;
using Lucerne.Lending.Integration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Lucerne.Lending.Workflow;

/// <summary>Automatic credit decision for submitted applications.</summary>
public sealed class ScoringService
{
    private readonly ApplicationRepository _applications;
    private readonly DecisionRepository _decisions;
    private readonly ProductRepository _products;
    private readonly OutboxRepository _outbox;
    private readonly Db _db;
    private readonly ICoreCustomerService _customers;
    private readonly ICoreReferenceData _referenceData;
    private readonly ICreditBureau _bureau;
    private readonly DecisionEngine _engine;
    private readonly AffordabilityCalculator _affordability;
    private readonly LendingOptions _options;
    private readonly ILogger<ScoringService> _log;

    public ScoringService(ApplicationRepository applications, DecisionRepository decisions, ProductRepository products, OutboxRepository outbox,
        Db db, ICoreCustomerService customers, ICoreReferenceData referenceData, ICreditBureau bureau, DecisionEngine engine,
        AffordabilityCalculator affordability, IOptions<LendingOptions> options, ILogger<ScoringService> log)
    {
        _applications = applications;
        _decisions = decisions;
        _products = products;
        _outbox = outbox;
        _db = db;
        _customers = customers;
        _referenceData = referenceData;
        _bureau = bureau;
        _engine = engine;
        _affordability = affordability;
        _options = options.Value;
        _log = log;
    }

    public async Task<int> ProcessSubmittedAsync(int take, CancellationToken ct)
    {
        var done = 0;
        foreach (var row in await _applications.ListByStatusAsync(ApplicationStatus.Submitted, take, ct))
        {
            if (!await _applications.TransitionAsync(row.Id, ApplicationStatus.Submitted, ApplicationStatus.Scoring, row.Version, _options.SystemUser, null, ct: ct))
                continue; // taken by another instance
            try
            {
                await ScoreAsync(row.Id, ct);
                done++;
            }
            catch (Exception ex) when (ex is BureauException or CoreBankingException or TimeoutException)
            {
                _log.LogWarning(ex, "Scoring of {Reference} failed, will retry", row.Reference);
                await _applications.RecordErrorAsync(row.Id, ex.Message, ct);
                var current = (await _applications.GetRowAsync(row.Id, ct))!;
                await _applications.TransitionAsync(row.Id, ApplicationStatus.Scoring, ApplicationStatus.Failed, current.Version, _options.SystemUser, ex.Message, ct: ct);
            }
        }
        return done;
    }

    /// <summary>Failed applications go back to the queue after a pause (bureau or core temporarily unavailable).</summary>
    public async Task<int> RetryFailedAsync(TimeSpan after, int maxAttempts, CancellationToken ct)
    {
        var count = 0;
        foreach (var row in await _applications.ListByStatusAsync(ApplicationStatus.Failed, 50, ct))
        {
            if (row.Attempts >= maxAttempts || row.UpdatedUtc > DateTime.UtcNow - after) continue;
            if (await _applications.TransitionAsync(row.Id, ApplicationStatus.Failed, ApplicationStatus.Submitted, row.Version, _options.SystemUser, "Retry", ct: ct))
                count++;
        }
        return count;
    }

    public async Task ScoreAsync(long applicationId, CancellationToken ct)
    {
        var record = (await _applications.GetAsync(applicationId, ct))!;
        var app = record.ToDomain();
        var product = (await _products.GetAsync(app.ProductCode, ct))!;
        var businessDate = await _referenceData.GetBusinessDateAsync(ct);
        var refCode = ReferenceRateCode(product.ReferenceRate);
        var refRate = await _referenceData.GetReferenceRateAsync(refCode, businessDate, ct);

        var main = app.Main;
        var customer = await _customers.FindByPersonalIdentityCodeAsync(main.PersonalIdentityCode, ct);
        CoreExposure? exposure = null;
        if (customer != null)
        {
            await _applications.SetApplicantCustomerNumberAsync(main.Id, customer.CustomerNumber);
            exposure = await _customers.GetExposureAsync(customer.CustomerNumber, ct);
        }
        if (app.CoApplicant is { } co && await _customers.FindByPersonalIdentityCodeAsync(co.PersonalIdentityCode, ct) is { } coCustomer)
            await _applications.SetApplicantCustomerNumberAsync(co.Id, coCustomer.CustomerNumber);

        var reports = new List<ConsumerReport>();
        foreach (var applicant in app.Applicants)
            reports.Add(await BureauAsync(applicant, app.Reference, ct));

        // price with the indicative grade C first; affordability is stressed on that rate
        var indicative = Pricing.Quote(product, app.Amount, app.TermMonths, RiskGrade.C, refRate);
        var budget = _affordability.Calculate(app, indicative.NominalRatePercent, product.MonthlyFee);
        var facts = BuildFacts(app, product, customer, exposure, reports, budget, businessDate);
        var result = await _engine.DecideAsync(facts);

        var target = result.Outcome switch
        {
            DecisionOutcome.Approve => ApplicationStatus.Approved,
            DecisionOutcome.Refer => ApplicationStatus.Referred,
            _ => ApplicationStatus.Declined
        };
        var current = (await _applications.GetRowAsync(applicationId, ct))!;
        await _db.InTransactionAsync(async tx =>
        {
            await _decisions.AddAsync(new DecisionRow
            {
                ApplicationId = applicationId,
                Outcome = result.Outcome.ToString(),
                RiskGrade = result.Grade.ToString(),
                Score = result.Score,
                ReasonCodes = string.Join(',', result.Reasons.Select(r => r.Code)),
                ScorecardJson = JsonSerializer.Serialize(result.ScoreLines),
                RuleTraceJson = JsonSerializer.Serialize(new { result.Reasons, result.Trace }),
                AffordabilityJson = JsonSerializer.Serialize(budget),
                PolicyVersion = result.PolicyVersion,
                DecidedBy = "SYSTEM",
                CreatedUtc = DateTime.UtcNow
            }, tx);
            await _applications.ClearErrorAsync(applicationId, tx);
            if (!await _applications.TransitionAsync(applicationId, ApplicationStatus.Scoring, target, current.Version, _options.SystemUser,
                    $"{result.Outcome} grade {result.Grade} score {result.Score} {string.Join(",", result.Reasons.Select(r => r.Code))}", tx))
                throw new InvalidOperationException("Application changed during scoring.");
            if (target == ApplicationStatus.Approved)
                await OfferIssuer.IssueAsync(_applications, _decisions, _outbox, applicationId, current.Version + 1, product, app.Amount, app.TermMonths,
                    result.Grade, refCode, refRate, null, _options, _options.SystemUser, tx);
            if (target == ApplicationStatus.Declined)
                await _outbox.EnqueueAsync(Topics.DocumentRequested, new DocumentRequest(DocumentKinds.DeclineLetter, applicationId, null), tx);
            return true;
        }, ct);
        _log.LogInformation("Application {Reference}: {Outcome} grade {Grade} score {Score}", app.Reference, result.Outcome, result.Grade, result.Score);
    }

    private async Task<ConsumerReport> BureauAsync(Applicant applicant, string reference, CancellationToken ct)
    {
        var recent = await _decisions.RecentBureauReportAsync(applicant.PersonalIdentityCode, TimeSpan.FromDays(_options.BureauReuseDays), ct);
        ConsumerReport report;
        string raw;
        if (recent != null && recent.ApplicantId != applicant.Id)
        {
            raw = recent.RawXml;
            report = (ConsumerReport)new System.Xml.Serialization.XmlSerializer(typeof(ConsumerReport)).Deserialize(new StringReader(raw))!;
        }
        else
        {
            (report, raw) = await _bureau.GetReportAsync(applicant.PersonalIdentityCode, reference, ct);
        }
        await _decisions.AddBureauReportAsync(new BureauReportRow
        {
            ApplicantId = applicant.Id,
            Provider = "NORDICCR",
            RequestId = report.RequestId,
            Score = report.PersonFound ? report.Score : null,
            PaymentDefault = report.PaymentDefaultCount > 0,
            DefaultCount = report.PaymentDefaultCount,
            DebtRestructuring = report.DebtRestructuring,
            CreditBlock = report.CreditBlock,
            ReportedLoans = report.ReportedLoans,
            RawXml = raw,
            CreatedUtc = DateTime.UtcNow
        });
        return report;
    }

    public static CreditFacts BuildFacts(LoanApplication app, LoanProduct product, CoreCustomer? customer, CoreExposure? exposure,
        IReadOnlyList<ConsumerReport> reports, AffordabilityResult budget, DateOnly today)
    {
        var main = app.Main;
        var age = PersonalIdentityCode.AgeOn(main.PersonalIdentityCode, today);
        var oldest = app.Applicants.Max(a => PersonalIdentityCode.AgeOn(a.PersonalIdentityCode, today));
        var mainReport = reports[0];
        return new CreditFacts
        {
            ProductCode = product.Code,
            Amount = app.Amount,
            TermMonths = app.TermMonths,
            RequiresCollateral = product.RequiresCollateral,
            CollateralValue = app.CollateralValue ?? 0,
            LoanToValue = app.CollateralValue is > 0 ? Math.Round(app.Amount / app.CollateralValue.Value, 4) : 0,
            HasCoApplicant = app.CoApplicant != null,
            Age = age,
            AgeAtMaturity = oldest + (int)Math.Ceiling(app.TermMonths / 12m),
            Resident = app.Applicants.All(a => a.ResidentInFinland),
            Employment = main.Employment.ToString(),
            MonthsEmployed = main.EmployedSince is { } since ? (today.Year - since.Year) * 12 + today.Month - since.Month : 0,
            OnProbation = app.Applicants.Any(a => a.OnProbation),
            Housing = main.Housing.ToString(),
            PoliticallyExposed = app.Applicants.Any(a => a.PoliticallyExposed),
            NetIncome = budget.HouseholdNetIncome,
            GrossIncome = app.Applicants.Sum(a => a.GrossMonthlyIncome),
            BureauPersonFound = reports.All(r => r.PersonFound),
            BureauScore = mainReport.PersonFound ? (app.CoApplicant != null ? reports.Min(r => r.Score) : mainReport.Score) : 0,
            PaymentDefault = reports.Any(r => r.PaymentDefaultCount > 0),
            PaymentDefaultCount = reports.Sum(r => r.PaymentDefaultCount),
            CreditBlock = reports.Any(r => r.CreditBlock),
            DebtRestructuring = reports.Any(r => r.DebtRestructuring),
            CreditInquiries12M = reports.Max(r => r.CreditInquiries12M),
            ExistingCustomer = customer != null,
            CustomerMonths = customer == null ? 0 : Math.Max(0, (today.Year - customer.CustomerSince.Year) * 12 + today.Month - customer.CustomerSince.Month),
            KycStatus = customer?.KycStatus ?? "",
            CoreBlocked = customer?.Blocked ?? false,
            Deceased = customer?.Deceased ?? false,
            InternalRating = customer?.InternalRating.Trim() ?? "",
            ExistingBalance = exposure?.TotalBalance ?? 0,
            ExistingArrears = exposure?.TotalArrears ?? 0,
            ExistingMaxDpd = exposure?.MaxDaysPastDue ?? 0,
            Dsti = budget.DebtServiceToIncome,
            Surplus = budget.Surplus
        };
    }

    public static string ReferenceRateCode(ReferenceRateType type) => type switch
    {
        ReferenceRateType.Euribor12M => "E12",
        ReferenceRateType.Euribor3M => "E3M",
        _ => "FIX"
    };
}

internal static class OfferIssuer
{
    public static async Task IssueAsync(ApplicationRepository applications, DecisionRepository decisions, OutboxRepository outbox, long applicationId,
        int version, LoanProduct product, decimal amount, int term, RiskGrade grade, string refCode, decimal refRate, decimal? marginOverride,
        LendingOptions options, string actor, System.Data.IDbTransaction tx)
    {
        var quote = Pricing.Quote(product, amount, term, grade, refRate, marginOverride ?? -1m);
        await decisions.WithdrawOpenOffersAsync(applicationId, tx);
        await decisions.AddOfferAsync(new OfferRow
        {
            ApplicationId = applicationId,
            Amount = amount,
            TermMonths = term,
            ReferenceRateCode = refCode,
            ReferenceRatePercent = quote.ReferenceRatePercent,
            MarginPercent = quote.MarginPercent,
            NominalRatePercent = quote.NominalRatePercent,
            AprPercent = quote.AprPercent,
            MonthlyPayment = quote.MonthlyPayment,
            MonthlyFee = quote.MonthlyFee,
            ArrangementFee = quote.ArrangementFee,
            TotalRepayable = quote.TotalRepayable,
            ValidUntilUtc = DateTime.UtcNow.Date.AddDays(options.OfferValidDays + 1).AddSeconds(-1),
            Status = "Open",
            CreatedUtc = DateTime.UtcNow
        }, tx);
        if (!await applications.TransitionAsync(applicationId, ApplicationStatus.Approved, ApplicationStatus.OfferIssued, version, actor,
                $"Offer {quote.NominalRatePercent:0.00} % / APR {quote.AprPercent:0.00} %", tx))
            throw new InvalidOperationException("Application changed while issuing the offer.");
        await outbox.EnqueueAsync(Topics.DocumentRequested, new DocumentRequest(DocumentKinds.PreContractInformation, applicationId, null), tx);
    }
}
