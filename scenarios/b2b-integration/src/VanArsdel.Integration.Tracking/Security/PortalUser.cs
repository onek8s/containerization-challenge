using System;
using System.Collections.Generic;
using System.Linq;

namespace VanArsdel.Integration.Tracking.Security
{
    public static class TrackingRoles
    {
        /// <summary>Customer service and sales: every partner.</summary>
        public const string CustomerService = "CustomerService";

        /// <summary>A partner's own staff: only their PartnerId.</summary>
        public const string Partner = "Partner";

        /// <summary>Manages portal users.</summary>
        public const string Admin = "TrackingAdmin";

        public static readonly string[] All = { CustomerService, Partner, Admin };
    }

    /// <summary>dbo.PortalUser. The tracking portal has its own user list because partner staff have no CORP accounts.</summary>
    public class PortalUser
    {
        public int UserId { get; set; }
        public string UserName { get; set; }
        public string DisplayName { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }

        /// <summary>Set for partner users; internal users see all partners.</summary>
        public string PartnerId { get; set; }

        public string Roles { get; set; }
        public bool Locked { get; set; }
        public int FailedAttempts { get; set; }
        public DateTime? LockedUntilUtc { get; set; }
        public DateTime? LastLoginUtc { get; set; }
        public DateTime PasswordChangedUtc { get; set; }
        public bool MustChangePassword { get; set; }

        public IList<string> RoleList
        {
            get { return (Roles ?? string.Empty).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(r => r.Trim()).ToList(); }
        }

        public bool IsLockedOut(DateTime nowUtc)
        {
            return Locked || (LockedUntilUtc.HasValue && LockedUntilUtc.Value > nowUtc);
        }
    }

    public interface IPortalUserStore
    {
        PortalUser Find(string userName);

        IList<PortalUser> All();

        void Save(PortalUser user, string changedBy);

        void RecordLogin(string userName, bool succeeded, int failedAttempts, DateTime? lockedUntilUtc, DateTime atUtc, string address);
    }
}
