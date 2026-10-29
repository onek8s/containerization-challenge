using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Lucerne.ProjectSites.Domain
{
    /// <summary>
    /// Engineering document number: project-originator-discipline-type-sequence, e.g. P19041-LUC-STR-DWG-0012.
    /// Introduced with the 2018 numbering standard (LE-QP-07 rev C); older sites used free text numbers,
    /// which are left as they are.
    /// </summary>
    public sealed class DocumentNumber : IEquatable<DocumentNumber>
    {
        public const string DefaultOriginator = "LUC";

        private static readonly Regex Pattern = new Regex(
            @"^(?<project>P\d{5})-(?<orig>[A-Z]{3})-(?<disc>[A-Z]{3})-(?<type>[A-Z]{2,4})-(?<seq>\d{4})$",
            RegexOptions.Compiled);

        public static readonly string[] DocumentTypes =
        {
            "DWG", // drawing
            "CAL", // calculation
            "RPT", // report
            "SPC", // specification
            "MOD", // model
            "SCH", // schedule
            "COR", // correspondence
            "MIN", // minutes
            "TRN"  // transmittal
        };

        public DocumentNumber(string project, string originator, string discipline, string type, int sequence)
        {
            if (!Project.IsValidNumber(project))
            {
                throw new ArgumentException("Invalid project number.", nameof(project));
            }

            if (sequence < 1 || sequence > 9999)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence));
            }

            ProjectNumber = project;
            Originator = originator;
            Discipline = discipline;
            Type = type;
            Sequence = sequence;
        }

        public string ProjectNumber { get; }
        public string Originator { get; }
        public string Discipline { get; }
        public string Type { get; }
        public int Sequence { get; }

        /// <summary>Key of the counter the sequence is drawn from (one counter per discipline and type).</summary>
        public string CounterKey
        {
            get { return CounterKeyFor(Discipline, Type); }
        }

        public static string CounterKeyFor(string discipline, string type)
        {
            return "lucerne.counter." + discipline + "." + type;
        }

        public static bool TryParse(string text, out DocumentNumber number)
        {
            number = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var m = Pattern.Match(text.Trim().ToUpperInvariant());
            if (!m.Success)
            {
                return false;
            }

            var type = m.Groups["type"].Value;
            if (Array.IndexOf(DocumentTypes, type) < 0 || Domain.Discipline.Find(m.Groups["disc"].Value) == null)
            {
                return false;
            }

            number = new DocumentNumber(
                m.Groups["project"].Value,
                m.Groups["orig"].Value,
                m.Groups["disc"].Value,
                type,
                int.Parse(m.Groups["seq"].Value, CultureInfo.InvariantCulture));
            return true;
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}-{1}-{2}-{3}-{4:0000}", ProjectNumber, Originator, Discipline, Type, Sequence);
        }

        public bool Equals(DocumentNumber other)
        {
            return other != null && string.Equals(ToString(), other.ToString(), StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as DocumentNumber);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(ToString());
        }
    }

    /// <summary>
    /// Revision codes: P01..P99 preliminary (shared for coordination), C01..C99 contractual (issued).
    /// A document never goes back from C to P, and numbers only increase.
    /// </summary>
    public static class RevisionRules
    {
        private static readonly Regex Pattern = new Regex(@"^(?<kind>[PC])(?<n>\d{2})$", RegexOptions.Compiled);

        public const string First = "P01";

        public static bool IsValid(string revision)
        {
            return revision != null && Pattern.IsMatch(revision);
        }

        public static bool IsContractual(string revision)
        {
            return IsValid(revision) && revision[0] == 'C';
        }

        public static string Next(string current, bool contractual)
        {
            if (string.IsNullOrEmpty(current))
            {
                return contractual ? "C01" : First;
            }

            var m = Pattern.Match(current);
            if (!m.Success)
            {
                throw new ArgumentException("Invalid revision: " + current, nameof(current));
            }

            var kind = m.Groups["kind"].Value[0];
            var n = int.Parse(m.Groups["n"].Value, CultureInfo.InvariantCulture);

            if (contractual && kind == 'P')
            {
                return "C01";
            }

            if (!contractual && kind == 'C')
            {
                throw new InvalidOperationException("A contractual revision cannot be followed by a preliminary one.");
            }

            if (n >= 99)
            {
                throw new InvalidOperationException("Revision counter exhausted for " + current);
            }

            return string.Format(CultureInfo.InvariantCulture, "{0}{1:00}", kind, n + 1);
        }

        /// <summary>Returns null when the change is allowed, otherwise the reason it is refused.</summary>
        public static string ValidateChange(string before, string after)
        {
            if (string.Equals(before, after, StringComparison.Ordinal))
            {
                return null;
            }

            if (!IsValid(after))
            {
                return "Revision must be P01-P99 or C01-C99.";
            }

            if (string.IsNullOrEmpty(before) || !IsValid(before))
            {
                return null;
            }

            if (before[0] == 'C' && after[0] == 'P')
            {
                return "A contractual revision cannot be changed back to a preliminary revision.";
            }

            if (before[0] == after[0] && string.CompareOrdinal(after, before) < 0)
            {
                return "Revision numbers can only increase.";
            }

            return null;
        }
    }
}
