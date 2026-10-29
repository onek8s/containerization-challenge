using System.Collections.Generic;

namespace Fabrikam.Housing.Identity.Ldap
{
    public class LdapOptions
    {
        /// <summary>Directory (default) or Simulated.</summary>
        public string Mode { get; set; } = "Directory";
        public string Server { get; set; } = "dc01.corp.local";
        public int Port { get; set; } = 636;
        public bool UseSsl { get; set; } = true;
        public string Domain { get; set; } = "CORP";
        public string DnsDomain { get; set; } = "corp.local";
        public string SearchBase { get; set; } = "DC=corp,DC=local";
        /// <summary>AD group (CN) to housing role.</summary>
        public Dictionary<string, string> GroupRoles { get; set; } = new Dictionary<string, string>();
        public List<SimulatedLdapUser> SimulatedUsers { get; set; } = new List<SimulatedLdapUser>();
    }

    public class SimulatedLdapUser
    {
        public string SamAccountName { get; set; }
        public string Password { get; set; }
        public string DisplayName { get; set; }
        public string Mail { get; set; }
        public string Patch { get; set; }
        public List<string> Groups { get; set; } = new List<string>();
    }

    public class DirectoryUser
    {
        public string SamAccountName { get; set; }
        public string DisplayName { get; set; }
        public string Mail { get; set; }
        public string Patch { get; set; }
        public IList<string> Groups { get; set; } = new List<string>();
    }
}
