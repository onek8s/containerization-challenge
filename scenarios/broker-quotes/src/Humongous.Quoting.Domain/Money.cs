namespace Humongous.Quoting.Domain;

public static class Money
{
    public static decimal Round(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);

    public static string Format(decimal amount) =>
        amount.ToString("#,##0.00", System.Globalization.CultureInfo.InvariantCulture);
}
