using System.Security.Cryptography.X509Certificates;

namespace Proseware.ControlTower.Identity.Services;

/// <summary>Token signing certificate, installed in LocalMachine\My with the private key readable by the app pool identity.</summary>
public static class SigningCredentials
{
    public static X509Certificate2 FromStore(string thumbprint, StoreLocation location = StoreLocation.LocalMachine)
    {
        thumbprint = thumbprint.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
        using var store = new X509Store(StoreName.My, location);
        store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
        var found = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, validOnly: false);
        if (found.Count == 0)
            throw new InvalidOperationException($"Signing certificate {thumbprint} not found in {location}\\My.");
        var cert = found[0];
        if (!cert.HasPrivateKey)
            throw new InvalidOperationException($"Signing certificate {thumbprint} has no accessible private key.");
        if (cert.NotAfter < DateTime.Now.AddDays(30))
            Console.Error.WriteLine($"WARNING: signing certificate {thumbprint} expires {cert.NotAfter:yyyy-MM-dd}.");
        return cert;
    }
}
