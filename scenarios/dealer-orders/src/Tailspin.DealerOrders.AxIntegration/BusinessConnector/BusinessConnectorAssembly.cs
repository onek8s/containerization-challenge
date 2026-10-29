using System;
using System.IO;
using System.Reflection;
using log4net;

namespace Tailspin.DealerOrders.AxIntegration.BusinessConnector
{
    /// <summary>
    /// Loads Microsoft.Dynamics.BusinessConnectorNet at run time. The assembly comes with the AX 2012 Business Connector
    /// setup (GAC and BusinessConnector\Bin); it is not referenced at compile time so the solution builds on machines
    /// without the AX client components.
    /// </summary>
    internal static class BusinessConnectorAssembly
    {
        public const string FullName = "Microsoft.Dynamics.BusinessConnectorNet, Version=6.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35";
        public const string Namespace = "Microsoft.Dynamics.BusinessConnectorNet";

        private static readonly ILog Log = LogManager.GetLogger(typeof(BusinessConnectorAssembly));
        private static readonly object Gate = new object();
        private static Assembly _assembly;

        public static Assembly Load(string path)
        {
            lock (Gate)
            {
                if (_assembly != null) return _assembly;
                try
                {
                    _assembly = string.IsNullOrEmpty(path) ? Assembly.Load(FullName) : Assembly.LoadFrom(path);
                }
                catch (Exception ex) when (ex is FileNotFoundException || ex is FileLoadException || ex is BadImageFormatException)
                {
                    throw new InvalidOperationException(
                        "The Dynamics AX 2012 .NET Business Connector is not installed on this server (" + (path ?? FullName) + "). " +
                        "Install the Business Connector from the AX 2012 R3 setup and configure Ax.Configuration.", ex);
                }
                Log.InfoFormat("Business Connector loaded from {0} ({1})", _assembly.Location, _assembly.GetName().Version);
                return _assembly;
            }
        }

        public static Type Type(Assembly assembly, string name) =>
            assembly.GetType(Namespace + "." + name, throwOnError: true);
    }
}
