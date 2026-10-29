using System;
using System.ServiceProcess;

namespace CreditDesk.CreditService
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length > 0 && string.Equals(args[0], "/console", StringComparison.OrdinalIgnoreCase))
            {
                // Interactive mode for troubleshooting on the server.
                var host = new CreditServiceHost();
                host.Start();
                Console.WriteLine("Credit Desk credit service is listening:");
                foreach (var address in host.ListenAddresses)
                    Console.WriteLine("  " + address);
                Console.WriteLine("Press Enter to stop.");
                Console.ReadLine();
                host.Stop();
                return 0;
            }

            ServiceBase.Run(new CreditWindowsService());
            return 0;
        }
    }
}
