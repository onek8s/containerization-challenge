using System;
using System.Reflection;
using System.Web.Http;
using Autofac;
using Autofac.Integration.WebApi;
using Microsoft.Owin;
using Microsoft.Owin.Security.OAuth;
using Owin;
using Wingtip.FieldSales.Api.Auth;
using Wingtip.FieldSales.Api.Infrastructure;
using Wingtip.FieldSales.Api.Services;
using Wingtip.FieldSales.Data;

[assembly: OwinStartup(typeof(Wingtip.FieldSales.Api.Startup))]

namespace Wingtip.FieldSales.Api
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            var settings = ApiSettings.FromConfig();
            var container = BuildContainer(settings);

            // Bearer tokens and the protected refresh tickets are encrypted with the ASP.NET machine key
            // (system.web/machineKey in Web.config).
            app.UseOAuthAuthorizationServer(new OAuthAuthorizationServerOptions
            {
                TokenEndpointPath = new PathString("/token"),
                AccessTokenExpireTimeSpan = settings.AccessTokenLifetime,
                AllowInsecureHttp = settings.AllowInsecureHttp,
                Provider = new SalesAuthorizationServerProvider(() => ApplicationUserManager.Create(new FieldSalesDbContext()), settings),
                RefreshTokenProvider = new RefreshTokenProvider(() => new FieldSalesDbContext(), settings)
            });
            app.UseOAuthBearerAuthentication(new OAuthBearerAuthenticationOptions());

            var config = new HttpConfiguration();
            WebApiConfig.Register(config);
            config.DependencyResolver = new AutofacWebApiDependencyResolver(container);

            app.UseAutofacMiddleware(container);
            app.UseAutofacWebApi(config);
            app.UseWebApi(config);
        }

        private static IContainer BuildContainer(ApiSettings settings)
        {
            var builder = new ContainerBuilder();
            builder.RegisterInstance(settings);
            builder.RegisterApiControllers(Assembly.GetExecutingAssembly());

            builder.RegisterType<FieldSalesDbContext>().AsSelf().InstancePerRequest();
            builder.RegisterType<SalesProcedures>().InstancePerRequest();
            builder.RegisterType<CatalogService>().InstancePerRequest();
            builder.RegisterType<OrderService>().InstancePerRequest();
            builder.RegisterType<WarehouseClient>().SingleInstance();

            return builder.Build();
        }
    }
}
