using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using VanArsdel.Integration.Edi.Definitions;

namespace VanArsdel.Integration.Edi.Validation
{
    /// <summary>Element level checks: mandatory, length, numeric/date/time format and code lists.</summary>
    public static class SegmentValidator
    {
        public static void Validate(SegmentDef def, Segment segment, IList<EdiError> errors)
        {
            if (segment.ElementCount > def.Elements.Length)
            {
                var extra = segment.Elements.Skip(def.Elements.Length).Any(e => e.Any(v => !string.IsNullOrEmpty(v)));
                if (extra)
                {
                    errors.Add(new EdiError(def.Tag, segment.Position, def.Elements.Length + 1, EdiErrorCodes.TooManyElements, def.Tag + " has more than " + def.Elements.Length + " elements."));
                }
            }

            for (var i = 0; i < def.Elements.Length; i++)
            {
                var ed = def.Elements[i];
                var values = i < segment.ElementCount ? segment.Elements[i] : new string[0];
                if (ed.IsComposite)
                {
                    var empty = values.All(string.IsNullOrEmpty);
                    if (empty)
                    {
                        if (ed.Mandatory)
                        {
                            errors.Add(Error(def, segment, i, EdiErrorCodes.MandatoryElementMissing, "Mandatory composite " + ed.Name + " is missing.", null));
                        }

                        continue;
                    }

                    for (var c = 0; c < ed.Components.Length; c++)
                    {
                        CheckValue(def, segment, i, ed.Components[c], c < values.Length ? values[c] : null, errors);
                    }
                }
                else
                {
                    CheckValue(def, segment, i, ed, values.Length > 0 ? values[0] : null, errors);
                }
            }
        }

        private static void CheckValue(SegmentDef def, Segment segment, int index, ElementDef ed, string value, IList<EdiError> errors)
        {
            if (string.IsNullOrEmpty(value) || (def.Tag == "ISA" && value.Trim().Length == 0))
            {
                if (ed.Mandatory)
                {
                    errors.Add(Error(def, segment, index, EdiErrorCodes.MandatoryElementMissing, "Mandatory element " + ed.Name + " is missing.", null));
                }

                return;
            }

            var length = ed.Type == ElementType.R || ed.Type == ElementType.N0 || ed.Type == ElementType.N2
                ? value.Count(char.IsDigit)
                : value.Length;
            if (length > ed.MaxLength)
            {
                errors.Add(Error(def, segment, index, EdiErrorCodes.TooLong, ed.Name + " is longer than " + ed.MaxLength + ".", value));
            }
            else if (length < ed.MinLength)
            {
                errors.Add(Error(def, segment, index, EdiErrorCodes.TooShort, ed.Name + " is shorter than " + ed.MinLength + ".", value));
            }

            switch (ed.Type)
            {
                case ElementType.N0:
                case ElementType.N2:
                    long n;
                    if (!long.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out n))
                    {
                        errors.Add(Error(def, segment, index, EdiErrorCodes.InvalidCharacter, ed.Name + " must be numeric.", value));
                    }

                    break;
                case ElementType.R:
                    decimal r;
                    if (!decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out r))
                    {
                        errors.Add(Error(def, segment, index, EdiErrorCodes.InvalidCharacter, ed.Name + " must be a decimal number.", value));
                    }

                    break;
                case ElementType.DT:
                    if (!Domain.Codes.EdiDate.ParseX12Date(value).HasValue)
                    {
                        errors.Add(Error(def, segment, index, EdiErrorCodes.InvalidDate, ed.Name + " is not a valid date.", value));
                    }

                    break;
                case ElementType.TM:
                    if (!Domain.Codes.EdiDate.ParseX12Time(value).HasValue)
                    {
                        errors.Add(Error(def, segment, index, EdiErrorCodes.InvalidTime, ed.Name + " is not a valid time.", value));
                    }

                    break;
                case ElementType.ID:
                    if (ed.Codes != null && !ed.Codes.Contains(value))
                    {
                        errors.Add(Error(def, segment, index, EdiErrorCodes.InvalidCode, ed.Name + " code '" + value + "' is not allowed.", value));
                    }

                    break;
            }
        }

        private static EdiError Error(SegmentDef def, Segment segment, int index, string code, string text, string bad)
        {
            return new EdiError(def.Tag, segment.Position, index + 1, code, text) { BadValue = bad };
        }
    }
}
