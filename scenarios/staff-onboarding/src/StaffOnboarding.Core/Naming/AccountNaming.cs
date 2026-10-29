using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using StaffOnboarding.Core.Requests;

namespace StaffOnboarding.Core.Naming
{
    /// <summary>
    /// Account naming standard IT-STD-07: firstname.lastname in lower case ASCII,
    /// at most 20 characters, external staff prefixed with "ext-".
    /// </summary>
    public class AccountNaming
    {
        public const int MaxSamAccountNameLength = 20;
        public const string ExternalPrefix = "ext-";

        private static readonly HashSet<string> Reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "administrator", "admin", "guest", "krbtgt", "root", "system", "service", "helpdesk", "support", "test"
        };

        private static readonly Dictionary<char, string> Transliterations = new Dictionary<char, string>
        {
            { 'ß', "ss" }, { 'æ', "ae" }, { 'ø', "o" }, { 'œ', "oe" }, { 'đ', "d" }, { 'ł', "l" },
            { 'þ', "th" }, { 'ð', "d" }, { 'ı', "i" }
        };

        private readonly string _upnSuffix;
        private readonly string _mailDomain;

        public AccountNaming(string upnSuffix, string mailDomain)
        {
            if (string.IsNullOrWhiteSpace(upnSuffix)) throw new ArgumentException("UPN suffix is required.", "upnSuffix");
            if (string.IsNullOrWhiteSpace(mailDomain)) throw new ArgumentException("Mail domain is required.", "mailDomain");
            _upnSuffix = upnSuffix.TrimStart('@');
            _mailDomain = mailDomain.TrimStart('@');
        }

        /// <summary>Picks the first free account name. <paramref name="isTaken"/> checks the directory.</summary>
        public AccountName Generate(PersonDetails person, EmploymentType employmentType, Func<string, bool> isTaken)
        {
            if (person == null) throw new ArgumentNullException("person");
            if (isTaken == null) throw new ArgumentNullException("isTaken");

            var first = Normalize(FirstToken(person.DisplayFirstName));
            var last = Normalize(person.LastName);
            if (first.Length == 0 || last.Length == 0)
                throw new ArgumentException("First and last name must contain at least one letter.");

            var prefix = IsExternal(employmentType) ? ExternalPrefix : string.Empty;

            foreach (var candidate in Candidates(prefix, first, last))
            {
                if (Reserved.Contains(candidate)) continue;
                if (isTaken(candidate)) continue;

                return new AccountName(candidate, UpnFor(candidate), MailFor(candidate));
            }

            throw new InvalidOperationException("No free account name for " + person.DisplayName + ".");
        }

        public string UpnFor(string samAccountName)
        {
            return samAccountName + "@" + _upnSuffix;
        }

        /// <summary>External staff get firstname.lastname.ext as mail alias.</summary>
        public string MailFor(string samAccountName)
        {
            var local = samAccountName.StartsWith(ExternalPrefix, StringComparison.Ordinal)
                ? samAccountName.Substring(ExternalPrefix.Length) + ".ext"
                : samAccountName;
            return local + "@" + _mailDomain;
        }

        public static bool IsExternal(EmploymentType employmentType)
        {
            return employmentType == EmploymentType.Contractor;
        }

        internal static IEnumerable<string> Candidates(string prefix, string first, string last)
        {
            yield return Fit(prefix, first, last, string.Empty);
            yield return Fit(prefix, first.Substring(0, 1), last, string.Empty);
            for (var n = 2; n <= 99; n++)
                yield return Fit(prefix, first, last, n.ToString(CultureInfo.InvariantCulture));
        }

        private static string Fit(string prefix, string first, string last, string suffix)
        {
            var budget = MaxSamAccountNameLength - prefix.Length - suffix.Length - 1;
            if (first.Length + last.Length > budget)
            {
                var lastLength = Math.Max(budget - first.Length, Math.Min(last.Length, budget / 2));
                last = last.Substring(0, Math.Min(last.Length, lastLength));
                var firstLength = budget - last.Length;
                first = first.Substring(0, Math.Min(first.Length, firstLength));
            }
            return prefix + first + "." + last + suffix;
        }

        private static string FirstToken(string name)
        {
            if (name == null) return string.Empty;
            var parts = name.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 0 ? string.Empty : parts[0];
        }

        public static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var lower = value.Trim().ToLowerInvariant();
            var sb = new StringBuilder(lower.Length);
            foreach (var ch in lower)
            {
                string mapped;
                if (Transliterations.TryGetValue(ch, out mapped)) sb.Append(mapped);
                else sb.Append(ch);
            }

            var decomposed = sb.ToString().Normalize(NormalizationForm.FormD);
            var result = new StringBuilder(decomposed.Length);
            foreach (var ch in decomposed.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark))
            {
                if (ch >= 'a' && ch <= 'z') result.Append(ch);
            }
            return result.ToString();
        }
    }

    public class AccountName
    {
        public AccountName(string samAccountName, string userPrincipalName, string mail)
        {
            SamAccountName = samAccountName;
            UserPrincipalName = userPrincipalName;
            Mail = mail;
        }

        public string SamAccountName { get; private set; }
        public string UserPrincipalName { get; private set; }
        public string Mail { get; private set; }
    }
}
