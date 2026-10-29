using Humongous.Quoting.Contracts;
using Humongous.Quoting.Data.Entities;
using Humongous.Quoting.Domain;

namespace Humongous.Quoting.Data;

public static class Mapping
{
    public static QuoteDetail ToDetail(this Quote q, IEnumerable<StoredDocument>? documents = null) => new()
    {
        Id = q.Id,
        Reference = q.Reference,
        AgencyCode = q.AgencyCode,
        AgencyName = q.Agency?.Name ?? "",
        BrokerReference = q.BrokerReference,
        Status = q.Status,
        ProductVersion = q.ProductVersion,
        InceptionDate = q.InceptionDate,
        ValidUntil = q.ValidUntil,
        CommissionRate = q.CommissionRate,
        Risk = q.Risk,
        Premium = q.Premium,
        Referrals = q.Referrals.Select(r => new Referral(r.Code, r.Text, r.Source)).ToList(),
        Clauses = q.Clauses,
        RatingEngine = q.RatingEngine,
        RatingTables = q.RatingTables,
        RatingError = q.RatingError,
        UnderwriterNote = q.UnderwriterNote,
        PolicyId = q.PolicyId,
        RenewalOfPolicyId = q.RenewalOfPolicyId,
        Documents = (documents ?? []).Select(ToInfo).ToList(),
        History = q.Events.OrderBy(e => e.AtUtc).Select(e => new QuoteHistoryEntry(e.AtUtc, e.Event, e.By, e.Detail)).ToList()
    };

    public static PolicySummary ToSummary(this Policy p) => new()
    {
        Id = p.Id,
        PolicyNumber = p.PolicyNumber,
        AgencyCode = p.AgencyCode,
        InsuredName = p.InsuredName,
        Status = p.Status,
        InceptionDate = p.InceptionDate,
        ExpiryDate = p.ExpiryDate,
        AnnualGrossPremium = p.AnnualGrossPremium
    };

    public static PolicyDetail ToDetail(this Policy p, IEnumerable<StoredDocument>? documents = null) => new()
    {
        Id = p.Id,
        PolicyNumber = p.PolicyNumber,
        AgencyCode = p.AgencyCode,
        Status = p.Status,
        InceptionDate = p.InceptionDate,
        ExpiryDate = p.ExpiryDate,
        ProductVersion = p.ProductVersion,
        Risk = p.Risk,
        Premium = p.Premium,
        Clauses = p.Clauses,
        QuoteId = p.QuoteId,
        RenewalQuoteId = p.RenewalQuoteId,
        Transactions = p.Transactions.OrderBy(t => t.CreatedUtc).Select(t => new PolicyTransactionInfo
        {
            Id = t.Id,
            Type = t.Type,
            EffectiveDate = t.EffectiveDate,
            GrossPremium = t.GrossPremium,
            Ipt = t.Ipt,
            Commission = t.Commission,
            Reason = t.Reason,
            CreatedUtc = t.CreatedUtc
        }).ToList(),
        Documents = (documents ?? []).Select(ToInfo).ToList()
    };

    public static DocumentInfo ToInfo(this StoredDocument d) => new(d.Id, d.DocumentType, d.FileName, d.CreatedUtc);

    public static AgencyInfo ToInfo(this Agency a) => new()
    {
        Code = a.Code,
        Name = a.Name,
        FcaNumber = a.FcaNumber,
        CommissionRate = a.CommissionRate,
        Active = a.Active,
        DocumentEmail = a.DocumentEmail
    };
}
