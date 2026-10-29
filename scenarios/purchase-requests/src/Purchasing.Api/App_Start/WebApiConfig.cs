using System.Web.Http;
using Contoso.Purchasing.Api.Infrastructure;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace Contoso.Purchasing.Api
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            config.MapHttpAttributeRoutes();

            config.Filters.Add(new AuthorizeAttribute());
            config.Filters.Add(new PolicyExceptionFilter());
            config.Filters.Add(new UnauthorizedFilter());
            config.Filters.Add(new SqlExceptionFilter());

            var json = config.Formatters.JsonFormatter;
            json.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
            json.SerializerSettings.Converters.Add(new StringEnumConverter());
            json.SerializerSettings.DateTimeZoneHandling = DateTimeZoneHandling.Local;
            config.Formatters.Remove(config.Formatters.XmlFormatter);
        }
    }
}
