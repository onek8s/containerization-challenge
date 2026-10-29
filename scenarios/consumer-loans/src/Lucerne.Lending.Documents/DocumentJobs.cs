using Lucerne.Lending.Data;
using Lucerne.Lending.Documents.Rendering;
using Lucerne.Lending.Domain;
using Lucerne.Lending.Integration;

namespace Lucerne.Lending.Documents;

/// <summary>Creates the requested documents and starts the follow-up steps (signing, customer e-mail).</summary>
public sealed class DocumentJobs
{
    private readonly ApplicationRepository _applications;
    private readonly DecisionRepository _decisions;
    private readonly DocumentRepository _documents;
    private readonly ProductRepository _products;
    private readonly CollectionRepository _collections;
    private readonly OutboxRepository _outbox;
    private readonly Db _db;
    private readonly IESignClient _esign;
    private readonly ILogger<DocumentJobs> _log;

    public DocumentJobs(ApplicationRepository applications, DecisionRepository decisions, DocumentRepository documents, ProductRepository products,
        CollectionRepository collections, OutboxRepository outbox, Db db, IESignClient esign, ILogger<DocumentJobs> log)
    {
        _applications = applications;
        _decisions = decisions;
        _documents = documents;
        _products = products;
        _collections = collections;
        _outbox = outbox;
        _db = db;
        _esign = esign;
        _log = log;
    }

    public async Task HandleAsync(DocumentRequest request, CancellationToken ct)
    {
        switch (request.Kind)
        {
            case DocumentKinds.PreContractInformation:
                await PreContractAsync(request.ApplicationId!.Value, ct);
                break;
            case DocumentKinds.LoanAgreement:
                await AgreementAsync(request.ApplicationId!.Value, ct);
                break;
            case DocumentKinds.DeclineLetter:
                await DeclineAsync(request.ApplicationId!.Value, ct);
                break;
            case DocumentKinds.CollectionLetter:
            case DocumentKinds.PaymentPlanConfirmation:
                await CollectionAsync(request, ct);
                break;
            default:
                throw new InvalidOperationException($"Unknown document kind {request.Kind}.");
        }
    }

    private async Task<LoanDocumentModel> ModelAsync(long applicationId, CancellationToken ct)
    {
        var app = await _applications.GetAsync(applicationId, ct) ?? throw new KeyNotFoundException($"Application {applicationId}");
        var product = (await _products.GetAsync(app.Row.ProductCode, ct))!;
        var offer = await _decisions.CurrentOfferAsync(applicationId, ct) ?? throw new InvalidOperationException($"No offer for {app.Row.Reference}");
        return new LoanDocumentModel(app, product, offer, await _decisions.LatestAsync(applicationId, ct));
    }

    private async Task PreContractAsync(long applicationId, CancellationToken ct)
    {
        var m = await ModelAsync(applicationId, ct);
        var pdf = LoanDocuments.PreContractInformation(m);
        await _documents.AddAsync(new DocumentRow
        {
            ApplicationId = applicationId,
            Kind = DocumentKinds.PreContractInformation,
            FileName = $"{m.Application.Row.Reference}-SECCI.pdf",
            Content = pdf,
            Status = "Generated"
        });
        var main = m.Application.Applicants.First(a => a.IsMain);
        await _outbox.EnqueueAsync(Topics.CustomerNotification, new CustomerNotification(main.Email, "OfferReady",
            new Dictionary<string, string> { ["reference"] = m.Application.Row.Reference, ["name"] = main.FirstName,
                ["validUntil"] = m.Offer.ValidUntilUtc.ToLocalTime().ToString("d.M.yyyy") }));
    }

    private async Task AgreementAsync(long applicationId, CancellationToken ct)
    {
        var m = await ModelAsync(applicationId, ct);
        if (m.Application.Row.StatusValue != ApplicationStatus.OfferAccepted)
        {
            _log.LogInformation("Agreement for {Reference} skipped, status {Status}", m.Application.Row.Reference, m.Application.Row.StatusValue);
            return;
        }
        var existing = await _documents.LatestAsync(applicationId, DocumentKinds.LoanAgreement, ct);
        long docId;
        byte[] pdf;
        if (existing is { Status: "Generated" } && existing.Content != null)
        {
            docId = existing.Id;
            pdf = existing.Content;
        }
        else
        {
            pdf = LoanDocuments.LoanAgreement(m);
            docId = await _documents.AddAsync(new DocumentRow
            {
                ApplicationId = applicationId,
                Kind = DocumentKinds.LoanAgreement,
                FileName = $"{m.Application.Row.Reference}-agreement.pdf",
                Content = pdf,
                Status = "Generated"
            });
        }

        var signers = m.Application.Applicants.Select(a => new Signer(a.FullName, a.Email, a.PersonalIdentityCode)).ToList();
        var order = await _esign.CreateOrderAsync(m.Application.Row.Reference, $"{m.Application.Row.Reference}-agreement.pdf", pdf, signers, ct);
        var url = order.Signers.FirstOrDefault()?.Url ?? "";
        await _db.InTransactionAsync(async tx =>
        {
            await _documents.SetSigningAsync(docId, order.Id, url, tx);
            var row = m.Application.Row;
            if (!await _applications.TransitionAsync(row.Id, ApplicationStatus.OfferAccepted, ApplicationStatus.AwaitingSignature, row.Version, "DOCUMENTS",
                    $"Signing order {order.Id}", tx))
                throw new InvalidOperationException($"Application {row.Reference} changed while sending for signing.");
            foreach (var (applicant, link) in m.Application.Applicants.Zip(order.Signers))
                await _outbox.EnqueueAsync(Topics.CustomerNotification, new CustomerNotification(applicant.Email, "SignAgreement",
                    new Dictionary<string, string> { ["reference"] = row.Reference, ["name"] = applicant.FirstName, ["url"] = link.Url }), tx);
            return true;
        }, ct);
    }

    private async Task DeclineAsync(long applicationId, CancellationToken ct)
    {
        var app = await _applications.GetAsync(applicationId, ct) ?? throw new KeyNotFoundException($"Application {applicationId}");
        var decision = await _decisions.LatestAsync(applicationId, ct);
        var bureau = (await _decisions.BureauReportsAsync(applicationId, ct)).Count > 0;
        await _documents.AddAsync(new DocumentRow
        {
            ApplicationId = applicationId,
            Kind = DocumentKinds.DeclineLetter,
            FileName = $"{app.Row.Reference}-decision.pdf",
            Content = LoanDocuments.DeclineLetter(app, decision, bureau),
            Status = "Generated"
        });
        var main = app.Applicants.First(a => a.IsMain);
        await _outbox.EnqueueAsync(Topics.CustomerNotification, new CustomerNotification(main.Email, "Declined",
            new Dictionary<string, string> { ["reference"] = app.Row.Reference, ["name"] = main.FirstName }));
    }

    private async Task CollectionAsync(DocumentRequest request, CancellationToken ct)
    {
        var caseRow = await _collections.GetAsync(request.CollectionCaseId!.Value, ct) ?? throw new KeyNotFoundException("Collection case");
        var recipient = request.Recipient ?? new Recipient("Customer " + caseRow.CustomerNumber, "", "", "", null);
        var values = request.Values ?? new Dictionary<string, string>();
        byte[] pdf;
        string name;
        if (request.Kind == DocumentKinds.PaymentPlanConfirmation)
        {
            var plan = (await _collections.PlansAsync(caseRow.Id, ct)).First();
            pdf = CollectionDocuments.PlanConfirmation(recipient, plan, values);
            name = $"{caseRow.Reference}-payment-plan.pdf";
        }
        else
        {
            pdf = CollectionDocuments.Letter(request.Template!, recipient, values);
            name = $"{caseRow.Reference}-{request.Template!.ToLowerInvariant()}.pdf";
        }
        await _documents.AddAsync(new DocumentRow
        {
            CollectionCaseId = caseRow.Id,
            Kind = request.Kind,
            FileName = name,
            Content = pdf,
            Status = "Generated"
        });
        await _collections.AddActionAsync(caseRow.Id, "Letter", name, "DOCUMENTS");
    }
}
