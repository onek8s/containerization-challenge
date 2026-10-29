using System;
using System.Web;
using Southridge.Clinic.Billing;

namespace Southridge.Clinic.StaffWeb.Handlers
{
    /// <summary>Statement preview for a patient (id = PatientId). Does not record a statement.</summary>
    public class StatementHandler : PdfHandler
    {
        protected override byte[] Create(HttpContext context, int id, out string fileName, ref string contentType)
        {
            fileName = "statement-" + id + ".pdf";
            StatementResult result = new StatementService().Preview(id, DateTime.Today);
            return result == null ? null : result.Pdf;
        }
    }
}
