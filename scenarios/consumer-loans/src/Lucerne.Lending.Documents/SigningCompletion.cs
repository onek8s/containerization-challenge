using Lucerne.Lending.Contracts;
using Lucerne.Lending.Data;
using Lucerne.Lending.Domain;
using Lucerne.Lending.Integration;

namespace Lucerne.Lending.Documents;

/// <summary>Finishes signing orders reported by the provider's callback or found by the status poll.</summary>
public sealed class SigningCompletion
{
    private readonly ApplicationRepository _applications;
    private readonly DocumentRepository _documents;
    private readonly Db _db;
    private readonly IESignClient _esign;
    private readonly ILogger<SigningCompletion> _log;

    public SigningCompletion(ApplicationRepository applications, DocumentRepository documents, Db db, IESignClient esign, ILogger<SigningCompletion> log)
    {
        _applications = applications;
        _documents = documents;
        _db = db;
        _esign = esign;
        _log = log;
    }

    public async Task HandleCallbackAsync(SigningCallback callback, CancellationToken ct) => await ApplyAsync(callback.OrderId, callback.State, ct);

    public async Task<int> PollAsync(CancellationToken ct)
    {
        var n = 0;
        foreach (var doc in await _documents.AwaitingSignatureAsync(ct))
        {
            if (doc.SigningOrderId == null || doc.CreatedUtc > DateTime.UtcNow.AddMinutes(-10)) continue;
            var order = await _esign.GetOrderAsync(doc.SigningOrderId, ct);
            if (order.State is "signed" or "cancelled" or "expired")
            {
                await ApplyAsync(order.Id, order.State, ct);
                n++;
            }
        }
        return n;
    }

    private async Task ApplyAsync(string orderId, string state, CancellationToken ct)
    {
        var doc = await _documents.FindBySigningOrderAsync(orderId, ct);
        if (doc?.ApplicationId == null || doc.Status != "SentForSigning")
        {
            _log.LogInformation("Signing order {OrderId} state {State}: nothing to do", orderId, state);
            return;
        }
        var row = (await _applications.GetRowAsync(doc.ApplicationId.Value, ct))!;
        if (row.StatusValue != ApplicationStatus.AwaitingSignature) return;

        switch (state)
        {
            case "signed":
                var signed = await _esign.DownloadSignedAsync(orderId, ct);
                await _db.InTransactionAsync(async tx =>
                {
                    await _documents.AddAsync(new DocumentRow
                    {
                        ApplicationId = row.Id,
                        Kind = DocumentKinds.SignedAgreement,
                        FileName = $"{row.Reference}-agreement-signed.pdf",
                        Content = signed,
                        Status = "Signed",
                        SigningOrderId = orderId
                    }, tx);
                    await _documents.SetStatusAsync(doc.Id, "Signed", tx);
                    return await _applications.TransitionAsync(row.Id, ApplicationStatus.AwaitingSignature, ApplicationStatus.Signed, row.Version,
                        "ESIGN", $"Signing order {orderId} completed", tx);
                }, ct);
                _log.LogInformation("Agreement of {Reference} signed", row.Reference);
                break;
            case "cancelled":
            case "expired":
                await _db.InTransactionAsync(async tx =>
                {
                    await _documents.SetStatusAsync(doc.Id, state == "cancelled" ? "Cancelled" : "Expired", tx);
                    return await _applications.TransitionAsync(row.Id, ApplicationStatus.AwaitingSignature,
                        state == "cancelled" ? ApplicationStatus.Cancelled : ApplicationStatus.Expired, row.Version, "ESIGN", $"Signing order {orderId} {state}", tx);
                }, ct);
                break;
        }
    }
}
