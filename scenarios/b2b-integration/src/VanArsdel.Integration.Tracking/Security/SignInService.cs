using System;
using System.Configuration;
using System.Globalization;
using System.Web;
using System.Web.Security;
using log4net;

namespace VanArsdel.Integration.Tracking.Security
{
    public enum SignInResult
    {
        Success,
        Failed,
        LockedOut,
        MustChangePassword
    }

    /// <summary>Forms authentication against dbo.PortalUser with lockout after repeated failures.</summary>
    public class SignInService
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(SignInService));
        private readonly IPortalUserStore _users;
        private readonly PasswordHasher _hasher;

        public SignInService(IPortalUserStore users, PasswordHasher hasher)
        {
            _users = users;
            _hasher = hasher;
            MaxFailedAttempts = Setting("Tracking.MaxFailedAttempts", 5);
            LockoutMinutes = Setting("Tracking.LockoutMinutes", 15);
            PasswordMaxAgeDays = Setting("Tracking.PasswordMaxAgeDays", 180);
        }

        public int MaxFailedAttempts { get; set; }
        public int LockoutMinutes { get; set; }
        public int PasswordMaxAgeDays { get; set; }

        public Func<DateTime> Clock { get; set; } = () => DateTime.UtcNow;

        public SignInResult Validate(string userName, string password, string clientAddress, out PortalUser user)
        {
            var now = Clock();
            user = string.IsNullOrWhiteSpace(userName) ? null : _users.Find(userName.Trim());
            if (user == null)
            {
                // same work for unknown user names
                bool ignored;
                _hasher.Verify(password, "PBKDF2$10000$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=", out ignored);
                Log.InfoFormat("Sign-in failed for unknown user '{0}' from {1}", userName, clientAddress);
                return SignInResult.Failed;
            }

            if (user.IsLockedOut(now))
            {
                Log.WarnFormat("Sign-in refused for locked user {0} from {1}", user.UserName, clientAddress);
                return SignInResult.LockedOut;
            }

            bool rehash;
            if (!_hasher.Verify(password, user.PasswordHash, out rehash))
            {
                var failed = user.FailedAttempts + 1;
                DateTime? until = failed >= MaxFailedAttempts ? now.AddMinutes(LockoutMinutes) : (DateTime?)null;
                _users.RecordLogin(user.UserName, false, until.HasValue ? 0 : failed, until, now, clientAddress);
                Log.InfoFormat("Sign-in failed for {0} from {1} ({2} failures{3})", user.UserName, clientAddress, failed, until.HasValue ? ", locked" : string.Empty);
                return until.HasValue ? SignInResult.LockedOut : SignInResult.Failed;
            }

            if (rehash)
            {
                user.PasswordHash = _hasher.Hash(password);
                _users.Save(user, user.UserName);
            }

            _users.RecordLogin(user.UserName, true, 0, null, now, clientAddress);
            var expired = user.PasswordChangedUtc.AddDays(PasswordMaxAgeDays) < now;
            return user.MustChangePassword || expired ? SignInResult.MustChangePassword : SignInResult.Success;
        }

        public HttpCookie CreateCookie(PortalUser user, bool persistent)
        {
            var userData = (user.PartnerId ?? string.Empty) + "|" + string.Join(",", user.RoleList);
            var ticket = new FormsAuthenticationTicket(2, user.UserName, Clock().ToLocalTime(), Clock().ToLocalTime().Add(FormsAuthentication.Timeout), persistent, userData, FormsAuthentication.FormsCookiePath);
            var cookie = new HttpCookie(FormsAuthentication.FormsCookieName, FormsAuthentication.Encrypt(ticket))
            {
                HttpOnly = true,
                Secure = FormsAuthentication.RequireSSL,
                Path = FormsAuthentication.FormsCookiePath
            };

            if (persistent)
            {
                cookie.Expires = ticket.Expiration;
            }

            return cookie;
        }

        public void ChangePassword(PortalUser user, string newPassword)
        {
            user.PasswordHash = _hasher.Hash(newPassword);
            user.MustChangePassword = false;
            _users.Save(user, user.UserName);
        }

        private static int Setting(string key, int fallback)
        {
            int value;
            return int.TryParse(ConfigurationManager.AppSettings[key], NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : fallback;
        }
    }
}
