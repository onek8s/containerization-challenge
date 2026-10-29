using System;
using System.Net;
using System.ServiceModel;
using Polly;
using Polly.Retry;
using RestSharp;

namespace Lucerne.ProjectSites.Integration
{
    public static class RetryPolicies
    {
        public static readonly TimeSpan[] Delays = { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30) };

        public static Func<int, TimeSpan> DelayProvider = attempt => Delays[Math.Min(attempt, Delays.Length) - 1];

        /// <summary>Communication errors and timeouts of the ERP SOAP service (faults are not retried).</summary>
        public static RetryPolicy Wcf(Action<Exception, TimeSpan> onRetry)
        {
            return Policy
                .Handle<CommunicationException>(ex => !(ex is FaultException))
                .Or<TimeoutException>()
                .WaitAndRetry(Delays.Length, DelayProvider, (ex, delay) => onRetry?.Invoke(ex, delay));
        }

        /// <summary>REST: network failures, 429 and 5xx.</summary>
        public static RetryPolicy<IRestResponse> Rest(Action<string, TimeSpan> onRetry)
        {
            return Policy
                .HandleResult<IRestResponse>(IsTransient)
                .WaitAndRetry(Delays.Length, DelayProvider, (outcome, delay) =>
                    onRetry?.Invoke(outcome.Result.ResponseStatus + " " + (int)outcome.Result.StatusCode, delay));
        }

        public static bool IsTransient(IRestResponse response)
        {
            if (response.ResponseStatus != ResponseStatus.Completed)
            {
                return true;
            }

            var code = (int)response.StatusCode;
            return code == 429 || code == (int)HttpStatusCode.BadGateway || code == (int)HttpStatusCode.ServiceUnavailable
                || code == (int)HttpStatusCode.GatewayTimeout || code == 500;
        }
    }

    public class IntegrationException : Exception
    {
        public IntegrationException(string message)
            : base(message)
        {
        }

        public IntegrationException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
