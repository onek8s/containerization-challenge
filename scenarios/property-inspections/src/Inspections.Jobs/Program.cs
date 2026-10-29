using System;
using System.Diagnostics;
using System.Threading;
using Northwind.Inspections.Infrastructure.Diagnostics;

namespace Northwind.Inspections.Jobs
{
    internal static class Program
    {
        private const string Usage = @"Inspections.Jobs <command>

  run-reports            Send queued inspection reports (scheduled every 5 minutes)
  purge                  Remove photos of reported inspections after the retention period (scheduled daily)
  status                 Show settings, queue depth and share access for the current account
  protect-secret <name>  Store SharePassword or SmtpPassword for the current account (DPAPI)
  hash-password          Print a password hash for dbo.Adjusters or dbo.Partners
  install                Register event log sources and performance counters (administrator)";

        private static int Main(string[] args)
        {
            InspectionsLog.UseSource(InspectionsLog.JobsSource);
            if (args.Length == 0)
            {
                Console.WriteLine(Usage);
                return 1;
            }

            var command = args[0].ToLowerInvariant();
            try
            {
                switch (command)
                {
                    case "run-reports":
                        return Exclusive(command, () => new ReportJob().Run());
                    case "purge":
                        return Exclusive(command, () => new PurgeJob().Run());
                    case "status":
                        return AdminCommands.Status();
                    case "protect-secret":
                        return AdminCommands.ProtectSecret(args.Length > 1 ? args[1] : null);
                    case "hash-password":
                        return AdminCommands.HashPassword();
                    case "install":
                        return AdminCommands.Install();
                    default:
                        Console.WriteLine(Usage);
                        return 1;
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError("{0} failed: {1}", command, ex);
                InspectionsLog.Error(EventId.UnhandledError, ex, "Inspections.Jobs {0} failed.", command);
                return 2;
            }
        }

        /// <summary>Overlapping scheduled runs on the same server skip instead of competing.</summary>
        private static int Exclusive(string name, Func<int> run)
        {
            bool created;
            using (var mutex = new Mutex(false, @"Global\Northwind.Inspections.Jobs." + name, out created))
            {
                if (!mutex.WaitOne(TimeSpan.Zero))
                {
                    Trace.TraceInformation("{0} is already running on this server.", name);
                    return 0;
                }
                try
                {
                    return run();
                }
                finally
                {
                    mutex.ReleaseMutex();
                }
            }
        }
    }
}
