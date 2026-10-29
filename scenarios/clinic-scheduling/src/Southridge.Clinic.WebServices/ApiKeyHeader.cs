using System;
using System.Web;
using System.Web.Services.Protocols;
using System.Xml;
using Southridge.Clinic.Data;
using Southridge.Clinic.Data.Repositories;
using Southridge.Clinic.Domain;
using Southridge.Clinic.Infrastructure;

namespace Southridge.Clinic.WebServices
{
    /// <summary>SOAP header carrying the caller's API key (issued per kiosk / per web site in Admin, API clients).</summary>
    [System.Xml.Serialization.XmlRoot(Namespace = ServiceNamespaces.Common)]
    public class ApiKeyHeader : SoapHeader
    {
        public string ApiKey { get; set; }
    }

    public static class ServiceNamespaces
    {
        public const string Common = "http://schemas.southridgemedical.org/clinic/ws/2008/11";
        public const string Booking = "http://schemas.southridgemedical.org/clinic/ws/booking/2008/11";
        public const string Kiosk = "http://schemas.southridgemedical.org/clinic/ws/kiosk/2009/02";
    }

    /// <summary>API key validation and SOAP fault translation shared by the ASMX services.</summary>
    public abstract class ClinicWebServiceBase : System.Web.Services.WebService
    {
        private const string CacheKeyPrefix = "apikey:";

        protected ApiClientInfo Authenticate(ApiKeyHeader header, string clientType)
        {
            if (header == null || string.IsNullOrEmpty(header.ApiKey))
            {
                throw Fault("AUTH", "The ApiKeyHeader SOAP header is required.", true);
            }

            string hash = ApiKeys.Hash(header.ApiKey);
            string cacheKey = CacheKeyPrefix + clientType + ":" + hash;
            var client = HttpRuntime.Cache[cacheKey] as ApiClientInfo;
            if (client == null)
            {
                client = new SystemRepository().ValidateApiKey(hash, clientType);
                if (client == null)
                {
                    ClinicLog.Warning(LogCategory.Security, "Rejected API key for " + clientType + " from " + ClientAddress);
                    throw Fault("AUTH", "The API key is not valid for this service.", true);
                }

                HttpRuntime.Cache.Insert(cacheKey, client, null, DateTime.UtcNow.AddMinutes(5), System.Web.Caching.Cache.NoSlidingExpiration);
            }

            return client;
        }

        protected string ClientAddress
        {
            get { return HttpContext.Current == null ? null : HttpContext.Current.Request.UserHostAddress; }
        }

        /// <summary>Runs the operation and turns rule violations and unexpected errors into SOAP faults.</summary>
        protected T Execute<T>(string operation, Func<T> body)
        {
            try
            {
                return body();
            }
            catch (SoapException)
            {
                throw;
            }
            catch (ClinicRuleException ex)
            {
                throw Fault("RULE", ex.Message, true);
            }
            catch (Exception ex)
            {
                Exception replaced = ErrorHandler.Handle(ex, ExceptionPolicies.ServiceBoundary);
                throw Fault("INTERNAL", replaced != null ? replaced.Message : "Operation " + operation + " failed.", false);
            }
        }

        protected static SoapException Fault(string code, string message, bool client)
        {
            var doc = new XmlDocument();
            XmlNode detail = doc.CreateNode(XmlNodeType.Element, SoapException.DetailElementName.Name, SoapException.DetailElementName.Namespace);
            XmlElement codeElement = doc.CreateElement("code", ServiceNamespaces.Common);
            codeElement.InnerText = code;
            detail.AppendChild(codeElement);
            return new SoapException(message, client ? SoapException.ClientFaultCode : SoapException.ServerFaultCode, string.Empty, detail);
        }
    }
}
