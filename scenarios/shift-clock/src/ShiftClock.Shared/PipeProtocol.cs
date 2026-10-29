using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShiftClock.Shared;

public static class PipeProtocol
{
    public const string PipeName = "Northwind.ShiftClock.Supervisor";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task WriteAsync<T>(Stream stream, T message, CancellationToken ct)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(message, Json);
        var length = BitConverter.GetBytes(bytes.Length);
        await stream.WriteAsync(length, ct);
        await stream.WriteAsync(bytes, ct);
        await stream.FlushAsync(ct);
    }

    public static async Task<T?> ReadAsync<T>(Stream stream, CancellationToken ct)
    {
        var header = new byte[4];
        if (!await ReadExactAsync(stream, header, ct))
            return default;
        var length = BitConverter.ToInt32(header);
        if (length is <= 0 or > 16 * 1024 * 1024)
            throw new InvalidDataException($"Invalid message length {length}.");
        var body = new byte[length];
        if (!await ReadExactAsync(stream, body, ct))
            return default;
        return JsonSerializer.Deserialize<T>(body, Json);
    }

    private static async Task<bool> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var n = await stream.ReadAsync(buffer.AsMemory(read), ct);
            if (n == 0)
                return false;
            read += n;
        }
        return true;
    }
}

public enum SupervisorCommand
{
    GetStatus,
    ListPunches,
    AddCorrection,
    DeletePunch,
    ExportPayroll
}

public sealed record SupervisorRequest
{
    public SupervisorCommand Command { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public Punch? Correction { get; init; }
    public Guid? PunchId { get; init; }
}

public sealed record SupervisorResponse
{
    public bool Ok { get; init; }
    public string? Error { get; init; }
    public string? CallerName { get; init; }
    public bool CallerIsSupervisor { get; init; }
    public ReaderStatus? Status { get; init; }
    public List<Punch>? Punches { get; init; }
    public string? ExportPath { get; init; }

    public static SupervisorResponse Fail(string error) => new() { Ok = false, Error = error };
}

public sealed record ReaderStatus(
    string Terminal,
    string PortName,
    bool PortOpen,
    DateTime? LastBadgeAt,
    string? LastBadgeName,
    int PunchesToday,
    int RejectedFrames,
    DateTime ServiceStartedAt);
