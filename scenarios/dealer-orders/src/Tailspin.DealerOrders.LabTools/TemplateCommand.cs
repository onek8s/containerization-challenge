using System;
using System.IO;
using Tailspin.DealerOrders.Documents.Word;

namespace Tailspin.DealerOrders.LabTools
{
    internal static class TemplateCommand
    {
        public static int Run(Options options)
        {
            var folder = options.Arg(0) ?? throw new ArgumentException("Give the output folder.");
            Directory.CreateDirectory(folder);
            TemplateFactory.WriteAll(folder);
            Console.WriteLine("Templates written to " + folder);
            return 0;
        }
    }
}
