using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Litware.OrderHub.Integration.Configuration;

namespace Litware.OrderHub.Integration.Reporting
{
    public interface IInvoiceRenderer
    {
        /// <summary>Returns the invoice PDF, or null when invoice documents are switched off.</summary>
        Task<byte[]> RenderAsync(string orderNumber, CancellationToken cancellationToken);
    }

    /// <summary>Renders the invoice report on the report server through URL access (rs:Format=PDF).</summary>
    public class SsrsInvoiceRenderer : IInvoiceRenderer, IDisposable
    {
        private readonly HttpClient _client;
        private readonly string _reportServerUrl;
        private readonly string _reportPath;

        public SsrsInvoiceRenderer(string reportServerUrl, string reportPath, ICredentials credentials, TimeSpan timeout)
        {
            _reportServerUrl = reportServerUrl.TrimEnd('/');
            _reportPath = reportPath;
            var handler = new HttpClientHandler
            {
                PreAuthenticate = true,
                UseDefaultCredentials = credentials == null,
                Credentials = credentials
            };
            _client = new HttpClient(handler) { Timeout = timeout };
        }

        public static IInvoiceRenderer Create(SettingsReader settings)
        {
            if (!settings.GetBool("Reports.InvoicePdf", true))
            {
                return new NoInvoiceRenderer();
            }

            string user = settings.Get("Reports.Username");
            ICredentials credentials = null;
            if (user != null)
            {
                string domain = null;
                int slash = user.IndexOf('\\');
                if (slash > 0)
                {
                    domain = user.Substring(0, slash);
                    user = user.Substring(slash + 1);
                }
                credentials = new NetworkCredential(user, settings.Require("Reports.Password"), domain);
            }

            return new SsrsInvoiceRenderer(
                settings.Get("Reports.ServerUrl", "http://reports.corp.local/ReportServer"),
                settings.Get("Reports.InvoiceReport", "/Order Hub/Invoice"),
                credentials,
                settings.GetTime("Reports.Timeout", TimeSpan.FromSeconds(60)));
        }

        public async Task<byte[]> RenderAsync(string orderNumber, CancellationToken cancellationToken)
        {
            string url = _reportServerUrl + "?" + Uri.EscapeDataString(_reportPath).Replace("%2F", "/") +
                         "&rs:Command=Render&rs:Format=PDF&OrderNumber=" + Uri.EscapeDataString(orderNumber);

            using (var response = await Resilience.Http("Invoice " + orderNumber)
                       .ExecuteAsync(ct => _client.GetAsync(url, ct), cancellationToken))
            {
                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException("Report server returned " + (int)response.StatusCode + " " + response.ReasonPhrase + " for " + orderNumber + ".");
                }

                string type = response.Content.Headers.ContentType?.MediaType;
                if (type != "application/pdf")
                {
                    // An rs:Format error comes back as an HTML page with status 200.
                    throw new InvalidOperationException("Report server returned " + type + " instead of a PDF for " + orderNumber + ".");
                }
                return await response.Content.ReadAsByteArrayAsync();
            }
        }

        public void Dispose()
        {
            _client.Dispose();
        }
    }

    public class NoInvoiceRenderer : IInvoiceRenderer
    {
        public Task<byte[]> RenderAsync(string orderNumber, CancellationToken cancellationToken)
        {
            return Task.FromResult<byte[]>(null);
        }
    }
}
