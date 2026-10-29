using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using Fabrikam.Housing.ContractorApi.Infrastructure;
using Fabrikam.Housing.Payments;
using Fabrikam.Housing.Hosting;
using Fabrikam.Housing.Hosting.Health;
using Fabrikam.Housing.Hosting.Security;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Fabrikam.Housing.ContractorApi
{
    public class Startup
    {
        public const string ContractorPolicy = "Contractor";

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
            services.AddHousingDataProtection(Configuration, "contractor-api");
            services.AddHttpContextAccessor();
            services.AddScoped<CurrentContractor>();
            services.AddScoped<PaystreamEventProcessor>();
            services.Configure<ApiLimitsOptions>(Configuration.GetSection("ApiLimits"));

            JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
            var auth = Configuration.GetSection("Authentication");
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.Authority = auth["Authority"];
                    options.Audience = auth["Audience"] ?? HousingScopes.ContractorApi;
                    options.RequireHttpsMetadata = auth.GetValue("RequireHttpsMetadata", true);
                    options.TokenValidationParameters.NameClaimType = "client_id";
                });
            services.AddAuthorization(options =>
            {
                options.AddPolicy(ContractorPolicy, policy => policy
                    .RequireAuthenticatedUser()
                    .RequireAssertion(ctx => ctx.User.HasClaim("scope", HousingScopes.ContractorApi)
                        && CurrentContractor.CodeFrom(ctx.User) != null));
            });

            services.AddControllers(options => options.Filters.Add<BusinessRuleExceptionFilter>())
                .AddNewtonsoftJson(options =>
                {
                    options.SerializerSettings.Converters.Add(new StringEnumConverter());
                    options.SerializerSettings.DateTimeZoneHandling = DateTimeZoneHandling.Local;
                    options.SerializerSettings.DateFormatString = "yyyy-MM-dd'T'HH:mm:ss";
                    options.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
                })
                .AddFluentValidation(fv => fv.RegisterValidatorsFromAssemblyContaining<Startup>());

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "Fabrikam Homes contractor API",
                    Version = "v1",
                    Description = "Work orders, appointments, completions, invoices and compliance certificates for Fabrikam Homes' repairs contractors."
                });
                c.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.OAuth2,
                    Flows = new OpenApiOAuthFlows
                    {
                        ClientCredentials = new OpenApiOAuthFlow
                        {
                            TokenUrl = new Uri(auth["Authority"].TrimEnd('/') + "/connect/token"),
                            Scopes = new Dictionary<string, string> { [HousingScopes.ContractorApi] = "Contractor API" }
                        }
                    }
                });
                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "oauth2" } }] = new[] { HousingScopes.ContractorApi }
                });
            });
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
                app.UseExceptionHandler("/error");
            }

            app.UseHousingHealth();
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Contractor API v1");
                c.OAuthClientId("contractor-api-swagger");
            });

            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseMiddleware<RateLimitMiddleware>();
            app.UseEndpoints(endpoints => endpoints.MapControllers());
        }
    }
}
