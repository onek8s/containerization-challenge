using System;
using System.Configuration;
using Wingtip.Invoices.Infrastructure.Hosting;

namespace Wingtip.Invoices.Gateway
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length > 0 && args[0].TrimStart('-', '/').Equals("protect-config", StringComparison.OrdinalIgnoreCase))
            {
                return ProtectConfiguration();
            }

            var result = ServiceRunner.Run(args, "InvoiceGateway", () => new GatewayService());
            if (result == -1)
            {
                Console.WriteLine("Usage: Invoices.Gateway.exe [console | install-diagnostics | protect-config]");
                return 1;
            }
            return result;
        }

        /// <summary>
        /// Encrypts appSettings and connectionStrings of Invoices.Gateway.exe.config with the machine key (DPAPI).
        /// Run on the gateway server after the FTP password has been entered.
        /// </summary>
        private static int ProtectConfiguration()
        {
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            foreach (var name in new[] { "appSettings", "connectionStrings" })
            {
                var section = config.GetSection(name);
                if (!section.SectionInformation.IsProtected)
                {
                    section.SectionInformation.ProtectSection("DataProtectionConfigurationProvider");
                    section.SectionInformation.ForceSave = true;
                }
            }
            config.Save(ConfigurationSaveMode.Modified);
            Console.WriteLine("Configuration sections encrypted in {0}.", config.FilePath);
            return 0;
        }
    }
}
