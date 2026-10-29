using System;
using System.Configuration;
using System.Data.SqlClient;

namespace VanArsdel.Integration.Management
{
    /// <summary>
    /// Location of the BizTalk group (management database). Every management API connects with the caller's Windows
    /// identity: the portal app pool account and the monitor service account are members of the BizTalk Operators
    /// group, the binding tool runs as a BizTalk administrator.
    /// </summary>
    public class BizTalkGroup
    {
        public BizTalkGroup(string managementServer, string managementDatabase)
        {
            if (string.IsNullOrEmpty(managementServer))
            {
                throw new ArgumentNullException("managementServer");
            }

            ManagementServer = managementServer;
            ManagementDatabase = string.IsNullOrEmpty(managementDatabase) ? "BizTalkMgmtDb" : managementDatabase;
        }

        public string ManagementServer { get; private set; }
        public string ManagementDatabase { get; private set; }

        /// <summary>WMI namespace of the group; host instance queries go to the server named in BizTalk.WmiServer.</summary>
        public string WmiServer { get; set; }

        public string ExplorerConnectionString
        {
            get
            {
                return new SqlConnectionStringBuilder
                {
                    DataSource = ManagementServer,
                    InitialCatalog = ManagementDatabase,
                    IntegratedSecurity = true,
                    ApplicationName = "VanArsdel.Integration.Management"
                }.ConnectionString;
            }
        }

        public static BizTalkGroup FromConfig()
        {
            var settings = ConfigurationManager.AppSettings;
            var group = new BizTalkGroup(settings["BizTalk.MgmtDbServer"], settings["BizTalk.MgmtDbName"]);
            group.WmiServer = settings["BizTalk.WmiServer"] ?? ".";
            return group;
        }
    }
}
