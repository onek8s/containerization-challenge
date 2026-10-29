using System.Globalization;

namespace Proseware.ControlTower.Domain;

public readonly record struct Money(decimal Amount, string Currency)
{
    public static Money Eur(decimal amount) => new(amount, "EUR");

    public static Money Zero(string currency) => new(0m, currency);

    public Money Round() => new(Math.Round(Amount, 2, MidpointRounding.AwayFromZero), Currency);

    public static Money operator +(Money a, Money b)
    {
        if (!string.Equals(a.Currency, b.Currency, StringComparison.Ordinal))
            throw new InvalidOperationException($"Cannot add {a.Currency} and {b.Currency}.");
        return new Money(a.Amount + b.Amount, a.Currency);
    }

    public static Money operator *(Money a, decimal factor) => new(a.Amount * factor, a.Currency);

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Amount:0.00} {Currency}");
}
