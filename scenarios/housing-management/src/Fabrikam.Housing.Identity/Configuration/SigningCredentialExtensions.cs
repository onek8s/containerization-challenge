using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Fabrikam.Housing.Identity.Configuration
{
    public static class SigningCredentialExtensions
    {
        /// <summary>
        /// Signing:Mode = Store loads the token signing certificate from LocalMachine\My by thumbprint;
        /// Development creates a key file (Signing:DeveloperKeyFile, default tempkey.jwk) in the application folder.
        /// </summary>
        public static IIdentityServerBuilder AddHousingSigningCredential(this IIdentityServerBuilder builder, IConfiguration configuration, IHostEnvironment environment)
        {
            var mode = configuration["Signing:Mode"] ?? "Store";
            if (string.Equals(mode, "Development", StringComparison.OrdinalIgnoreCase))
            {
                var file = configuration["Signing:DeveloperKeyFile"] ?? "tempkey.jwk";
                var path = Path.GetFullPath(Path.Combine(environment.ContentRootPath, file));
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                return builder.AddDeveloperSigningCredential(persistKey: true, filename: path);
            }

            var thumbprint = (configuration["Signing:Thumbprint"] ?? "").Replace(" ", "").ToUpperInvariant();
            using (var store = new X509Store(StoreName.My, StoreLocation.LocalMachine))
            {
                store.Open(OpenFlags.ReadOnly);
                var found = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, validOnly: false);
                if (found.Count == 0)
                    throw new InvalidOperationException($"Token signing certificate {thumbprint} was not found in LocalMachine\\My.");
                builder.AddSigningCredential(found[0]);

                var previous = (configuration["Signing:PreviousThumbprint"] ?? "").Replace(" ", "").ToUpperInvariant();
                if (previous.Length > 0)
                {
                    var old = store.Certificates.Find(X509FindType.FindByThumbprint, previous, validOnly: false);
                    if (old.Count > 0) builder.AddValidationKey(old[0]);
                }
            }
            return builder;
        }
    }
}
