using System.Collections.Generic;
using System.Threading.Tasks;
using Litware.OrderHub.Integration.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Litware.OrderHub.Portal.Pages.Reports
{
    public class IndexModel : PageModel
    {
        private const string XlsxType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        private readonly IOrderStore _store;

        public IndexModel(IOrderStore store)
        {
            _store = store;
        }

        public IList<ReportFileInfo> Files { get; private set; }

        public async Task OnGetAsync()
        {
            Files = await _store.GetReportFilesAsync(90);
        }

        public async Task<IActionResult> OnGetDownloadAsync(string name)
        {
            var content = await _store.GetReportFileAsync(name);
            if (content == null) return NotFound();
            return File(content, XlsxType, name);
        }
    }
}
