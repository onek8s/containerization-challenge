using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Services;
using System.Web.Services;
using Southridge.Clinic.Data;
using Southridge.Clinic.Domain;

namespace Southridge.Clinic.StaffWeb.Services
{
    /// <summary>Script service for the patient pickers (AutoCompleteExtender) on the booking and referral pages.</summary>
    [WebService(Namespace = "http://schemas.southridgemedical.org/clinic/staff/2009/04")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [ScriptService]
    public class PatientLookup : WebService
    {
        [WebMethod]
        [ScriptMethod]
        public string[] Complete(string prefixText, int count)
        {
            if (!User.Identity.IsAuthenticated || string.IsNullOrEmpty(prefixText) || prefixText.Trim().Length < 2)
            {
                return new string[0];
            }

            string text = prefixText.Trim();
            string mrn = MedicalRecordNumber.Normalize(text);
            using (ClinicDataContext db = ClinicDataContextFactory.CreateReadOnly())
            {
                IQueryable<Patient> query = db.Patients.Where(p => p.IsActive);
                if (mrn != null)
                {
                    query = query.Where(p => p.Mrn == mrn);
                }
                else
                {
                    string[] parts = text.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    string last = parts[0];
                    query = query.Where(p => p.LastName.StartsWith(last));
                    if (parts.Length > 1)
                    {
                        string first = parts[1];
                        query = query.Where(p => p.FirstName.StartsWith(first));
                    }
                }

                return query.OrderBy(p => p.LastName).ThenBy(p => p.FirstName).Take(Math.Min(Math.Max(count, 1), 20))
                    .Select(p => new { p.PatientId, p.LastName, p.FirstName, p.DateOfBirth, p.Mrn })
                    .ToList()
                    .Select(p => AjaxControlToolkit.AutoCompleteExtender.CreateAutoCompleteItem(
                        p.LastName + ", " + p.FirstName + "  " + p.DateOfBirth.ToString("MM/dd/yyyy") + "  " + p.Mrn, p.PatientId.ToString()))
                    .ToArray();
            }
        }

        /// <summary>Diagnosis picker on the visit page. The context key is the code set (9 or 10) for the date of service.</summary>
        [WebMethod]
        [ScriptMethod]
        public string[] Diagnoses(string prefixText, int count, string contextKey)
        {
            if (!User.Identity.IsAuthenticated || string.IsNullOrEmpty(prefixText) || prefixText.Trim().Length < 2)
            {
                return new string[0];
            }

            byte codeSet;
            if (!byte.TryParse(contextKey, out codeSet))
            {
                codeSet = (byte)DiagnosisCodeSet.Icd10;
            }

            string text = prefixText.Trim().ToUpperInvariant();
            using (ClinicDataContext db = ClinicDataContextFactory.CreateReadOnly())
            {
                return db.DiagnosisCodes
                    .Where(d => d.IsActive && d.CodeSet == codeSet && (d.Code.StartsWith(text) || d.Description.Contains(text)))
                    .OrderBy(d => d.Code)
                    .Take(Math.Min(Math.Max(count, 1), 25))
                    .ToList()
                    .Select(d => AjaxControlToolkit.AutoCompleteExtender.CreateAutoCompleteItem(d.Code + "  " + d.Description, d.Code))
                    .ToArray();
            }
        }
    }
}
