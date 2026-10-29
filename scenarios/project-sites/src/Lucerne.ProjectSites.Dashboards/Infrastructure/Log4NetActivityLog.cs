using System;
using log4net;
using Lucerne.ProjectSites.Core;

namespace Lucerne.ProjectSites.Dashboards.Infrastructure
{
    public sealed class Log4NetActivityLog : IActivityLog
    {
        private readonly ILog _log;

        public Log4NetActivityLog(string name)
        {
            _log = LogManager.GetLogger(name);
        }

        public void Info(string message)
        {
            _log.Info(message);
        }

        public void Warn(string message)
        {
            _log.Warn(message);
        }

        public void Error(string message, Exception exception)
        {
            _log.Error(message, exception);
        }
    }
}
