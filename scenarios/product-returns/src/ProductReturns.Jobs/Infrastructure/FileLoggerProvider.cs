using System;
using System.IO;
using Microsoft.Extensions.Logging;

namespace ProductReturns.Jobs.Infrastructure
{
    /// <summary>Appends job output to a daily log file per job so operations can check runs.</summary>
    public sealed class FileLoggerProvider : ILoggerProvider
    {
        private readonly object _lock = new object();
        private readonly string _file;

        public FileLoggerProvider(string folder, string job)
        {
            if (string.IsNullOrEmpty(folder))
            {
                return;
            }

            Directory.CreateDirectory(folder);
            _file = Path.Combine(folder, job + "-" + DateTime.Now.ToString("yyyyMMdd") + ".log");
        }

        public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

        public void Dispose()
        {
        }

        private void Write(string line)
        {
            if (_file == null)
            {
                return;
            }

            lock (_lock)
            {
                File.AppendAllText(_file, line + Environment.NewLine);
            }
        }

        private sealed class FileLogger : ILogger
        {
            private readonly FileLoggerProvider _provider;
            private readonly string _category;

            public FileLogger(FileLoggerProvider provider, string category)
            {
                _provider = provider;
                _category = category;
            }

            public IDisposable BeginScope<TState>(TState state) => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information && !_category.StartsWith("Microsoft.", StringComparison.Ordinal);

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                if (!IsEnabled(logLevel))
                {
                    return;
                }

                var line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + logLevel.ToString().ToUpperInvariant().PadRight(12) + formatter(state, exception);
                if (exception != null)
                {
                    line += Environment.NewLine + exception;
                }

                _provider.Write(line);
            }
        }
    }
}
