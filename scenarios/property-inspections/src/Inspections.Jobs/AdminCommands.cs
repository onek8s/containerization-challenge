using System;
using System.Linq;
using System.Security.Principal;
using System.Text;
using Northwind.Inspections.Core;
using Northwind.Inspections.Infrastructure.Configuration;
using Northwind.Inspections.Infrastructure.Data;
using Northwind.Inspections.Infrastructure.Diagnostics;
using Northwind.Inspections.Infrastructure.Imaging;

namespace Northwind.Inspections.Jobs
{
    internal static class AdminCommands
    {
        private static readonly string[] SecretNames = { SettingNames.SharePassword, SettingNames.SmtpPassword };

        public static int Install()
        {
            if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
            {
                Console.Error.WriteLine("Run 'install' from an elevated prompt.");
                return 1;
            }
            InspectionsLog.Install();
            Console.WriteLine("Event log '{0}' and sources registered.", InspectionsLog.LogName);
            InspectionsCounters.Install();
            Console.WriteLine("Performance counter category '{0}' created.", InspectionsCounters.Category);
            return 0;
        }

        public static int ProtectSecret(string name)
        {
            var match = SecretNames.FirstOrDefault(s => s.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (match == null)
            {
                Console.Error.WriteLine("Secret name must be one of: " + string.Join(", ", SecretNames));
                return 1;
            }

            Console.WriteLine("Storing {0} for {1}. The value can only be read by this account.", match, WindowsIdentity.GetCurrent().Name);
            var value = ReadHidden("Value: ");
            if (value != ReadHidden("Confirm: "))
            {
                Console.Error.WriteLine("The values do not match.");
                return 1;
            }
            InspectionSettings.WriteSecret(match, value);
            Console.WriteLine("Stored under HKLM\\{0}\\{1}.", SettingNames.RegistryKey, InspectionSettings.SecretKeyForCurrentAccount());
            return 0;
        }

        public static int HashPassword()
        {
            var value = ReadHidden("Password: ");
            Console.WriteLine(PasswordHasher.Hash(value));
            return 0;
        }

        public static int Status()
        {
            var settings = InspectionSettings.Current;
            Console.WriteLine("Account        {0}", WindowsIdentity.GetCurrent().Name);
            Console.WriteLine("Photo share    {0} (as {1})", settings.PhotoShare, settings.ShareUser ?? "process account");
            Console.WriteLine("SMTP           {0}:{1} from {2}", settings.SmtpHost, settings.SmtpPort, settings.SmtpFrom);
            Console.WriteLine("Portal         {0}", settings.PortalUrl);
            Console.WriteLine("Retention      {0} days", settings.PurgeAfterDays);

            var db = new Db(settings.ConnectionString);
            try
            {
                Console.WriteLine("Report queue   {0}", new ReportQueue(db).Depth());
            }
            catch (Exception ex)
            {
                Console.WriteLine("Database       FAILED: {0}", ex.Message);
            }
            Console.WriteLine("Share access   {0}", new PhotoStore(settings, db).ShareReachable() ? "OK" : "FAILED");
            foreach (var name in SecretNames)
            {
                string state;
                try
                {
                    state = (name == SettingNames.SharePassword ? settings.SharePassword : settings.SmtpPassword) == null ? "not set" : "OK";
                }
                catch (InvalidOperationException ex)
                {
                    state = ex.Message;
                }
                Console.WriteLine("{0,-14} {1}", name, state);
            }
            return 0;
        }

        private static string ReadHidden(string prompt)
        {
            Console.Write(prompt);
            var sb = new StringBuilder();
            while (true)
            {
                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Enter)
                {
                    break;
                }
                if (key.Key == ConsoleKey.Backspace)
                {
                    if (sb.Length > 0)
                    {
                        sb.Length--;
                    }
                    continue;
                }
                sb.Append(key.KeyChar);
            }
            Console.WriteLine();
            return sb.ToString();
        }
    }
}
