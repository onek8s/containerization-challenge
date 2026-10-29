using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CreditDesk.Core
{
    /// <summary>
    /// Maps a decision to the bookmark values of the confirmation letter template.
    /// Bookmark names are fixed by the template maintained by Credit Control.
    /// </summary>
    public static class LetterFields
    {
        public static readonly string[] Bookmarks =
        {
            "LetterDate", "Reference", "CustomerName", "ContactName", "AddressBlock",
            "AccountNumber", "PreviousLimit", "NewLimit", "ValidUntil", "DecisionText",
            "SignedBy", "SignedByTitle"
        };

        public static IDictionary<string, string> Build(LetterMessage m, DateTime letterDate)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            var culture = CultureFor(m.Language);
            var values = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["LetterDate"] = letterDate.ToString("D", culture),
                ["Reference"] = Reference(m),
                ["CustomerName"] = m.CustomerName ?? "",
                ["ContactName"] = string.IsNullOrWhiteSpace(m.ContactName) ? DefaultSalutation(m.Language) : m.ContactName,
                ["AddressBlock"] = AddressBlock(m),
                ["AccountNumber"] = m.AccountNumber ?? "",
                ["PreviousLimit"] = m.PreviousLimit.ToString("N2", culture),
                ["NewLimit"] = (m.Approved ? m.NewLimit : m.PreviousLimit).ToString("N2", culture),
                ["ValidUntil"] = m.Approved && m.ValidUntil.HasValue ? m.ValidUntil.Value.ToString("d", culture) : "",
                ["DecisionText"] = DecisionText(m),
                ["SignedBy"] = m.SignedBy ?? "",
                ["SignedByTitle"] = m.SignedByTitle ?? ""
            };
            return values;
        }

        public static string Reference(LetterMessage m) =>
            string.Format(CultureInfo.InvariantCulture, "CD-{0:yyyy}-{1:D6}", m.DecidedAt, m.RequestId);

        /// <summary>File name used for the PDF copy on the letters share.</summary>
        public static string FileName(LetterMessage m)
        {
            var name = new StringBuilder();
            foreach (var c in m.AccountNumber ?? "")
                name.Append(char.IsLetterOrDigit(c) ? c : '_');
            return string.Format(CultureInfo.InvariantCulture, "{0}_{1}_{2:yyyyMMdd}.pdf", name, Reference(m), m.DecidedAt);
        }

        public static string AddressBlock(LetterMessage m)
        {
            var lines = new List<string>();
            void Add(string s) { if (!string.IsNullOrWhiteSpace(s)) lines.Add(s.Trim()); }
            Add(m.CustomerName);
            Add(m.AddressLine1);
            Add(m.AddressLine2);
            Add(((m.PostalCode ?? "") + " " + (m.City ?? "")).Trim());
            if (!string.IsNullOrWhiteSpace(m.CountryCode) && !string.Equals(m.CountryCode, "US", StringComparison.OrdinalIgnoreCase))
                Add(m.CountryCode.ToUpperInvariant());
            // Word uses vertical tab as a manual line break inside a paragraph.
            return string.Join("\v", lines);
        }

        private static string DecisionText(LetterMessage m)
        {
            var culture = CultureFor(m.Language);
            if (m.Approved && m.NewLimit < m.PreviousLimit)
                return string.Format(culture, "Following our review of your account, your credit limit has been adjusted to {0:N2}.", m.NewLimit);
            if (m.Approved)
                return string.Format(culture, "We are pleased to confirm that your credit limit has been increased to {0:N2}.", m.NewLimit);
            var text = "After careful review we are unable to increase your credit limit at this time.";
            if (!string.IsNullOrWhiteSpace(m.DecisionComment))
                text += " " + m.DecisionComment.Trim();
            return text;
        }

        private static string DefaultSalutation(string language) => "Accounts Payable";

        private static CultureInfo CultureFor(string language)
        {
            switch ((language ?? "en").ToLowerInvariant())
            {
                case "en-gb": return CultureInfo.GetCultureInfo("en-GB");
                default: return CultureInfo.GetCultureInfo("en-US");
            }
        }
    }
}
