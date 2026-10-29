using ContosoFoods.TradeRebates.Core;
using ContosoFoods.TradeRebates.Rules;

namespace ContosoFoods.TradeRebates.Api;

public sealed class SubmitClaimRequest
{
    /// <summary>Claim period yyyyMM.</summary>
    public int Period { get; set; }

    /// <summary>Distributor's own reference, e.g. the sell-out report number.</summary>
    public string? Reference { get; set; }

    public List<ClaimLineDto> Lines { get; set; } = [];
}

public sealed class ClaimLineDto
{
    public string InvoiceNo { get; set; } = "";
    public DateTime InvoiceDate { get; set; }
    public string? CustomerNo { get; set; }
    public string Gtin { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal NetAmount { get; set; }
}

public sealed record SubmitClaimResponse(int ClaimId, string Status, int Lines, int Warnings, int Rejected, decimal TotalAmount);

public static class ClaimRequestValidator
{
    public const int MaxLines = 5000;

    public static Dictionary<string, string[]> Validate(SubmitClaimRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (!ClaimFileParser.IsValidPeriod(request.Period))
            errors["period"] = ["Period must be yyyyMM."];
        if (request.Reference is { Length: > 100 })
            errors["reference"] = ["Reference can be at most 100 characters."];
        if (request.Lines is null || request.Lines.Count == 0)
            errors["lines"] = ["At least one line is required."];
        else if (request.Lines.Count > MaxLines)
            errors["lines"] = [$"At most {MaxLines} lines per claim."];
        else
        {
            for (int i = 0; i < request.Lines.Count; i++)
            {
                ClaimLineDto l = request.Lines[i];
                var lineErrors = new List<string>();
                if (string.IsNullOrWhiteSpace(l.InvoiceNo) || l.InvoiceNo.Length > 20)
                    lineErrors.Add("invoiceNo is required (max 20 characters).");
                if (l.InvoiceDate == default)
                    lineErrors.Add("invoiceDate is required.");
                if (l.CustomerNo is { Length: > 20 })
                    lineErrors.Add("customerNo can be at most 20 characters.");
                if (string.IsNullOrWhiteSpace(l.Gtin) || l.Gtin.Trim().Length > 14 || !l.Gtin.Trim().All(char.IsAsciiDigit))
                    lineErrors.Add("gtin must be 8 to 14 digits.");
                if (lineErrors.Count > 0)
                    errors[$"lines[{i}]"] = lineErrors.ToArray();
            }
        }
        return errors;
    }

    public static IReadOnlyList<ClaimLineInput> ToInput(SubmitClaimRequest request) =>
        request.Lines.Select(l => new ClaimLineInput
        {
            InvoiceNo = l.InvoiceNo.Trim(),
            InvoiceDate = l.InvoiceDate.Date,
            CustomerNo = l.CustomerNo?.Trim() ?? "",
            Gtin = l.Gtin.Trim(),
            Quantity = l.Quantity,
            NetAmount = l.NetAmount
        }).ToList();
}
