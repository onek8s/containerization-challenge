using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using log4net;
using log4net.Config;
using Southridge.Clinic.Data.Repositories;
using Southridge.Clinic.Infrastructure;

namespace Southridge.Clinic.BillingBatch
{
    /// <summary>
    /// Nightly billing run (Task Scheduler on clinic-int, 01:30). Without arguments all steps run in order.
    /// Usage: SouthridgeBilling.exe [claims] [send] [responses] [statements] [aging] [/date:yyyy-MM-dd]
    /// </summary>
    internal static class Program
    {
        private const string JobName = "NightlyBilling";
        private static readonly ILog Log = LogManager.GetLogger(typeof(Program));
        private static readonly string[] AllSteps = { "claims", "send", "responses", "statements", "aging" };

        private static int Main(string[] args)
        {
            XmlConfigurator.Configure(new FileInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log4net.config")));
            DateTime runDate = DateTime.Today;
            var steps = new List<string>();
            foreach (string arg in args)
            {
                if (arg.StartsWith("/date:", StringComparison.OrdinalIgnoreCase))
                {
                    runDate = DateTime.ParseExact(arg.Substring(6), "yyyy-MM-dd", CultureInfo.InvariantCulture);
                }
                else if (AllSteps.Contains(arg.ToLowerInvariant()))
                {
                    steps.Add(arg.ToLowerInvariant());
                }
                else
                {
                    Console.Error.WriteLine("Unknown argument " + arg + ". Steps: " + string.Join(" ", AllSteps) + " [/date:yyyy-MM-dd]");
                    return 2;
                }
            }

            if (steps.Count == 0)
            {
                steps.AddRange(AllSteps);
            }

            ClinicContainer.ApplyConfiguration(ClinicContainer.Current);
            BatchSettings settings = BatchSettings.Load();
            using (JobLock jobLock = JobLock.TryAcquire(settings.WorkFolder, JobName))
            {
                if (jobLock == null)
                {
                    Log.Warn("Another billing run is still going; exiting.");
                    return 3;
                }

                var system = new SystemRepository();
                int runId = system.StartJob(JobName);
                var summary = new List<string>();
                bool ok = true;
                var billing = new BillingSteps(settings, runDate);
                Log.InfoFormat("Billing run {0} for {1:yyyy-MM-dd}: {2}", runId, runDate, string.Join(", ", steps.ToArray()));
                foreach (string step in steps)
                {
                    try
                    {
                        string result = Run(billing, step);
                        Log.Info(step + ": " + result);
                        summary.Add(step + ": " + result);
                    }
                    catch (Exception ex)
                    {
                        // Later steps still run: statements and the aging report do not depend on the clearinghouse.
                        ok = false;
                        Log.Error("Step " + step + " failed.", ex);
                        ErrorHandler.Handle(ex, ExceptionPolicies.DataAccess);
                        summary.Add(step + ": FAILED " + ex.Message);
                    }
                }

                system.FinishJob(runId, ok, string.Join("; ", summary.ToArray()));
                return ok ? 0 : 1;
            }
        }

        private static string Run(BillingSteps billing, string step)
        {
            switch (step)
            {
                case "claims":
                    return billing.CreateClaims();
                case "send":
                    return billing.SendClaims();
                case "responses":
                    return billing.ReceiveResponses();
                case "statements":
                    return billing.Statements();
                default:
                    return billing.AgingReport();
            }
        }
    }
}
