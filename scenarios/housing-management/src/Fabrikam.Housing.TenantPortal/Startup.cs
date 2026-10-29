using System;
using Fabrikam.Housing.Hosting;
using Fabrikam.Housing.Hosting.Health;
using Fabrikam.Housing.Hosting.Security;
using Fabrikam.Housing.TenantPortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Fabrikam.Housing.TenantPortal
{
    public class Startup
    {
        public const string TenantPolicy = "Tenant";

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddHousingCore(Configuration, typeof(Startup).Assembly);
            services.AddHousingDataProtection(Configuration, "tenants");
            services.AddHousingSignIn(Configuration, "Fabrikam.Tenants");

            services.AddAuthorization(options =>
            {
                options.AddPolicy(TenantPolicy, policy => policy.RequireAuthenticatedUser().RequireClaim(HousingClaimTypes.TenantId));
            });

            // The report-a-repair wizard keeps its state in session; photos wait in Uploads:TempFolder until submit.
            services.AddDistributedMemoryCache();
            services.AddSession(options =>
            {
                options.Cookie.Name = "Fabrikam.Tenants.Session";
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
                options.IdleTimeout = TimeSpan.FromMinutes(20);
            });

            services.AddSameSiteCookiePolicy();
            services.AddHttpContextAccessor();
            services.AddScoped<CurrentTenant>();
            services.AddScoped<RepairWizardStore>();
            services.AddScoped<TenantNotifier>();

            services.AddControllersWithViews(options =>
            {
                options.Filters.Add(new AuthorizeFilter(TenantPolicy));
                options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
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
            app.UseCookiePolicy();
            app.UseRouting();
            app.UseSession();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseEndpoints(endpoints => endpoints.MapDefaultControllerRoute());
        }
    }
}
