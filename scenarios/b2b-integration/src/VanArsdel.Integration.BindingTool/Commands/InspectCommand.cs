using System;
using System.IO;
using System.Linq;
using System.Xml;
using VanArsdel.Integration.Deployment;

namespace VanArsdel.Integration.BindingTool.Commands
{
    internal static class InspectCommand
    {
        public static int Run(InspectOptions o, TextWriter output)
        {
            var doc = new XmlDocument();
            doc.Load(o.Bindings);
            var report = BindingsInspector.Inspect(doc);

            foreach (var e in report.Endpoints.OrderBy(e => e.Kind).ThenBy(e => e.Name))
            {
                output.WriteLine("{0,-16} {1,-40} {2,-14} {3,-22} {4}", e.Kind, e.Name, e.TransportType, e.Host, e.Address);
            }

            output.WriteLine();
            output.WriteLine("{0} orchestration(s) bound", report.Orchestrations.Count);

            var problems = report.Problems.ToList();
            if (!string.Equals(o.Environment, "DEV", StringComparison.OrdinalIgnoreCase))
            {
                problems.AddRange(report.Endpoints
                    .Where(e => (e.Address ?? string.Empty).IndexOf("localhost", StringComparison.OrdinalIgnoreCase) >= 0 && e.TransportType != "WCF-BasicHttp")
                    .Select(e => e.Name + ": address points to localhost (" + e.Address + ")"));
            }

            foreach (var p in problems)
            {
                output.WriteLine("PROBLEM: " + p);
            }

            return problems.Count == 0 ? 0 : 4;
        }
    }
}
