using System;
using System.Collections.Concurrent;
using System.Threading;
using log4net;

namespace Tailspin.DealerOrders.AxIntegration.BusinessConnector
{
    /// <summary>
    /// Keeps a few Business Connector sessions logged on (logon takes seconds and uses a client access license).
    /// Sessions are recycled after a number of calls or a maximum age, and thrown away after a communication error.
    /// Sessions for LogonAs users are created per call and not pooled.
    /// </summary>
    public sealed class BusinessConnectorPool : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(BusinessConnectorPool));

        private readonly AxSettings _settings;
        private readonly ConcurrentBag<BusinessConnectorSession> _idle = new ConcurrentBag<BusinessConnectorSession>();
        private readonly SemaphoreSlim _slots;
        private readonly string _domain;

        public BusinessConnectorPool(AxSettings settings)
        {
            _settings = settings;
            _slots = new SemaphoreSlim(settings.SessionPoolSize, settings.SessionPoolSize);
            var proxy = settings.BusinessConnectorProxy ?? "";
            _domain = proxy.Contains("\\") ? proxy.Split('\\')[0] : Environment.UserDomainName;
        }

        public T Use<T>(string runAsUser, Func<BusinessConnectorSession, T> work)
        {
            if (!string.IsNullOrEmpty(runAsUser))
            {
                using (var personal = BusinessConnectorSession.LogonAs(_settings, runAsUser, _domain))
                    return work(personal);
            }

            if (!_slots.Wait(_settings.CallTimeout))
                throw new Contracts.Ax.AxUnavailableException("All Business Connector sessions are busy.");
            BusinessConnectorSession session = null;
            try
            {
                while (_idle.TryTake(out session))
                {
                    if (!session.Broken && session.Calls < _settings.SessionMaxCalls && DateTime.UtcNow - session.CreatedUtc < _settings.SessionMaxAge) break;
                    Log.DebugFormat("Recycling Business Connector session after {0} calls", session.Calls);
                    session.Dispose();
                    session = null;
                }
                session = session ?? BusinessConnectorSession.Logon(_settings);
                return work(session);
            }
            finally
            {
                if (session != null)
                {
                    if (session.Broken) session.Dispose();
                    else _idle.Add(session);
                }
                _slots.Release();
            }
        }

        public void Dispose()
        {
            while (_idle.TryTake(out var session)) session.Dispose();
            _slots.Dispose();
        }
    }
}
