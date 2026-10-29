using System;
using System.Security.Principal;
using System.Web;
using System.Web.Security;

namespace VanArsdel.Integration.Tracking.Security
{
    /// <summary>Principal built from the forms ticket: user name, roles and the partner the user is limited to.</summary>
    public class TrackingPrincipal : GenericPrincipal
    {
        public TrackingPrincipal(IIdentity identity, string[] roles, string partnerId) : base(identity, roles)
        {
            PartnerId = string.IsNullOrEmpty(partnerId) ? null : partnerId;
        }

        /// <summary>Null for internal users.</summary>
        public string PartnerId { get; private set; }

        public bool IsInternal
        {
            get { return PartnerId == null; }
        }

        public static void Attach(HttpContext context)
        {
            var identity = context.User == null ? null : context.User.Identity as FormsIdentity;
            if (identity == null || !identity.IsAuthenticated)
            {
                return;
            }

            var data = identity.Ticket.UserData ?? string.Empty;
            var bar = data.IndexOf('|');
            var partner = bar < 0 ? null : data.Substring(0, bar);
            var roles = (bar < 0 ? data : data.Substring(bar + 1)).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            context.User = new TrackingPrincipal(identity, roles, partner);
        }

        public static TrackingPrincipal From(IPrincipal user)
        {
            var p = user as TrackingPrincipal;
            if (p == null)
            {
                throw new InvalidOperationException("The request has no tracking principal.");
            }

            return p;
        }

        /// <summary>The partner filter to apply: a partner user always gets their own id, whatever was asked for.</summary>
        public string Scope(string requestedPartnerId)
        {
            return IsInternal ? (string.IsNullOrWhiteSpace(requestedPartnerId) ? null : requestedPartnerId.Trim().ToUpperInvariant()) : PartnerId;
        }

        public bool MaySee(string partnerId)
        {
            return IsInternal || string.Equals(PartnerId, partnerId, StringComparison.OrdinalIgnoreCase);
        }
    }
}
