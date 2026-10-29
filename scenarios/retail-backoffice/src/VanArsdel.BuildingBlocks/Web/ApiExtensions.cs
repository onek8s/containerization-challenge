using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FluentValidation.AspNetCore;
using HealthChecks.UI.Client;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Serilog;
using VanArsdel.BuildingBlocks.Mediator;
using VanArsdel.BuildingBlocks.Security;

namespace VanArsdel.BuildingBlocks.Web
{
    public static class ApiExtensions
    {
        /// <summary>
        /// Controllers, validation, MediatR, AutoMapper, JWT authentication, Swagger and health checks.
        /// </summary>
        public static IMvcBuilder AddVanArsdelApi(this IServiceCollection services, IConfiguration configuration, string title,
            Assembly apiAssembly, Action<Microsoft.AspNetCore.Authorization.AuthorizationOptions> policies)
        {
            var mvc = services.AddControllers()
                .AddNewtonsoftJson(o =>
                {
                    o.SerializerSettings.NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore;
                    o.SerializerSettings.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter());
                })
                .AddFluentValidation(fv => fv.RegisterValidatorsFromAssembly(apiAssembly));

            services.AddMediatR(apiAssembly);
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            services.AddAutoMapper(apiAssembly);

            services.AddVanArsdelJwt(configuration);
            services.AddAuthorization(policies);

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = title, Version = "v1" });
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "Access token from Entra ID or ADFS",
                });
                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
                        new List<string>()
                    },
                });
                c.CustomSchemaIds(t => t.FullName?.Replace("+", "."));
                var xml = Path.Combine(AppContext.BaseDirectory, apiAssembly.GetName().Name + ".xml");
                if (File.Exists(xml)) c.IncludeXmlComments(xml);
            });

            services.AddHealthChecks();
            services.Configure<ForwardedHeadersOptions>(o =>
            {
                o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
                o.KnownNetworks.Clear();
                o.KnownProxies.Clear();
            });
            return mvc;
        }

        public static IApplicationBuilder UseVanArsdelApi(this IApplicationBuilder app, IWebHostEnvironment env, Action<IEndpointRouteBuilder> endpoints = null)
        {
            app.UseForwardedHeaders();
            app.UseMiddleware<CorrelationIdMiddleware>();
            app.UseSerilogRequestLogging(o =>
            {
                o.GetLevel = (ctx, elapsed, ex) => ctx.Request.Path.StartsWithSegments("/health")
                    ? Serilog.Events.LogEventLevel.Verbose
                    : Serilog.Events.LogEventLevel.Information;
            });
            app.UseMiddleware<ExceptionHandlingMiddleware>();

            app.UseSwagger();
            app.UseSwaggerUI(c => c.SwaggerEndpoint("v1/swagger.json", "v1"));

            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(e =>
            {
                e.MapControllers();
                e.MapVanArsdelHealth();
                endpoints?.Invoke(e);
            });
            return app;
        }

        public static void MapVanArsdelHealth(this IEndpointRouteBuilder e)
        {
            e.MapHealthChecks("/health", new HealthCheckOptions
            {
                ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
            });
            e.MapHealthChecks("/health/live", new HealthCheckOptions
            {
                Predicate = _ => false,
            });
        }

        public static bool IsLab(this IHostEnvironment env) => env.IsEnvironment("Lab");

        public static IEnumerable<string> ConfiguredCorsOrigins(IConfiguration configuration) =>
            (configuration["Cors:Origins"] ?? string.Empty).Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim());
    }
}
