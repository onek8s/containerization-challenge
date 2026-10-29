namespace ContosoFoods.TradeRebates.Rules
{
    /// <summary>GS1 GTIN-8/12/13/14 check digit validation.</summary>
    public static class Gtin
    {
        public static bool IsValid(string? gtin)
        {
            if (gtin == null)
                return false;
            string value = gtin.Trim();
            if (value.Length != 8 && value.Length != 12 && value.Length != 13 && value.Length != 14)
                return false;

            int sum = 0;
            for (int i = 0; i < value.Length - 1; i++)
            {
                char c = value[i];
                if (c < '0' || c > '9')
                    return false;
                int digit = c - '0';
                // Weights 3,1,3,1... counted from the digit left of the check digit.
                int positionFromRight = value.Length - 1 - i;
                sum += positionFromRight % 2 == 1 ? digit * 3 : digit;
            }

            char check = value[value.Length - 1];
            if (check < '0' || check > '9')
                return false;
            return (10 - sum % 10) % 10 == check - '0';
        }

        /// <summary>Pads GTIN-8/12/13 to the 14 digit form used for storage.</summary>
        public static string ToGtin14(string gtin) => gtin.Trim().PadLeft(14, '0');
    }
}
