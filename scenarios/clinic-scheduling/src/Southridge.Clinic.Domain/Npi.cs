namespace Southridge.Clinic.Domain
{
    /// <summary>National Provider Identifier check (Luhn over the 80840 prefixed number).</summary>
    public static class Npi
    {
        public static bool IsValid(string npi)
        {
            if (npi == null)
            {
                return false;
            }

            npi = npi.Trim();
            if (npi.Length != 10)
            {
                return false;
            }

            foreach (char c in npi)
            {
                if (!char.IsDigit(c))
                {
                    return false;
                }
            }

            if (npi[0] != '1' && npi[0] != '2')
            {
                return false;
            }

            string withPrefix = "80840" + npi.Substring(0, 9);
            return MedicalRecordNumber.CheckDigit(withPrefix) == npi[9] - '0';
        }

        public static string WithCheckDigit(string firstNine)
        {
            return firstNine + MedicalRecordNumber.CheckDigit("80840" + firstNine);
        }
    }
}
