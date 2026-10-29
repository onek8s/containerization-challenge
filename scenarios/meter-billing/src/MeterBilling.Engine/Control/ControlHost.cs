using System;
using System.Net;
using System.Web.Http;
using Microsoft.Owin.Hosting;
using Newtonsoft.Json.Converters;
using Owin;

namespace Coho.Billing.Engine.Control
{
    /// <summary>
    /// Self-hosted control endpoint (HTTP.sys through HttpListener, Windows authentication).
    /// The URL must be reserved for the service account: netsh http add urlacl url=http://+:8441/ user=CORP\svc-billing
    /// </summary>
    public sealed class ControlHost : IDisposable
    {
        private readonly IDisposable _app;

        private ControlHost(IDisposable app)
        {
            _app = app;
        }

        internal static BillingEngine Engine { get; private set; }

        public static ControlHost Start(BillingEngine engine)
        {
            Engine = engine;
            var app = WebApp.Start(engine.Settings.ControlUrl, Configure);
            return new ControlHost(app);
        }

        private static void Configure(IAppBuilder app)
        {
            var listener = (HttpListener)app.Properties["System.Net.HttpListener"];
            listener.AuthenticationSchemes = AuthenticationSchemes.IntegratedWindowsAuthentication;
            listener.UnsafeConnectionNtlmAuthentication = true;

            var config = new HttpConfiguration();
            config.MapHttpAttributeRoutes();
            config.Formatters.Remove(config.Formatters.XmlFormatter);
            config.Formatters.JsonFormatter.SerializerSettings.Converters.Add(new StringEnumConverter());
            config.Filters.Add(new ControlExceptionFilter());
            config.IncludeErrorDetailPolicy = IncludeErrorDetailPolicy.LocalOnly;
            config.EnsureInitialized();
            app.UseWebApi(config);
        }

        public void Dispose()
        {
            _app.Dispose();
        }
    }
}
