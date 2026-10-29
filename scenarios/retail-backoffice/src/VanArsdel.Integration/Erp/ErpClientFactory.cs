using System;
using System.ServiceModel;
using System.ServiceModel.Channels;

namespace VanArsdel.Integration.Erp
{
    public class ErpClientSettings
    {
        public string Endpoint { get; set; } = "https://erp01.corp.local:8443/MerxIntegration/ErpIntegration.svc";
        public string UserName { get; set; }
        public int TimeoutSeconds { get; set; } = 60;
        public string ItemPrefix { get; set; } = "VA";
        public string WebShopSite { get; set; } = "DC01";
    }

    /// <summary>
    /// Builds the basicHttp(s) binding. The ERP uses HTTP basic authentication over TLS; the lab ERP
    /// simulator is plain HTTP.
    /// </summary>
    public static class ErpClientFactory
    {
        public static ErpIntegrationClient Create(ErpClientSettings settings, string password)
        {
            var uri = new Uri(settings.Endpoint);
            var https = uri.Scheme == Uri.UriSchemeHttps;
            var binding = new BasicHttpBinding(https ? BasicHttpSecurityMode.Transport : BasicHttpSecurityMode.TransportCredentialOnly)
            {
                MaxReceivedMessageSize = 16 * 1024 * 1024,
                SendTimeout = TimeSpan.FromSeconds(settings.TimeoutSeconds),
                ReceiveTimeout = TimeSpan.FromSeconds(settings.TimeoutSeconds),
            };
            binding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Basic;

            var client = new ErpIntegrationClient(binding, new EndpointAddress(uri));
            client.ClientCredentials.UserName.UserName = settings.UserName;
            client.ClientCredentials.UserName.Password = password;
            return client;
        }

        public static bool IsRetryable(Exception ex)
        {
            if (ex is FaultException<ErpFault> fault) return fault.Detail?.Retryable ?? false;
            return ex is CommunicationException || ex is TimeoutException;
        }
    }
}
