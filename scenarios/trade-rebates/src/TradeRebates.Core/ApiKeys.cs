using System.Security.Cryptography;
using System.Text;

namespace ContosoFoods.TradeRebates.Core;

/// <summary>Distributor API keys: "tr_" + 40 random characters. Only the SHA-256 hash and the first 8 characters are stored.</summary>
public static class ApiKeys
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";

    public static string Generate()
    {
        var chars = new char[40];
        for (int i = 0; i < chars.Length; i++)
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return "tr_" + new string(chars);
    }

    public static byte[] Hash(string key) => SHA256.HashData(Encoding.UTF8.GetBytes(key.Trim()));

    public static string Prefix(string key) => key.Trim().Length >= 8 ? key.Trim()[..8] : key.Trim().PadRight(8);

    public static bool LooksValid(string? key) => key is { Length: 43 } && key.StartsWith("tr_", StringComparison.Ordinal);
}
