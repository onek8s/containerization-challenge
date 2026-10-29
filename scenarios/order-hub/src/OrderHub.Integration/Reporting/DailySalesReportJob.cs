using System;
using System.Linq;
using System.Threading.Tasks;
using Litware.OrderHub.Integration.Data;
using Serilog;

namespace Litware.OrderHub.Integration.Reporting
{
    /// <summary>Creates the sales workbook of the previous day once the configured time of day has passed.</summary>
    public class DailySalesReportJob
    {
        private readonly IOrderStore _store;
        private readonly TimeSpan _runAt;
        private readonly Func<DateTime> _now;

        public DailySalesReportJob(IOrderStore store, TimeSpan runAt, Func<DateTime> now = null)
        {
            _store = store;
            _runAt = runAt;
            _now = now ?? (() => DateTime.Now);
        }

        /// <returns>The file created, or null when nothing was due.</returns>
        public async Task<string> RunIfDueAsync()
        {
            DateTime now = _now();
            if (now.TimeOfDay < _runAt) return null;

            DateTime day = now.Date.AddDays(-1);
            string fileName = SalesWorkbook.FileName(day);
            var existing = await _store.GetReportFilesAsync(10);
            if (existing.Any(f => f.FileName == fileName)) return null;

            return await CreateAsync(day);
        }

        public async Task<string> CreateAsync(DateTime day)
        {
            DateTime fromUtc = DateTime.SpecifyKind(day.Date, DateTimeKind.Local).ToUniversalTime();
            DateTime toUtc = DateTime.SpecifyKind(day.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime();
            var lines = await _store.GetInvoicedLinesAsync(fromUtc, toUtc);

            string fileName = SalesWorkbook.FileName(day);
            await _store.SaveReportFileAsync(fileName, day, SalesWorkbook.Build(day, lines));
            Log.Information("Sales workbook {File} created with {Lines} invoice lines", fileName, lines.Count);
            return fileName;
        }
    }
}
