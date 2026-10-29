using System;
using Wingtip.Invoices.Infrastructure.Hosting;

namespace Wingtip.Invoices.Processor
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            var result = ServiceRunner.Run(args, "InvoiceProcessor", () => new ProcessorService());
            if (result == -1)
            {
                Console.WriteLine("Usage: Invoices.Processor.exe [console | install-diagnostics]");
                return 1;
            }
            return result;
        }
    }
}
