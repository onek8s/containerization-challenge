using System;
using System.Net.Http;
using System.Net.Sockets;
using Polly;
using Polly.Retry;
using Renci.SshNet.Common;
using Serilog;

namespace Litware.OrderHub.Integration
{
    public static class Resilience
    {
        /// <summary>Retries connection level SFTP failures; file errors are not retried.</summary>
        public static RetryPolicy Sftp(string what)
        {
            return Policy
                .Handle<SocketException>()
                .Or<SshConnectionException>()
                .Or<SshOperationTimeoutException>()
                .Or<ProxyException>()
                .WaitAndRetry(new[] { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30) },
                    (ex, delay, attempt, ctx) => Log.Warning(ex, "{What}: SFTP attempt {Attempt} failed, retrying in {Delay}", what, attempt, delay));
        }

        public static AsyncRetryPolicy<HttpResponseMessage> Http(string what)
        {
            return Policy
                .Handle<HttpRequestException>()
                .OrResult<HttpResponseMessage>(r => (int)r.StatusCode >= 500 || (int)r.StatusCode == 408)
                .WaitAndRetryAsync(new[] { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30) },
                    (outcome, delay, attempt, ctx) => Log.Warning(outcome.Exception, "{What}: attempt {Attempt} failed ({Status}), retrying in {Delay}",
                        what, attempt, outcome.Result?.StatusCode, delay));
        }
    }
}
