using AutoMapper;
using Lucerne.Lending.Contracts;
using Lucerne.Lending.Data;
using Lucerne.Lending.Domain;
using Microsoft.Extensions.Options;

namespace Lucerne.Lending.Workflow;

public sealed class ValidationFailedException : Exception
{
    public ValidationFailedException(IReadOnlyList<string> errors) : base(string.Join(" ", errors)) => Errors = errors;
    public IReadOnlyList<string> Errors { get; }
}

public sealed class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}

/// <summary>Customer facing application operations (portal and branch channel through the API).</summary>
public sealed class ApplicationService
{
    private readonly ApplicationRepository _applications;
    private readonly DecisionRepository _decisions;
    private readonly DocumentRepository _documents;
    private readonly ProductRepository _products;
    private readonly OutboxRepository _outbox;
    private readonly Db _db;
    private readonly IMapper _mapper;
    private readonly LendingOptions _options;

    public ApplicationService(ApplicationRepository applications, DecisionRepository decisions, DocumentRepository documents,
        ProductRepository products, OutboxRepository outbox, Db db, IMapper mapper, IOptions<LendingOptions> options)
    {
        _applications = applications;
        _decisions = decisions;
        _documents = documents;
        _products = products;
        _outbox = outbox;
        _db = db;
        _mapper = mapper;
        _options = options.Value;
    }

    public async Task<ApplicationCreated> SubmitAsync(ApplicationRequest request, string? customerUserId, string actor, CancellationToken ct = default)
    {
        var product = await _products.GetAsync(request.ProductCode, ct);
        var errors = new List<string>();
        if (product == null || !product.Active) errors.Add($"Unknown product {request.ProductCode}.");
        var app = _mapper.Map<LoanApplication>(request);
        app.Status = ApplicationStatus.Submitted;
        app.CustomerUserId = customerUserId;
        var main = _mapper.Map<Applicant>(request.Applicant);
        main.IsMain = true;
        app.Applicants.Add(main);
        if (request.CoApplicant != null)
        {
            var co = _mapper.Map<Applicant>(request.CoApplicant);
            co.IsMain = false;
            app.Applicants.Add(co);
            if (product is { AllowsCoApplicant: false }) errors.Add("This product does not allow a co-applicant.");
        }
        if (product != null) errors.AddRange(product.Validate(app.Amount, app.TermMonths, app.Purpose));
        errors.AddRange(ApplicationValidator.Validate(app, product));
        if (errors.Count > 0) throw new ValidationFailedException(errors);

        var open = await _applications.ListForPersonAsync(main.PersonalIdentityCode, ct);
        if (open.Any(a => ApplicationStatusRules.IsOpen(a.StatusValue) && a.StatusValue != ApplicationStatus.Draft))
            throw new ConflictException("An application is already in progress for this person.");

        var row = await _applications.CreateAsync(app, actor, ct);
        return new ApplicationCreated { Reference = row.Reference, Status = row.StatusValue.ToString() };
    }

    public async Task<ApplicationDetailDto?> GetDetailAsync(string reference, CancellationToken ct = default)
    {
        var record = await _applications.GetAsync(reference, ct);
        if (record == null) return null;
        var product = await _products.GetAsync(record.Row.ProductCode, ct);
        var offer = await _decisions.CurrentOfferAsync(record.Row.Id, ct);
        var docs = await _documents.ListForApplicationAsync(record.Row.Id, ct);
        var detail = new ApplicationDetailDto
        {
            Reference = record.Row.Reference,
            ProductCode = record.Row.ProductCode,
            ProductName = product?.Name ?? record.Row.ProductCode,
            Purpose = ((LoanPurpose)record.Row.Purpose).ToString(),
            Amount = record.Row.Amount,
            TermMonths = record.Row.TermMonths,
            DueDay = record.Row.DueDay,
            Status = record.Row.StatusValue.ToString(),
            StatusText = StatusTexts.Customer(record.Row.StatusValue),
            DisbursementIban = record.Row.DisbursementIban,
            ApplicantName = record.Applicants.First(a => a.IsMain).FullName,
            CoApplicantName = record.Applicants.FirstOrDefault(a => !a.IsMain)?.FullName,
            Offer = offer == null ? null : _mapper.Map<OfferDto>(offer),
            SigningUrl = docs.LastOrDefault(d => d.Kind == DocumentKinds.LoanAgreement && d.Status == "SentForSigning")?.SigningUrl,
            CoreAccountNumber = record.Row.CoreAccountNumber,
            CanCancel = ApplicationStatusRules.CustomerCanCancel(record.Row.StatusValue),
            Documents = docs.Where(d => d.Status != "Pending").Select(d => _mapper.Map<DocumentDto>(d)).ToList(),
            CreatedUtc = record.Row.CreatedUtc
        };
        return detail;
    }

    public async Task<IReadOnlyList<ApplicationSummaryDto>> ListForCustomerAsync(string personalIdentityCode, CancellationToken ct = default)
    {
        var products = (await _products.ListAsync(false, ct)).ToDictionary(p => p.Code);
        return (await _applications.ListForPersonAsync(personalIdentityCode, ct)).Select(a => new ApplicationSummaryDto
        {
            Reference = a.Reference,
            ProductCode = a.ProductCode,
            ProductName = products.TryGetValue(a.ProductCode, out var p) ? p.Name : a.ProductCode,
            Amount = a.Amount,
            TermMonths = a.TermMonths,
            Status = a.StatusValue.ToString(),
            CreatedUtc = a.CreatedUtc,
            UpdatedUtc = a.UpdatedUtc
        }).ToList();
    }

    public async Task AcceptOfferAsync(string reference, AcceptOfferRequest request, string actor, CancellationToken ct = default)
    {
        if (!request.ConfirmsInformationRead)
            throw new ValidationFailedException(new[] { "Please confirm that you have read the pre-contract information." });
        if (!Iban.IsValid(request.DisbursementIban))
            throw new ValidationFailedException(new[] { "Disbursement account IBAN is not valid." });
        var row = await _applications.GetRowAsync(reference, ct) ?? throw new KeyNotFoundException(reference);
        var offer = await _decisions.CurrentOfferAsync(row.Id, ct);
        if (offer == null || offer.Id != request.OfferId || offer.Status != "Open") throw new ConflictException("The offer is no longer open.");
        if (offer.ValidUntilUtc < DateTime.UtcNow) throw new ConflictException("The offer has expired.");
        await _db.InTransactionAsync(async tx =>
        {
            if (await _decisions.AcceptOfferAsync(offer.Id, tx) != 1) throw new ConflictException("The offer is no longer open.");
            await _applications.SetDisbursementIbanAsync(row.Id, Iban.Normalize(request.DisbursementIban), tx);
            if (!await _applications.TransitionAsync(row.Id, ApplicationStatus.OfferIssued, ApplicationStatus.OfferAccepted, row.Version, actor,
                    $"Offer {offer.Id} accepted", tx))
                throw new ConflictException("The application was changed, please reload.");
            await _outbox.EnqueueAsync(Topics.DocumentRequested, new DocumentRequest(DocumentKinds.LoanAgreement, row.Id, null), tx);
            return true;
        }, ct);
    }

    public async Task CancelAsync(string reference, string actor, string reason, CancellationToken ct = default)
    {
        var row = await _applications.GetRowAsync(reference, ct) ?? throw new KeyNotFoundException(reference);
        if (!ApplicationStatusRules.CustomerCanCancel(row.StatusValue)) throw new ConflictException("The application can no longer be cancelled.");
        await _db.InTransactionAsync(async tx =>
        {
            await _decisions.WithdrawOpenOffersAsync(row.Id, tx);
            if (!await _applications.TransitionAsync(row.Id, row.StatusValue, ApplicationStatus.Cancelled, row.Version, actor, reason, tx))
                throw new ConflictException("The application was changed, please reload.");
            return true;
        }, ct);
    }

    public async Task<int> ExpireOffersAsync(CancellationToken ct = default)
    {
        var expired = await _decisions.ExpiredOpenOffersAsync(DateTime.UtcNow, ct);
        foreach (var offer in expired)
        {
            var row = await _applications.GetRowAsync(offer.ApplicationId, ct);
            await _db.InTransactionAsync(async tx =>
            {
                await _decisions.ExpireOfferAsync(offer.Id, tx);
                if (row is { StatusValue: ApplicationStatus.OfferIssued })
                    await _applications.TransitionAsync(row.Id, row.StatusValue, ApplicationStatus.Expired, row.Version, _options.SystemUser, "Offer expired", tx);
                return true;
            }, ct);
        }
        return expired.Count;
    }
}

public static class StatusTexts
{
    public static string Customer(ApplicationStatus s) => s switch
    {
        ApplicationStatus.Submitted or ApplicationStatus.Scoring => "We are processing your application.",
        ApplicationStatus.Referred => "Your application is being reviewed by our loan officer.",
        ApplicationStatus.Approved => "Your application has been approved; the offer is being prepared.",
        ApplicationStatus.OfferIssued => "Your loan offer is ready. Please review and accept it.",
        ApplicationStatus.OfferAccepted => "Thank you. The loan agreement is being prepared for signing.",
        ApplicationStatus.AwaitingSignature => "Please sign the loan agreement with your bank credentials.",
        ApplicationStatus.Signed => "The agreement is signed. The loan will be paid out shortly.",
        ApplicationStatus.Booked => "The loan has been opened and the payment is on its way.",
        ApplicationStatus.Disbursed => "The loan has been paid to your account.",
        ApplicationStatus.Declined => "Unfortunately we cannot grant the loan. A letter with the reasons is in your documents.",
        ApplicationStatus.Cancelled => "The application was cancelled.",
        ApplicationStatus.Expired => "The offer has expired.",
        ApplicationStatus.Failed => "We are processing your application.",
        _ => s.ToString()
    };
}
