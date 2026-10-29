using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.EventLog;
using ProductReturns.Portal.Services;

namespace ProductReturns.Portal
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
            services.Configure<EventLogSettings>(s =>
            {
                s.SourceName = "Product Returns Portal";
                s.LogName = "Application";
            });

            // Keys are shared by the portal instances on returns-web and protected with the machine key.
            var dataProtection = services.AddDataProtection()
                .SetApplicationName("ProductReturns.Portal")
                .PersistKeysToFileSystem(new DirectoryInfo(Configuration["DataProtection:KeysFolder"]));
            if (!Environment.IsDevelopment())
            {
                dataProtection.ProtectKeysWithDpapi(protectToLocalMachine: true);
            }

            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                foreach (var proxy in Configuration.GetSection("Proxy:KnownProxies").Get<string[]>() ?? Array.Empty<string>())
                {
                    options.KnownProxies.Add(IPAddress.Parse(proxy));
                }
            });

            services.AddDistributedMemoryCache();
            services.AddSession(options =>
            {
                options.Cookie.Name = ".ProductReturns.Session";
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
                options.IdleTimeout = TimeSpan.FromMinutes(30);
            });

            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.Cookie.Name = ".ProductReturns.Staff";
                    options.LoginPath = "/staff/login";
                    options.LogoutPath = "/staff/logout";
                    options.AccessDeniedPath = "/staff/denied";
                    options.ExpireTimeSpan = TimeSpan.FromHours(9);
                    options.SlidingExpiration = true;
                });

            services.AddAuthorization(options =>
            {
                options.AddPolicy(StaffRoles.AnyStaff, p => p.RequireRole(StaffRoles.Agent, StaffRoles.Supervisor));
                options.AddPolicy(StaffRoles.Supervisor, p => p.RequireRole(StaffRoles.Supervisor));
            });

            services.Configure<StaffDirectoryOptions>(Configuration.GetSection("StaffDirectory"));
            if (string.Equals(Configuration["StaffDirectory:Mode"], "Configured", StringComparison.OrdinalIgnoreCase))
            {
                services.AddSingleton<IStaffDirectory, ConfiguredStaffDirectory>();
            }
            else
            {
                services.AddSingleton<IStaffDirectory, ActiveDirectoryStaffDirectory>();
            }

            services.AddHttpClient<ReturnsApiClient>(client =>
                {
                    client.BaseAddress = new Uri(Configuration["Api:BaseUrl"]);
                    client.Timeout = TimeSpan.FromSeconds(30);
                    var apiKey = Configuration["Api:ApiKey"];
                    if (!string.IsNullOrEmpty(apiKey))
                    {
                        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
                    }
                })
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    // The API authenticates the portal by its service account (Kerberos).
                    UseDefaultCredentials = Configuration.GetValue("Api:UseDefaultCredentials", true),
                    PreAuthenticate = true
                });

            services.AddControllersWithViews();
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseForwardedHeaders();

            if (Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseStaticFiles();
            app.UseRouting();
            app.UseSession();
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
