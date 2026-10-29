using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Southridge.Clinic.Domain;

namespace Southridge.Clinic.Tests
{
    [TestClass]
    public class ClinicRulesTests
    {
        [TestMethod]
        public void HolidaysFollowTheObservedRules()
        {
            var calendar = new ClinicCalendar();

            Assert.IsTrue(calendar.IsHoliday(new DateTime(2013, 5, 27)), "Memorial Day");
            Assert.IsTrue(calendar.IsHoliday(new DateTime(2013, 9, 2)), "Labor Day");
            Assert.IsTrue(calendar.IsHoliday(new DateTime(2013, 11, 28)), "Thanksgiving");
            Assert.IsTrue(calendar.IsHoliday(new DateTime(2015, 7, 3)), "July 4th on a Saturday is observed Friday");
            Assert.IsTrue(calendar.IsHoliday(new DateTime(2011, 12, 26)), "Christmas on a Sunday is observed Monday");
            Assert.IsFalse(calendar.IsHoliday(new DateTime(2013, 3, 12)));
        }

        [TestMethod]
        public void BusinessDaysSkipWeekendsAndHolidays()
        {
            var calendar = new ClinicCalendar();

            Assert.AreEqual(new DateTime(2013, 11, 29), calendar.AddBusinessDays(new DateTime(2013, 11, 27), 1));
            Assert.AreEqual(new DateTime(2013, 3, 18), calendar.AddBusinessDays(new DateTime(2013, 3, 15), 1));
            Assert.AreEqual(new DateTime(2013, 3, 15), calendar.AddBusinessDays(new DateTime(2013, 3, 18), -1));
            Assert.AreEqual(new DateTime(2013, 3, 16), calendar.NextOpenDay(new DateTime(2013, 3, 15)), "Saturday clinics are open");
        }

        [TestMethod]
        public void StatusTransitions()
        {
            Assert.IsTrue(AppointmentRules.CanChange(AppointmentStatus.Scheduled, AppointmentStatus.CheckedIn));
            Assert.IsTrue(AppointmentRules.CanChange(AppointmentStatus.NoShow, AppointmentStatus.CheckedIn));
            Assert.IsFalse(AppointmentRules.CanChange(AppointmentStatus.Completed, AppointmentStatus.Cancelled));
            Assert.IsFalse(AppointmentRules.CanChange(AppointmentStatus.InRoom, AppointmentStatus.NoShow));
        }

        [TestMethod]
        [ExpectedException(typeof(ClinicRuleException))]
        public void InvalidTransitionThrows()
        {
            AppointmentRules.EnsureCanChange(AppointmentStatus.Cancelled, AppointmentStatus.Scheduled);
        }

        [TestMethod]
        public void FrontDeskPolicies()
        {
            DateTime start = new DateTime(2013, 3, 12, 9, 0, 0);

            Assert.IsTrue(AppointmentRules.IsLateCancellation(start, start.AddHours(-23)));
            Assert.IsFalse(AppointmentRules.IsLateCancellation(start, start.AddHours(-25)));
            Assert.IsTrue(AppointmentRules.IsNoShow(start, AppointmentStatus.Confirmed, start.AddMinutes(31)));
            Assert.IsFalse(AppointmentRules.IsNoShow(start, AppointmentStatus.CheckedIn, start.AddHours(2)));
            Assert.IsTrue(AppointmentRules.IsKioskCheckInWindow(start, start.AddMinutes(-45)));
            Assert.IsFalse(AppointmentRules.IsKioskCheckInWindow(start, start.AddMinutes(16)));
            Assert.IsFalse(AppointmentRules.CanBookOnline(start, start.AddHours(-1)));
            Assert.IsTrue(AppointmentRules.CanBookOnline(start, start.AddDays(-90)));
            Assert.IsFalse(AppointmentRules.CanBookOnline(start, start.AddDays(-91)));
        }

        [TestMethod]
        public void MedicalRecordNumbersCarryACheckDigit()
        {
            string mrn = MedicalRecordNumber.Format(1045230);

            Assert.AreEqual(9, mrn.Length);
            Assert.AreEqual('-', mrn[7]);
            Assert.IsTrue(MedicalRecordNumber.IsValid(mrn));
            Assert.IsTrue(MedicalRecordNumber.IsValid(mrn.Replace("-", string.Empty)));
            char wrong = mrn[8] == '9' ? '0' : (char)(mrn[8] + 1);
            Assert.IsFalse(MedicalRecordNumber.IsValid(mrn.Substring(0, 8) + wrong));
        }

        [TestMethod]
        public void NpiLuhnCheck()
        {
            Assert.IsTrue(Npi.IsValid("1234567893"));
            Assert.IsFalse(Npi.IsValid("1234567890"));
            Assert.IsFalse(Npi.IsValid("3234567893"));
            Assert.AreEqual("1234567893", Npi.WithCheckDigit("123456789"));
        }

        [TestMethod]
        public void DiagnosisCodeSetsChangeOnTheTransitionDate()
        {
            Assert.AreEqual(DiagnosisCodeSet.Icd9, DiagnosisCodes.CodeSetFor(new DateTime(2015, 9, 30)));
            Assert.AreEqual(DiagnosisCodeSet.Icd10, DiagnosisCodes.CodeSetFor(new DateTime(2015, 10, 1)));
            Assert.IsTrue(DiagnosisCodes.IsValid("250.00", DiagnosisCodeSet.Icd9));
            Assert.IsTrue(DiagnosisCodes.IsValid("V70.0", DiagnosisCodeSet.Icd9));
            Assert.IsTrue(DiagnosisCodes.IsValid("E11.9", DiagnosisCodeSet.Icd10));
            Assert.IsFalse(DiagnosisCodes.IsValid("E11.9", DiagnosisCodeSet.Icd9));
            Assert.AreEqual("E119", DiagnosisCodes.ToClaimFormat("e11.9"));
            Assert.AreEqual("E879.8", DiagnosisCodes.FromClaimFormat("E8798", DiagnosisCodeSet.Icd9));
        }

        [TestMethod]
        public void PhoneNumbers()
        {
            Assert.AreEqual("+12535550144", PhoneNumber.ToE164("(253) 555-0144"));
            Assert.AreEqual("+12535550144", PhoneNumber.ToE164("1-253-555-0144"));
            Assert.IsNull(PhoneNumber.ToE164("555-0144"));
            Assert.AreEqual("(253) 555-0144", PhoneNumber.Format("2535550144"));
        }
    }
}
