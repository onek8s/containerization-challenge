using System;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.AspNet.SignalR;
using Microsoft.Owin;
using Microsoft.Owin.Security.Cookies;
using Owin;
using Tailspin.DealerOrders.Data;
using Tailspin.DealerOrders.Infrastructure;

[assembly: OwinStartup(typeof(Tailspin.DealerOrders.DealerPortal.Startup))]

namespace Tailspin.DealerOrders.DealerPortal
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            app.CreatePerOwinContext(() => new DealerOrdersContext());
            app.CreatePerOwinContext<PortalUserManager>(PortalUserManager.Create);
            app.CreatePerOwinContext<PortalSignInManager>(PortalSignInManager.Create);

            app.UseCookieAuthentication(new CookieAuthenticationOptions
            {
                AuthenticationType = DefaultAuthenticationTypes.ApplicationCookie,
                LoginPath = new PathString("/account/login"),
                CookieName = ".TSDEALERS",
                CookieSecure = Settings.Bool("Portal.RequireSsl", true) ? CookieSecureOption.Always : CookieSecureOption.SameAsRequest,
                ExpireTimeSpan = TimeSpan.FromHours(Settings.Int("Portal.SessionHours", 8)),
                SlidingExpiration = true,
                Provider = new CookieAuthenticationProvider
                {
                    // Signs users out within 15 minutes when an admin disables them or their password changes.
                    OnValidateIdentity = SecurityStampValidator.OnValidateIdentity<PortalUserManager, PortalUser>(
                        TimeSpan.FromMinutes(15), (manager, user) => user.GenerateUserIdentityAsync(manager, DefaultAuthenticationTypes.ApplicationCookie))
                }
            });

            GlobalHost.Configuration.DisconnectTimeout = TimeSpan.FromSeconds(30);
            app.MapSignalR("/signalr", new HubConfiguration { EnableDetailedErrors = false, EnableJavaScriptProxies = true });
        }
    }
}
