using System.Security.Cryptography;
using System.Text;

namespace FreightRates.RatingHost.Engine;

/// <summary>
/// The licence password is kept in configuration protected with DPAPI for the local machine,
/// so the service account can read it on this server only.
/// </summary>
public static class LicencePassword
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Contoso.FreightRates.RatingHost/MRS");

    public static string Protect(string password)
    {
        var data = ProtectedData.Protect(Encoding.UTF8.GetBytes(password), Entropy, DataProtectionScope.LocalMachine);
        return Convert.ToBase64String(data);
    }

    public static string Unprotect(string protectedPassword)
    {
        if (string.IsNullOrWhiteSpace(protectedPassword))
            throw new InvalidOperationException("RatingEngine:EncryptedPassword is not configured. Run 'FreightRates.RatingHost.exe protect-password' on this server.");
        var data = ProtectedData.Unprotect(Convert.FromBase64String(protectedPassword), Entropy, DataProtectionScope.LocalMachine);
        return Encoding.UTF8.GetString(data);
    }
}
