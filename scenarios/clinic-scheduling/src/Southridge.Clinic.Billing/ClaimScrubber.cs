using System;
using System.Collections.Generic;
using System.Linq;
using Southridge.Clinic.Data;
using Southridge.Clinic.Domain;

namespace Southridge.Clinic.Billing
{
    /// <summary>Front-end edits run before a claim goes to the clearinghouse. Failed claims stay in the work queue.</summary>
    public class ClaimScrubber
    {
        private readonly DateTime _today;

        public ClaimScrubber(DateTime today)
        {
            _today = today;
        }

        public IList<string> Check(ClaimData claim, int timelyFilingDays)
        {
            var errors = new List<string>();
            if (claim.Diagnoses.Count == 0)
            {
                errors.Add("No diagnosis on the encounter.");
            }

            DiagnosisCodeSet expected = DiagnosisCodes.CodeSetFor(claim.DateOfService);
            foreach (ClaimDiagnosis d in claim.Diagnoses)
            {
                if (d.CodeSet != (int)expected)
                {
                    errors.Add(string.Format("Diagnosis {0} is ICD-{1} but the date of service requires ICD-{2}.", d.Code, d.CodeSet, (int)expected));
                }
                else if (!DiagnosisCodes.IsValid(d.Code, expected))
                {
                    errors.Add("Diagnosis code " + d.Code + " is not valid.");
                }
            }

            if (claim.Lines.Count == 0)
            {
                errors.Add("No charge lines.");
            }

            if (claim.Lines.Count > 50)
            {
                errors.Add("More than 50 service lines; split the claim.");
            }

            foreach (ClaimLine line in claim.Lines)
            {
                if (!ProcedureCodes.IsValid(line.CptCode))
                {
                    errors.Add("Line " + line.LineNumber + ": procedure code " + line.CptCode + " is not valid.");
                }

                if (!ProcedureCodes.IsValidModifier(line.Modifier1) || !ProcedureCodes.IsValidModifier(line.Modifier2))
                {
                    errors.Add("Line " + line.LineNumber + ": modifier is not valid.");
                }

                if (line.Units <= 0 || line.Amount <= 0)
                {
                    errors.Add("Line " + line.LineNumber + ": units and amount must be positive.");
                }

                foreach (char p in line.DiagnosisPointers ?? string.Empty)
                {
                    if (!char.IsDigit(p) || p - '0' < 1 || p - '0' > Math.Min(4, claim.Diagnoses.Count))
                    {
                        errors.Add("Line " + line.LineNumber + ": diagnosis pointer " + p + " does not point to a diagnosis.");
                    }
                }

                if (ProcedureCodes.IsPreventive(line.CptCode) && !claim.Diagnoses.Any(d => d.Code.StartsWith("Z00") || d.Code.StartsWith("V70") || d.Code.StartsWith("V20")))
                {
                    errors.Add("Line " + line.LineNumber + ": preventive visit without a routine examination diagnosis.");
                }
            }

            if (!Npi.IsValid(claim.RenderingNpi))
            {
                errors.Add("Rendering provider NPI " + claim.RenderingNpi + " is not valid.");
            }

            if (!Npi.IsValid(claim.GroupNpi))
            {
                errors.Add("Billing provider NPI is not valid.");
            }

            if (string.IsNullOrEmpty(claim.MemberId))
            {
                errors.Add("Member id is missing.");
            }

            if (claim.Relationship != (byte)InsuredRelationship.Self && string.IsNullOrEmpty(claim.SubscriberLastName))
            {
                errors.Add("Subscriber name is required when the patient is not the subscriber.");
            }

            if (string.IsNullOrEmpty(claim.PatientAddress1) || string.IsNullOrEmpty(claim.PatientZip))
            {
                errors.Add("Patient address is incomplete.");
            }

            if (timelyFilingDays > 0 && (_today - claim.DateOfService).TotalDays > timelyFilingDays)
            {
                errors.Add(string.Format("Timely filing limit of {0} days has passed.", timelyFilingDays));
            }

            return errors;
        }
    }
}
