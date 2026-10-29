using System;
using Litware.OrderHub.Integration.Configuration;
using Litware.OrderHub.Integration.Messaging;
using MassTransit;
using Serilog;

namespace Litware.OrderHub.Portal.Services
{
    /// <summary>Send-only bus connection for the operator actions (resubmit, resend).</summary>
    public class PortalBus
    {
        private readonly Lazy<IBusControl> _bus;

        public PortalBus(SettingsReader settings)
        {
            _bus = new Lazy<IBusControl>(() =>
            {
                var bus = HubBus.Create(BusSettings.Read(settings));
                bus.Start();
                Log.Information("Connected to {Host}", settings.Get("RabbitMq.Host"));
                return bus;
            });
        }

        public IBusControl Bus => _bus.Value;

        public void Stop()
        {
            if (_bus.IsValueCreated) _bus.Value.Stop(TimeSpan.FromSeconds(10));
        }
    }
}
