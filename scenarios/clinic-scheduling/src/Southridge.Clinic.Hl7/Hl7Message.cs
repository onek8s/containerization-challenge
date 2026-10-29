using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Southridge.Clinic.Hl7
{
    /// <summary>
    /// Minimal HL7 v2 message model (pipe and hat encoding). Fields are addressed the way the interface
    /// specifications are written: "PID-5-1" is segment PID, field 5, component 1. Repeating segments
    /// use an index: "OBX(2)-5".
    /// </summary>
    public class Hl7Message
    {
        public const char SegmentSeparator = '\r';

        private readonly List<Hl7Segment> _segments = new List<Hl7Segment>();
        private Hl7Encoding _encoding = Hl7Encoding.Default;

        public Hl7Encoding Encoding
        {
            get { return _encoding; }
        }

        public IList<Hl7Segment> Segments
        {
            get { return _segments; }
        }

        public string MessageType
        {
            get { return Get("MSH-9-1"); }
        }

        public string TriggerEvent
        {
            get { return Get("MSH-9-2"); }
        }

        /// <summary>"ADT^A04" style, as stored in the message log.</summary>
        public string TypeAndTrigger
        {
            get { return MessageType + "^" + TriggerEvent; }
        }

        public string ControlId
        {
            get { return Get("MSH-10"); }
        }

        public string Version
        {
            get { return Get("MSH-12"); }
        }

        public static Hl7Message Parse(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException("text");
            }

            text = text.Replace("\r\n", "\r").Replace('\n', '\r').Trim('\r', ' ', '\t', (char)0x0B, (char)0x1C);
            if (!text.StartsWith("MSH", StringComparison.Ordinal) || text.Length < 8)
            {
                throw new Hl7Exception("Message does not start with an MSH segment.");
            }

            var message = new Hl7Message();
            message._encoding = new Hl7Encoding(text[3], text[4], text[5], text[6], text[7]);
            foreach (string line in text.Split(SegmentSeparator))
            {
                if (line.Trim().Length == 0)
                {
                    continue;
                }

                message._segments.Add(Hl7Segment.Parse(line, message._encoding));
            }

            return message;
        }

        public static Hl7Message Create(string messageType, string triggerEvent, string structure, string sendingApplication, string sendingFacility,
            string receivingApplication, string receivingFacility, string controlId, DateTime timestamp, string version)
        {
            var message = new Hl7Message();
            Hl7Segment msh = message.AddSegment("MSH");
            msh.SetField(1, message._encoding.FieldSeparator.ToString());
            msh.SetField(2, message._encoding.EncodingCharacters);
            message.Set("MSH-3", sendingApplication);
            message.Set("MSH-4", sendingFacility);
            message.Set("MSH-5", receivingApplication);
            message.Set("MSH-6", receivingFacility);
            message.Set("MSH-7", Hl7DateTime.Format(timestamp));
            message.Set("MSH-9-1", messageType);
            message.Set("MSH-9-2", triggerEvent);
            if (!string.IsNullOrEmpty(structure))
            {
                message.Set("MSH-9-3", structure);
            }

            message.Set("MSH-10", controlId);
            message.Set("MSH-11", "P");
            message.Set("MSH-12", version);
            return message;
        }

        public Hl7Segment AddSegment(string name)
        {
            var segment = new Hl7Segment(name, _encoding);
            _segments.Add(segment);
            return segment;
        }

        public Hl7Segment GetSegment(string name)
        {
            return GetSegment(name, 1);
        }

        public Hl7Segment GetSegment(string name, int occurrence)
        {
            int seen = 0;
            foreach (Hl7Segment s in _segments)
            {
                if (s.Name == name && ++seen == occurrence)
                {
                    return s;
                }
            }

            return null;
        }

        public List<Hl7Segment> GetSegments(string name)
        {
            return _segments.FindAll(delegate(Hl7Segment s) { return s.Name == name; });
        }

        public bool HasSegment(string name)
        {
            return GetSegment(name) != null;
        }

        public string Get(string path)
        {
            Hl7Path p = Hl7Path.Parse(path);
            Hl7Segment segment = GetSegment(p.Segment, p.Occurrence);
            if (segment == null)
            {
                return string.Empty;
            }

            return segment.Get(p.Field, p.Repetition, p.Component, p.SubComponent);
        }

        public void Set(string path, string value)
        {
            Hl7Path p = Hl7Path.Parse(path);
            Hl7Segment segment = GetSegment(p.Segment, p.Occurrence);
            while (segment == null)
            {
                AddSegment(p.Segment);
                segment = GetSegment(p.Segment, p.Occurrence);
            }

            segment.Set(p.Field, p.Repetition, p.Component, p.SubComponent, value);
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach (Hl7Segment s in _segments)
            {
                sb.Append(s.ToString());
                sb.Append(SegmentSeparator);
            }

            return sb.ToString();
        }

        /// <summary>Readable form for logs and the message viewer: one segment per line.</summary>
        public string ToDisplayString()
        {
            return ToString().TrimEnd(SegmentSeparator).Replace(SegmentSeparator.ToString(), Environment.NewLine);
        }
    }

    public class Hl7Exception : Exception
    {
        public Hl7Exception(string message)
            : base(message)
        {
        }

        public Hl7Exception(string message, Exception inner)
            : base(message, inner)
        {
        }
    }

    internal struct Hl7Path
    {
        public string Segment;
        public int Occurrence;
        public int Field;
        public int Repetition;
        public int Component;
        public int SubComponent;

        public static Hl7Path Parse(string path)
        {
            var p = new Hl7Path();
            p.Occurrence = 1;
            p.Repetition = 1;
            string[] parts = path.Split('-');
            string seg = parts[0];
            int paren = seg.IndexOf('(');
            if (paren > 0)
            {
                p.Occurrence = int.Parse(seg.Substring(paren + 1, seg.Length - paren - 2), CultureInfo.InvariantCulture);
                seg = seg.Substring(0, paren);
            }

            p.Segment = seg;
            if (parts.Length > 1)
            {
                string field = parts[1];
                int rep = field.IndexOf('(');
                if (rep > 0)
                {
                    p.Repetition = int.Parse(field.Substring(rep + 1, field.Length - rep - 2), CultureInfo.InvariantCulture);
                    field = field.Substring(0, rep);
                }

                p.Field = int.Parse(field, CultureInfo.InvariantCulture);
            }

            if (parts.Length > 2)
            {
                p.Component = int.Parse(parts[2], CultureInfo.InvariantCulture);
            }

            if (parts.Length > 3)
            {
                p.SubComponent = int.Parse(parts[3], CultureInfo.InvariantCulture);
            }

            return p;
        }
    }
}
