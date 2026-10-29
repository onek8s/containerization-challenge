using System.Globalization;
using System.Text;

namespace Southridge.Clinic.LabStubs;

public sealed class ClearinghouseOptions
{
    /// <summary>Folder the billing batch uploads 837 files to (Billing:ExchangeOutbound).</summary>
    public string Outbound { get; set; } = "";

    /// <summary>Folder the billing batch picks 999 and 835 files up from (Billing:ExchangeInbound).</summary>
    public string Inbound { get; set; } = "";
    public int PollSeconds { get; set; } = 5;
    public int AllowedPercent { get; set; } = 80;
    public decimal Copay { get; set; } = 15m;
    public int DenyEvery { get; set; } = 7;
}

/// <summary>
/// Picks up 837P files from the clinic's drop folder, answers each with a 999 and one 835 per payer
/// (allowed amount as a percentage of charges, a copay as patient responsibility, every n-th claim denied).
/// </summary>
public sealed class Clearinghouse(ClearinghouseOptions options, TrafficLog log, ILogger<Clearinghouse> logger) : BackgroundService
{
    private int _interchange = 700000;
    private int _claimCounter;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var root = Path.Combine(Path.GetTempPath(), "southridge-clearinghouse");
        if (options.Outbound.Length == 0) options.Outbound = Path.Combine(root, "to-clearinghouse");
        if (options.Inbound.Length == 0) options.Inbound = Path.Combine(root, "from-clearinghouse");
        Directory.CreateDirectory(options.Outbound);
        Directory.CreateDirectory(options.Inbound);
        logger.LogInformation("Clearinghouse watching {Folder}", Path.GetFullPath(options.Outbound));
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, options.PollSeconds)));
        do
        {
            foreach (var file in Directory.GetFiles(options.Outbound).Where(f => !f.EndsWith(".part", StringComparison.OrdinalIgnoreCase)).Order())
            {
                try
                {
                    Process(file);
                }
                catch (Exception ex) when (ex is FormatException or IOException)
                {
                    logger.LogWarning("{File}: {Error}", Path.GetFileName(file), ex.Message);
                    log.Add("CLEARINGHOUSE", "in", "837", Path.GetFileName(file), "Rejected: " + ex.Message);
                    File.Move(file, file + ".bad", true);
                }
            }
        }
        while (await timer.WaitForNextTickAsync(ct));
    }

    private void Process(string file)
    {
        var name = Path.GetFileName(file);
        var content = File.ReadAllText(file);
        if (content.Length < 106 || !content.StartsWith("ISA", StringComparison.Ordinal))
        {
            throw new FormatException("not an X12 interchange");
        }

        char element = content[3], terminator = content[105];
        var segments = content.Split(terminator).Select(s => s.Trim('\r', '\n', ' ')).Where(s => s.Length > 0).Select(s => s.Split(element)).ToList();
        var gs = segments.First(s => s[0] == "GS");
        var groupControl = gs[6];

        var claims = new List<(string Payer, string PayerId, string Claim, decimal Charge)>();
        string payer = "", payerId = "";
        foreach (var s in segments)
        {
            if (s[0] == "NM1" && s.Length > 9 && s[1] == "PR")
            {
                payer = s[3];
                payerId = s[9];
            }
            else if (s[0] == "CLM")
            {
                claims.Add((payer, payerId, s[1], decimal.Parse(s[2], CultureInfo.InvariantCulture)));
            }
        }

        if (claims.Count == 0)
        {
            throw new FormatException("no CLM segments");
        }

        var stamp = DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        Write($"ACK_{groupControl}_{stamp}.999", Ack999(groupControl, claims.Count));
        log.Add("CLEARINGHOUSE", "in", "837", groupControl, $"{name}: {claims.Count} claims, {claims.Sum(c => c.Charge):0.00} charged");

        foreach (var group in claims.GroupBy(c => (c.Payer, c.PayerId)))
        {
            var remit = Remittance835(group.Key.Payer, group.Key.PayerId, group.Select(c => (c.Claim, c.Charge)).ToList(), out var paid);
            Write($"ERA_{group.Key.PayerId}_{groupControl}_{stamp}.835", remit);
            log.Add("CLEARINGHOUSE", "out", "835", group.Key.PayerId, $"{group.Key.Payer}: {group.Count()} claims, {paid:0.00} paid");
        }

        var done = Path.Combine(options.Outbound, "accepted");
        Directory.CreateDirectory(done);
        File.Move(file, Path.Combine(done, name), true);
    }

    private void Write(string name, string content)
    {
        var target = Path.Combine(options.Inbound, name);
        File.WriteAllText(target + ".part", content, Encoding.ASCII);
        File.Move(target + ".part", target, true);
    }

    private string Envelope(string functionalId, string version, StringBuilder body, int bodySegments)
    {
        var now = DateTime.Now;
        var control = Interlocked.Increment(ref _interchange).ToString("D9", CultureInfo.InvariantCulture);
        var sb = new StringBuilder();
        sb.Append("ISA*00*          *00*          *ZZ*CLEARHOUSE     *ZZ*SMGCLINIC      *")
          .Append(now.ToString("yyMMdd*HHmm", CultureInfo.InvariantCulture))
          .Append("*^*00501*").Append(control).Append("*0*T*:~\n");
        var group = control.TrimStart('0');
        sb.Append($"GS*{functionalId}*CLEARHOUSE*SMGCLINIC*{now:yyyyMMdd}*{now:HHmm}*{group}*X*{version}~\n");
        sb.Append(body);
        sb.Append($"SE*{bodySegments + 1}*0001~\n");
        sb.Append($"GE*1*{group}~\n");
        sb.Append($"IEA*1*{control}~\n");
        return sb.ToString();
    }

    private string Ack999(string groupControl, int claims)
    {
        var body = new StringBuilder();
        body.Append("ST*999*0001*005010X231A1~\n");
        body.Append($"AK1*HC*{groupControl}*005010X222A1~\n");
        body.Append("AK2*837*0001*005010X222A1~\n");
        body.Append("IK5*A~\n");
        body.Append("AK9*A*1*1*1~\n");
        return Envelope("FA", "005010X231A1", body, 5);
    }

    private string Remittance835(string payer, string payerId, List<(string Claim, decimal Charge)> claims, out decimal totalPaid)
    {
        var now = DateTime.Now;
        var trace = "EFT" + now.ToString("yyMMddHHmmss", CultureInfo.InvariantCulture) + payerId;
        var lines = new List<string>();
        totalPaid = 0;
        foreach (var (claim, charge) in claims)
        {
            var denied = options.DenyEvery > 0 && Interlocked.Increment(ref _claimCounter) % options.DenyEvery == 0;
            var payerClaim = "PCN" + claim;
            if (denied)
            {
                lines.Add($"CLP*{claim}*4*{Amount(charge)}*0*0*12*{payerClaim}~");
                lines.Add($"CAS*CO*16*{Amount(charge)}~");
                continue;
            }

            var allowed = Math.Round(charge * options.AllowedPercent / 100m, 2);
            var copay = Math.Min(options.Copay, allowed);
            var paid = allowed - copay;
            totalPaid += paid;
            lines.Add($"CLP*{claim}*1*{Amount(charge)}*{Amount(paid)}*{Amount(copay)}*12*{payerClaim}~");
            if (charge > allowed) lines.Add($"CAS*CO*45*{Amount(charge - allowed)}~");
            if (copay > 0) lines.Add($"CAS*PR*3*{Amount(copay)}~");
        }

        var body = new StringBuilder();
        var head = new List<string>
        {
            "ST*835*0001*005010X221A1~",
            $"BPR*I*{Amount(totalPaid)}*C*ACH*CCP*01*125000024*DA*0099887766*1{payerId}**01*125000105*DA*4455667788*{now:yyyyMMdd}~",
            $"TRN*1*{trace}*1{payerId}~",
            $"DTM*405*{now:yyyyMMdd}~",
            $"N1*PR*{payer}~",
            $"REF*2U*{payerId}~",
            "N1*PE*SOUTHRIDGE MEDICAL GROUP*XX*1588667790~",
            "LX*1~"
        };
        foreach (var l in head.Concat(lines))
        {
            body.Append(l).Append('\n');
        }

        return Envelope("HP", "005010X221A1", body, head.Count + lines.Count);
    }

    private static string Amount(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
