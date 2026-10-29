using System.Diagnostics;

namespace ShiftClock.Service;

/// <summary>Audit entries for the plant security team, written to the Application log.</summary>
public sealed class EventLogWriter
{
    public const string Source = "Shift Clock";

    public const int ServiceStarted = 1000;
    public const int ReaderConnected = 1001;
    public const int ReaderDisconnected = 1002;
    public const int PunchRecorded = 2000;
    public const int UnknownBadge = 2001;
    public const int PunchCorrected = 3000;
    public const int PunchDeleted = 3001;
    public const int PayrollExported = 3100;
    public const int AccessDenied = 4000;

    private readonly ILogger<EventLogWriter> _logger;

    public EventLogWriter(ILogger<EventLogWriter> logger) => _logger = logger;

    public void Write(int eventId, string message, EventLogEntryType type = EventLogEntryType.Information)
    {
        try
        {
            EventLog.WriteEntry(Source, message, type, eventId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not write event {EventId} to the event log: {Message}", eventId, message);
        }
    }
}
