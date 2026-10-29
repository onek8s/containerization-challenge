namespace Proseware.ControlTower.Domain.Shipments;

/// <summary>House shipment numbers: PF + two-digit year + dash + 7-digit sequence + mod 10 check digit.</summary>
public static class ShipmentNumber
{
    public static string Format(int year, long sequence)
    {
        if (sequence is < 0 or > 999_999) throw new ArgumentOutOfRangeException(nameof(sequence));
        var body = $"{year % 100:00}{sequence:000000}";
        return $"PF{body[..2]}-{body[2..]}{CheckDigit(body)}";
    }

    public static bool IsValid(string? number)
    {
        if (number is null || number.Length != 12 || !number.StartsWith("PF", StringComparison.Ordinal) || number[4] != '-') return false;
        var body = number.Substring(2, 2) + number.Substring(5, 6);
        if (!body.All(char.IsDigit) || !char.IsDigit(number[11])) return false;
        return CheckDigit(body) == number[11] - '0';
    }

    internal static int CheckDigit(string digits)
    {
        // Luhn
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
