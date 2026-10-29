using System;
using System.Linq;
using VanArsdel.Customers.Api.Documents;

namespace VanArsdel.Customers.Api.Domain
{
    /// <summary>
    /// VanArsdel Club: 1 point per euro (10 SEK), tier by spend in the last 12 months.
    /// Gold and Platinum earn 1.5x and 2x. 100 points = 5 EUR voucher.
    /// </summary>
    public static class LoyaltyRules
    {
        public const int MaxTransactionsKept = 300;

        public static decimal ToEur(decimal amount, string currency) =>
            string.Equals(currency, "SEK", StringComparison.OrdinalIgnoreCase) ? Math.Round(amount / 11.2m, 2) : amount;

        public static int PointsFor(decimal amountEur, string tier)
        {
            var multiplier = tier switch
            {
                LoyaltyTiers.Platinum => 2.0m,
                LoyaltyTiers.Gold => 1.5m,
                _ => 1.0m,
            };
            return (int)Math.Floor(amountEur * multiplier);
        }

        public static string TierFor(decimal rollingSpendEur) =>
            rollingSpendEur >= 2000 ? LoyaltyTiers.Platinum
            : rollingSpendEur >= 800 ? LoyaltyTiers.Gold
            : rollingSpendEur >= 300 ? LoyaltyTiers.Silver
            : LoyaltyTiers.Bronze;

        public static decimal RollingSpend(LoyaltyAccount account, DateTime nowUtc) =>
            account.Transactions.Where(t => t.Date >= nowUtc.AddMonths(-12) && (t.Type == "Earn" || t.Type == "Return")).Sum(t => t.AmountEur);

        /// <summary>Adds a points transaction unless one with the same reference exists. Returns the points added.</summary>
        public static int Apply(LoyaltyAccount account, string type, decimal amountEur, string source, string reference, DateTime date)
        {
            if (account.Transactions.Any(t => t.Reference == reference && t.Type == type)) return 0;
            var points = type == "Return" ? -PointsFor(Math.Abs(amountEur), account.Tier) : PointsFor(amountEur, account.Tier);
            account.Transactions.Add(new PointsTransaction
            {
                Date = date,
                Type = type,
                Points = points,
                AmountEur = type == "Return" ? -Math.Abs(amountEur) : amountEur,
                Source = source,
                Reference = reference,
            });
            account.Points = Math.Max(0, account.Points + points);
            if (account.Transactions.Count > MaxTransactionsKept)
            {
                account.Transactions = account.Transactions.OrderByDescending(t => t.Date).Take(MaxTransactionsKept).OrderBy(t => t.Date).ToList();
            }
            return points;
        }

        /// <summary>Luhn-checked 12-digit card number starting with 6438.</summary>
        public static string NewCardNo(Random random)
        {
            var digits = "6438" + string.Concat(Enumerable.Range(0, 7).Select(_ => random.Next(10).ToString()));
            return digits + LuhnDigit(digits);
        }

        public static bool IsValidCardNo(string cardNo) =>
            cardNo != null && cardNo.Length == 12 && cardNo.All(char.IsDigit) && LuhnDigit(cardNo.Substring(0, 11)) == cardNo[11] - '0';

        private static int LuhnDigit(string digits)
        {
            var sum = 0;
            var doubleIt = true;
            for (var i = digits.Length - 1; i >= 0; i--)
            {
                var d = digits[i] - '0';
                if (doubleIt)
                {
                    d *= 2;
                    if (d > 9) d -= 9;
                }
                sum += d;
                doubleIt = !doubleIt;
            }
            return (10 - sum % 10) % 10;
        }
    }
}
