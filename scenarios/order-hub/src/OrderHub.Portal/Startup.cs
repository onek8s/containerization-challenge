using System;
using System.IO;
using Litware.OrderHub.Integration.Configuration;
using Litware.OrderHub.Integration.Data;
using Litware.OrderHub.Integration.Messaging;
using Litware.OrderHub.Portal.Services;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Litware.OrderHub.Portal
{
    public class Startup
    {
        private readonly IConfiguration _configuration;

        public Startup(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            string connectionString = _configuration.GetConnectionString("OrderHub");

            services.AddDbContext<PortalIdentityContext>(o => o.UseSqlServer(connectionString));
            services.AddIdentity<IdentityUser, IdentityRole>(o =>
                {
                    o.Password.RequiredLength = 10;
                    o.Password.RequireNonAlphanumeric = false;
                    o.Lockout.MaxFailedAccessAttempts = 5;
                    o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                    o.User.RequireUniqueEmail = false;
                })
                .AddEntityFrameworkStores<PortalIdentityContext>()
                .AddDefaultTokenProviders();

            services.ConfigureApplicationCookie(o =>
            {
                o.LoginPath = "/Account/Login";
                o.LogoutPath = "/Account/Logout";
                o.AccessDeniedPath = "/Account/AccessDenied";
                o.Cookie.Name = "OrderHub.Auth";
                o.ExpireTimeSpan = TimeSpan.FromHours(10);
                o.SlidingExpiration = true;
            });

            string keyFolder = _configuration["DataProtection:KeyFolder"];
            if (!string.IsNullOrEmpty(keyFolder))
            {
                services.AddDataProtection()
                    .SetApplicationName("OrderHub.Portal")
                    .PersistKeysToFileSystem(new DirectoryInfo(keyFolder));
            }

            services.AddSingleton<IOrderStore>(new SqlOrderStore(connectionString));

            var settings = new SettingsReader(key => _configuration[key.Replace('.', ':')]);
            services.AddSingleton(settings);
            services.AddSingleton<PortalBus>();
            services.AddSingleton<IPublishEndpoint>(sp => sp.GetRequiredService<PortalBus>().Bus);

            services.AddMvc()
                .SetCompatibilityVersion(CompatibilityVersion.Version_2_1)
                .AddRazorPagesOptions(o =>
                {
                    o.Conventions.AuthorizeFolder("/");
                    o.Conventions.AllowAnonymousToPage("/Account/Login");
                    o.Conventions.AllowAnonymousToPage("/Account/AccessDenied");
                    o.Conventions.AllowAnonymousToPage("/Error");
                    o.Conventions.AuthorizeFolder("/Users", "Admin");
                    o.Conventions.AuthorizeFolder("/Products", "Admin");
                });

            services.AddAuthorization(o =>
            {
                o.AddPolicy("Admin", p => p.RequireRole(Roles.Admin));
                o.AddPolicy("Operator", p => p.RequireRole(Roles.Admin, Roles.Operator));
            });
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env, IApplicationLifetime lifetime, PortalBus bus)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
            }

            lifetime.ApplicationStopping.Register(bus.Stop);

            app.UseStaticFiles();
            app.UseAuthentication();
            app.UseMvc();
        }
    }
}
