using System.Collections.Concurrent;
using System.Text;
using VanArsdel.Integration.Domain.Orders;
using VanArsdel.Integration.Domain.Partners;
using VanArsdel.Integration.Edi;
using VanArsdel.Integration.LabStubs.Rules;
using VanArsdel.Integration.LabStubs.Seed;
using VanArsdel.Integration.Mapping;
using VanArsdel.Integration.Mapping.Functoids;
using VanArsdel.Integration.Messaging;
using VanArsdel.Integration.Monitoring;

namespace VanArsdel.Integration.LabStubs.Lab
{
    /// <summary>
    /// Everything the lab platform shares: the partner directory and maps the BizTalk hosts would load, and
    /// in-memory stand-ins for the integration database (duplicates, control numbers, interchanges), the BAM
    /// OrderLifecycle activity and the SMTP pickup folder. Files go to the work folder, never to the source tree.
    /// </summary>
    public class LabContext
    {
        private readonly ConcurrentDictionary<string, MapRunner> _maps = new ConcurrentDictionary<string, MapRunner>(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, BrlPolicy> _policies = new ConcurrentDictionary<string, BrlPolicy>(StringComparer.Ordinal);
        private readonly object _gate = new object();
        private long _nextInterchangeKey = 1;

        public LabContext(string workFolder, SeedData seed, DateTime businessNowLocal)
        {
            Work = workFolder;
            Seed = seed;
            Directory = seed.Directory();
            Now = businessNowLocal;
            Schemas = SchemaValidator.FromFolder(ScenarioPaths.Schemas);
            DateFunctoids.Clock = () => Now;
            XrefProviderLocator.Current = new DirectoryXrefProvider(Directory, () => Now.Date);
            foreach (var folder in new[] { "archive", "mail", "suspended", "erp/inbound", "erp/outbound", "sftp", "out" })
            {
                System.IO.Directory.CreateDirectory(Path.Combine(Work, folder));
            }
        }

        public string Work { get; }
        public SeedData Seed { get; }
        public PartnerDirectory Directory { get; }
        public SchemaValidator Schemas { get; }

        /// <summary>Simulated local business time (maps' Today(), rules' cut-off, ack timeouts).</summary>
        public DateTime Now { get; set; }

        /// <summary>The lab runs on UK time in winter (GMT), so business time and UTC are the same and runs repeat exactly.</summary>
        public DateTime UtcNow => DateTime.SpecifyKind(Now, DateTimeKind.Utc);

        public IDuplicateStore Duplicates { get; } = new InMemoryDuplicateStore();
        public IControlNumberSequence ControlNumbers { get; } = new InMemoryControlNumberSequence(1001);
        public List<InterchangeRecord> Interchanges { get; } = new List<InterchangeRecord>();
        public ConcurrentDictionary<string, OrderActivityRow> Bam { get; } = new ConcurrentDictionary<string, OrderActivityRow>(StringComparer.Ordinal);
        public List<string> Suspended { get; } = new List<string>();
        public List<string> Log { get; } = new List<string>();

        public string WorkPath(params string[] parts) => Path.Combine(new[] { Work }.Concat(parts).ToArray());

        public MapRunner Map(string name)
        {
            var xslt = Path.Combine(ScenarioPaths.Maps, "Xslt");
            return _maps.GetOrAdd(name, n => new MapRunner(n, Path.Combine(xslt, n + ".xslt"), Path.Combine(xslt, "Extensions.xml")));
        }

        public BrlPolicy Policy(string name)
        {
            return _policies.GetOrAdd(name, n => BrlPolicy.Load(Path.Combine(ScenarioPaths.Rules, n + ".xml")));
        }

        public void Trace(string format, params object[] args)
        {
            var line = string.Format(System.Globalization.CultureInfo.InvariantCulture, format, args);
            lock (_gate)
            {
                Log.Add(line);
            }

            Console.WriteLine("  " + line);
        }

        public void Suspend(string what, Exception error, byte[] body)
        {
            var file = WorkPath("suspended", DateTime.UtcNow.ToString("HHmmssfff") + "_" + Guid.NewGuid().ToString("N")[..8] + ".dat");
            File.WriteAllBytes(file, body ?? Array.Empty<byte>());
            lock (_gate)
            {
                Suspended.Add(what + ": " + error.Message);
            }

            Trace("SUSPENDED {0}: {1}", what, error.Message);
        }

        public InterchangeRecord RegisterInterchange(string partnerId, InterchangeDirection direction, string type, string control, string group, int count, string fileName, TimeSpan? ackWindow)
        {
            var record = new InterchangeRecord
            {
                PartnerId = partnerId,
                Direction = direction,
                TransactionType = type,
                ControlNumber = control,
                GroupControlNumber = group,
                TransactionCount = count,
                CreatedUtc = UtcNow,
                AckStatus = ackWindow.HasValue ? AckStatus.Pending : AckStatus.NotRequired,
                AckDueUtc = ackWindow.HasValue ? UtcNow.Add(ackWindow.Value) : (DateTime?)null,
                FileName = fileName
            };

            lock (Interchanges)
            {
                record.InterchangeKey = _nextInterchangeKey++;
                Interchanges.Add(record);
            }

            return record;
        }

        public OrderActivityRow Track(string partnerId, string orderNumber)
        {
            return Bam.GetOrAdd(partnerId + "|" + orderNumber, id => new OrderActivityRow { ActivityId = id, PartnerId = partnerId, OrderNumber = orderNumber });
        }

        /// <summary>SMTP send port stand-in: an .eml in mail/, like SmtpClient with SpecifiedPickupDirectory.</summary>
        public string Mail(string to, string subject, string body)
        {
            var file = WorkPath("mail", Now.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N")[..6] + ".eml");
            var eml = new StringBuilder()
                .Append("From: b2b-noreply@vanarsdel.com\r\n")
                .Append("To: ").Append(to).Append("\r\n")
                .Append("Subject: ").Append(subject).Append("\r\n")
                .Append("Date: ").Append(UtcNow.ToString("r", System.Globalization.CultureInfo.InvariantCulture)).Append("\r\n")
                .Append("Content-Type: text/plain; charset=utf-8\r\n\r\n")
                .Append(body);
            File.WriteAllText(file, eml.ToString(), new UTF8Encoding(false));
            Trace("mail to {0}: {1}", to, subject);
            return file;
        }
    }
}
