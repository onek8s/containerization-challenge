using System;

namespace Lucerne.ProjectSites.Core
{
    /// <summary>Logging seam: ULS in the farm, log4net in the service and the add-in, console in the lab tools.</summary>
    public interface IActivityLog
    {
        void Info(string message);
        void Warn(string message);
        void Error(string message, Exception exception);
    }

    public sealed class NullActivityLog : IActivityLog
    {
        public static readonly NullActivityLog Instance = new NullActivityLog();

        public void Info(string message)
        {
        }

        public void Warn(string message)
        {
        }

        public void Error(string message, Exception exception)
        {
        }
    }
}
