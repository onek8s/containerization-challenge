using System.Net.Http.Formatting;
using System.Web.Http;
using System.Web.Http.ExceptionHandling;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Woodgrove.Statements.Data;

namespace Woodgrove.Statements.Api
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            config.MapHttpAttributeRoutes();
            config.MessageHandlers.Add(new ApiKeyHandler(new ApiClientRepository(Db.FromConfig())));
            config.Services.Replace(typeof(IExceptionLogger), new TraceExceptionLogger());

            config.Formatters.Clear();
            var json = new JsonMediaTypeFormatter();
            json.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
            json.SerializerSettings.DateFormatString = "yyyy-MM-dd";
            json.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
            config.Formatters.Add(json);
            config.IncludeErrorDetailPolicy = IncludeErrorDetailPolicy.LocalOnly;
        }
    }

    class TraceExceptionLogger : ExceptionLogger
    {
        public override void Log(ExceptionLoggerContext context)
        {
            System.Diagnostics.Trace.TraceError("{0} {1}: {2}", context.Request.Method, context.Request.RequestUri, context.Exception);
        }
    }
}
