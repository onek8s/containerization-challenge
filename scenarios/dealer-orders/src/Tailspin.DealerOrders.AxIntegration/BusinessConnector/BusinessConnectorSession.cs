using System;
using System.Net;
using System.Reflection;
using System.Security.Principal;
using log4net;
using Tailspin.DealerOrders.Contracts.Ax;

namespace Tailspin.DealerOrders.AxIntegration.BusinessConnector
{
    /// <summary>
    /// One logged-on Business Connector session (Microsoft.Dynamics.BusinessConnectorNet.Axapta). A session is bound to
    /// one Windows identity on the AOS: either the process identity (Logon) or an AX user through the Business Connector
    /// proxy (LogonAs). Sessions are not thread safe; the pool hands each one to a single caller at a time.
    /// </summary>
    public sealed class BusinessConnectorSession : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(BusinessConnectorSession));

        private readonly dynamic _axapta;
        private bool _loggedOn;

        private BusinessConnectorSession(Assembly assembly, string runAsUser)
        {
            _axapta = Activator.CreateInstance(BusinessConnectorAssembly.Type(assembly, "Axapta"));
            RunAsUser = runAsUser;
            CreatedUtc = DateTime.UtcNow;
        }

        public string RunAsUser { get; }
        public DateTime CreatedUtc { get; }
        public int Calls { get; private set; }
        public bool Broken { get; private set; }

        /// <summary>Logs on with the Windows identity of the current process (the Business Connector proxy account).</summary>
        public static BusinessConnectorSession Logon(AxSettings settings)
        {
            var session = new BusinessConnectorSession(BusinessConnectorAssembly.Load(settings.BusinessConnectorPath), null);
            session.Invoke(() => session._axapta.Logon(settings.Company, settings.Language, settings.ObjectServer, settings.Configuration), "Logon");
            session._loggedOn = true;
            Log.InfoFormat("Business Connector session to {0} company {1} as {2}", settings.ObjectServer, settings.Company, WindowsIdentity.GetCurrent().Name);
            return session;
        }

        /// <summary>
        /// Logs on as another AX user through the Business Connector proxy. The process must run as the proxy account
        /// configured in AX (System administration > Setup > System > System service accounts).
        /// </summary>
        public static BusinessConnectorSession LogonAs(AxSettings settings, string axUserAlias, string domain)
        {
            var session = new BusinessConnectorSession(BusinessConnectorAssembly.Load(settings.BusinessConnectorPath), axUserAlias);
            var proxy = CredentialCache.DefaultNetworkCredentials;
            session.Invoke(() => session._axapta.LogonAs(axUserAlias, domain, proxy, settings.Company, settings.Language, settings.ObjectServer, settings.Configuration), "LogonAs");
            session._loggedOn = true;
            Log.InfoFormat("Business Connector session as {0}\\{1} through proxy {2}", domain, axUserAlias, WindowsIdentity.GetCurrent().Name);
            return session;
        }

        /// <summary>Calls a static X++ class method, e.g. TSDealerOrderService::createSalesOrder(xml).</summary>
        public object CallStatic(string className, string method, params object[] args)
        {
            Calls++;
            return Invoke(() =>
            {
                switch (args.Length)
                {
                    case 0: return (object)_axapta.CallStaticClassMethod(className, method);
                    case 1: return (object)_axapta.CallStaticClassMethod(className, method, args[0]);
                    case 2: return (object)_axapta.CallStaticClassMethod(className, method, args[0], args[1]);
                    case 3: return (object)_axapta.CallStaticClassMethod(className, method, args[0], args[1], args[2]);
                    default: return (object)_axapta.CallStaticClassMethod(className, method, args);
                }
            }, className + "::" + method);
        }

        /// <summary>Calls a static table method, e.g. CustTable::find(accountNum).</summary>
        public dynamic CallStaticRecord(string tableName, string method, params object[] args)
        {
            Calls++;
            return Invoke(() => args.Length == 1
                ? (object)_axapta.CallStaticRecordMethod(tableName, method, args[0])
                : (object)_axapta.CallStaticRecordMethod(tableName, method, args), tableName + "::" + method);
        }

        /// <summary>Runs an X++ select statement on a table buffer and returns the buffer (AxaptaRecord).</summary>
        public dynamic Select(string tableName, string statement)
        {
            Calls++;
            return Invoke(() =>
            {
                var record = _axapta.CreateAxaptaRecord(tableName);
                record.ExecuteStmt(statement);
                return (object)record;
            }, "select " + tableName);
        }

        public void TtsBegin() => Invoke(() => { _axapta.TTSBegin(); return null; }, "TTSBegin");
        public void TtsCommit() => Invoke(() => { _axapta.TTSCommit(); return null; }, "TTSCommit");

        public void TtsAbort()
        {
            try { _axapta.TTSAbort(); }
            catch (Exception ex) { Log.Warn("TTSAbort failed; the session will be discarded", ex); Broken = true; }
        }

        private object Invoke(Func<object> call, string what)
        {
            try
            {
                return call();
            }
            catch (Exception ex)
            {
                var translated = Translate(ex, what);
                if (translated is AxUnavailableException) Broken = true;
                throw translated;
            }
        }

        /// <summary>Maps Business Connector exception types to retryable or business errors.</summary>
        internal static Exception Translate(Exception ex, string what)
        {
            var inner = ex is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : ex;
            var type = inner.GetType().FullName ?? "";
            if (!type.StartsWith(BusinessConnectorAssembly.Namespace, StringComparison.Ordinal))
                return new AxBusinessException($"{what} failed: {inner.Message}", null, inner);

            switch (inner.GetType().Name)
            {
                case "ServerUnreachableException":
                case "ServerCommunicationErrorException":
                case "SessionTerminatedException":
                case "ConnectionLostException":
                case "NotLoggedOnException":
                case "LogonUserLicenseException":
                    return new AxUnavailableException($"{what}: {inner.Message}", inner);
                case "LogonFailedException":
                case "PermissionDeniedException":
                case "NoSecurityKeyException":
                    return new AxBusinessException($"{what}: AX refused the logon or access ({inner.GetType().Name}). Check the AX user and Business Connector proxy setup.", new[] { inner.Message }, inner);
                case "XppException":
                    return new AxBusinessException($"{what}: X++ error.", new[] { inner.Message }, inner);
                default:
                    return new AxBusinessException($"{what}: {inner.GetType().Name}.", new[] { inner.Message }, inner);
            }
        }

        public void Dispose()
        {
            try
            {
                if (_loggedOn) _axapta.Logoff();
            }
            catch (Exception ex)
            {
                Log.Debug("Logoff failed", ex);
            }
            finally
            {
                _loggedOn = false;
                (_axapta as IDisposable)?.Dispose();
            }
        }
    }
}
