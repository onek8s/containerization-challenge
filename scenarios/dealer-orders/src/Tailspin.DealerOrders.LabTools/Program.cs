using System;
using System.Collections.Generic;
using System.Linq;
using log4net;
using Tailspin.DealerOrders.Infrastructure.Logging;

namespace Tailspin.DealerOrders.LabTools
{
    /// <summary>
    /// Test system tools:
    ///   dolab schema [--server sql01] [--var Name=Value ...]   create the database from db\*.sql
    ///   dolab seed [--password Lab-Passw0rd]                 lab dealers, EDI partners, portal users, campaigns
    ///   dolab sync                                           one price/stock/item/rate sync from the configured AX
    ///   dolab certs &lt;folder&gt;                                 self-signed AS2 certificates for us and two dealers
    ///   dolab send-850 &lt;file&gt; [--url ..] [--partner-pfx ..] [--our-cer ..]   post an 850 to the AS2 receiver as a dealer
    ///   dolab templates &lt;folder&gt;                             write the Word templates
    ///   dolab flow                                           in-memory order flow against the AX simulator
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Verbs: schema, seed, sync, certs, send-850, templates, flow");
                return 1;
            }

            LogSetup.Configure("LabTools");
            var options = Options.Parse(args.Skip(1));
            try
            {
                switch (args[0].ToLowerInvariant())
                {
                    case "schema": return SchemaCommand.Run(options);
                    case "seed": return SeedCommand.Run(options);
                    case "sync": return SeedCommand.Sync(options);
                    case "certs": return As2Commands.Certificates(options);
                    case "send-850": return As2Commands.Send(options);
                    case "templates": return TemplateCommand.Run(options);
                    case "flow": return FlowCommand.Run(options);
                    default:
                        Console.Error.WriteLine("Unknown verb " + args[0]);
                        return 1;
                }
            }
            catch (Exception ex)
            {
                LogManager.GetLogger(typeof(Program)).Error(args[0] + " failed", ex);
                Console.Error.WriteLine(ex.GetBaseException().Message);
                return 2;
            }
        }
    }

    internal class Options
    {
        public List<string> Positional { get; } = new List<string>();
        public Dictionary<string, List<string>> Named { get; } = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        public string this[string name, string defaultValue = null] => Named.TryGetValue(name, out var v) && v.Count > 0 ? v[v.Count - 1] : defaultValue;

        public IEnumerable<string> All(string name) => Named.TryGetValue(name, out var v) ? v : Enumerable.Empty<string>();

        public string Arg(int index, string defaultValue = null) => index < Positional.Count ? Positional[index] : defaultValue;

        public static Options Parse(IEnumerable<string> args)
        {
            var options = new Options();
            string pending = null;
            foreach (var a in args)
            {
                if (a.StartsWith("--"))
                {
                    pending = a.Substring(2);
                    if (!options.Named.ContainsKey(pending)) options.Named[pending] = new List<string>();
                }
                else if (pending != null)
                {
                    options.Named[pending].Add(a);
                    pending = null;
                }
                else options.Positional.Add(a);
            }
            return options;
        }
    }
}
