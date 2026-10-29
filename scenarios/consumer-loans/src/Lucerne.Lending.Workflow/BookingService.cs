using Lucerne.Lending.CoreBanking;
using Lucerne.Lending.Data;
using Lucerne.Lending.Domain;
using Lucerne.Lending.Integration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Lucerne.Lending.Workflow;

public sealed record BookingOutcome(bool Success, string? AccountNumber, string Message);

/// <summary>Opens the loan account in the core ledger and pays out the loan.</summary>
public sealed class BookingService
{
    private readonly ApplicationRepository _applications;
    private readonly DecisionRepository _decisions;
    private readonly ProductRepository _products;
    private readonly LoanRepository _loans;
    private readonly LedgerRepository _ledger;
    private readonly OutboxRepository _outbox;
    private readonly Db _db;
    private readonly ICoreCustomerService _customers;
    private readonly ICoreLoanService _coreLoans;
    private readonly ICoreReferenceData _referenceData;
    private readonly PaymentsHubClient _payments;
    private readonly LendingOptions _options;
    private readonly ILogger<BookingService> _log;

    public BookingService(ApplicationRepository applications, DecisionRepository decisions, ProductRepository products, LoanRepository loans,
        LedgerRepository ledger, OutboxRepository outbox, Db db, ICoreCustomerService customers, ICoreLoanService coreLoans,
        ICoreReferenceData referenceData, PaymentsHubClient payments, IOptions<LendingOptions> options, ILogger<BookingService> log)
    {
        _applications = applications;
        _decisions = decisions;
        _products = products;
        _loans = loans;
        _ledger = ledger;
        _outbox = outbox;
        _db = db;
        _customers = customers;
        _coreLoans = coreLoans;
        _referenceData = referenceData;
        _payments = payments;
        _options = options.Value;
        _log = log;
    }

    public async Task<int> BookPendingAsync(CancellationToken ct)
    {
        var count = 0;
        foreach (var row in await _applications.ListByStatusAsync(ApplicationStatus.Signed, 20, ct))
        {
            if (row.Amount > _options.AutoBookingLimit) continue; // Operations books these from the back office
            var outcome = await BookAsync(row.Reference, _options.SystemUser, ct);
            if (outcome.Success) count++;
        }
        return count;
    }

    public async Task<BookingOutcome> BookAsync(string reference, string officer, CancellationToken ct)
    {
        var record = await _applications.GetAsync(reference, ct) ?? throw new KeyNotFoundException(reference);
        var row = record.Row;
        if (row.StatusValue is not (ApplicationStatus.Signed or ApplicationStatus.Booked))
            return new BookingOutcome(false, null, $"Application is {row.StatusValue}.");
        var offer = await _decisions.CurrentOfferAsync(row.Id, ct);
        if (offer is not { Status: "Accepted" }) return new BookingOutcome(false, null, "No accepted offer.");
        var product = (await _products.GetAsync(row.ProductCode, ct))!;
        var officerId = officer.Length > 10 ? officer[..10].ToUpperInvariant() : officer.ToUpperInvariant();

        try
        {
            var account = row.CoreAccountNumber;
            if (row.StatusValue == ApplicationStatus.Signed)
            {
                var cif = await EnsureCustomerAsync(record.Applicants.First(a => a.IsMain), officerId, ct);
                var co = record.Applicants.FirstOrDefault(a => !a.IsMain);
                var coCif = co == null ? null : await EnsureCustomerAsync(co, officerId, ct);
                var businessDate = await _referenceData.GetBusinessDateAsync(ct);
                var result = await _coreLoans.BookAsync(new LoanBookingRequest(row.Reference, cif, coCif, product.CoreProductCode, offer.Amount,
                    offer.TermMonths, offer.ReferenceRateCode, offer.MarginPercent, offer.NominalRatePercent, offer.ArrangementFee, offer.MonthlyFee,
                    row.DueDay, DueDates.FirstDueDate(businessDate, row.DueDay), _options.BranchCode, officerId), ct);
                if (!result.Success)
                {
                    await _applications.RecordErrorAsync(row.Id, $"LNBOOK {result.ReturnCode}: {result.Message}", ct);
                    return new BookingOutcome(false, null, $"Core rejected the booking: {result.Message} ({result.ReturnCode})");
                }
                account = result.AccountNumber!;
                await _db.InTransactionAsync(async tx =>
                {
                    await _applications.SetCoreAccountAsync(row.Id, account, tx);
                    if (await _loans.GetAsync(account, ct) == null)
                        await _loans.AddAsync(new LoanRow
                        {
                            AccountNumber = account,
                            ApplicationId = row.Id,
                            CustomerNumber = cif,
                            ProductCode = row.ProductCode,
                            Amount = offer.Amount,
                            NominalRatePercent = offer.NominalRatePercent,
                            MonthlyInstalment = offer.MonthlyPayment + offer.MonthlyFee,
                            BookedUtc = DateTime.UtcNow,
                            CoreStatus = "BOOK"
                        }, tx);
                    if (!await _applications.TransitionAsync(row.Id, ApplicationStatus.Signed, ApplicationStatus.Booked, row.Version, officer,
                            $"Core account {account}", tx))
                        throw new ConflictException("Application changed during booking.");
                    return true;
                }, ct);
                row = (await _applications.GetRowAsync(row.Id, ct))!;
            }

            return await DisburseAsync(row, account!, offer, record.Applicants.First(a => a.IsMain), officerId, ct);
        }
        catch (CoreBankingException ex)
        {
            _log.LogError(ex, "Booking of {Reference} failed", reference);
            await _applications.RecordErrorAsync(row.Id, ex.Message, ct);
            return new BookingOutcome(false, row.CoreAccountNumber, ex.Message);
        }
    }

    private async Task<BookingOutcome> DisburseAsync(ApplicationRow row, string account, OfferRow offer, Applicant main, string officer, CancellationToken ct)
    {
        var iban = row.DisbursementIban ?? throw new InvalidOperationException("Disbursement account missing.");
        var disb = await _coreLoans.DisburseAsync(new DisbursementRequest(account, offer.Amount, iban, $"Lucerne loan {row.Reference}", officer), ct);
        if (!disb.Success && disb.ReturnCode != CoreReturnCodes.Duplicate)
        {
            await _applications.RecordErrorAsync(row.Id, $"LNDISB {disb.ReturnCode}: {disb.Message}", ct);
            return new BookingOutcome(false, account, $"Disbursement rejected: {disb.Message} ({disb.ReturnCode})");
        }
        if (disb.Success && disb.ExternalTransfer)
            await _payments.ReleaseAsync(new ReleasePaymentRequest(disb.CoreTransactionId!, account, iban, main.FullName, offer.Amount,
                $"Lucerne loan {row.Reference}", offer.Amount <= _payments.InstantLimit), ct);

        var date = await _referenceData.GetBusinessDateAsync(ct);
        await _db.InTransactionAsync(async tx =>
        {
            if (disb.Success)
            {
                var entries = new List<GlEntryRow>
                {
                    Gl(date, GlAccounts.LoansReceivable, offer.Amount, 0, $"Loan {account} disbursement", "DISB", row.Reference),
                    Gl(date, disb.ExternalTransfer ? GlAccounts.OutgoingPayments : GlAccounts.CustomerDeposits, 0, offer.Amount,
                        $"Loan {account} disbursement", "DISB", row.Reference)
                };
                if (offer.ArrangementFee > 0)
                {
                    entries.Add(Gl(date, GlAccounts.LoansReceivable, offer.ArrangementFee, 0, $"Loan {account} arrangement fee", "FEE", row.Reference));
                    entries.Add(Gl(date, GlAccounts.FeeIncome, 0, offer.ArrangementFee, $"Loan {account} arrangement fee", "FEE", row.Reference));
                }
                await _ledger.AddAsync(entries, tx);
            }
            await _loans.MarkDisbursedAsync(account, offer.Amount, tx);
            await _applications.ClearErrorAsync(row.Id, tx);
            if (!await _applications.TransitionAsync(row.Id, ApplicationStatus.Booked, ApplicationStatus.Disbursed, row.Version, officer,
                    disb.Success ? $"Paid to {Iban.Format(iban)} ({disb.CoreTransactionId})" : "Already disbursed in core", tx))
                throw new ConflictException("Application changed during disbursement.");
            await _outbox.EnqueueAsync(Topics.CustomerNotification, new CustomerNotification(main.Email, "LoanDisbursed",
                new Dictionary<string, string> { ["reference"] = row.Reference, ["account"] = account, ["amount"] = offer.Amount.ToString("0.00"),
                    ["paymentReference"] = PaymentReference.ForLoanAccount(account) }), tx);
            return true;
        }, ct);
        _log.LogInformation("Application {Reference} booked to {Account} and disbursed", row.Reference, account);
        return new BookingOutcome(true, account, disb.Message);
    }

    private GlEntryRow Gl(DateOnly date, string account, decimal debit, decimal credit, string text, string type, string reference) => new()
    {
        BusinessDate = date,
        GlAccount = account,
        CostCenter = _options.CostCenter,
        Debit = debit,
        Credit = credit,
        Text = text,
        SourceType = type,
        SourceRef = reference
    };

    private async Task<string> EnsureCustomerAsync(Applicant a, string officer, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(a.CoreCustomerNumber)) return a.CoreCustomerNumber.Trim();
        var existing = await _customers.FindByPersonalIdentityCodeAsync(a.PersonalIdentityCode, ct);
        var cif = existing?.CustomerNumber ?? await _customers.CreateCustomerAsync(new NewCustomerRequest(a.PersonalIdentityCode, a.FirstName, a.LastName,
            a.StreetAddress, a.PostalCode, a.City, a.Citizenship, a.Email, a.Phone, _options.BranchCode, officer), ct);
        await _applications.SetApplicantCustomerNumberAsync(a.Id, cif);
        a.CoreCustomerNumber = cif;
        return cif;
    }
}
