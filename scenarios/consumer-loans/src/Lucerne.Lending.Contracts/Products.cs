namespace Lucerne.Lending.Contracts;

public sealed class ProductDto
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal MinAmount { get; set; }
    public decimal MaxAmount { get; set; }
    public int MinTermMonths { get; set; }
    public int MaxTermMonths { get; set; }
    public decimal IndicativeRatePercent { get; set; }
    public decimal ArrangementFee { get; set; }
    public decimal MonthlyFee { get; set; }
    public bool RequiresCollateral { get; set; }
    public bool AllowsCoApplicant { get; set; }
    public string[] Purposes { get; set; } = Array.Empty<string>();
}

public sealed class CalculatorRequest
{
    public string ProductCode { get; set; } = "";
    public decimal Amount { get; set; }
    public int TermMonths { get; set; }
}

public sealed class CalculatorResponse
{
    public decimal NominalRatePercent { get; set; }
    public decimal AprPercent { get; set; }
    public decimal MonthlyPayment { get; set; }
    public decimal MonthlyFee { get; set; }
    public decimal ArrangementFee { get; set; }
    public decimal TotalRepayable { get; set; }
    public string Example { get; set; } = "";
}
