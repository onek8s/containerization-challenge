using System.Net.Http.Formatting;
using System.Web.Http;
using MassTransit;
using Owin;

namespace Litware.OrderHub.Intake.Api
{
    public class ApiOptions
    {
        public IBus Bus { get; set; }
        public string WebshopSecret { get; set; }
    }

    public static class Startup
    {
        public static void Configure(IAppBuilder app, ApiOptions options)
        {
            var config = new HttpConfiguration();
            config.MapHttpAttributeRoutes();
            config.Formatters.Clear();
            config.Formatters.Add(new JsonMediaTypeFormatter());
            config.Properties[typeof(ApiOptions)] = options;
            config.IncludeErrorDetailPolicy = IncludeErrorDetailPolicy.Never;
            app.UseWebApi(config);
        }

        public static ApiOptions Options(this ApiController controller)
        {
            return (ApiOptions)controller.Configuration.Properties[typeof(ApiOptions)];
        }
    }
}
