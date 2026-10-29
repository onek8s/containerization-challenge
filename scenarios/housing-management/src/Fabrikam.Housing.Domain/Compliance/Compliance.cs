using System;
using System.Collections.Generic;
using System.Linq;
using Fabrikam.Housing.Assets;
using Fabrikam.Housing.Common;

namespace Fabrikam.Housing.Compliance
{
    public enum ComplianceType
    {
        GasSafety = 1,
        Electrical = 2,
        FireRiskAssessment = 3,
        Legionella = 4,
        Asbestos = 5,
        LiftInspection = 6,
        SmokeAndCoAlarms = 7
    }

    public enum CertificateOutcome
    {
        Satisfactory = 1,
        Unsatisfactory = 2,
        /// <summary>EICR code C1/C2 or gas "at risk": remedial work raised.</summary>
        RemedialRequired = 3
    }

    public enum ComplianceStatus
    {
        Compliant = 1,
        DueSoon = 2,
        Overdue = 3,
        NoCertificate = 4,
        NotApplicable = 5
    }

    public class ComplianceCertificate : AuditedEntity
    {
        public int? PropertyId { get; set; }
        public Property Property { get; set; }
        /// <summary>For communal certificates (fire risk, lifts, legionella) the estate instead of a property.</summary>
        public int? EstateId { get; set; }
        public ComplianceType Type { get; set; }
        public string CertificateNumber { get; set; }
        public DateTime InspectedOn { get; set; }
        public DateTime ExpiresOn { get; set; }
        public CertificateOutcome Outcome { get; set; }
        public string EngineerName { get; set; }
        /// <summary>Gas Safe licence or NICEIC/NAPIT registration.</summary>
        public string EngineerRegistration { get; set; }
        public int? ContractorId { get; set; }
        public int? DocumentId { get; set; }
        public string Observations { get; set; }
        public int? RemedialWorkOrderId { get; set; }
        public bool Superseded { get; set; }
    }

    public class ComplianceReminder : Entity
    {
        public int PropertyId { get; set; }
        public ComplianceType Type { get; set; }
        public DateTime DueDate { get; set; }
        public int Stage { get; set; }
        public DateTime SentAt { get; set; }
        public string Channel { get; set; }
    }

    public static class ComplianceRules
    {
        public const int DueSoonDays = 60;

        /// <summary>Validity of a satisfactory certificate.</summary>
        public static DateTime ExpiryFor(ComplianceType type, DateTime inspectedOn)
        {
            switch (type)
            {
                case ComplianceType.GasSafety: return inspectedOn.AddYears(1).AddDays(-1);
                case ComplianceType.Electrical: return inspectedOn.AddYears(5).AddDays(-1);
                case ComplianceType.FireRiskAssessment: return inspectedOn.AddYears(1).AddDays(-1);
                case ComplianceType.Legionella: return inspectedOn.AddYears(2).AddDays(-1);
                case ComplianceType.Asbestos: return inspectedOn.AddYears(1).AddDays(-1);
                case ComplianceType.LiftInspection: return inspectedOn.AddMonths(6).AddDays(-1);
                default: return inspectedOn.AddYears(1).AddDays(-1);
            }
        }

        public static bool AppliesTo(ComplianceType type, Property property)
        {
            switch (type)
            {
                case ComplianceType.GasSafety: return property.HasGasSupply;
                case ComplianceType.Asbestos: return property.AsbestosPresent;
                case ComplianceType.Electrical:
                case ComplianceType.SmokeAndCoAlarms: return true;
                default: return false; // communal: recorded against the estate
            }
        }

        public static ComplianceStatus StatusOf(ComplianceCertificate latest, bool applies, DateTime today)
        {
            if (!applies) return ComplianceStatus.NotApplicable;
            if (latest == null) return ComplianceStatus.NoCertificate;
            if (latest.ExpiresOn.Date < today.Date) return ComplianceStatus.Overdue;
            if ((latest.ExpiresOn.Date - today.Date).TotalDays <= DueSoonDays) return ComplianceStatus.DueSoon;
            return ComplianceStatus.Compliant;
        }

        /// <summary>
        /// Gas servicing access letters: first at 56 days before expiry, second at 28 days, final (with the
        /// tenancy clause about access) at 14 days. Returns the stage that should have been sent by today.
        /// </summary>
        public static int ReminderStageDue(DateTime expiresOn, DateTime today)
        {
            var days = (expiresOn.Date - today.Date).TotalDays;
            if (days <= 14) return 3;
            if (days <= 28) return 2;
            if (days <= 56) return 1;
            return 0;
        }

        public static ComplianceCertificate Latest(IEnumerable<ComplianceCertificate> certificates, ComplianceType type)
        {
            return certificates.Where(c => c.Type == type && !c.Superseded).OrderByDescending(c => c.InspectedOn).FirstOrDefault();
        }

        public static void ValidateGasSafeNumber(string number)
        {
            if (string.IsNullOrWhiteSpace(number) || number.Length != 6 || !number.All(char.IsDigit))
                throw new BusinessRuleException("GAS_SAFE", "Gas Safe registration numbers have six digits.");
        }
    }
}
