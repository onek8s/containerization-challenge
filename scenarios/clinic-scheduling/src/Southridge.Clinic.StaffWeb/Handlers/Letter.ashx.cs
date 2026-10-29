using System.Linq;
using System.Web;
using Southridge.Clinic.Data;
using Southridge.Clinic.Documents;
using Southridge.Clinic.Scheduling;

namespace Southridge.Clinic.StaffWeb.Handlers
{
    /// <summary>Appointment confirmation letter (iTextSharp).</summary>
    public class LetterHandler : PdfHandler
    {
        protected override byte[] Create(HttpContext context, int id, out string fileName, ref string contentType)
        {
            fileName = "appointment-" + id + ".pdf";
            using (ClinicDataContext db = ClinicDataContextFactory.CreateReadOnly())
            {
                Appointment a = db.Appointments.SingleOrDefault(x => x.AppointmentId == id);
                if (a == null)
                {
                    return null;
                }

                return PdfDocuments.AppointmentLetter(new AppointmentLetterData
                {
                    To = new LetterAddress
                    {
                        Name = a.Patient.FirstName + " " + a.Patient.LastName,
                        Line1 = a.Patient.Address1,
                        Line2 = a.Patient.Address2,
                        City = a.Patient.City,
                        State = a.Patient.State,
                        Zip = a.Patient.Zip
                    },
                    PatientName = a.Patient.FirstName + " " + a.Patient.LastName,
                    Mrn = a.Patient.Mrn,
                    StartsAt = a.StartsAt,
                    ProviderName = ClinicLookups.ProviderName(a.Provider),
                    AppointmentType = a.AppointmentType.Name,
                    SiteName = a.Site.Name,
                    SiteAddress = a.Site.Address1 + ", " + a.Site.City + ", " + a.Site.State + " " + a.Site.Zip,
                    SitePhone = a.Site.Phone,
                    ConfirmationCode = a.ConfirmationCode,
                    NewPatient = a.AppointmentType.NewPatient
                });
            }
        }
    }
}
