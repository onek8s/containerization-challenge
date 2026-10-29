using System;
using System.Configuration;
using System.IO;

namespace Contoso.Purchasing.Jobs
{
    internal static class JobLog
    {
        private static readonly object Sync = new object();
        private static string _file;

        public static void Init(string job)
        {
            var folder = ConfigurationManager.AppSettings["LogFolder"];
            Directory.CreateDirectory(folder);
            _file = Path.Combine(folder, "jobs-" + DateTime.Now.ToString("yyyyMMdd") + ".log");
            Info("---- " + job + " started on " + Environment.MachineName + " as " + Environment.UserDomainName + "\\" + Environment.UserName);
        }

        public static void Info(string message)
        {
            Write("INFO ", message);
        }

        public static void Error(string message)
        {
            Write("ERROR", message);
        }

        private static void Write(string level, string message)
        {
            var line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + level + " " + message;
            Console.WriteLine(line);
            if (_file == null) return;
            lock (Sync)
            {
                File.AppendAllText(_file, line + Environment.NewLine);
            }
        }
    }
}
