using System;
using Wingtip.Invoices.Infrastructure.Hosting;

namespace Wingtip.Invoices.ErpLink
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            var result = ServiceRunner.Run(args, "InvoiceErpLink", () => new ErpLinkService());
            if (result == -1)
            {
                Console.WriteLine("Usage: Invoices.ErpLink.exe [console | install-diagnostics]");
                return 1;
            }
            return result;
        }
    }
}
