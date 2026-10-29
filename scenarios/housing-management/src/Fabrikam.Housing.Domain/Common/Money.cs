using System;

namespace Fabrikam.Housing.Common
{
    public static class Money
    {
        public static decimal Round(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);

        public static int ToPence(decimal amount) => (int)Math.Round(amount * 100m, 0, MidpointRounding.AwayFromZero);

        public static string Format(decimal amount) => amount < 0 ? "-£" + (-amount).ToString("N2") : "£" + amount.ToString("N2");
    }
}
