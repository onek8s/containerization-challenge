using System.Collections.Generic;
using System.Text;

namespace Southridge.Clinic.Hl7
{
    /// <summary>One segment, kept as raw (escaped) field strings. Values are unescaped on read and escaped on write.</summary>
    public class Hl7Segment
    {
        private readonly string _name;
        private readonly Hl7Encoding _encoding;
        private readonly List<string> _fields = new List<string>();

        public Hl7Segment(string name, Hl7Encoding encoding)
        {
            _name = name;
            _encoding = encoding;
            _fields.Add(name);
        }

        public string Name
        {
            get { return _name; }
        }

        public int FieldCount
        {
            get { return _fields.Count - 1; }
        }

        private bool IsMsh
        {
            get { return _name == "MSH" || _name == "FHS" || _name == "BHS"; }
        }

        internal static Hl7Segment Parse(string line, Hl7Encoding encoding)
        {
            string[] parts = line.Split(encoding.FieldSeparator);
            var segment = new Hl7Segment(parts[0], encoding);
            if (segment.IsMsh)
            {
                // MSH-1 is the field separator itself, MSH-2 the encoding characters.
                segment._fields.Add(encoding.FieldSeparator.ToString());
                for (int i = 1; i < parts.Length; i++)
                {
                    segment._fields.Add(parts[i]);
                }
            }
            else
            {
                for (int i = 1; i < parts.Length; i++)
                {
                    segment._fields.Add(parts[i]);
                }
            }

            return segment;
        }

        /// <summary>Raw field value (escaped, with all repetitions and components).</summary>
        public string GetRawField(int field)
        {
            return field < _fields.Count ? _fields[field] : string.Empty;
        }

        public void SetField(int field, string raw)
        {
            while (_fields.Count <= field)
            {
                _fields.Add(string.Empty);
            }

            _fields[field] = raw ?? string.Empty;
        }

        public int RepetitionCount(int field)
        {
            string raw = GetRawField(field);
            return raw.Length == 0 ? 0 : raw.Split(_encoding.RepetitionSeparator).Length;
        }

        public string Get(int field)
        {
            return Get(field, 1, 0, 0);
        }

        public string Get(int field, int component)
        {
            return Get(field, 1, component, 0);
        }

        public string Get(int field, int repetition, int component, int subComponent)
        {
            string raw = GetRawField(field);
            if (field <= 2 && IsMsh)
            {
                return raw;
            }

            if (repetition > 0)
            {
                string[] reps = raw.Split(_encoding.RepetitionSeparator);
                raw = repetition <= reps.Length ? reps[repetition - 1] : string.Empty;
            }

            if (component > 0)
            {
                string[] comps = raw.Split(_encoding.ComponentSeparator);
                raw = component <= comps.Length ? comps[component - 1] : string.Empty;
                if (subComponent > 0)
                {
                    string[] subs = raw.Split(_encoding.SubComponentSeparator);
                    raw = subComponent <= subs.Length ? subs[subComponent - 1] : string.Empty;
                }
            }

            return _encoding.Unescape(raw);
        }

        public void Set(int field, int repetition, int component, int subComponent, string value)
        {
            string escaped = _encoding.Escape(value ?? string.Empty);
            if (field <= 2 && IsMsh)
            {
                SetField(field, value);
                return;
            }

            if (component == 0)
            {
                if (repetition <= 1 && RepetitionCount(field) <= 1)
                {
                    SetField(field, escaped);
                    return;
                }

                SetField(field, Replace(GetRawField(field), _encoding.RepetitionSeparator, repetition, escaped));
                return;
            }

            string raw = GetRawField(field);
            string[] reps = raw.Split(_encoding.RepetitionSeparator);
            int r = repetition < 1 ? 1 : repetition;
            string rep = r <= reps.Length ? reps[r - 1] : string.Empty;
            string comp = Split(rep, _encoding.ComponentSeparator, component);
            if (subComponent > 0)
            {
                comp = Replace(comp, _encoding.SubComponentSeparator, subComponent, escaped);
            }
            else
            {
                comp = escaped;
            }

            rep = Replace(rep, _encoding.ComponentSeparator, component, comp);
            SetField(field, Replace(raw, _encoding.RepetitionSeparator, r, rep));
        }

        public void AddRepetition(int field, string rawValue)
        {
            string raw = GetRawField(field);
            SetField(field, raw.Length == 0 ? rawValue : raw + _encoding.RepetitionSeparator + rawValue);
        }

        public override string ToString()
        {
            var sb = new StringBuilder(_name);
            int last = _fields.Count - 1;
            while (last > 0 && _fields[last].Length == 0)
            {
                last--;
            }

            int start = IsMsh ? 2 : 1;
            if (IsMsh)
            {
                sb.Append(_encoding.FieldSeparator);
            }

            for (int i = start; i <= last; i++)
            {
                if (i > start || !IsMsh)
                {
                    sb.Append(_encoding.FieldSeparator);
                }

                sb.Append(_fields[i]);
            }

            return sb.ToString();
        }

        private static string Split(string value, char separator, int index)
        {
            string[] parts = value.Split(separator);
            return index <= parts.Length ? parts[index - 1] : string.Empty;
        }

        private static string Replace(string value, char separator, int index, string replacement)
        {
            var parts = new List<string>(value.Length == 0 ? new string[0] : value.Split(separator));
            while (parts.Count < index)
            {
                parts.Add(string.Empty);
            }

            parts[index - 1] = replacement;
            int last = parts.Count - 1;
            while (last > 0 && parts[last].Length == 0)
            {
                last--;
            }

            return string.Join(separator.ToString(), parts.GetRange(0, last + 1).ToArray());
        }
    }
}
