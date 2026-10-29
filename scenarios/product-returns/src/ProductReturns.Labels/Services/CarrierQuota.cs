using System;
using System.Globalization;
using System.IO;
using Microsoft.Extensions.Options;

namespace ProductReturns.Labels.Services
{
    /// <summary>
    /// Counts labels created per day against the carrier contract. The counter is kept in
    /// quota.txt in the state folder ("yyyy-MM-dd;count") so it survives service restarts.
    /// </summary>
    public class CarrierQuota
    {
        private readonly object _lock = new object();
        private readonly LabelOptions _options;
        private readonly string _file;

        public CarrierQuota(IOptions<LabelOptions> options)
        {
            _options = options.Value;
            Directory.CreateDirectory(_options.StateFolder);
            _file = Path.Combine(_options.StateFolder, "quota.txt");
        }

        public int UsedToday
        {
            get
            {
                lock (_lock)
                {
                    return Read().Count;
                }
            }
        }

        public bool TryTake()
        {
            lock (_lock)
            {
                var (day, count) = Read();
                if (count >= _options.DailyQuota)
                {
                    return false;
                }

                Write(day, count + 1);
                return true;
            }
        }

        /// <summary>Gives back a slot when the carrier call failed before a label was created.</summary>
        public void Release()
        {
            lock (_lock)
            {
                var (day, count) = Read();
                Write(day, Math.Max(0, count - 1));
            }
        }

        private (DateTime Day, int Count) Read()
        {
            var today = DateTime.Today;
            if (!File.Exists(_file))
            {
                return (today, 0);
            }

            var parts = File.ReadAllText(_file).Trim().Split(';');
            if (parts.Length == 2
                && DateTime.TryParseExact(parts[0], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
                && day == today
                && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var count))
            {
                return (today, count);
            }

            return (today, 0);
        }

        private void Write(DateTime day, int count)
        {
            File.WriteAllText(_file, day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ";" + count.ToString(CultureInfo.InvariantCulture));
        }
    }
}
