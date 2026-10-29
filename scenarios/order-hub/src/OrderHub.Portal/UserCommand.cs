using System;
using System.Linq;
using Litware.OrderHub.Portal.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace Litware.OrderHub.Portal
{
    /// <summary>OrderHub.Portal.exe create-user &lt;name&gt; &lt;password&gt; [Admin|Operator]: first administrator after deployment.</summary>
    internal static class UserCommand
    {
        public static int Run(string[] args)
        {
            if (args.Length < 3)
            {
                Console.Error.WriteLine("Usage: OrderHub.Portal.exe create-user <name> <password> [Admin|Operator]");
                return 2;
            }
            string role = args.Length > 3 ? args[3] : Roles.Admin;
            if (!Roles.All.Contains(role))
            {
                Console.Error.WriteLine("Role must be Admin or Operator.");
                return 2;
            }

            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile("appsettings.Production.json", optional: true)
                .Build();

            var services = new ServiceCollection();
            services.AddLogging(b => b.AddConsole());
            services.AddDbContext<PortalIdentityContext>(o => o.UseSqlServer(configuration.GetConnectionString("OrderHub")));
            services.AddIdentityCore<IdentityUser>(o => { o.Password.RequiredLength = 10; o.Password.RequireNonAlphanumeric = false; })
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<PortalIdentityContext>();

            using (var provider = services.BuildServiceProvider())
            {
                var roles = provider.GetRequiredService<RoleManager<IdentityRole>>();
                foreach (var name in Roles.All)
                {
                    if (!roles.RoleExistsAsync(name).Result) roles.CreateAsync(new IdentityRole(name)).Wait();
                }

                var users = provider.GetRequiredService<UserManager<IdentityUser>>();
                var user = users.FindByNameAsync(args[1]).Result;
                IdentityResult result;
                if (user == null)
                {
                    user = new IdentityUser(args[1]);
                    result = users.CreateAsync(user, args[2]).Result;
                }
                else
                {
                    var token = users.GeneratePasswordResetTokenAsync(user).Result;
                    result = users.ResetPasswordAsync(user, token, args[2]).Result;
                }

                if (result.Succeeded && !users.IsInRoleAsync(user, role).Result)
                {
                    result = users.AddToRoleAsync(user, role).Result;
                }

                if (!result.Succeeded)
                {
                    Console.Error.WriteLine(string.Join(Environment.NewLine, result.Errors.Select(e => e.Description)));
                    return 1;
                }
                Console.WriteLine("User " + args[1] + " is " + role + ".");
                return 0;
            }
        }
    }
}
