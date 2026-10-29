using System.Security.Cryptography;

namespace StaffOnboarding.Provisioning.Provisioning;

/// <summary>
/// Random initial password. It is never shown or stored: the service desk sets the first password
/// with the new employee on the first day.
/// </summary>
public static class PasswordGenerator
{
    private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lower = "abcdefghijkmnopqrstuvwxyz";
    private const string Digits = "23456789";
    private const string Symbols = "!#%+-=?@_";

    public static string Create(int length = 24)
    {
        if (length < 8) throw new ArgumentOutOfRangeException(nameof(length));
        var all = Upper + Lower + Digits + Symbols;
        var chars = new char[length];
        chars[0] = Pick(Upper);
        chars[1] = Pick(Lower);
        chars[2] = Pick(Digits);
        chars[3] = Pick(Symbols);
        for (var i = 4; i < length; i++) chars[i] = Pick(all);
        RandomNumberGenerator.Shuffle(chars.AsSpan());
        return new string(chars);
    }

    private static char Pick(string set) => set[RandomNumberGenerator.GetInt32(set.Length)];
}
