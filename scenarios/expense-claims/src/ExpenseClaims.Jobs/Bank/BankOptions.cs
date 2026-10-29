namespace ExpenseClaims.Jobs.Bank;

public sealed class BankOptions
{
    public const string SectionName = "Bank";

    /// <summary>
    /// <c>Gateway</c>: the payment file is uploaded to the bank's corporate gateway.
    /// <c>Folder</c>: the file is only written to <see cref="OutboundFolder"/> for the bank's transfer client.
    /// </summary>
    public string Mode { get; set; } = "Gateway";

    public string GatewayUrl { get; set; } = "https://corporate-gateway.nordbank.example/api/v2/";

    /// <summary>Thumbprint of the client certificate in LocalMachine\My that the bank issued for the company.</summary>
    public string ClientCertificateThumbprint { get; set; } = "";

    /// <summary>Customer id agreed with the bank, sent in every request.</summary>
    public string CustomerId { get; set; } = "";

    public string OutboundFolder { get; set; } = @"D:\Interfaces\Bank\Outbound";

    public string DebtorName { get; set; } = "Lamna Healthcare Oy";
    public string DebtorIban { get; set; } = "";
    public string DebtorBic { get; set; } = "";
    public string DebtorOrganisationId { get; set; } = "";

    /// <summary>Payment files are dated for the next business day after this local time.</summary>
    public TimeSpan CutOffTime { get; set; } = new(14, 0, 0);

    public bool UsesGateway => string.Equals(Mode, "Gateway", StringComparison.OrdinalIgnoreCase);
}
