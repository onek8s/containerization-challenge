using System;
using System.Diagnostics;
using System.Globalization;
using System.ServiceProcess;
using Woodgrove.Statements.Data;

namespace Woodgrove.Statements.Distribution
{
    static class Program
    {
        // AccountStatements.Distribution.exe                      Windows service
        // AccountStatements.Distribution.exe console              poll loop in the console (Enter stops)
        // AccountStatements.Distribution.exe run <id>             run one subscription now
        // AccountStatements.Distribution.exe retry                resend failed deliveries now
        // AccountStatements.Distribution.exe install-eventlog     create the event log source (administrator)
        static int Main(string[] args)
        {
            if (!Environment.UserInteractive && args.Length == 0)
            {
                ServiceBase.Run(new DistributionService());
                return 0;
            }

            var command = args.Length > 0 ? args[0].ToLowerInvariant() : "console";
            try
            {
                if (command == "install-eventlog")
                {
                    if (!EventLog.SourceExists(Log.Source)) EventLog.CreateEventSource(Log.Source, "Application");
                    Console.WriteLine("Event log source '" + Log.Source + "' is registered.");
                    return 0;
                }

                var settings = DistributionSettings.FromConfig();
                Log.Initialize(settings.LogFolder);
                var worker = new DistributionWorker(settings, Db.FromConfig());
                switch (command)
                {
                    case "console":
                        worker.Start();
                        Console.WriteLine("Polling. Press Enter to stop.");
                        Console.ReadLine();
                        worker.Stop(TimeSpan.FromMinutes(5));
                        return 0;
                    case "run":
                        if (args.Length < 2) throw new ArgumentException("Usage: run <subscriptionId>");
                        var outcome = worker.RunNow(int.Parse(args[1], CultureInfo.InvariantCulture), Environment.UserDomainName + "\\" + Environment.UserName);
                        foreach (var m in outcome.Messages) Console.WriteLine(m);
                        return outcome.Failures == 0 ? 0 : 2;
                    case "retry":
                        Console.WriteLine(worker.Executor.ProcessRetries(DateTime.Now) + " deliveries resent.");
                        return 0;
                    default:
                        Console.Error.WriteLine("Unknown command " + command);
                        return 1;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 1;
            }
        }
    }
}
