namespace ExpenseClaims.Data.Entities;

public class ExchangeRate
{
    public DateOnly Date { get; set; }
    public string Currency { get; set; } = "";
    public decimal UnitsPerEuro { get; set; }
}
