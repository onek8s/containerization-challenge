using System.Globalization;
using Proseware.ControlTower.Data.Seed;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Integration.Edifact;
using Proseware.ControlTower.Integration.Sftp;

namespace Proseware.ControlTower.LabTools.Commands;

/// <summary>
/// Plays an EDIFACT carrier: picks up our IFTMIN booking instructions from the partner folder (or SFTP
/// account), acknowledges them with CONTRL and reports the transport with a sequence of IFTSTA messages.
/// </summary>
public sealed class EdiPartnerCommand : ILabCommand
{
    // status code (code list 9011 as used by our partners), share of the planned transit time
    private static readonly (string Code, double At, string Text)[] RoadSequence =
    {
        ("10", 0.00, "Goods collected"),
        ("16", 0.25, "Arrived at terminal"),
        ("17", 0.35, "Departed terminal"),
        ("20", 0.85, "Out for delivery"),
        ("21", 1.00, "Delivered"),
    };

    private static readonly (string Code, double At, string Text)[] SeaSequence =
    {
        ("AE", 0.05, "Loaded on vessel"),
        ("VD", 0.10, "Vessel departure"),
        ("VA", 0.90, "Vessel arrival"),
        ("UV", 0.95, "Unloaded from vessel"),
        ("CR", 0.97, "Customs released"),
        ("OA", 1.00, "Gate out"),
    };

    public string Name => "edi-partner";

    public string Usage =>
        "--carrier NRDL|VNRL|OCNX|NSFL (--folder <partner folder> | --host <sftp host> --user <user> --password <pw> [--port 22]) " +
        "[--in /in] [--out /out] [--step-seconds 20] [--once]";

    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        var a = new LabArgs(args);
        var code = a.Required("carrier").ToUpperInvariant();
        var carrier = ReferenceData.Carriers().FirstOrDefault(c => c.Code == code)
            ?? throw new LabArgumentException($"Unknown carrier {code}.");
        var step = TimeSpan.FromSeconds(a.GetInt("step-seconds", 20));
        var sea = carrier.Mode == TransportMode.Sea;

        // The partner sees the account from the other side: it reads what we upload (/in) and writes to /out.
        var options = new SftpOptions
        {
            LocalFolder = a.Get("folder"),
            Host = a.Get("host") ?? "",
            Port = a.GetInt("port", 22),
            UserName = a.Get("user") ?? "",
            Password = a.Get("password"),
            InboundDirectory = a.Get("in", "/in"),
            ArchiveDirectory = a.Get("in", "/in").TrimEnd('/') + "/processed",
            OutboundDirectory = a.Get("out", "/out"),
            FilePattern = "*.edi"
        };
        if (options.LocalFolder is null && options.Host.Length == 0) throw new LabArgumentException("Give --folder or --host.");

        var pending = new List<(DateTimeOffset Due, string Number, string Reference, string Sscc, string Code, string Text, string Location, string LocationName)>();
        var counter = 0;
        Console.WriteLine($"Playing carrier {carrier.Code} ({carrier.EdiPartnerId}); status step {step.TotalSeconds:0} s. Ctrl+C stops.");

        while (!cancellationToken.IsCancellationRequested)
        {
            using (var channel = new PartnerFileChannelFactory().Open(options))
            {
                foreach (var file in channel.ListInbound())
                {
                    var text = System.Text.Encoding.Latin1.GetString(channel.Download(file));
                    channel.Archive(file);
                    EdifactInterchange interchange;
                    try
                    {
                        interchange = EdifactReader.Parse(text);
                    }
                    catch (EdifactException ex)
                    {
                        Console.WriteLine($"{file.Name}: unreadable ({ex.Message})");
                        continue;
                    }

                    foreach (var message in interchange.Messages.Where(m => m.Type == IftminBuilder.MessageType))
                    {
                        var number = message.All("RFF").FirstOrDefault(r => r.Get(0, 0) == "CU")?.Get(0, 1) ?? "";
                        var reference = message.All("RFF").FirstOrDefault(r => r.Get(0, 0) == "BN")?.Get(0, 1)
                                        ?? $"{carrier.Code}{DateTime.UtcNow:yyMMdd}{++counter:D4}";
                        var from = message.All("LOC").FirstOrDefault(l => l.Get(0) == "5");
                        var to = message.All("LOC").FirstOrDefault(l => l.Get(0) == "8");
                        var sscc = message.All("GIN").FirstOrDefault()?.Get(1, 0) ?? "";
                        Console.WriteLine($"IFTMIN {interchange.ControlReference}: {number} {from?.Get(1, 0)} -> {to?.Get(1, 0)}, our ref {reference}");

                        var sequence = sea ? SeaSequence : RoadSequence;
                        var start = DateTimeOffset.UtcNow;
                        var total = step * sequence.Length;
                        foreach (var s in sequence)
                        {
                            var at = s.At >= 0.85 ? to : from;
                            if (s.Code is "16" or "17") at = null;
                            pending.Add((start + total * s.At + step, number, reference, sscc, s.Code, s.Text, at?.Get(1, 0) ?? "", at?.Get(1, 3) ?? ""));
                        }
                    }

                    if (interchange.Messages.Any(m => m.Type != "CONTRL"))
                        Write(channel, carrier.EdiPartnerId!, carrier.EdiQualifier, interchange.SenderId, interchange.SenderQualifier, "CONTRL",
                            ContrlBuilder.Acknowledge(interchange, "1"));
                }

                var due = pending.Where(p => p.Due <= DateTimeOffset.UtcNow).ToList();
                foreach (var group in due.GroupBy(d => d.Number))
                {
                    var m = new EdifactMessage { ReferenceNumber = "1", Type = IftstaParser.MessageType, Release = "96A" };
                    m.Segments.Add(EdifactSegment.Create("BGM", "77", $"ST{DateTime.UtcNow:HHmmss}{++counter}", "9"));
                    m.Segments.Add(EdifactSegment.Composite("DTM", "137", DateTime.UtcNow.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture), "203"));
                    var cni = 1;
                    foreach (var s in group)
                    {
                        m.Segments.Add(EdifactSegment.Create("CNI", cni++, new[] { s.Number }));
                        m.Segments.Add(EdifactSegment.Create("STS", "1", new[] { s.Code }));
                        m.Segments.Add(EdifactSegment.Composite("RFF", "BN", s.Reference));
                        m.Segments.Add(EdifactSegment.Composite("DTM", "334", DateTime.UtcNow.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture), "203"));
                        if (s.Location.Length > 0) m.Segments.Add(EdifactSegment.Create("LOC", "175", new[] { s.Location, "", "", s.LocationName }));
                        m.Segments.Add(EdifactSegment.Create("FTX", "AHN", null, null, new[] { s.Text }));
                        if (s.Sscc.Length > 0) m.Segments.Add(EdifactSegment.Create("GIN", "BJ", s.Sscc));
                        Console.WriteLine($"IFTSTA {s.Number}: {s.Code} {s.Text}");
                    }
                    Write(channel, carrier.EdiPartnerId!, carrier.EdiQualifier, "6430012345000", "14", "IFTSTA", m);
                }
                pending.RemoveAll(p => due.Contains(p));
            }

            if (a.Has("once") && pending.Count == 0) break;
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        }
        return 0;
    }

    private static int _interchangeCounter;

    private static void Write(IPartnerFileChannel channel, string senderId, string? senderQualifier, string recipientId, string? recipientQualifier, string type, EdifactMessage message)
    {
        var now = DateTime.UtcNow;
        var reference = $"{now:yyMMddHHmm}{Interlocked.Increment(ref _interchangeCounter) % 10000:D4}";
        var interchange = new EdifactInterchange
        {
            SenderId = senderId,
            SenderQualifier = senderQualifier,
            RecipientId = recipientId,
            RecipientQualifier = recipientQualifier,
            Prepared = now,
            ControlReference = reference
        };
        interchange.Messages.Add(message);
        channel.Upload(ControlReferences.FileName(senderId, type, reference), System.Text.Encoding.Latin1.GetBytes(EdifactWriter.Write(interchange)));
    }
}
