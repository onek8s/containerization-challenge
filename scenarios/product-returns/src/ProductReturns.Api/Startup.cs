using System;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.EventLog;
using Microsoft.OpenApi.Models;
using ProductReturns.Api.Security;
using ProductReturns.Api.Services;
using ProductReturns.Core;
using ProductReturns.Data;

namespace ProductReturns.Api
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
                s.SourceName = "Product Returns API";
                s.LogName = "Application";
            });

            services.AddReturnsDatabase(Configuration["Database:Provider"], Configuration.GetConnectionString("Returns"));

            var policyOptions = Configuration.GetSection("ReturnPolicy").Get<ReturnPolicyOptions>() ?? new ReturnPolicyOptions();
            services.AddSingleton(policyOptions);
            services.AddSingleton<ReturnPolicy>();
            services.AddSingleton<RefundCalculator>();
            services.Configure<ApiSecurityOptions>(Configuration.GetSection("Security"));
            services.Configure<CatalogCacheOptions>(Configuration.GetSection("CatalogCache"));
            services.AddSingleton<CatalogCache>();
            services.AddHostedService<CatalogRefreshService>();
            services.AddSingleton<IBusinessClock, BusinessClock>();
            services.AddScoped<ReturnService>();

            services.AddAuthentication(NegotiateDefaults.AuthenticationScheme)
                .AddNegotiate()
                .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, null);

            services.AddAuthorization(options =>
            {
                options.AddPolicy(Policies.Portal, p => p
                    .AddAuthenticationSchemes(NegotiateDefaults.AuthenticationScheme, ApiKeyAuthenticationHandler.SchemeName)
                    .RequireAuthenticatedUser()
                    .AddRequirements(new PortalCallerRequirement()));
                options.AddPolicy(Policies.Scanner, p => p
                    .AddAuthenticationSchemes(ApiKeyAuthenticationHandler.SchemeName)
                    .RequireRole(Roles.Scanner));
            });
            services.AddSingleton<IAuthorizationHandler, PortalCallerHandler>();

            services.AddControllers()
                .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Product Returns API", Version = "v1" });
            });
        }

        public void Configure(IApplicationBuilder app)
        {
            if (Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Product Returns API v1"));
            }

            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
