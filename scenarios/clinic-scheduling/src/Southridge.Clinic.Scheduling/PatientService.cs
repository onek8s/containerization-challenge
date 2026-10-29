using System;
using System.Collections.Generic;
using System.Linq;
using Southridge.Clinic.Data;
using Southridge.Clinic.Data.Repositories;
using Southridge.Clinic.Domain;
using Southridge.Clinic.Hl7;
using Southridge.Clinic.Infrastructure;

namespace Southridge.Clinic.Scheduling
{
    public class RegistrationResult
    {
        public int PatientId { get; set; }
        public string Mrn { get; set; }
        public bool PossibleDuplicate { get; set; }
    }

    /// <summary>Registration and demographic changes, with the ADT messages to the lab and the hospital.</summary>
    public class PatientService
    {
        private readonly PatientRepository _patients;
        private readonly InterfaceNotifier _notifier;

        public PatientService()
            : this(new PatientRepository(), new InterfaceNotifier())
        {
        }

        public PatientService(PatientRepository patients, InterfaceNotifier notifier)
        {
            _patients = patients;
            _notifier = notifier;
        }

        public RegistrationResult Register(NewPatient patient, string userName, bool allowDuplicate)
        {
            Validate(patient);
            List<PatientSearchRow> duplicates = _patients.FindDuplicates(patient.LastName, patient.DateOfBirth)
                .Where(d => PatientIdentity.LikelySamePerson(d.LastName, d.FirstName, d.DateOfBirth, patient.LastName, patient.FirstName, patient.DateOfBirth))
                .ToList();
            if (duplicates.Count > 0 && !allowDuplicate)
            {
                return new RegistrationResult { PatientId = duplicates[0].PatientId, Mrn = duplicates[0].Mrn, PossibleDuplicate = true };
            }

            patient.HomePhone = PhoneNumber.Digits(patient.HomePhone);
            patient.MobilePhone = PhoneNumber.Digits(patient.MobilePhone);
            string mrn;
            int id = _patients.Create(patient, userName, out mrn);
            ClinicLog.Audit(userName, "RegisterPatient", "Patient " + mrn + " registered");
            SendAdt(id, "A04");
            return new RegistrationResult { PatientId = id, Mrn = mrn };
        }

        public void Update(Patient changed, string userName)
        {
            using (ClinicDataContext db = ClinicDataContextFactory.Create())
            {
                Patient current = db.Patients.Single(p => p.PatientId == changed.PatientId);
                if (!current.RowVersion.Equals(changed.RowVersion))
                {
                    throw new ClinicRuleException("The patient was changed by someone else. Reload the page and try again.");
                }

                current.FirstName = changed.FirstName;
                current.MiddleName = changed.MiddleName;
                current.LastName = changed.LastName;
                current.DateOfBirth = changed.DateOfBirth;
                current.Sex = changed.Sex;
                current.Address1 = changed.Address1;
                current.Address2 = changed.Address2;
                current.City = changed.City;
                current.State = changed.State;
                current.Zip = changed.Zip;
                current.HomePhone = PhoneNumber.Digits(changed.HomePhone);
                current.MobilePhone = PhoneNumber.Digits(changed.MobilePhone);
                current.Email = changed.Email;
                current.PreferredContact = changed.PreferredContact;
                current.ReminderOptIn = changed.ReminderOptIn;
                current.PreferredSiteId = changed.PreferredSiteId;
                current.PrimaryProviderId = changed.PrimaryProviderId;
                current.ModifiedBy = userName;
                current.ModifiedOn = DateTime.Now;
                db.SubmitChanges();
            }

            SendAdt(changed.PatientId, "A08");
        }

        public static void Validate(NewPatient patient)
        {
            if (string.IsNullOrEmpty(patient.FirstName) || string.IsNullOrEmpty(patient.LastName))
            {
                throw new ClinicRuleException("First and last name are required.");
            }

            if (patient.DateOfBirth > DateTime.Today || patient.DateOfBirth < new DateTime(1900, 1, 1))
            {
                throw new ClinicRuleException("The date of birth is not valid.");
            }

            if (!string.IsNullOrEmpty(patient.HomePhone) && PhoneNumber.Digits(patient.HomePhone) == null)
            {
                throw new ClinicRuleException("The home phone number is not valid.");
            }

            if (!string.IsNullOrEmpty(patient.MobilePhone) && PhoneNumber.Digits(patient.MobilePhone) == null)
            {
                throw new ClinicRuleException("The mobile phone number is not valid.");
            }
        }

        private void SendAdt(int patientId, string trigger)
        {
            try
            {
                _notifier.PatientEvent(patientId, trigger);
            }
            catch (Exception ex)
            {
                ClinicLog.Error(LogCategory.Interface, "Could not queue ADT^" + trigger + " for patient " + patientId, ex);
            }
        }
    }
}
