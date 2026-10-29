using System;
using System.Threading.Tasks;
using log4net;
using Microsoft.AspNet.SignalR.Client;
using Tailspin.DealerOrders.Infrastructure;

namespace Tailspin.DealerOrders.Notifications
{
    /// <summary>
    /// SignalR 2 client connected to the dealer portal's OrderStatusHub. The portal accepts the "Publish" call only from
    /// connections that present the service key header.
    /// </summary>
    public class PortalPushClient : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(PortalPushClient));

        private readonly string _url;
        private readonly string _serviceKey;
        private readonly object _gate = new object();
        private HubConnection _connection;
        private IHubProxy _hub;
        private DateTime _nextConnectUtc = DateTime.MinValue;

        public PortalPushClient()
        {
            _url = Settings.Get("Push.PortalUrl", "https://dealers.corp.local/");
            _serviceKey = Settings.Get("Push.ServiceKey");
            Enabled = Settings.Bool("Push.Enabled", true);
        }

        public bool Enabled { get; }

        public bool IsConnected => _connection != null && _connection.State == ConnectionState.Connected;

        /// <summary>Connects if needed; after a failure waits a minute before the next try so mail is not held up.</summary>
        public bool EnsureConnected()
        {
            if (!Enabled) return false;
            lock (_gate)
            {
                if (IsConnected) return true;
                if (DateTime.UtcNow < _nextConnectUtc) return false;
                try
                {
                    _connection?.Dispose();
                    _connection = new HubConnection(_url, useDefaultUrl: true);
                    _connection.Headers.Add("X-Tailspin-Service-Key", _serviceKey ?? "");
                    _connection.Closed += () => Log.Warn("Portal push connection closed");
                    _connection.Reconnecting += () => Log.Info("Portal push connection reconnecting");
                    _hub = _connection.CreateHubProxy("OrderStatusHub");
                    if (!_connection.Start().Wait(TimeSpan.FromSeconds(30))) throw new TimeoutException("SignalR connection did not start in 30 seconds.");
                    Log.InfoFormat("Connected to {0}signalr ({1})", _url, _connection.Transport?.Name);
                    return true;
                }
                catch (Exception ex)
                {
                    _nextConnectUtc = DateTime.UtcNow.AddMinutes(1);
                    Log.Warn($"Could not connect to the portal hub at {_url}", ex.GetBaseException());
                    return false;
                }
            }
        }

        public Task Publish(int dealerId, string payloadJson) => _hub.Invoke("Publish", dealerId, payloadJson);

        public void Dispose()
        {
            _connection?.Stop();
            _connection?.Dispose();
        }
    }
}
