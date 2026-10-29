using System.Security.Cryptography;
using Humongous.Documents.Mail;
using Humongous.Documents.Rendering;
using Humongous.Quoting.Contracts.Messages;
using Humongous.Quoting.Data;
using Humongous.Quoting.Data.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Humongous.Documents.Consumers;

public sealed class DocumentRequestedConsumer(
    QuotingDbContext db,
    DocumentComposer composer,
    IMailSender mail,
    ILogger<DocumentRequestedConsumer> logger) : IConsumer<DocumentRequested>
{
    public async Task Consume(ConsumeContext<DocumentRequested> context)
    {
        var m = context.Message;
        var ct = context.CancellationToken;

        var quote = m.QuoteId is { } quoteId ? await db.Quotes.AsNoTracking().FirstOrDefaultAsync(q => q.Id == quoteId, ct) : null;
        var policy = m.PolicyId is { } policyId ? await db.Policies.AsNoTracking().FirstOrDefaultAsync(p => p.Id == policyId, ct) : null;
        var transaction = m.TransactionId is { } transactionId ? await db.PolicyTransactions.AsNoTracking().FirstOrDefaultAsync(t => t.Id == transactionId, ct) : null;
        var agencyCode = policy?.AgencyCode ?? quote?.AgencyCode;
        if (agencyCode is null)
        {
            logger.LogWarning("{Type} requested for a quote or policy that does not exist ({QuoteId}/{PolicyId})", m.DocumentType, m.QuoteId, m.PolicyId);
            return;
        }
        var agency = await db.Agencies.AsNoTracking().FirstAsync(a => a.Code == agencyCode, ct);

        var (fileName, pdf) = composer.Render(new DocumentModel(m.DocumentType, agency, quote, policy, transaction, DateTime.UtcNow));
        var document = new StoredDocument
        {
            Id = Guid.NewGuid(),
            DocumentType = m.DocumentType.ToString(),
            QuoteId = quote?.Id,
            PolicyId = policy?.Id,
            TransactionId = transaction?.Id,
            AgencyCode = agencyCode,
            FileName = fileName,
            Content = pdf,
            Sha256 = Convert.ToHexString(SHA256.HashData(pdf)),
            CreatedUtc = DateTime.UtcNow
        };

        var recipients = new List<string>();
        if (m.EmailToBroker && !string.IsNullOrWhiteSpace(agency.DocumentEmail)) recipients.Add(agency.DocumentEmail);
        if (!string.IsNullOrWhiteSpace(m.AdditionalRecipient)) recipients.Add(m.AdditionalRecipient);
        if (recipients.Count > 0)
        {
            var reference = policy?.PolicyNumber ?? quote!.Reference;
            await mail.SendAsync(new OutgoingMail(recipients,
                $"{reference} {policy?.InsuredName ?? quote?.InsuredName} - {fileName}",
                $"Please find attached the {m.DocumentType} for {reference}.\r\n\r\nHumongous Insurance broker services",
                fileName, pdf), ct);
            document.EmailedTo = string.Join("; ", recipients);
            document.EmailedUtc = DateTime.UtcNow;
        }

        db.Documents.Add(document);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Created {Type} {FileName} ({Bytes} bytes) for {Agency}", m.DocumentType, fileName, pdf.Length, agencyCode);

        await context.Publish(new DocumentReady
        {
            DocumentId = document.Id,
            DocumentType = m.DocumentType,
            QuoteId = document.QuoteId,
            PolicyId = document.PolicyId,
            AgencyCode = agencyCode,
            FileName = fileName
        });
    }
}
