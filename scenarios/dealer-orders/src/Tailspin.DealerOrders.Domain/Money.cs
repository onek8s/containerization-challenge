using System;
using System.Globalization;

namespace Tailspin.DealerOrders.Domain
{
    /// <summary>An amount in one currency. Arithmetic between different currencies is refused.</summary>
    public struct Money : IEquatable<Money>, IComparable<Money>
    {
        public Money(decimal amount, string currency)
        {
            if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
                throw new ArgumentException("Currency must be an ISO 4217 code.", nameof(currency));
            Amount = amount;
            Currency = currency.ToUpperInvariant();
        }

        public decimal Amount { get; }
        public string Currency { get; }

        public static Money Zero(string currency) => new Money(0m, currency);

        public Money Round() => new Money(Math.Round(Amount, Currencies.Decimals(Currency), MidpointRounding.AwayFromZero), Currency);

        public Money Multiply(decimal factor) => new Money(Amount * factor, Currency);

        public Money Percent(decimal percent) => new Money(Amount * percent / 100m, Currency);

        public static Money operator +(Money a, Money b) { Check(a, b); return new Money(a.Amount + b.Amount, a.Currency); }
        public static Money operator -(Money a, Money b) { Check(a, b); return new Money(a.Amount - b.Amount, a.Currency); }
        public static Money operator -(Money a) => new Money(-a.Amount, a.Currency);
        public static bool operator ==(Money a, Money b) => a.Equals(b);
        public static bool operator !=(Money a, Money b) => !a.Equals(b);
        public static bool operator >(Money a, Money b) { Check(a, b); return a.Amount > b.Amount; }
        public static bool operator <(Money a, Money b) { Check(a, b); return a.Amount < b.Amount; }
        public static bool operator >=(Money a, Money b) { Check(a, b); return a.Amount >= b.Amount; }
        public static bool operator <=(Money a, Money b) { Check(a, b); return a.Amount <= b.Amount; }

        private static void Check(Money a, Money b)
        {
            if (!string.Equals(a.Currency, b.Currency, StringComparison.Ordinal))
                throw new InvalidOperationException($"Cannot combine {a.Currency} and {b.Currency} amounts.");
        }

        public bool Equals(Money other) => Amount == other.Amount && string.Equals(Currency, other.Currency, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is Money m && Equals(m);
        public override int GetHashCode() => unchecked((Amount.GetHashCode() * 397) ^ (Currency ?? "").GetHashCode());

        public int CompareTo(Money other) { Check(this, other); return Amount.CompareTo(other.Amount); }

        public override string ToString() =>
            Amount.ToString("N" + Currencies.Decimals(Currency ?? "EUR"), CultureInfo.InvariantCulture) + " " + Currency;
    }

    public static class Currencies
    {
        public const string Euro = "EUR";

        /// <summary>Number of decimals used on dealer documents. SEK and NOK machine prices are quoted in whole crowns.</summary>
        public static int Decimals(string currency)
        {
            switch (currency)
            {
                case "SEK":
                case "NOK":
                case "JPY":
                    return 0;
                default:
                    return 2;
            }
        }
    }
}
