using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using RestSharp;

namespace Fabrikam.Housing.Integration
{
    /// <summary>
    /// Creates RestSharp clients that go out through the corporate proxy and retries transient failures.
    /// </summary>
    public class RestClientFactory
    {
        private readonly IntegrationOptions _options;

        public RestClientFactory(IOptions<IntegrationOptions> options)
        {
            _options = options.Value;
        }

        public IRestClient Create(string baseUrl, bool internalHost = false)
        {
            var client = new RestClient(baseUrl) { Timeout = 30000 };
            if (!internalHost && !string.IsNullOrEmpty(_options.ProxyUrl))
            {
                client.Proxy = new WebProxy(_options.ProxyUrl)
                {
                    BypassProxyOnLocal = true,
                    UseDefaultCredentials = _options.ProxyUseDefaultCredentials
                };
            }
            return client;
        }

        public static AsyncRetryPolicy<IRestResponse> RetryPolicy(ILogger logger, string service)
        {
            return Policy
                .HandleResult<IRestResponse>(IsTransient)
                .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                    (outcome, delay, attempt, _) => logger.LogWarning("{Service} call failed ({Status} {Error}), retry {Attempt} in {Delay}",
                        service, (int)outcome.Result.StatusCode, outcome.Result.ErrorMessage, attempt, delay));
        }

        public static bool IsTransient(IRestResponse response)
        {
            if (response.ResponseStatus == ResponseStatus.TimedOut || response.ResponseStatus == ResponseStatus.Error) return true;
            var code = (int)response.StatusCode;
            return code == 408 || code == 429 || code >= 500;
        }

        public static IRestResponse EnsureSuccess(IRestResponse response, string service)
        {
            if (response.IsSuccessful) return response;
            throw new IntegrationException(service, (int)response.StatusCode,
                $"{service} returned {(int)response.StatusCode} {response.StatusDescription}: {response.ErrorMessage ?? Truncate(response.Content)}", response.ErrorException);
        }

        private static string Truncate(string s) => s == null ? null : s.Length > 500 ? s.Substring(0, 500) : s;
    }

    public class IntegrationException : Exception
    {
        public IntegrationException(string service, int statusCode, string message, Exception inner = null) : base(message, inner)
        {
            Service = service;
            StatusCode = statusCode;
        }

        public string Service { get; }
        public int StatusCode { get; }
    }
}
