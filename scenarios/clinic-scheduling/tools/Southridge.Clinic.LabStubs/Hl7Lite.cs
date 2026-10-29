using System.Net.Sockets;
using System.Text;

namespace Southridge.Clinic.LabStubs;

/// <summary>Just enough HL7 v2 for the partner stand-ins: pipe-delimited segments, MLLP framing and ACKs.</summary>
public sealed class Hl7Lite
{
    private static readonly Encoding Latin1 = Encoding.Latin1;

    private readonly List<string[]> _segments;

    private Hl7Lite(List<string[]> segments) => _segments = segments;

    public static Hl7Lite Parse(string text) =>
        new(text.Split('\r', '\n').Where(s => s.Length > 0).Select(s => s.Split('|')).ToList());

    public string Type => Get("MSH", 9, 1) + "^" + Get("MSH", 9, 2);

    public string ControlId => Get("MSH", 10);

    public IEnumerable<string[]> Segments(string name) => _segments.Where(s => s[0] == name);

    /// <summary>Field n of the first segment with that name (MSH counts the field separator as MSH-1).</summary>
    public string Get(string segment, int field, int component = 0)
    {
        var s = _segments.FirstOrDefault(x => x[0] == segment);
        return s == null ? "" : Field(s, segment == "MSH" ? field - 1 : field, component);
    }

    public static string Field(string[] segment, int index, int component = 0)
    {
        if (index >= segment.Length) return "";
        var value = segment[index].Split('~')[0];
        if (component == 0) return value;
        var parts = value.Split('^');
        return component <= parts.Length ? parts[component - 1] : "";
    }

    public static string Msh(string type, string sendingApp, string sendingFacility, string controlId) =>
        $"MSH|^~\\&|{sendingApp}|{sendingFacility}|SMGIE|SMG|{DateTime.Now:yyyyMMddHHmmss}||{type}|{controlId}|P|2.3";

    public static string Ack(Hl7Lite message, string code, string text, string app)
    {
        var sb = new StringBuilder();
        sb.Append(Msh("ACK^" + message.Get("MSH", 9, 2), app, message.Get("MSH", 6), "ACK" + DateTime.Now.ToString("HHmmssfff"))).Append('\r');
        sb.Append($"MSA|{code}|{message.ControlId}|{text}").Append('\r');
        return sb.ToString();
    }

    public static async Task WriteAsync(Stream stream, string message, CancellationToken ct)
    {
        var body = Latin1.GetBytes(message);
        var frame = new byte[body.Length + 3];
        frame[0] = 0x0B;
        body.CopyTo(frame, 1);
        frame[^2] = 0x1C;
        frame[^1] = 0x0D;
        await stream.WriteAsync(frame, ct);
        await stream.FlushAsync(ct);
    }

    /// <summary>Reads one framed message, or null when the peer closes the connection.</summary>
    public static async Task<string?> ReadAsync(Stream stream, CancellationToken ct)
    {
        var buffer = new MemoryStream();
        var one = new byte[1];
        var inMessage = false;
        while (true)
        {
            if (await stream.ReadAsync(one, ct) == 0) return null;
            var b = one[0];
            if (!inMessage)
            {
                inMessage = b == 0x0B;
                continue;
            }

            if (b == 0x1C)
            {
                await stream.ReadAsync(one, ct);
                return Latin1.GetString(buffer.ToArray());
            }

            buffer.WriteByte(b);
        }
    }

    /// <summary>Sends one message to the clinic interface engine and returns its ACK.</summary>
    public static async Task<string> SendAsync(string host, int port, string message, CancellationToken ct)
    {
        using var client = new TcpClient();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        await client.ConnectAsync(host, port, timeout.Token);
        var stream = client.GetStream();
        await WriteAsync(stream, message, timeout.Token);
        return await ReadAsync(stream, timeout.Token) ?? throw new IOException("Connection closed before the ACK.");
    }
}
