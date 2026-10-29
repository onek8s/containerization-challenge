using Lucerne.Lending.Data;
using Lucerne.Lending.Integration;

namespace Lucerne.Lending.Documents;

public sealed class Notifications
{
    private readonly IMailSender _mail;
    private readonly IConfiguration _configuration;

    public Notifications(IMailSender mail, IConfiguration configuration)
    {
        _mail = mail;
        _configuration = configuration;
    }

    public Task SendAsync(CustomerNotification n, CancellationToken ct)
    {
        var portal = _configuration["Portal:BaseUrl"] ?? "https://apply.corp.local";
        string V(string k) => n.Values.TryGetValue(k, out var v) ? v : "";
        var (subject, body) = n.Template switch
        {
            "OfferReady" => ($"Your loan offer {V("reference")} is ready",
                $"Hello {V("name")},\n\nyour loan offer is ready. You can review it and the pre-contract information in the online service: " +
                $"{portal}/applications/{V("reference")}\n\nThe offer is valid until {V("validUntil")}."),
            "SignAgreement" => ($"Sign your loan agreement {V("reference")}",
                $"Hello {V("name")},\n\nyour loan agreement is ready for signing with your bank credentials:\n{V("url")}"),
            "Declined" => ($"Decision on your loan application {V("reference")}",
                $"Hello {V("name")},\n\nthe decision on your loan application is available in the online service: {portal}/applications/{V("reference")}"),
            "LoanDisbursed" => ($"Your loan {V("account")} has been paid",
                $"Hello,\n\nthe loan ({V("amount")} EUR) has been paid to your account. Repayments are made with reference {V("paymentReference")}."),
            _ => throw new InvalidOperationException($"Unknown mail template {n.Template}.")
        };
        return _mail.SendAsync(n.Email, subject, body + "\n\nLucerne Savings Bank\nThis is an automatic message, please do not reply.", null, ct);
    }
}
