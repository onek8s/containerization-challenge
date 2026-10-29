using System;
using System.Configuration;

namespace Tailspin.DealerOrders.AxIntegration
{
    /// <summary>Connection settings for Dynamics AX 2012 R3 (appSettings "Ax.*").</summary>
    public class AxSettings
    {
        /// <summary>AOS instance as host:port of the TCP/IP port, e.g. aos01.corp.local:2712.</summary>
        public string ObjectServer { get; set; } = "aos01.corp.local:2712";

        public string Company { get; set; } = "TSM";
        public string Language { get; set; } = "en-us";

        /// <summary>
        /// Business Connector configuration: the path of an exported client configuration (.axc) or the name of a
        /// configuration stored in the registry by the Microsoft Dynamics AX 2012 Configuration utility.
        /// </summary>
        public string Configuration { get; set; } = @"C:\DealerOrders\Ax\TailspinAX-BC.axc";

        /// <summary>
        /// Optional path of Microsoft.Dynamics.BusinessConnectorNet.dll. When empty the assembly is loaded from the GAC
        /// where the Business Connector setup installs it.
        /// </summary>
        public string BusinessConnectorPath { get; set; }

        /// <summary>Business Connector proxy account (domain\user). Needed for LogonAs; the service runs as this account.</summary>
        public string BusinessConnectorProxy { get; set; } = @"CORP\svc-ax-bcproxy";

        public int SessionPoolSize { get; set; } = 2;
        public int SessionMaxCalls { get; set; } = 500;
        public TimeSpan SessionMaxAge { get; set; } = TimeSpan.FromHours(4);

        /// <summary>Base address of the AIF integration port (NetTcp), e.g. net.tcp://aos01.corp.local:8201/DynamicsAx/Services/.</summary>
        public string AifBaseAddress { get; set; } = "net.tcp://aos01.corp.local:8201/DynamicsAx/Services/";

        /// <summary>Basic or enhanced integration port that publishes the document services.</summary>
        public string AifPort { get; set; } = "TSDealerServices";

        /// <summary>User principal name of the AOS service account (Kerberos identity check of the NetTcp endpoint).</summary>
        public string AosServiceUpn { get; set; } = "svc-ax-aos@corp.local";

        public TimeSpan CallTimeout { get; set; } = TimeSpan.FromMinutes(2);

        public static AxSettings FromConfig()
        {
            var s = new AxSettings();
            string Get(string key) => ConfigurationManager.AppSettings["Ax." + key];
            s.ObjectServer = Get("ObjectServer") ?? s.ObjectServer;
            s.Company = Get("Company") ?? s.Company;
            s.Language = Get("Language") ?? s.Language;
            s.Configuration = Get("Configuration") ?? s.Configuration;
            s.BusinessConnectorPath = Get("BusinessConnectorPath");
            s.BusinessConnectorProxy = Get("BusinessConnectorProxy") ?? s.BusinessConnectorProxy;
            if (int.TryParse(Get("SessionPoolSize"), out var pool)) s.SessionPoolSize = pool;
            if (int.TryParse(Get("SessionMaxCalls"), out var calls)) s.SessionMaxCalls = calls;
            if (TimeSpan.TryParse(Get("SessionMaxAge"), out var age)) s.SessionMaxAge = age;
            s.AifBaseAddress = Get("AifBaseAddress") ?? s.AifBaseAddress;
            s.AifPort = Get("AifPort") ?? s.AifPort;
            s.AosServiceUpn = Get("AosServiceUpn") ?? s.AosServiceUpn;
            if (TimeSpan.TryParse(Get("CallTimeout"), out var timeout)) s.CallTimeout = timeout;
            return s;
        }
    }
}
