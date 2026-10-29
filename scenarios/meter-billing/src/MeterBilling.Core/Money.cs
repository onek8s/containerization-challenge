using System;

namespace Coho.Billing.Core
{
    public static class Money
    {
        public static decimal Round(decimal value)
        {
            return Math.Round(value, 2, MidpointRounding.AwayFromZero);
        }

        public static decimal RoundPrice(decimal value)
        {
            return Math.Round(value, 5, MidpointRounding.AwayFromZero);
        }
    }
}
