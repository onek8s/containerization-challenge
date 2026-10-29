using Fabrikam.Housing.BackOffice.Hubs;
using Fabrikam.Housing.BackOffice.Infrastructure;
using Fabrikam.Housing.Hosting;
using Fabrikam.Housing.Hosting.Health;
using Fabrikam.Housing.Hosting.Security;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Fabrikam.Housing.BackOffice
{
    public class Startup
    {
        public Startup(IConfiguration configuration, IWebHostEnvironment environment)
        {
            Configuration = configuration;
            Environment = environment;
        }

        public IConfiguration Configuration { get; }
        public IWebHostEnvironment Environment { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddHousingCore(Configuration, typeof(Startup).Assembly);
            services.AddHousingDataProtection(Configuration, "housing");
            services.AddHousingSignIn(Configuration, ".Fabrikam.Housing");
            if (Environment.IsDevelopment())
            {
                // The development identity server runs on plain http, where browsers drop SameSite=None cookies.
                services.PostConfigure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, o =>
                {
                    o.CorrelationCookie.SameSite = SameSiteMode.Lax;
                    o.NonceCookie.SameSite = SameSiteMode.Lax;
                });
            }

            services.AddAuthorization(options =>
            {
                options.AddPolicy(Policies.Staff, p => p.RequireRole(HousingRoles.Staff));
                options.AddPolicy(Policies.Income, p => p.RequireRole(HousingRoles.Income, HousingRoles.Officer, HousingRoles.Admin));
                options.AddPolicy(Policies.Repairs, p => p.RequireRole(HousingRoles.Repairs, HousingRoles.Officer, HousingRoles.Admin));
                options.AddPolicy(Policies.Compliance, p => p.RequireRole(HousingRoles.Compliance, HousingRoles.Admin));
                options.AddPolicy(Policies.Finance, p => p.RequireRole(HousingRoles.Finance, HousingRoles.Admin));
                options.AddPolicy(Policies.Admin, p => p.RequireRole(HousingRoles.Admin));
            });

            services.AddControllersWithViews(options =>
            {
                options.Filters.Add(new AuthorizeFilter(Policies.Staff));
                options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
            });
            services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = 30 * 1024 * 1024);

            services.AddSignalR();
            services.AddHostedService<RepairsBoardBroadcaster>();
            services.AddScoped<ReportCache>();
            services.AddScoped<TenancyLetters>();
        }

        public void Configure(IApplicationBuilder app)
        {
            if (Environment.IsDevelopment())
            {
                DevelopmentDatabase.EnsureSeeded(app.ApplicationServices);
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHousingHealth();
            app.UseStaticFiles();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHub<RepairsHub>("/hubs/repairs").RequireAuthorization(Policies.Staff);
                endpoints.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
