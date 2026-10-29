using System.Globalization;
using System.Text;

namespace ShiftClock.Shared;

/// <summary>
/// Parses frames sent by the gate badge readers (125 kHz, ASCII output mode):
/// STX, 10 hex characters of card data, 2 hex characters of XOR checksum, CR, LF, ETX.
/// </summary>
public sealed class BadgeFrameParser
{
    public const byte Stx = 0x02;
    public const byte Etx = 0x03;
    private const int PayloadLength = 14; // 10 data + 2 checksum + CR + LF

    private readonly List<byte> _buffer = new();
    private bool _inFrame;

    public int RejectedFrames { get; private set; }

    /// <summary>Feeds raw bytes from the port and returns any complete badge IDs.</summary>
    public IReadOnlyList<string> Feed(ReadOnlySpan<byte> data)
    {
        var badges = new List<string>();
        foreach (var b in data)
        {
            if (b == Stx)
            {
                _buffer.Clear();
                _inFrame = true;
                continue;
            }

            if (!_inFrame)
                continue;

            if (b == Etx)
            {
                _inFrame = false;
                if (TryDecode(_buffer, out var badge))
                    badges.Add(badge);
                else
                    RejectedFrames++;
                _buffer.Clear();
                continue;
            }

            _buffer.Add(b);
            if (_buffer.Count > PayloadLength)
            {
                _inFrame = false;
                _buffer.Clear();
                RejectedFrames++;
            }
        }
        return badges;
    }

    public static bool TryDecode(IReadOnlyList<byte> payload, out string badgeId)
    {
        badgeId = string.Empty;
        if (payload.Count != PayloadLength || payload[12] != '\r' || payload[13] != '\n')
            return false;

        var text = Encoding.ASCII.GetString(payload.Take(12).ToArray());
        var bytes = new byte[6];
        for (var i = 0; i < 6; i++)
        {
            if (!byte.TryParse(text.AsSpan(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out bytes[i]))
                return false;
        }

        byte xor = 0;
        for (var i = 0; i < 5; i++)
            xor ^= bytes[i];
        if (xor != bytes[5])
            return false;

        badgeId = text[..10].ToUpperInvariant();
        return true;
    }

    public static byte[] BuildFrame(string badgeId)
    {
        if (badgeId.Length != 10)
            throw new ArgumentException("Badge ID must be 10 hex characters.", nameof(badgeId));

        byte xor = 0;
        for (var i = 0; i < 5; i++)
            xor ^= byte.Parse(badgeId.AsSpan(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        var body = Encoding.ASCII.GetBytes(badgeId.ToUpperInvariant() + xor.ToString("X2") + "\r\n");
        return [Stx, .. body, Etx];
    }
}
