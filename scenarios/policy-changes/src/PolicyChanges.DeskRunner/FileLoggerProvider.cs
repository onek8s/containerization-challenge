using System.Collections.Concurrent;

namespace PolicyChanges.DeskRunner;

/// <summary>Daily log files in the desk user's profile; the runner has no console window when started at logon.</summary>
internal sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _folder;
    private readonly object _lock = new();
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();

    public FileLoggerProvider(string folder)
    {
        _folder = Environment.ExpandEnvironmentVariables(folder);
        Directory.CreateDirectory(_folder);
    }

    public ILogger CreateLogger(string categoryName) => _loggers.GetOrAdd(categoryName, c => new FileLogger(this, c));

    internal void Write(string category, LogLevel level, string message, Exception? ex)
    {
        var now = DateTime.Now;
        var line = $"{now:yyyy-MM-dd HH:mm:ss.fff} [{level.ToString()[..3].ToUpperInvariant()}] {category}: {message}";
        if (ex is not null) line += Environment.NewLine + ex;
        lock (_lock)
        {
            File.AppendAllText(Path.Combine(_folder, $"desk-{now:yyyyMMdd}.log"), line + Environment.NewLine);
        }
    }

    public void Dispose()
    {
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

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel)) _provider.Write(_category, logLevel, formatter(state, exception), exception);
        }
    }
}
