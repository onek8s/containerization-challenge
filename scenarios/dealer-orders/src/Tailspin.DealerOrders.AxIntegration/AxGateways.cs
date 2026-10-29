using System;
using System.Configuration;
using log4net;
using Tailspin.DealerOrders.AxIntegration.Aif;
using Tailspin.DealerOrders.AxIntegration.BusinessConnector;
using Tailspin.DealerOrders.AxIntegration.QueryService;
using Tailspin.DealerOrders.Contracts.Ax;
using Tailspin.DealerOrders.LabStubs;

namespace Tailspin.DealerOrders.AxIntegration
{
    /// <summary>
    /// The three AX gateways for a host, chosen by the "Ax.Mode" app setting: BusinessConnector (production: Business
    /// Connector for writes, AIF and Query Service for reads) or Simulator (test and training environments without AX).
    /// </summary>
    public sealed class AxGateways : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(AxGateways));
        private readonly IDisposable[] _owned;

        private AxGateways(IAxSalesGateway sales, IAxDocumentReader reader, IAxQueryReader query, string mode, params IDisposable[] owned)
        {
            Sales = sales;
            Reader = reader;
            Query = query;
            Mode = mode;
            _owned = owned;
        }

        public IAxSalesGateway Sales { get; }
        public IAxDocumentReader Reader { get; }
        public IAxQueryReader Query { get; }
        public string Mode { get; }

        public static AxGateways FromConfig()
        {
            var mode = ConfigurationManager.AppSettings["Ax.Mode"] ?? "BusinessConnector";
            if (string.Equals(mode, "Simulator", StringComparison.OrdinalIgnoreCase))
            {
                var options = new AxSimulatorOptions { StateFile = ConfigurationManager.AppSettings["Ax.Simulator.StateFile"] };
                if (TimeSpan.TryParse(ConfigurationManager.AppSettings["Ax.Simulator.ConfirmAfter"], out var confirm)) options.ConfirmAfter = confirm;
                if (TimeSpan.TryParse(ConfigurationManager.AppSettings["Ax.Simulator.ShipAfter"], out var ship)) options.ShipAfter = ship;
                if (TimeSpan.TryParse(ConfigurationManager.AppSettings["Ax.Simulator.InvoiceAfter"], out var invoice)) options.InvoiceAfter = invoice;
                if (int.TryParse(ConfigurationManager.AppSettings["Ax.Simulator.UnavailableEvery"], out var every)) options.UnavailableEvery = every;
                var simulator = new AxSimulator(options);
                Log.WarnFormat("AX simulator in use (state file {0}); no data is sent to Dynamics AX", options.StateFile ?? "(memory)");
                return new AxGateways(simulator, simulator, simulator, "Simulator");
            }

            if (!string.Equals(mode, "BusinessConnector", StringComparison.OrdinalIgnoreCase))
                throw new ConfigurationErrorsException($"Ax.Mode '{mode}' is not supported (BusinessConnector or Simulator).");

            var settings = AxSettings.FromConfig();
            var pool = new BusinessConnectorPool(settings);
            var channels = new AxChannels(settings);
            var query = new AxQueryReader(channels);
            Log.InfoFormat("AX {0} company {1} through the Business Connector, AIF at {2}", settings.ObjectServer, settings.Company, channels.AifAddress);
            return new AxGateways(new BusinessConnectorSalesGateway(pool), new AifDocumentReader(channels, query), query, "BusinessConnector", pool, channels);
        }

        public void Dispose()
        {
            foreach (var d in _owned) d.Dispose();
        }
    }
}
