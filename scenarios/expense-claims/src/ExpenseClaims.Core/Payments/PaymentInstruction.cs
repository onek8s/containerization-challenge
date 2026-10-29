namespace ExpenseClaims.Core.Payments;

public sealed record PaymentInstruction(
    string EndToEndId,
    string CreditorName,
    string CreditorIban,
    string? CreditorBic,
    decimal Amount,
    string RemittanceInfo);

public sealed record DebtorAccount(string Name, string Iban, string Bic, string OrganisationId);
