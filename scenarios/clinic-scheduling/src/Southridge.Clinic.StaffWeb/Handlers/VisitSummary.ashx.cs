using System.Web;
using Southridge.Clinic.Documents;

namespace Southridge.Clinic.StaffWeb.Handlers
{
    public class VisitSummaryHandler : PdfHandler
    {
        protected override byte[] Create(HttpContext context, int id, out string fileName, ref string contentType)
        {
            fileName = "visit-" + id + ".pdf";
            return VisitSummaryBuilder.Pdf(id);
        }
    }
}
