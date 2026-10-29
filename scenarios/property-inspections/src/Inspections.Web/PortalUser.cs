using System;
using System.Globalization;
using System.Web;
using System.Web.Security;
using Northwind.Inspections.Core;

namespace Northwind.Inspections.Web
{
    /// <summary>Adjuster details carried in the forms authentication ticket (id|supervisor|display name).</summary>
    public sealed class PortalUser
    {
        public const string AdjusterRole = "Adjuster";
        public const string SupervisorRole = "Supervisor";

        public int Id { get; private set; }
        public string Login { get; private set; }
        public string DisplayName { get; private set; }
        public bool IsSupervisor { get; private set; }

        public static PortalUser Current
        {
            get
            {
                var identity = HttpContext.Current.User.Identity as FormsIdentity;
                if (identity == null)
                {
                    throw new InvalidOperationException("Not signed in.");
                }
                return FromTicket(identity.Ticket);
            }
        }

        public static PortalUser FromTicket(FormsAuthenticationTicket ticket)
        {
            var parts = (ticket.UserData ?? string.Empty).Split('|');
            return new PortalUser
            {
                Login = ticket.Name,
                Id = parts.Length > 0 ? int.Parse(parts[0], CultureInfo.InvariantCulture) : 0,
                IsSupervisor = parts.Length > 1 && parts[1] == "1",
                DisplayName = parts.Length > 2 ? parts[2] : ticket.Name
            };
        }

        public static HttpCookie CreateCookie(Adjuster adjuster, bool remember)
        {
            var userData = adjuster.Id.ToString(CultureInfo.InvariantCulture) + "|" + (adjuster.IsSupervisor ? "1" : "0") + "|" + adjuster.DisplayName.Replace("|", " ");
            var now = DateTime.Now;
            var ticket = new FormsAuthenticationTicket(2, adjuster.Login, now, now.Add(FormsAuthentication.Timeout), remember, userData, FormsAuthentication.FormsCookiePath);
            return new HttpCookie(FormsAuthentication.FormsCookieName, FormsAuthentication.Encrypt(ticket))
            {
                HttpOnly = true,
                Secure = FormsAuthentication.RequireSSL,
                Path = FormsAuthentication.FormsCookiePath,
                Expires = remember ? ticket.Expiration : DateTime.MinValue
            };
        }
    }
}
