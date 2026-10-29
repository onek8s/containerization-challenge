using System.Security.Cryptography;
using System.Text;

namespace PolicyChanges.Data.Services;

public static class ApiKeys
{
    public static string Hash(string key) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key.Trim()))).ToLowerInvariant();

    public static string Generate(string partnerCode) =>
        $"pc_{partnerCode.ToLowerInvariant()}_{Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant()}";
}
