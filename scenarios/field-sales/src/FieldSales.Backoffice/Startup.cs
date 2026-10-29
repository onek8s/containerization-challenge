using System;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin;
using Microsoft.Owin.Security.Cookies;
using Owin;
using Wingtip.FieldSales.Backoffice.Services;
using Wingtip.FieldSales.Data;

[assembly: OwinStartup(typeof(Wingtip.FieldSales.Backoffice.Startup))]

namespace Wingtip.FieldSales.Backoffice
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            app.CreatePerOwinContext(FieldSalesDbContext.Create);
            app.CreatePerOwinContext<ApplicationUserManager>((options, context) =>
                new ApplicationUserManager(new Microsoft.AspNet.Identity.EntityFramework.UserStore<ApplicationUser>(context.Get<FieldSalesDbContext>())));
            app.CreatePerOwinContext<ApplicationSignInManager>(ApplicationSignInManager.Create);

            // The cookie is protected with the machine key, see system.web/machineKey.
            app.UseCookieAuthentication(new CookieAuthenticationOptions
            {
                AuthenticationType = DefaultAuthenticationTypes.ApplicationCookie,
                CookieName = "FieldSales.Backoffice",
                LoginPath = new PathString("/Account/Login"),
                ExpireTimeSpan = TimeSpan.FromHours(10),
                SlidingExpiration = true,
                Provider = new CookieAuthenticationProvider
                {
                    OnValidateIdentity = SecurityStampValidator.OnValidateIdentity<ApplicationUserManager, ApplicationUser>(
                        TimeSpan.FromMinutes(15), (manager, user) => user.CreateIdentityAsync(manager, DefaultAuthenticationTypes.ApplicationCookie))
                }
            });
        }
    }
}
