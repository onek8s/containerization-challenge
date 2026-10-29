using System.Diagnostics;

namespace LabelStation;

/// <summary>
/// Records printed labels to the Application event log and to a local history file.
/// </summary>
public sealed class PrintJournal
{
    private const string EventSource = "Label Station";

    private readonly string _historyFile;

    public PrintJournal()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Northwind", "LabelStation");
        Directory.CreateDirectory(folder);
        _historyFile = Path.Combine(folder, "print-history.csv");
    }

    public string HistoryFile => _historyFile;

    public void Record(ShippingLabel label, string printerName, string stationId)
    {
        var line = string.Join(';',
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture),
            stationId,
            printerName,
            label.OrderNumber,
            label.Carrier,
            label.TrackingCode,
            label.WeightKg.ToString(System.Globalization.CultureInfo.InvariantCulture));
        File.AppendAllLines(_historyFile, new[] { line });

        WriteEvent($"Printed {label.TrackingCode} ({label.Carrier}) on {printerName}.", EventLogEntryType.Information, 1000);
    }

    public void RecordFailure(ShippingLabel label, Exception error)
    {
        WriteEvent($"Printing {label.TrackingCode} failed: {error.Message}", EventLogEntryType.Error, 1001);
    }

    public IReadOnlyList<string> ReadRecent(int count)
    {
        if (!File.Exists(_historyFile))
        {
            return Array.Empty<string>();
        }

        return File.ReadLines(_historyFile).Reverse().Take(count).ToList();
    }

    private static void WriteEvent(string message, EventLogEntryType type, int eventId)
    {
        try
        {
            // Creating a new source requires administrative rights; the installer registers it.
            if (!EventLog.SourceExists(EventSource))
            {
                EventLog.CreateEventSource(EventSource, "Application");
            }

            EventLog.WriteEntry(EventSource, message, type, eventId);
        }
        catch (System.Security.SecurityException)
        {
            EventLog.WriteEntry("Application", message, type, eventId);
        }
    }
}
