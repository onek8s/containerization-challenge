using System;
using System.Configuration;
using System.IO;
using System.Web.Hosting;

namespace Tailspin.CustomerCare.Web.Services
{
    /// <summary>
    /// Local working folders of the portal: generated report files and uploads that belong to a new case
    /// that is not saved yet.
    /// </summary>
    public static class WorkFolders
    {
        public static string Reports
        {
            get { return Ensure(Resolve("CustomerCare:ReportFolder", "~/App_Data/Reports")); }
        }

        public static string Uploads
        {
            get { return Ensure(Resolve("CustomerCare:UploadFolder", Path.Combine(Path.GetTempPath(), "CustomerCare", "Uploads"))); }
        }

        public static string UploadsFor(string sessionId)
        {
            return Ensure(Path.Combine(Uploads, sessionId));
        }

        /// <summary>Removes files older than the given age; used by the hourly clean-up job.</summary>
        public static int DeleteOlderThan(string folder, TimeSpan age)
        {
            var count = 0;
            var limit = DateTime.UtcNow - age;
            foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(file) < limit)
                    {
                        File.Delete(file);
                        count++;
                    }
                }
                catch (IOException)
                {
                    // In use (download in progress); next run.
                }
            }

            foreach (var dir in Directory.EnumerateDirectories(folder))
            {
                if (Directory.GetFileSystemEntries(dir).Length == 0 && Directory.GetLastWriteTimeUtc(dir) < limit)
                {
                    Directory.Delete(dir);
                }
            }

            return count;
        }

        private static string Resolve(string key, string defaultValue)
        {
            var v = ConfigurationManager.AppSettings[key];
            if (string.IsNullOrWhiteSpace(v)) v = defaultValue;
            return v.StartsWith("~/", StringComparison.Ordinal) ? HostingEnvironment.MapPath(v) : Environment.ExpandEnvironmentVariables(v);
        }

        private static string Ensure(string path)
        {
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
