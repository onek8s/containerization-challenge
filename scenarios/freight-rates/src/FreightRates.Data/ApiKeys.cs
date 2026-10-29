using System.Security.Cryptography;
using System.Text;

namespace FreightRates.Data;

public static class ApiKeys
{
    public static string Generate() =>
        "fr_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();

    public static string Hash(string apiKey) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(apiKey))).ToLowerInvariant();
}
