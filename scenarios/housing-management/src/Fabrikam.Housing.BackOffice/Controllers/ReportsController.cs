using System;
using System.Threading.Tasks;
using Fabrikam.Housing.BackOffice.Infrastructure;
using Fabrikam.Housing.Common;
using Fabrikam.Housing.Data.Queries;
using Fabrikam.Housing.Documents;
using Microsoft.AspNetCore.Mvc;

namespace Fabrikam.Housing.BackOffice.Controllers
{
    public class ReportsController : Controller
    {
        private const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        private const string Xls = "application/vnd.ms-excel";

        private readonly ReportQueries _queries;
        private readonly IReportWorkbookWriter _writer;
        private readonly ReportCache _cache;
        private readonly IClock _clock;

        public ReportsController(ReportQueries queries, IReportWorkbookWriter writer, ReportCache cache, IClock clock)
        {
            _queries = queries;
            _writer = writer;
            _cache = cache;
            _clock = clock;
        }

        public IActionResult Index() => View();

        public async Task<IActionResult> Arrears(decimal minimum = 10m, string patch = null)
        {
            var today = _clock.Today;
            var bytes = await _cache.GetOrCreateAsync($"arrears:{minimum}:{patch}:{today:yyyyMMdd}",
                async () => _writer.Arrears(await _queries.ArrearsAsync(minimum, string.IsNullOrEmpty(patch) ? null : patch), _clock.Now));
            return File(bytes, Xlsx, $"Arrears_{patch ?? "ALL"}_{today:yyyyMMdd}.xlsx");
        }

        public async Task<IActionResult> RepairsPerformance(DateTime? from, DateTime? to)
        {
            var f = (from ?? _clock.Today.AddDays(-90)).Date;
            var t = (to ?? _clock.Today).Date;
            var bytes = await _cache.GetOrCreateAsync($"repairs:{f:yyyyMMdd}:{t:yyyyMMdd}",
                async () => _writer.RepairsPerformance(await _queries.RepairsPerformanceAsync(f, t, _clock.Now), f, t));
            return File(bytes, Xlsx, $"RepairsPerformance_{f:yyyyMMdd}_{t:yyyyMMdd}.xlsx");
        }

        public async Task<IActionResult> Compliance()
        {
            var today = _clock.Today;
            var bytes = await _cache.GetOrCreateAsync($"compliance:{today:yyyyMMdd}",
                async () => _writer.Compliance(await _queries.ComplianceAsync(), today));
            return File(bytes, Xlsx, $"Compliance_{today:yyyyMMdd}.xlsx");
        }

        public async Task<IActionResult> RentRoll()
        {
            var today = _clock.Today;
            var bytes = await _cache.GetOrCreateAsync($"rentroll:{today:yyyyMMdd}",
                async () => _writer.RentRollXls(await _queries.RentRollAsync(), today));
            return File(bytes, Xls, $"RentRoll_{today:yyyyMMdd}.xls");
        }
    }
}
