using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lucerne.ProjectSites.LabStubs.Seed;
using Lucerne.ProjectSites.LabStubs.Simulators;

namespace Lucerne.ProjectSites.LabStubs
{
    /// <summary>
    ///   run   [--out DIR] [--today yyyy-MM-dd] [--port 5081]   end-to-end run in simulator mode
    ///   serve [--port 5081] [--today yyyy-MM-dd]                 ERP, HR and document control simulators only
    /// </summary>
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            var command = args.FirstOrDefault() ?? "run";
            var today = DateTime.ParseExact(Option(args, "--today") ?? DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), "yyyy-MM-dd", CultureInfo.InvariantCulture);
            var port = int.Parse(Option(args, "--port") ?? "5081", CultureInfo.InvariantCulture);

            switch (command)
            {
                case "run":
                    var output = Option(args, "--out") ?? Path.Combine(Path.GetTempPath(), "lucerne-projectsites-lab");
                    return await new LabRunner(today, output, port).RunAsync();
                case "serve":
                    await using (var host = new SimulatorHost(new SeedData(today), port))
                    {
                        await host.StartAsync();
                        Console.WriteLine("Simulators on " + host.BaseUrl + ":");
                        Console.WriteLine("  ERP  " + host.BaseUrl + "/ProjectAccounting/ProjectService.svc");
                        Console.WriteLine("  HR   " + host.BaseUrl + "/hrapi/api/v2/employees  (X-Api-Key: " + SimulatorHost.HrApiKey + ")");
                        Console.WriteLine("  DC   " + host.BaseUrl + "/doccontrol  (client " + SimulatorHost.DocControlClientId + " / " + SimulatorHost.DocControlSecret + ")");
                        Console.WriteLine("Ctrl+C to stop.");
                        var done = new TaskCompletionSource<bool>();
                        Console.CancelKeyPress += (s, e) => { e.Cancel = true; done.TrySetResult(true); };
                        await done.Task;
                    }

                    return 0;
                default:
                    Console.Error.WriteLine("Commands: run, serve");
                    return 2;
            }
        }

        private static string Option(string[] args, string name)
        {
            var i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
