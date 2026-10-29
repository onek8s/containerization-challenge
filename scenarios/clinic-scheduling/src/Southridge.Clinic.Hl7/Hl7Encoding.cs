using System.Globalization;
using System.Text;

namespace Southridge.Clinic.Hl7
{
    public class Hl7Encoding
    {
        public static readonly Hl7Encoding Default = new Hl7Encoding('|', '^', '~', '\\', '&');

        public Hl7Encoding(char field, char component, char repetition, char escape, char subComponent)
        {
            FieldSeparator = field;
            ComponentSeparator = component;
            RepetitionSeparator = repetition;
            EscapeCharacter = escape;
            SubComponentSeparator = subComponent;
        }

        public char FieldSeparator { get; private set; }

        public char ComponentSeparator { get; private set; }

        public char RepetitionSeparator { get; private set; }

        public char EscapeCharacter { get; private set; }

        public char SubComponentSeparator { get; private set; }

        public string EncodingCharacters
        {
            get { return new string(new[] { ComponentSeparator, RepetitionSeparator, EscapeCharacter, SubComponentSeparator }); }
        }

        public string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            var sb = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                if (c == EscapeCharacter)
                {
                    sb.Append(EscapeCharacter).Append('E').Append(EscapeCharacter);
                }
                else if (c == FieldSeparator)
                {
                    sb.Append(EscapeCharacter).Append('F').Append(EscapeCharacter);
                }
                else if (c == ComponentSeparator)
                {
                    sb.Append(EscapeCharacter).Append('S').Append(EscapeCharacter);
                }
                else if (c == RepetitionSeparator)
                {
                    sb.Append(EscapeCharacter).Append('R').Append(EscapeCharacter);
                }
                else if (c == SubComponentSeparator)
                {
                    sb.Append(EscapeCharacter).Append('T').Append(EscapeCharacter);
                }
                else if (c == '\r')
                {
                    sb.Append(EscapeCharacter).Append(".br").Append(EscapeCharacter);
                }
                else if (c != '\n')
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        public string Unescape(string value)
        {
            if (string.IsNullOrEmpty(value) || value.IndexOf(EscapeCharacter) < 0)
            {
                return value;
            }

            var sb = new StringBuilder(value.Length);
            int i = 0;
            while (i < value.Length)
            {
                char c = value[i];
                if (c != EscapeCharacter)
                {
                    sb.Append(c);
                    i++;
                    continue;
                }

                int end = value.IndexOf(EscapeCharacter, i + 1);
                if (end < 0)
                {
                    sb.Append(value.Substring(i));
                    break;
                }

                string code = value.Substring(i + 1, end - i - 1);
                switch (code)
                {
                    case "F":
                        sb.Append(FieldSeparator);
                        break;
                    case "S":
                        sb.Append(ComponentSeparator);
                        break;
                    case "R":
                        sb.Append(RepetitionSeparator);
                        break;
                    case "E":
                        sb.Append(EscapeCharacter);
                        break;
                    case "T":
                        sb.Append(SubComponentSeparator);
                        break;
                    case ".br":
                        sb.Append("\r\n");
                        break;
                    default:
                        if (code.StartsWith("X") && code.Length % 2 == 1)
                        {
                            for (int h = 1; h < code.Length; h += 2)
                            {
                                sb.Append((char)int.Parse(code.Substring(h, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            }
                        }

                        break;
                }

                i = end + 1;
            }

            return sb.ToString();
        }
    }
}
