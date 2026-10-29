using System;
using System.IO;

namespace Southridge.Clinic.Infrastructure
{
    /// <summary>
    /// Single-run guard for the scheduled tasks: an exclusive lock file in the work folder. A second start while
    /// the first run is still going exits immediately.
    /// </summary>
    public sealed class JobLock : IDisposable
    {
        private FileStream _stream;

        private JobLock(FileStream stream)
        {
            _stream = stream;
        }

        public static JobLock TryAcquire(string folder, string jobName)
        {
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, jobName + ".lock");
            try
            {
                var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
                var writer = new StreamWriter(stream);
                writer.Write(Environment.MachineName + " " + DateTime.Now.ToString("s"));
                writer.Flush();
                return new JobLock(stream);
            }
            catch (IOException)
            {
                return null;
            }
        }

        public void Dispose()
        {
            if (_stream != null)
            {
                _stream.Dispose();
                _stream = null;
            }
        }
    }
}
