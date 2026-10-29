using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceModel;

namespace CreditDesk.CreditService
{
    /// <summary>Owns the WCF service host. Endpoints and security come from the .config file.</summary>
    internal sealed class CreditServiceHost
    {
        private ServiceHost _host;

        public IEnumerable<string> ListenAddresses =>
            _host?.Description.Endpoints.Select(e => e.Address.Uri.ToString()) ?? Enumerable.Empty<string>();

        public void Start()
        {
            _host = new ServiceHost(typeof(CreditService));
            _host.Faulted += (s, e) => Audit.Write("The credit service host faulted and stopped accepting calls.", EventLogEntryType.Error, 1002);
            _host.Open();
            Audit.Write("Credit service started. Listening on " + string.Join(", ", ListenAddresses), EventLogEntryType.Information, 1000);
        }

        public void Stop()
        {
            if (_host == null) return;
            try
            {
                if (_host.State == CommunicationState.Faulted) _host.Abort();
                else _host.Close(TimeSpan.FromSeconds(20));
            }
            catch (CommunicationException)
            {
                _host.Abort();
            }
            catch (TimeoutException)
            {
                _host.Abort();
            }
            _host = null;
            Audit.Write("Credit service stopped.", EventLogEntryType.Information, 1001);
        }
    }
}
