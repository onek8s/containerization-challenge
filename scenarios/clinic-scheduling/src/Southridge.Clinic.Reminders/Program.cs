using System;
using System.Linq;
using Southridge.Clinic.Data.Repositories;
using Southridge.Clinic.Infrastructure;

namespace Southridge.Clinic.Reminders
{
    /// <summary>
    /// Scheduled tasks on clinic-int: "reminders" every 30 minutes 07:00-20:00, "noshows" hourly,
    /// "waitlist" at 06:30 and 12:30.
    /// Usage: SouthridgeReminders.exe reminders|noshows|waitlist
    /// </summary>
    internal static class Program
    {
        private static readonly string[] Jobs = { "reminders", "noshows", "waitlist" };

        private static int Main(string[] args)
        {
            string job = args.Length == 1 ? args[0].ToLowerInvariant() : null;
            if (!Jobs.Contains(job))
            {
                Console.Error.WriteLine("Usage: SouthridgeReminders.exe " + string.Join("|", Jobs));
                return 2;
            }

            ClinicContainer.ApplyConfiguration(ClinicContainer.Current);
            string workFolder = ClinicSettings.Get("Reminders:WorkFolder", @"..\work\reminders");
            using (JobLock jobLock = JobLock.TryAcquire(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, workFolder), "Reminders-" + job))
            {
                if (jobLock == null)
                {
                    ClinicLog.Warning(LogCategory.Scheduling, "Reminders job " + job + " is already running.");
                    return 3;
                }

                var system = new SystemRepository();
                int runId = system.StartJob("Reminders-" + job);
                try
                {
                    var jobs = new ReminderJobs(new AppointmentRepository(), system, new SmtpMailer(), new HttpSmsGateway(), new SystemClock());
                    string summary = job == "reminders" ? jobs.SendReminders() : job == "noshows" ? jobs.MarkNoShows() : jobs.OfferWaitlist();
                    system.FinishJob(runId, true, summary);
                    ClinicLog.Info(LogCategory.Scheduling, "Reminders job {0}: {1}", job, summary);
                    Console.WriteLine(summary);
                    return 0;
                }
                catch (Exception ex)
                {
                    system.FinishJob(runId, false, ex.Message);
                    ErrorHandler.Handle(ex, ExceptionPolicies.DataAccess);
                    Console.Error.WriteLine(ex);
                    return 1;
                }
            }
        }
    }
}
