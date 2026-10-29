using System;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using Tailspin.CustomerCare.Data.Repositories;
using Tailspin.CustomerCare.Domain;

namespace Tailspin.CustomerCare.Archive
{
    /// <summary>
    /// Moves closed cases older than the retention period out of the database. Each case becomes a zip file
    /// (case.json + attachments) under &lt;Archive:Folder&gt;\yyyy\MM, then the case is deleted.
    ///   CustomerCare.Archive.exe [--months 24] [--batch 500] [--dry-run]
    /// Exit codes: 0 ok, 2 error, 3 already running.
    /// </summary>
    internal static class Program
    {
        private static TextWriter _log;

        private static int Main(string[] args)
        {
            using (var mutex = new Mutex(false, @"Global\TailspinCustomerCareArchive"))
            {
                if (!mutex.WaitOne(TimeSpan.Zero))
                {
                    Console.Error.WriteLine("Another archive run is in progress.");
                    return 3;
                }

                try
                {
                    OpenLog();
                    return Run(args);
                }
                catch (Exception ex)
                {
                    Log("ERROR " + ex);
                    return 2;
                }
                finally
                {
                    if (_log != null) _log.Dispose();
                    mutex.ReleaseMutex();
                }
            }
        }

        private static int Run(string[] args)
        {
            var months = int.Parse(Option(args, "--months") ?? ConfigurationManager.AppSettings["Archive:KeepMonths"] ?? "24", CultureInfo.InvariantCulture);
            var batch = int.Parse(Option(args, "--batch") ?? ConfigurationManager.AppSettings["Archive:BatchSize"] ?? "500", CultureInfo.InvariantCulture);
            var dryRun = args.Contains("--dry-run");
            var root = ConfigurationManager.AppSettings["Archive:Folder"];

            var cutoff = DateTime.UtcNow.AddMonths(-months);
            var repo = new ArchiveRepository();
            var ids = repo.Candidates(cutoff, batch);
            Log(string.Format("{0} case(s) closed before {1:yyyy-MM-dd} to archive{2}.", ids.Count, cutoff, dryRun ? " (dry run)" : string.Empty));

            var done = 0;
            foreach (var id in ids)
            {
                var c = repo.LoadFull(id);
                if (c == null) continue;

                var closed = c.ClosedUtc ?? c.CreatedUtc;
                var folder = Path.Combine(root, closed.ToString("yyyy", CultureInfo.InvariantCulture), closed.ToString("MM", CultureInfo.InvariantCulture));
                var file = Path.Combine(folder, c.CaseNumber + ".zip");

                if (dryRun)
                {
                    Log("  would archive " + c.CaseNumber + " to " + file);
                    continue;
                }

                Directory.CreateDirectory(folder);
                Write(c, file);
                repo.Purge(c.Id, file);
                done++;
            }

            if (!dryRun) Log(done + " case(s) archived.");
            return 0;
        }

        private static void Write(Case c, string file)
        {
            var tmp = file + ".tmp";
            using (var zip = ZipFile.Open(tmp, ZipArchiveMode.Create))
            {
                var data = new ArchivedCase
                {
                    CaseNumber = c.CaseNumber,
                    AccountNumber = c.Customer.AccountNumber,
                    CustomerName = c.Customer.Name,
                    Queue = c.Queue.Code,
                    Category = c.CategoryCode,
                    Subject = c.Subject,
                    Description = c.Description,
                    Priority = c.Priority.ToString(),
                    Channel = c.Channel.ToString(),
                    ExternalReference = c.ExternalReference,
                    CreatedUtc = c.CreatedUtc,
                    CreatedBy = c.CreatedBy,
                    ResolvedUtc = c.ResolvedUtc,
                    ClosedUtc = c.ClosedUtc,
                    AssignedTo = c.AssignedTo,
                    Notes = c.Notes.OrderBy(n => n.CreatedUtc).Select(n => new ArchivedNote { Author = n.Author, CreatedUtc = n.CreatedUtc, IsInternal = n.IsInternal, Body = n.Body }).ToList(),
                    Attachments = c.Attachments.Select(a => "attachments/" + a.Id + "_" + a.FileName).ToList()
                };

                using (var s = zip.CreateEntry("case.json").Open())
                {
                    new DataContractJsonSerializer(typeof(ArchivedCase)).WriteObject(s, data);
                }

                foreach (var a in c.Attachments)
                {
                    using (var s = zip.CreateEntry("attachments/" + a.Id + "_" + a.FileName, CompressionLevel.Optimal).Open())
                    {
                        s.Write(a.Content, 0, a.Content.Length);
                    }
                }
            }

            if (File.Exists(file)) File.Delete(file);
            File.Move(tmp, file);
        }

        private static string Option(string[] args, string name)
        {
            var i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        private static void OpenLog()
        {
            var folder = ConfigurationManager.AppSettings["Archive:LogFolder"];
            if (string.IsNullOrEmpty(folder)) return;
            Directory.CreateDirectory(folder);
            _log = new StreamWriter(Path.Combine(folder, "archive_" + DateTime.Now.ToString("yyyyMM") + ".log"), true, Encoding.UTF8) { AutoFlush = true };
        }

        private static void Log(string message)
        {
            var line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss ") + message;
            Console.WriteLine(line);
            if (_log != null) _log.WriteLine(line);
        }
    }
}
