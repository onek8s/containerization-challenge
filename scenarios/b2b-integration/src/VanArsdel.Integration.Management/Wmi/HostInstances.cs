using System;
using System.Collections.Generic;
using System.Management;
using log4net;
using VanArsdel.Integration.Monitoring;

namespace VanArsdel.Integration.Management.Wmi
{
    public interface IHostInstances
    {
        IList<HostInstanceStatus> All();

        void Start(string hostName, string serverName);

        void Stop(string hostName, string serverName);
    }

    /// <summary>
    /// Host instance status and control through the BizTalk WMI provider (root\MicrosoftBizTalkServer,
    /// MSBTS_HostInstance). ServiceState follows the Win32 service states; HostType 2 is isolated (no state).
    /// </summary>
    public class WmiHostInstances : IHostInstances
    {
        private const string Namespace = @"root\MicrosoftBizTalkServer";
        private const string Query = "SELECT Name, HostName, RunningServer, ServiceState, IsDisabled, HostType FROM MSBTS_HostInstance";

        private static readonly ILog Log = LogManager.GetLogger(typeof(WmiHostInstances));
        private readonly string _server;

        public WmiHostInstances(string server)
        {
            _server = string.IsNullOrEmpty(server) ? "." : server;
        }

        public IList<HostInstanceStatus> All()
        {
            var list = new List<HostInstanceStatus>();
            using (var searcher = new ManagementObjectSearcher(Scope(), new ObjectQuery(Query)))
            using (var results = searcher.Get())
            {
                foreach (ManagementObject mo in results)
                {
                    using (mo)
                    {
                        list.Add(ToStatus(
                            (string)mo["HostName"],
                            (string)mo["RunningServer"],
                            Convert.ToUInt32(mo["ServiceState"] ?? 0u),
                            Convert.ToBoolean(mo["IsDisabled"] ?? false),
                            Convert.ToUInt32(mo["HostType"] ?? 1u)));
                    }
                }
            }

            return list;
        }

        public void Start(string hostName, string serverName)
        {
            Invoke(hostName, serverName, "Start");
        }

        public void Stop(string hostName, string serverName)
        {
            Invoke(hostName, serverName, "Stop");
        }

        public static HostInstanceStatus ToStatus(string hostName, string serverName, uint serviceState, bool disabled, uint hostType)
        {
            var state = hostType == 2 ? HostInstanceState.NotApplicable : serviceState <= 8 ? (HostInstanceState)serviceState : HostInstanceState.Unknown;
            return new HostInstanceStatus { HostName = hostName, ServerName = serverName, State = state, Disabled = disabled };
        }

        /// <summary>WMI key of a host instance: "Microsoft BizTalk Server &lt;host&gt; &lt;server&gt;".</summary>
        public static string InstanceName(string hostName, string serverName)
        {
            return "Microsoft BizTalk Server " + hostName + " " + serverName;
        }

        private ManagementScope Scope()
        {
            var scope = new ManagementScope(@"\\" + _server + @"\" + Namespace, new ConnectionOptions
            {
                Impersonation = ImpersonationLevel.Impersonate,
                Authentication = AuthenticationLevel.PacketPrivacy,
                Timeout = TimeSpan.FromSeconds(30)
            });
            scope.Connect();
            return scope;
        }

        private void Invoke(string hostName, string serverName, string method)
        {
            var path = new ManagementPath(string.Format("MSBTS_HostInstance.MgmtDbNameOverride='',MgmtDbServerOverride='',Name='{0}'", InstanceName(hostName, serverName)));
            using (var instance = new ManagementObject(Scope(), path, null))
            {
                Log.InfoFormat("{0} host instance {1} on {2}", method, hostName, serverName);
                instance.InvokeMethod(method, null);
            }
        }
    }
}
