namespace Lucerne.Lending.Domain;

/// <summary>Finnish national reference (viitenumero, weights 7-3-1) and RF creditor reference (ISO 11649).</summary>
public static class PaymentReference
{
    public static string National(string baseNumber)
    {
        if (baseNumber.Length is < 3 or > 19 || !baseNumber.All(char.IsDigit))
            throw new ArgumentException("Reference base must be 3-19 digits.", nameof(baseNumber));
        int[] weights = { 7, 3, 1 };
        var sum = 0;
        for (int i = baseNumber.Length - 1, w = 0; i >= 0; i--, w++)
            sum += (baseNumber[i] - '0') * weights[w % 3];
        var check = (10 - sum % 10) % 10;
        return baseNumber + check;
    }

    public static bool IsValidNational(string reference) =>
        reference.Length is >= 4 and <= 20 && reference.All(char.IsDigit) && National(reference[..^1]) == reference;

    public static string Rf(string nationalReference)
    {
        var numeric = nationalReference + "271500"; // "RF00" -> R=27, F=15, 00
        var mod = Mod97(numeric);
        return $"RF{98 - mod:00}{nationalReference}";
    }

    private static int Mod97(string digits)
    {
        var remainder = 0;
        foreach (var c in digits) remainder = (remainder * 10 + (c - '0')) % 97;
        return remainder;
    }

    /// <summary>Repayment reference for a core loan account: account number digits.</summary>
    public static string ForLoanAccount(string coreAccountNumber) =>
        National(new string(coreAccountNumber.Where(char.IsDigit).ToArray()).TrimStart('0').PadLeft(3, '1'));
}
