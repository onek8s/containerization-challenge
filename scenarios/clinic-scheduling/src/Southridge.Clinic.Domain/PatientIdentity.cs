using System;
using System.Globalization;
using System.Text;

namespace Southridge.Clinic.Domain
{
    /// <summary>Name, age and matching helpers used by registration, the kiosk and the interface engine.</summary>
    public static class PatientIdentity
    {
        public static int AgeOn(DateTime dateOfBirth, DateTime onDate)
        {
            int age = onDate.Year - dateOfBirth.Year;
            if (onDate.Month < dateOfBirth.Month || (onDate.Month == dateOfBirth.Month && onDate.Day < dateOfBirth.Day))
            {
                age--;
            }

            return age;
        }

        public static bool IsMinor(DateTime dateOfBirth, DateTime onDate)
        {
            return AgeOn(dateOfBirth, onDate) < 18;
        }

        public static string DisplayName(string firstName, string lastName)
        {
            return (lastName ?? string.Empty).Trim() + ", " + (firstName ?? string.Empty).Trim();
        }

        /// <summary>Upper case, letters only, used for duplicate checks and HL7 matching.</summary>
        public static string MatchKey(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            string decomposed = name.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (char c in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                if (char.IsLetter(c))
                {
                    sb.Append(char.ToUpperInvariant(c));
                }
            }

            return sb.ToString();
        }

        public static bool LikelySamePerson(string lastName1, string firstName1, DateTime dob1, string lastName2, string firstName2, DateTime dob2)
        {
            if (dob1.Date != dob2.Date)
            {
                return false;
            }

            if (MatchKey(lastName1) != MatchKey(lastName2))
            {
                return false;
            }

            string f1 = MatchKey(firstName1);
            string f2 = MatchKey(firstName2);
            return f1 == f2 || (f1.Length > 0 && f2.Length > 0 && f1[0] == f2[0] && (f1.StartsWith(f2) || f2.StartsWith(f1)));
        }

        public static string SexCode(Sex sex)
        {
            switch (sex)
            {
                case Sex.Female:
                    return "F";
                case Sex.Male:
                    return "M";
                default:
                    return "U";
            }
        }

        public static Sex ParseSex(string code)
        {
            switch ((code ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "F":
                    return Sex.Female;
                case "M":
                    return Sex.Male;
                default:
                    return Sex.Unknown;
            }
        }
    }
}
