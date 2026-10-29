using System;
using Lucerne.ProjectSites.Core;
using Microsoft.SharePoint.Administration;

namespace Lucerne.ProjectSites.ServerObjectModel
{
    /// <summary>Writes to the ULS logs (area "Lucerne Project Sites").</summary>
    public class UlsActivityLog : IActivityLog
    {
        private static readonly SPDiagnosticsCategory Category =
            new SPDiagnosticsCategory("Lucerne Project Sites", TraceSeverity.Medium, EventSeverity.Error);

        private readonly string _component;

        public UlsActivityLog(string component)
        {
            _component = component;
        }

        public void Info(string message)
        {
            Write(TraceSeverity.Medium, message);
        }

        public void Warn(string message)
        {
            Write(TraceSeverity.Monitorable, message);
        }

        public void Error(string message, Exception exception)
        {
            Write(TraceSeverity.Unexpected, message + (exception == null ? string.Empty : ": " + exception));
        }

        private void Write(TraceSeverity severity, string message)
        {
            SPDiagnosticsService.Local.WriteTrace(0, Category, severity, "[{0}] {1}", _component, message);
        }
    }
}
