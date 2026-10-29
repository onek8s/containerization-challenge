using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Southridge.Clinic.Billing;
using Southridge.Clinic.Data;
using Southridge.Clinic.Domain;

namespace Southridge.Clinic.Tests
{
    [TestClass]
    public class BillingTests
    {
        internal static ClaimData SampleClaim(string claimNumber, DateTime dateOfService)
        {
            var claim = new ClaimData
            {
                ClaimNumber = claimNumber,
                DateOfService = dateOfService,
                Mrn = "1045230-1",
                PatientFirstName = "Maria",
                PatientLastName = "Lindqvist",
                PatientDateOfBirth = new DateTime(1971, 4, 2),
                PatientSex = 2,
                PatientAddress1 = "418 N Yakima Ave",
                PatientCity = "Tacoma",
                PatientState = "WA",
                PatientZip = "98403",
                MemberId = "PSW8812034",
                GroupNumber = "G4410",
                Relationship = (byte)InsuredRelationship.Self,
                PayerName = "Premera Blue Cross",
                ClaimPayerId = "47570",
                FilingIndicator = "BL",
                RenderingNpi = Npi.WithCheckDigit("123456789"),
                RenderingFirstName = "Anna",
                RenderingLastName = "Brennan",
                TaxonomyCode = "207Q00000X",
                SiteName = "Southridge Family Medicine",
                SiteAddress1 = "2201 S 19th St",
                SiteCity = "Tacoma",
                SiteState = "WA",
                SiteZip = "98405",
                PlaceOfService = "11",
                TaxId = "911234567",
                GroupNpi = Npi.WithCheckDigit("158866779")
            };
            bool icd10 = DiagnosisCodes.CodeSetFor(dateOfService) == DiagnosisCodeSet.Icd10;
            claim.Diagnoses.Add(new ClaimDiagnosis { Sequence = 1, Code = icd10 ? "E11.9" : "250.00", CodeSet = icd10 ? 10 : 9 });
            claim.Lines.Add(new ClaimLine { LineNumber = 1, CptCode = "99213", Units = 1, Amount = 125.00m, DiagnosisPointers = "1" });
            claim.Lines.Add(new ClaimLine { LineNumber = 2, CptCode = "83036", Units = 1, Amount = 30.00m, DiagnosisPointers = "1" });
            claim.TotalCharge = claim.Lines.Sum(l => l.Amount);
            return claim;
        }

        [TestMethod]
        public void CleanClaimPassesTheScrubber()
        {
            DateTime dos = new DateTime(2013, 3, 12);
            IList<string> errors = new ClaimScrubber(dos.AddDays(3)).Check(SampleClaim("C200100", dos), 90);

            Assert.AreEqual(0, errors.Count, string.Join("; ", errors.ToArray()));
        }

        [TestMethod]
        public void ScrubberFindsCommonRejections()
        {
            DateTime dos = new DateTime(2013, 3, 12);
            ClaimData claim = SampleClaim("C200101", dos);
            claim.Diagnoses[0].Code = "E11.9";
            claim.Diagnoses[0].CodeSet = 10;
            claim.Lines[1].DiagnosisPointers = "2";
            claim.Lines[0].Modifier1 = "2";
            claim.MemberId = null;
            claim.RenderingNpi = "1234567890";

            IList<string> errors = new ClaimScrubber(dos.AddDays(120)).Check(claim, 90);

            Assert.IsTrue(errors.Any(e => e.Contains("ICD-10 but the date of service requires ICD-9")));
            Assert.IsTrue(errors.Any(e => e.Contains("diagnosis pointer 2")));
            Assert.IsTrue(errors.Any(e => e.Contains("modifier")));
            Assert.IsTrue(errors.Any(e => e.Contains("Member id")));
            Assert.IsTrue(errors.Any(e => e.Contains("Rendering provider NPI")));
            Assert.IsTrue(errors.Any(e => e.Contains("Timely filing")));
        }

        [TestMethod]
        public void PreventiveVisitNeedsRoutineExamDiagnosis()
        {
            DateTime dos = new DateTime(2016, 2, 1);
            ClaimData claim = SampleClaim("C200102", dos);
            claim.Lines[0].CptCode = "99396";

            Assert.IsTrue(new ClaimScrubber(dos).Check(claim, 90).Any(e => e.Contains("preventive")));
            claim.Diagnoses[0].Code = "Z00.00";
            Assert.IsFalse(new ClaimScrubber(dos).Check(claim, 90).Any(e => e.Contains("preventive")));
        }

        [TestMethod]
        public void PatientShareEstimate()
        {
            Assert.AreEqual(20m, FeeCalculator.EstimatePatientShare(85m, 20m, true, PayerType.Commercial));
            Assert.AreEqual(0m, FeeCalculator.EstimatePatientShare(85m, 20m, false, PayerType.Commercial));
            Assert.AreEqual(10m, FeeCalculator.EstimatePatientShare(10m, 20m, true, PayerType.Commercial));
            Assert.AreEqual(59.50m, FeeCalculator.EstimatePatientShare(85m, 0m, true, PayerType.SelfPay));
        }

        [TestMethod]
        public void DunningLevelRisesWhileUnpaidAndStopsAtCollections()
        {
            var policy = new StatementPolicy(25, 3);

            Assert.AreEqual(0, policy.NextDunningLevel(new StatementData { Balance = 80m }));
            Assert.AreEqual(1, policy.NextDunningLevel(new StatementData { Balance = 80m, PreviousAmountDue = 80m, PreviousDunningLevel = 0 }));
            Assert.AreEqual(0, policy.NextDunningLevel(new StatementData { Balance = 40m, PreviousAmountDue = 80m, PreviousDunningLevel = 1 }));
            Assert.AreEqual(3, policy.NextDunningLevel(new StatementData { Balance = 95m, PreviousAmountDue = 80m, PreviousDunningLevel = 3 }));
            StringAssert.StartsWith(policy.Message(3), "FINAL NOTICE");
            Assert.AreEqual(new DateTime(2013, 4, 6), policy.DueDate(new DateTime(2013, 3, 12)));
        }

        [TestMethod]
        public void MoneyFormatsForX12()
        {
            Assert.AreEqual("155", Money.ToX12(155.00m));
            Assert.AreEqual("12.5", Money.ToX12(12.50m));
            Assert.AreEqual("0.07", Money.ToX12(0.07m));
        }
    }
}
