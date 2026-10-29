using System;
using System.Globalization;
using System.Linq;
using Southridge.Clinic.Data;
using Southridge.Clinic.Domain;

namespace Southridge.Clinic.Documents
{
    /// <summary>Loads an encounter with LINQ to SQL and builds the after visit summary PDF.</summary>
    public static class VisitSummaryBuilder
    {
        public static VisitSummaryData Load(int encounterId)
        {
            using (ClinicDataContext db = ClinicDataContextFactory.CreateReadOnly())
            {
                Encounter e = db.Encounters.SingleOrDefault(x => x.EncounterId == encounterId);
                if (e == null)
                {
                    return null;
                }

                var data = new VisitSummaryData
                {
                    PatientName = e.Patient.FirstName + " " + e.Patient.LastName,
                    Mrn = e.Patient.Mrn,
                    DateOfBirth = e.Patient.DateOfBirth,
                    DateOfService = e.DateOfService,
                    ProviderName = e.Provider.FirstName + " " + e.Provider.LastName + (e.Provider.Credentials == null ? string.Empty : ", " + e.Provider.Credentials),
                    SiteName = e.Site.Name,
                    ChiefComplaint = e.ChiefComplaint,
                    Notes = e.Notes
                };

                var vitals = new System.Collections.Generic.List<string>();
                if (!string.IsNullOrEmpty(e.VitalsBp))
                {
                    vitals.Add("BP " + e.VitalsBp);
                }

                if (e.VitalsPulse.HasValue)
                {
                    vitals.Add("Pulse " + e.VitalsPulse.Value);
                }

                if (e.VitalsWeightLb.HasValue)
                {
                    vitals.Add("Weight " + e.VitalsWeightLb.Value.ToString("0.0", CultureInfo.InvariantCulture) + " lb");
                }

                if (e.VitalsTempF.HasValue)
                {
                    vitals.Add("Temp " + e.VitalsTempF.Value.ToString("0.0", CultureInfo.InvariantCulture) + " F");
                }

                data.Vitals = string.Join(", ", vitals.ToArray());

                var diagnoses = from d in db.EncounterDiagnosis
                                where d.EncounterId == encounterId
                                join c in db.DiagnosisCodes on new { d.Code, d.CodeSet } equals new { c.Code, c.CodeSet } into dc
                                from c in dc.DefaultIfEmpty()
                                orderby d.Sequence
                                select new { d.Code, Description = c == null ? null : c.Description };
                foreach (var d in diagnoses)
                {
                    data.Diagnoses.Add(d.Code + (d.Description == null ? string.Empty : " " + d.Description));
                }

                foreach (Charge c in e.Charges.Where(c => c.Status != (byte)ChargeStatus.Voided).OrderBy(c => c.LineNumber))
                {
                    data.Procedures.Add(c.CptCode + " " + c.ProcedureCode.Description);
                }

                var results = db.LabResults
                    .Where(r => r.PatientId == e.PatientId && r.ObservedOn >= e.DateOfService.AddDays(-1) && r.ObservedOn < e.DateOfService.AddDays(8))
                    .OrderBy(r => r.ObservationName)
                    .ToList();
                foreach (LabResult r in results)
                {
                    data.Results.Add(new[] { r.ObservationName, r.Value + (r.Units == null ? string.Empty : " " + r.Units), r.ReferenceRange, r.AbnormalFlag });
                }

                data.NextAppointment = db.Appointments
                    .Where(a => a.PatientId == e.PatientId && a.StartsAt > e.DateOfService.AddDays(1) && (a.Status == (byte)AppointmentStatus.Scheduled || a.Status == (byte)AppointmentStatus.Confirmed))
                    .OrderBy(a => a.StartsAt)
                    .Select(a => (DateTime?)a.StartsAt)
                    .FirstOrDefault();
                return data;
            }
        }

        public static byte[] Pdf(int encounterId)
        {
            VisitSummaryData data = Load(encounterId);
            return data == null ? null : PdfDocuments.VisitSummary(data);
        }
    }
}
