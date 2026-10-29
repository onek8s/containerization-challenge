using System;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Dispatcher;
using System.Text;
using VanArsdel.Integration.Runtime.Configuration;

namespace VanArsdel.Integration.WcfExtensions
{
    /// <summary>
    /// Adds what the ERP order service gateway requires on every call: the client id and basic credentials of
    /// the integration account (from SSO, never in the binding file), a correlation id SOAP header and the
    /// partner the call is made for. Logs the round trip time per operation.
    /// </summary>
    public class ErpHeaderInspector : IClientMessageInspector
    {
        public const string HeaderNamespace = "http://schemas.vanarsdel.com/erp/headers/2016";
        private static readonly TraceSource Trace = new TraceSource("VanArsdel.Integration.Erp");

        private readonly string _clientIdSetting;
        private readonly string _secretSetting;

        public ErpHeaderInspector(string clientIdSetting, string secretSetting)
        {
            _clientIdSetting = clientIdSetting;
            _secretSetting = secretSetting;
        }

        public object BeforeSendRequest(ref Message request, IClientChannel channel)
        {
            var clientId = SsoSettings.Get(_clientIdSetting);
            var secret = SsoSettings.Get(_secretSetting);
            var correlationId = Guid.NewGuid().ToString("N");

            request.Headers.Add(MessageHeader.CreateHeader("CorrelationId", HeaderNamespace, correlationId));
            request.Headers.Add(MessageHeader.CreateHeader("ClientId", HeaderNamespace, clientId));

            HttpRequestMessageProperty http;
            object existing;
            if (request.Properties.TryGetValue(HttpRequestMessageProperty.Name, out existing))
            {
                http = (HttpRequestMessageProperty)existing;
            }
            else
            {
                http = new HttpRequestMessageProperty();
                request.Properties.Add(HttpRequestMessageProperty.Name, http);
            }

            http.Headers[HttpRequestHeader.Authorization] = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(clientId + ":" + secret));
            http.Headers["X-Correlation-Id"] = correlationId;

            return new CallState(request.Headers.Action, correlationId, Stopwatch.StartNew());
        }

        public void AfterReceiveReply(ref Message reply, object correlationState)
        {
            var state = (CallState)correlationState;
            state.Watch.Stop();
            Trace.TraceEvent(reply.IsFault ? TraceEventType.Warning : TraceEventType.Verbose, 10,
                string.Format(CultureInfo.InvariantCulture, "{0} {1} {2} ms{3}", state.Action, state.CorrelationId, state.Watch.ElapsedMilliseconds, reply.IsFault ? " (fault)" : string.Empty));
        }

        private sealed class CallState
        {
            public CallState(string action, string correlationId, Stopwatch watch)
            {
                Action = action;
                CorrelationId = correlationId;
                Watch = watch;
            }

            public string Action { get; private set; }
            public string CorrelationId { get; private set; }
            public Stopwatch Watch { get; private set; }
        }
    }
}
