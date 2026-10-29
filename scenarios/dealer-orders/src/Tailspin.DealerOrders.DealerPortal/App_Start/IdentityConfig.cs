using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin;
using Microsoft.Owin.Security;
using Tailspin.DealerOrders.Data;
using Tailspin.DealerOrders.Infrastructure;

namespace Tailspin.DealerOrders.DealerPortal
{
    public class PortalUserManager : UserManager<PortalUser>
    {
        public PortalUserManager(IUserStore<PortalUser> store) : base(store)
        {
        }

        public static PortalUserManager Create(IdentityFactoryOptions<PortalUserManager> options, IOwinContext context)
        {
            var manager = new PortalUserManager(new UserStore<PortalUser>(context.Get<DealerOrdersContext>()));
            manager.UserValidator = new UserValidator<PortalUser>(manager) { AllowOnlyAlphanumericUserNames = false, RequireUniqueEmail = true };
            manager.PasswordValidator = new PasswordValidator
            {
                RequiredLength = 10,
                RequireNonLetterOrDigit = false,
                RequireDigit = true,
                RequireLowercase = true,
                RequireUppercase = true
            };
            manager.UserLockoutEnabledByDefault = true;
            manager.DefaultAccountLockoutTimeSpan = TimeSpan.FromMinutes(Settings.Int("Portal.LockoutMinutes", 15));
            manager.MaxFailedAccessAttemptsBeforeLockout = Settings.Int("Portal.MaxFailedLogins", 5);

            var protector = options.DataProtectionProvider;
            if (protector != null)
                manager.UserTokenProvider = new DataProtectorTokenProvider<PortalUser>(protector.Create("PasswordReset"))
                {
                    TokenLifespan = TimeSpan.FromHours(24)
                };
            return manager;
        }

        public override async Task<bool> CheckPasswordAsync(PortalUser user, string password) =>
            !user.Disabled && await base.CheckPasswordAsync(user, password);
    }

    public class PortalSignInManager : SignInManager<PortalUser, string>
    {
        public PortalSignInManager(PortalUserManager userManager, IAuthenticationManager authenticationManager)
            : base(userManager, authenticationManager)
        {
        }

        public override Task<ClaimsIdentity> CreateUserIdentityAsync(PortalUser user) =>
            user.GenerateUserIdentityAsync((PortalUserManager)UserManager, DefaultAuthenticationTypes.ApplicationCookie);

        public static PortalSignInManager Create(IdentityFactoryOptions<PortalSignInManager> options, IOwinContext context) =>
            new PortalSignInManager(context.GetUserManager<PortalUserManager>(), context.Authentication);
    }
}
