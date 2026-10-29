using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Southridge.Clinic.LabStubs;

public sealed record TrafficEntry(DateTime At, string Partner, string Direction, string Type, string ControlId, string Summary, string Raw);

/// <summary>Recent HL7, SMS and clearinghouse traffic shown on the stub home page.</summary>
public sealed class TrafficLog
{
    private readonly ConcurrentQueue<TrafficEntry> _entries = new();

    public void Add(string partner, string direction, string type, string controlId, string summary, string raw = "")
    {
        _entries.Enqueue(new TrafficEntry(DateTime.Now, partner, direction, type, controlId, summary, raw));
        while (_entries.Count > 300 && _entries.TryDequeue(out _))
        {
        }
    }

    public IReadOnlyList<TrafficEntry> Recent() => _entries.Reverse().ToList();
}

public sealed class PartnerOptions
{
    public string EngineHost { get; set; } = "localhost";
    public int HospitalListenPort { get; set; } = 7661;
    public int HospitalAdtPort { get; set; } = 6661;
    public int LabListenPort { get; set; } = 7662;
    public int LabResultPort { get; set; } = 6662;
    public int LabResultDelaySeconds { get; set; } = 3;
    public int HospitalBillingListenPort { get; set; } = 7663;
}

/// <summary>
/// Stand-ins for St. Catherine hospital (SIU/ADT feed and the DFT billing feed) and Cascade Reference Lab
/// (receives ORM orders, returns ORU results to the clinic interface engine after a short delay).
/// </summary>
public sealed class Hl7Partners(PartnerOptions options, TrafficLog log, ILogger<Hl7Partners> logger) : BackgroundService
{
    private int _sequence;

    protected override Task ExecuteAsync(CancellationToken ct) => Task.WhenAll(
        ListenAsync("HOSP", options.HospitalListenPort, "STCADT", ct),
        ListenAsync("LAB", options.LabListenPort, "CRLAB", ct),
        ListenAsync("HOSPBILL", options.HospitalBillingListenPort, "STCPFS", ct));

    private async Task ListenAsync(string partner, int port, string app, CancellationToken ct)
    {
        var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
        logger.LogInformation("{Partner} stub listening on MLLP port {Port}", partner, port);
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(ct);
                _ = Task.Run(() => ServeAsync(partner, app, client, ct), ct);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            listener.Stop();
        }
    }

    private async Task ServeAsync(string partner, string app, TcpClient client, CancellationToken ct)
    {
        using (client)
        {
            var stream = client.GetStream();
            while (!ct.IsCancellationRequested)
            {
                string? text;
                try
                {
                    text = await Hl7Lite.ReadAsync(stream, ct);
                }
                catch (IOException)
                {
                    return;
                }

                if (text == null)
                {
                    return;
                }

                var message = Hl7Lite.Parse(text);
                log.Add(partner, "in", message.Type, message.ControlId, Describe(message), text);
                await Hl7Lite.WriteAsync(stream, Hl7Lite.Ack(message, "AA", "", app), ct);

                if (partner == "LAB" && message.Type.StartsWith("ORM^", StringComparison.Ordinal) && message.Get("ORC", 1) == "NW")
                {
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(TimeSpan.FromSeconds(options.LabResultDelaySeconds), ct);
                        await SendResultAsync(message, ct);
                    }, ct);
                }
            }
        }
    }

    private static string Describe(Hl7Lite m)
    {
        var patient = m.Get("PID", 5, 1) + ", " + m.Get("PID", 5, 2) + " MRN " + m.Get("PID", 3, 1);
        return m.Type switch
        {
            var t when t.StartsWith("ORM", StringComparison.Ordinal) => $"Order {m.Get("ORC", 2)} {m.Get("OBR", 4, 1)} for {patient}",
            var t when t.StartsWith("SIU", StringComparison.Ordinal) => $"Appointment {m.Get("SCH", 1, 1)} {m.Get("SCH", 11, 4)} for {patient}",
            var t when t.StartsWith("DFT", StringComparison.Ordinal) => $"{m.Segments("FT1").Count()} charges for {patient}",
            _ => patient
        };
    }

    /// <summary>ORU^R01 with plausible values for the clinic's test menu.</summary>
    private async Task SendResultAsync(Hl7Lite order, CancellationToken ct)
    {
        var placer = order.Get("ORC", 2);
        var test = order.Get("OBR", 4, 1);
        var filler = "CRL" + (900000 + Interlocked.Increment(ref _sequence)).ToString(CultureInfo.InvariantCulture);
        var now = DateTime.Now.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture);
        var pid = order.Segments("PID").First();
        var sb = new StringBuilder();
        sb.Append(Hl7Lite.Msh("ORU^R01", "CRLAB", "CASCADE", "CRL" + DateTime.Now.ToString("yyMMddHHmmssfff"))).Append('\r');
        sb.Append(string.Join('|', pid)).Append('\r');
        var obr = new string[26];
        Array.Fill(obr, "");
        obr[0] = "OBR";
        obr[1] = "1";
        obr[2] = placer;
        obr[3] = filler;
        obr[4] = $"{test}^{order.Get("OBR", 4, 2)}^L";
        obr[7] = now;
        obr[14] = now;
        obr[16] = order.Get("OBR", 16);
        obr[22] = now;
        obr[24] = "LAB";
        obr[25] = "F";
        sb.Append(string.Join('|', obr)).Append('\r');
        var i = 0;
        foreach (var (code, name, value, units, range, flag) in Observations(test))
        {
            i++;
            // OBX-11 result status, OBX-14 observation time
            sb.Append($"OBX|{i}|NM|{code}^{name}^LN||{value}|{units}|{range}|{flag}|||F|||{now}").Append('\r');
        }

        var text = sb.ToString();
        try
        {
            var ack = await Hl7Lite.SendAsync(options.EngineHost, options.LabResultPort, text, ct);
            var parsed = Hl7Lite.Parse(ack);
            log.Add("LAB", "out", "ORU^R01", placer, $"Results for {placer} ({i} observations): {parsed.Get("MSA", 1)} {parsed.Get("MSA", 3)}", text);
        }
        catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException)
        {
            log.Add("LAB", "out", "ORU^R01", placer, "Not delivered: " + ex.Message, text);
            logger.LogWarning("Result for {Order} not delivered: {Error}", placer, ex.Message);
        }
    }

    private static IEnumerable<(string, string, string, string, string, string)> Observations(string test) => test switch
    {
        "CBC" =>
        [
            ("6690-2", "WBC", "7.2", "10*3/uL", "4.0-11.0", "N"),
            ("789-8", "RBC", "4.61", "10*6/uL", "4.20-5.90", "N"),
            ("718-7", "Hemoglobin", "13.9", "g/dL", "13.0-17.0", "N"),
            ("4544-3", "Hematocrit", "41.2", "%", "39.0-51.0", "N"),
            ("777-3", "Platelets", "238", "10*3/uL", "150-400", "N")
        ],
        "CMP" =>
        [
            ("2345-7", "Glucose", "112", "mg/dL", "70-99", "H"),
            ("3094-0", "BUN", "16", "mg/dL", "7-20", "N"),
            ("2160-0", "Creatinine", "0.94", "mg/dL", "0.60-1.30", "N"),
            ("2951-2", "Sodium", "139", "mmol/L", "135-145", "N"),
            ("2823-3", "Potassium", "4.3", "mmol/L", "3.5-5.1", "N"),
            ("1742-6", "ALT", "27", "U/L", "7-56", "N")
        ],
        "LIPID" =>
        [
            ("2093-3", "Cholesterol", "214", "mg/dL", "<200", "H"),
            ("2571-8", "Triglycerides", "148", "mg/dL", "<150", "N"),
            ("2085-9", "HDL", "48", "mg/dL", ">40", "N"),
            ("13457-7", "LDL calculated", "136", "mg/dL", "<100", "H")
        ],
        "A1C" => [("4548-4", "Hemoglobin A1c", "6.4", "%", "4.0-5.6", "H")],
        "TSH" => [("3016-3", "TSH", "2.15", "uIU/mL", "0.40-4.00", "N")],
        "UA" =>
        [
            ("5811-5", "Specific gravity", "1.018", "", "1.005-1.030", "N"),
            ("2756-5", "pH", "6.0", "", "5.0-8.0", "N")
        ],
        _ => [("8251-1", "Comment", "1", "", "", "N")]
    };

    /// <summary>Hospital registration (ADT) sent to the clinic, as the hospital's ADT feed would.</summary>
    public async Task<string> SendAdtAsync(string trigger, string mrn, string hospitalNumber, string last, string first, string dob, string sex, CancellationToken ct)
    {
        var control = "STC" + DateTime.Now.ToString("yyMMddHHmmssfff");
        var pid3 = new List<string>();
        if (!string.IsNullOrWhiteSpace(mrn)) pid3.Add(mrn.Trim() + "^^^SMG^MR");
        if (!string.IsNullOrWhiteSpace(hospitalNumber)) pid3.Add(hospitalNumber.Trim() + "^^^STC^PI");
        var now = DateTime.Now.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture);
        var text = Hl7Lite.Msh("ADT^" + trigger, "STCADT", "STCATH", control) + "\r" +
                   $"EVN|{trigger}|{now}\r" +
                   $"PID|1||{string.Join('~', pid3)}||{last}^{first}||{dob}|{sex}|||100 Harbor Way^^Tacoma^WA^98402||(253)555-0144\r" +
                   $"PV1|1|E|ED^^^STCATH|||||||||||||||{hospitalNumber}V1||||||||||||||||||||||||{now}\r";
        try
        {
            var ack = Hl7Lite.Parse(await Hl7Lite.SendAsync(options.EngineHost, options.HospitalAdtPort, text, ct));
            var result = ack.Get("MSA", 1) + " " + ack.Get("MSA", 3);
            log.Add("HOSP", "out", "ADT^" + trigger, control, $"{last}, {first}: {result}", text);
            return result;
        }
        catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException)
        {
            log.Add("HOSP", "out", "ADT^" + trigger, control, "Not delivered: " + ex.Message, text);
            return "Not delivered: " + ex.Message;
        }
    }
}
