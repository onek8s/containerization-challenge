using System;
using System.Data.SqlClient;
using System.IO;
using Contoso.Purchasing.Data;

namespace Contoso.Purchasing.Jobs
{
    /// <summary>
    /// Purchase Requests batch jobs, started by Task Scheduler on the application server.
    ///   Purchasing.Jobs.exe reminders   weekdays 07:00
    ///   Purchasing.Jobs.exe export      every 30 minutes 06:00-20:00
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            var job = args.Length > 0 ? args[0].ToLowerInvariant() : "";
            if (job != "reminders" && job != "export")
            {
                Console.Error.WriteLine("Usage: Purchasing.Jobs.exe reminders|export");
                return 2;
            }

            try
            {
                JobLog.Init(job);
                var db = new Database();
                var now = DateTime.Now;
                var result = job == "reminders" ? new ReminderJob(db).Run(now) : new ExportJob(db).Run(now);
                JobLog.Info(job + " finished with exit code " + result);
                return result;
            }
            catch (SqlException ex)
            {
                JobLog.Error("Database error: " + ex.Message);
                return 3;
            }
            catch (IOException ex)
            {
                JobLog.Error("File error: " + ex.Message);
                return 4;
            }
            catch (Exception ex)
            {
                JobLog.Error(ex.ToString());
                return 1;
            }
        }
    }
}
