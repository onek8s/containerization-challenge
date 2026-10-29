using System.Diagnostics;

namespace Litware.DocControl.Platform;

/// <summary>
/// Writes important events to the Application log under the given source. The source is created by
/// the install scripts; when it does not exist, or the process is not on Windows, nothing is written.
/// </summary>
public sealed class EventLogWriter
{
    private readonly string _source;
    private readonly bool _enabled;

    public EventLogWriter(string source)
    {
        _source = source;
        try
        {
            _enabled = OperatingSystem.IsWindows() && EventLog.SourceExists(source);
        }
        catch (System.Security.SecurityException)
        {
            _enabled = false;
        }
    }

    public void Info(int eventId, string message) => Write(eventId, message, EventLogEntryType.Information);

    public void Warning(int eventId, string message) => Write(eventId, message, EventLogEntryType.Warning);

    public void Error(int eventId, string message) => Write(eventId, message, EventLogEntryType.Error);

    private void Write(int eventId, string message, EventLogEntryType type)
    {
        if (!_enabled || !OperatingSystem.IsWindows()) return;
        try
        {
            EventLog.WriteEntry(_source, message.Length > 31000 ? message[..31000] : message, type, eventId);
        }
        catch (InvalidOperationException)
        {
        }
    }
}
