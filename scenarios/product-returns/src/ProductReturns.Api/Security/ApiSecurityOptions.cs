using System.Collections.Generic;

namespace ProductReturns.Api.Security
{
    public class ApiSecurityOptions
    {
        /// <summary>Windows accounts allowed to call the API for the portal, e.g. CORP\svc-returns-web.</summary>
        public List<string> PortalAccounts { get; set; } = new List<string>();

        /// <summary>SHA-256 hashes of API keys the portal may use instead of Windows authentication.</summary>
        public List<string> PortalKeyHashes { get; set; } = new List<string>();
    }
}
