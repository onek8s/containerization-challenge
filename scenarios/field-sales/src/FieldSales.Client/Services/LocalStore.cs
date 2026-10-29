using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Wingtip.FieldSales.Contracts;

namespace Wingtip.FieldSales.Client.Services
{
    public enum OutboxState
    {
        Draft,
        Waiting,
        NeedsReview,
        Rejected,
        Sent
    }

    /// <summary>An order kept on the laptop until the office has received it.</summary>
    public class OutboxOrder
    {
        public OrderSubmitRequest Request { get; set; }
        public string CustomerName { get; set; }
        public decimal NetTotal { get; set; }
        public OutboxState State { get; set; }
        public string Message { get; set; }
        public string OrderNo { get; set; }
        public string ServerStatus { get; set; }
        public DateTime? SentAt { get; set; }
        public int Attempts { get; set; }
    }

    public class CatalogCache
    {
        public long Version { get; set; }
        public long PriceListVersion { get; set; }
        public DateTime SyncedAt { get; set; }
        public string CutOff { get; set; }
        public Dictionary<string, ProductDto> Products { get; set; }
        public List<PriceRuleDto> PriceRules { get; set; }
        public List<PromotionDto> Promotions { get; set; }
        public List<DateTime> Holidays { get; set; }
    }

    public class SequenceState
    {
        public DateTime Day { get; set; }
        public int Last { get; set; }
    }

    /// <summary>
    /// Offline data of one user under %LOCALAPPDATA%\Wingtip\FieldSales\&lt;user&gt;. Files are written to a temporary
    /// name first so that a laptop running out of battery does not leave half-written JSON behind.
    /// </summary>
    public class LocalStore
    {
        private static readonly JsonSerializerSettings Json = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            DateTimeZoneHandling = DateTimeZoneHandling.Local
        };

        public LocalStore(string userName)
        {
            var safe = string.Concat((userName ?? "default").Split(Path.GetInvalidFileNameChars()));
            Folder = Path.Combine(RootFolder, safe);
            Directory.CreateDirectory(Path.Combine(Folder, "outbox"));
            Directory.CreateDirectory(Path.Combine(Folder, "sent"));
        }

        public static string RootFolder
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Wingtip", "FieldSales"); }
        }

        public string Folder { get; private set; }

        public CatalogCache LoadCatalog()
        {
            return Read<CatalogCache>("catalog.json") ?? new CatalogCache
            {
                Products = new Dictionary<string, ProductDto>(StringComparer.OrdinalIgnoreCase),
                PriceRules = new List<PriceRuleDto>(),
                Promotions = new List<PromotionDto>(),
                Holidays = new List<DateTime>(),
                CutOff = "14:00"
            };
        }

        public void SaveCatalog(CatalogCache catalog)
        {
            Write("catalog.json", catalog);
        }

        public List<CustomerDto> LoadCustomers()
        {
            return Read<List<CustomerDto>>("customers.json") ?? new List<CustomerDto>();
        }

        public void SaveCustomers(List<CustomerDto> customers)
        {
            Write("customers.json", customers);
        }

        public List<OutboxOrder> LoadOutbox()
        {
            return Directory.GetFiles(Path.Combine(Folder, "outbox"), "*.json")
                .Select(f => Read<OutboxOrder>(Path.Combine("outbox", Path.GetFileName(f))))
                .Where(o => o != null)
                .OrderBy(o => o.Request.CreatedAt)
                .ToList();
        }

        public List<OutboxOrder> LoadSent(int days)
        {
            var since = DateTime.Today.AddDays(-days);
            return Directory.GetFiles(Path.Combine(Folder, "sent"), "*.json")
                .Where(f => File.GetLastWriteTime(f) >= since)
                .Select(f => Read<OutboxOrder>(Path.Combine("sent", Path.GetFileName(f))))
                .Where(o => o != null)
                .OrderByDescending(o => o.Request.CreatedAt)
                .ToList();
        }

        public void SaveOutbox(OutboxOrder order)
        {
            var name = order.Request.ClientOrderId.ToString("N") + ".json";
            if (order.State == OutboxState.Sent)
            {
                Write(Path.Combine("sent", name), order);
                var pending = Path.Combine(Folder, "outbox", name);
                if (File.Exists(pending)) File.Delete(pending);
            }
            else
            {
                Write(Path.Combine("outbox", name), order);
            }
        }

        public void DeleteOutbox(Guid clientOrderId)
        {
            var path = Path.Combine(Folder, "outbox", clientOrderId.ToString("N") + ".json");
            if (File.Exists(path)) File.Delete(path);
        }

        /// <summary>Next running number for today's rep references. Kept on the laptop because orders are numbered offline.</summary>
        public int NextSequence(DateTime today)
        {
            var state = Read<SequenceState>("sequence.json") ?? new SequenceState();
            if (state.Day.Date != today.Date) state = new SequenceState { Day = today.Date, Last = 0 };
            state.Last++;
            Write("sequence.json", state);
            return state.Last;
        }

        private T Read<T>(string relative) where T : class
        {
            var path = Path.Combine(Folder, relative);
            if (!File.Exists(path)) return null;
            try
            {
                return JsonConvert.DeserializeObject<T>(File.ReadAllText(path), Json);
            }
            catch (JsonException)
            {
                File.Move(path, path + "." + DateTime.Now.ToString("yyyyMMddHHmmss") + ".bad");
                return null;
            }
        }

        private void Write(string relative, object value)
        {
            var path = Path.Combine(Folder, relative);
            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonConvert.SerializeObject(value, Json));
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }
    }
}
