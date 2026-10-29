using System;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Tailspin.CustomerCare.Data.Repositories;
using Tailspin.CustomerCare.Domain;

namespace Tailspin.CustomerCare.Import
{
    /// <summary>
    /// Imports the nightly customer extract from billing.
    ///   CustomerCare.Import.exe                  processes every file in &lt;Import:Folder&gt;\Inbox
    ///   CustomerCare.Import.exe --file x.csv     processes one file (not moved afterwards)
    ///   CustomerCare.Import.exe --validate x.csv only checks the file
    /// Exit codes: 0 ok, 1 file rejected, 2 error, 3 already running.
    /// </summary>
    internal static class Program
    {
        private static TextWriter _log;

        private static int Main(string[] args)
        {
            using (var mutex = new Mutex(false, @"Global\TailspinCustomerCareImport"))
            {
                if (!mutex.WaitOne(TimeSpan.Zero))
                {
                    Console.Error.WriteLine("Another import is running.");
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
            if (args.Length == 2 && args[0] == "--validate")
            {
                var result = Parse(args[1]);
                Log(string.Format("{0}: {1} valid rows, {2} rejected.", args[1], result.Rows.Count, result.Errors.Count));
                foreach (var e in result.Errors.Take(50)) Log("  " + e);
                return result.Errors.Count == 0 ? 0 : 1;
            }

            if (args.Length == 2 && args[0] == "--file")
            {
                return Import(args[1], false);
            }

            if (args.Length > 0)
            {
                Console.Error.WriteLine("Usage: CustomerCare.Import.exe [--file <path> | --validate <path>]");
                return 2;
            }

            var root = ConfigurationManager.AppSettings["Import:Folder"];
            var inbox = Path.Combine(root, "Inbox");
            Directory.CreateDirectory(inbox);
            Directory.CreateDirectory(Path.Combine(root, "Processed"));
            Directory.CreateDirectory(Path.Combine(root, "Rejected"));

            var files = Directory.GetFiles(inbox, ConfigurationManager.AppSettings["Import:FilePattern"] ?? "*.csv").OrderBy(f => f).ToList();
            if (files.Count == 0)
            {
                Log("No files in " + inbox + ".");
                return 0;
            }

            var exit = 0;
            foreach (var f in files) exit = Math.Max(exit, Import(f, true));
            return exit;
        }

        private static int Import(string path, bool move)
        {
            Log("Importing " + path);
            var result = Parse(path);
            var total = result.Rows.Count + result.Errors.Count;
            var maxRejected = double.Parse(ConfigurationManager.AppSettings["Import:MaxRejectedPercent"] ?? "5", CultureInfo.InvariantCulture);

            if (result.Errors.Count > 0)
            {
                File.WriteAllLines(path + ".errors.txt", result.Errors.Select(e => e.ToString()), Encoding.UTF8);
                Log(string.Format("{0} of {1} rows rejected, details in {2}.errors.txt", result.Errors.Count, total, Path.GetFileName(path)));
            }

            if (total == 0 || result.Errors.Count * 100.0 / total > maxRejected)
            {
                Log("File rejected: too many invalid rows.");
                if (move) MoveTo(path, "Rejected");
                return 1;
            }

            var merge = new CustomerRepository().MergeImport(result.Rows, Path.GetFileName(path));
            Log(string.Format("Done: {0} new, {1} updated, {2} unchanged.", merge.Inserted, merge.Updated, merge.Unchanged));

            if (move) MoveTo(path, "Processed");
            return 0;
        }

        private static CustomerImportResult Parse(string path)
        {
            // Billing writes the extract in Windows-1252.
            using (var reader = new StreamReader(path, Encoding.GetEncoding(1252), true))
            {
                return CustomerCsvParser.Parse(reader);
            }
        }

        private static void MoveTo(string path, string folder)
        {
            var root = Path.GetDirectoryName(Path.GetDirectoryName(path));
            var target = Path.Combine(root, folder, DateTime.Now.ToString("yyyyMMdd_HHmmss_") + Path.GetFileName(path));
            File.Move(path, target);
            var errors = path + ".errors.txt";
            if (File.Exists(errors)) File.Move(errors, target + ".errors.txt");
        }

        private static void OpenLog()
        {
            var folder = ConfigurationManager.AppSettings["Import:LogFolder"];
            if (string.IsNullOrEmpty(folder)) return;
            Directory.CreateDirectory(folder);
            _log = new StreamWriter(Path.Combine(folder, "import_" + DateTime.Now.ToString("yyyyMM") + ".log"), true, Encoding.UTF8) { AutoFlush = true };
        }

        private static void Log(string message)
        {
            var line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss ") + message;
            Console.WriteLine(line);
            if (_log != null) _log.WriteLine(line);
        }
    }
}
