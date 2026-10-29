using System;
using System.Text.RegularExpressions;

namespace Southridge.Clinic.Domain
{
    public enum DiagnosisCodeSet
    {
        Icd9 = 9,
        Icd10 = 10
    }

    /// <summary>ICD-9-CM / ICD-10-CM handling. Dates of service on or after the transition date use ICD-10-CM.</summary>
    public static class DiagnosisCodes
    {
        public static readonly DateTime Icd10TransitionDate = new DateTime(2015, 10, 1);

        private static readonly Regex Icd9 = new Regex(@"^(V\d{2}(\.\d{1,2})?|E\d{3}(\.\d)?|\d{3}(\.\d{1,2})?)$", RegexOptions.Compiled);
        private static readonly Regex Icd10 = new Regex(@"^[A-TV-Z]\d[0-9A-Z](\.[0-9A-Z]{1,4})?$", RegexOptions.Compiled);

        public static DiagnosisCodeSet CodeSetFor(DateTime dateOfService)
        {
            return dateOfService.Date >= Icd10TransitionDate ? DiagnosisCodeSet.Icd10 : DiagnosisCodeSet.Icd9;
        }

        public static bool IsValid(string code, DiagnosisCodeSet codeSet)
        {
            if (string.IsNullOrEmpty(code))
            {
                return false;
            }

            code = code.Trim().ToUpperInvariant();
            return codeSet == DiagnosisCodeSet.Icd10 ? Icd10.IsMatch(code) : Icd9.IsMatch(code);
        }

        /// <summary>Claims carry diagnosis codes without the decimal point.</summary>
        public static string ToClaimFormat(string code)
        {
            return code == null ? null : code.Trim().ToUpperInvariant().Replace(".", string.Empty);
        }

        public static string FromClaimFormat(string code, DiagnosisCodeSet codeSet)
        {
            if (string.IsNullOrEmpty(code) || code.Length <= 3 || code.Contains("."))
            {
                return code;
            }

            int split = codeSet == DiagnosisCodeSet.Icd9 && code[0] == 'E' ? 4 : 3;
            return code.Substring(0, split) + "." + code.Substring(split);
        }
    }

    public static class ProcedureCodes
    {
        private static readonly Regex Cpt = new Regex(@"^\d{4}[0-9FTU]$", RegexOptions.Compiled);
        private static readonly Regex Hcpcs = new Regex(@"^[A-V]\d{4}$", RegexOptions.Compiled);
        private static readonly Regex ModifierPattern = new Regex(@"^[0-9A-Z]{2}$", RegexOptions.Compiled);

        public static bool IsValid(string code)
        {
            if (string.IsNullOrEmpty(code))
            {
                return false;
            }

            code = code.Trim().ToUpperInvariant();
            return Cpt.IsMatch(code) || Hcpcs.IsMatch(code);
        }

        public static bool IsValidModifier(string modifier)
        {
            return string.IsNullOrEmpty(modifier) || ModifierPattern.IsMatch(modifier.Trim().ToUpperInvariant());
        }

        /// <summary>Office visit evaluation and management codes (new 99201-99205, established 99211-99215).</summary>
        public static bool IsOfficeVisit(string code)
        {
            int value;
            if (code == null || !int.TryParse(code, out value))
            {
                return false;
            }

            return (value >= 99201 && value <= 99205) || (value >= 99211 && value <= 99215);
        }

        public static bool IsPreventive(string code)
        {
            int value;
            if (code == null || !int.TryParse(code, out value))
            {
                return false;
            }

            return value >= 99381 && value <= 99397;
        }
    }
}
