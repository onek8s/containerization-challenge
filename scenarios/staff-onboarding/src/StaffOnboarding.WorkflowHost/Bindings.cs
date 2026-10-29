using System;
using System.ServiceModel;

namespace StaffOnboarding.WorkflowHost
{
    internal static class Bindings
    {
        /// <summary>HTTP with Windows authentication (Kerberos) for the portal.</summary>
        public static BasicHttpBinding Intranet()
        {
            var binding = new BasicHttpBinding(BasicHttpSecurityMode.TransportCredentialOnly)
            {
                MaxReceivedMessageSize = 1024 * 1024,
                SendTimeout = TimeSpan.FromMinutes(2),
                ReceiveTimeout = TimeSpan.FromMinutes(10)
            };
            binding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Windows;
            return binding;
        }

        public static string ControlAddress(string path)
        {
            return "net.pipe://localhost/StaffOnboarding/" + path + "/control";
        }
    }
}
