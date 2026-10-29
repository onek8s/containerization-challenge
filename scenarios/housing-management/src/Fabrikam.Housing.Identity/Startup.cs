using System;
using Fabrikam.Housing.Hosting;
using Fabrikam.Housing.Hosting.Health;
using Fabrikam.Housing.Identity.Configuration;
using Fabrikam.Housing.Identity.Data;
using Fabrikam.Housing.Identity.Ldap;
using Fabrikam.Housing.Identity.Services;
using IdentityServer4.EntityFramework.DbContexts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Fabrikam.Housing.Identity
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

        private bool UseSqlite => string.Equals(Configuration["Database:Provider"], "Sqlite", StringComparison.OrdinalIgnoreCase);

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddHousingCore(Configuration, typeof(Startup).Assembly);
            services.AddHousingDataProtection(Configuration, "identity");

            services.AddDbContext<AuthDbContext>(options => UseDatabase(options, Configuration.GetConnectionString("Identity")));

            services.AddIdentity<ApplicationUser, IdentityRole>(options =>
                {
                    options.User.RequireUniqueEmail = false;
                    options.User.AllowedUserNameCharacters += "\\";
                    options.Password.RequiredLength = 10;
                    options.Password.RequireNonAlphanumeric = false;
                    options.Lockout.MaxFailedAccessAttempts = 5;
                    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                    options.Lockout.AllowedForNewUsers = true;
                })
                .AddEntityFrameworkStores<AuthDbContext>()
                .AddDefaultTokenProviders();

            services.ConfigureApplicationCookie(options =>
            {
                options.Cookie.Name = "Fabrikam.Identity";
                options.LoginPath = "/Account/Login";
                options.LogoutPath = "/Account/Logout";
            });

            var migrationsAssembly = typeof(Startup).Assembly.GetName().Name;
            services.AddIdentityServer(options =>
                {
                    var issuer = Configuration["IdentityServer:IssuerUri"];
                    if (!string.IsNullOrEmpty(issuer)) options.IssuerUri = issuer;
                    options.UserInteraction.LoginUrl = "/Account/Login";
                    options.UserInteraction.LogoutUrl = "/Account/Logout";
                    options.UserInteraction.ErrorUrl = "/Home/Error";
                    options.Events.RaiseErrorEvents = true;
                    options.Events.RaiseFailureEvents = true;
                })
                .AddInMemoryIdentityResources(IdentityConfig.IdentityResources)
                .AddInMemoryApiScopes(IdentityConfig.ApiScopes)
                .AddInMemoryApiResources(IdentityConfig.ApiResources)
                .AddClientStore<ContractorClientStore>()
                .AddAspNetIdentity<ApplicationUser>()
                .AddProfileService<HousingProfileService>()
                .AddOperationalStore(options =>
                {
                    options.ConfigureDbContext = db => UseDatabase(db, Configuration.GetConnectionString("Grants"), migrationsAssembly);
                    options.DefaultSchema = UseSqlite ? null : "auth";
                    options.EnableTokenCleanup = true;
                    options.TokenCleanupInterval = 3600;
                })
                .AddHousingSigningCredential(Configuration, Environment);

            services.Configure<LdapOptions>(Configuration.GetSection("Ldap"));
            if (string.Equals(Configuration["Ldap:Mode"], "Simulated", StringComparison.OrdinalIgnoreCase))
                services.AddSingleton<IStaffDirectory, SimulatedStaffDirectory>();
            else
                services.AddSingleton<IStaffDirectory, LdapStaffDirectory>();
            services.AddScoped<StaffSignIn>();

            services.AddSameSiteCookiePolicy();
            services.AddControllersWithViews();
        }

        private void UseDatabase(DbContextOptionsBuilder options, string connectionString, string migrationsAssembly = null)
        {
            if (UseSqlite)
                options.UseSqlite(connectionString, sql => { if (migrationsAssembly != null) sql.MigrationsAssembly(migrationsAssembly); });
            else
                options.UseSqlServer(connectionString, sql =>
                {
                    if (migrationsAssembly != null) sql.MigrationsAssembly(migrationsAssembly);
                    sql.EnableRetryOnFailure(3);
                });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                DevelopmentDatabase.EnsureSeeded(app.ApplicationServices);
                IdentityDevelopmentSeeder.Seed(app.ApplicationServices);
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
            app.UseIdentityServer();
            app.UseAuthorization();
            app.UseEndpoints(endpoints => endpoints.MapDefaultControllerRoute());
        }
    }
}
