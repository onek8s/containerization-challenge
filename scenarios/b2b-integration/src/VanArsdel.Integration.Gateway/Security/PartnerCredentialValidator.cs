using System;
using System.IdentityModel.Selectors;
using System.ServiceModel;
using log4net;
using VanArsdel.Integration.Data;

namespace VanArsdel.Integration.Gateway.Security
{
    /// <summary>
    /// WS-Security UsernameToken check (customUserNamePasswordValidatorType in Web.config). The user name is the
    /// PartnerId; TLS terminates on the reverse proxy and again on IIS, so the token never travels in clear text.
    /// </summary>
    public class PartnerCredentialValidator : UserNamePasswordValidator
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(PartnerCredentialValidator));
        private static readonly Lazy<IPartnerCredentialStore> DefaultStore = new Lazy<IPartnerCredentialStore>(() =>
        {
            IntegrationDatabase.EnsureFactory();
            return new SqlPartnerCredentialStore(new IntegrationDatabase());
        });

        private readonly IPartnerCredentialStore _store;

        public PartnerCredentialValidator() : this(null)
        {
        }

        public PartnerCredentialValidator(IPartnerCredentialStore store)
        {
            _store = store;
        }

        public Func<DateTime> Clock { get; set; } = () => DateTime.UtcNow;

        public override void Validate(string userName, string password)
        {
            var store = _store ?? DefaultStore.Value;
            var partnerId = (userName ?? string.Empty).Trim().ToUpperInvariant();
            var credential = partnerId.Length == 0 ? null : store.Find(partnerId);

            if (credential == null || !credential.Enabled || (credential.ExpiresUtc.HasValue && credential.ExpiresUtc.Value < Clock()))
            {
                Log.WarnFormat("Gateway sign-in refused for '{0}' (unknown, disabled or expired)", partnerId);
                throw new FaultException("Unknown partner or wrong secret.");
            }

            var hash = SqlPartnerCredentialStore.HashSecret(credential.Salt, password ?? string.Empty);
            if (!string.Equals(hash, credential.SecretHash, StringComparison.OrdinalIgnoreCase))
            {
                Log.WarnFormat("Gateway sign-in refused for '{0}' (wrong secret)", partnerId);
                throw new FaultException("Unknown partner or wrong secret.");
            }
        }
    }
}
