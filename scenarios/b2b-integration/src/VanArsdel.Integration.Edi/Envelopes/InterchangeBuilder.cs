using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml;
using VanArsdel.Integration.Edi.Definitions;

namespace VanArsdel.Integration.Edi.Envelopes
{
    /// <summary>
    /// Builds an outbound interchange from EDI XML documents of one type: envelope, ST/SE or UNH/UNT control values
    /// and counts. Equivalent of the EDI assembler with the agreement's envelope settings.
    /// </summary>
    public class InterchangeBuilder
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private readonly InterchangeHeader _header;
        private readonly Delimiters _delimiters;
        private readonly bool _lineBreaks;

        public InterchangeBuilder(InterchangeHeader header, Delimiters delimiters = null, bool lineBreakAfterSegment = true)
        {
            _header = header;
            _delimiters = delimiters ?? (header.Standard == EdiStandard.X12 ? Delimiters.X12Default : Delimiters.EdifactDefault);
            _lineBreaks = lineBreakAfterSegment;
        }

        /// <param name="groupControlNumber">GS06 for X12; ignored for EDIFACT.</param>
        /// <param name="firstSetControlNumber">ST02/UNH1 of the first set; following sets count up.</param>
        public string Build(IEnumerable<XmlDocument> documents, string groupControlNumber, int firstSetControlNumber = 1)
        {
            var docs = documents.ToList();
            if (docs.Count == 0)
            {
                throw new ArgumentException("Nothing to send.", "documents");
            }

            var defs = docs.Select(EdiXmlReader.DefinitionFor).ToList();
            if (defs.Select(d => d.Type).Distinct().Count() > 1)
            {
                throw new ArgumentException("All documents of an interchange must have the same type.", "documents");
            }

            var sb = new StringBuilder();
            if (_header.Standard == EdiStandard.X12)
            {
                AppendIsa(sb);
                var def = defs[0];
                Append(sb, Segment.Of("GS", def.FunctionalId, _header.SenderId, _header.ReceiverId,
                    _header.Prepared.ToString("yyyyMMdd", Inv), _header.Prepared.ToString("HHmm", Inv), groupControlNumber, "X", "004010"));
                for (var i = 0; i < docs.Count; i++)
                {
                    AppendSet(sb, docs[i], (firstSetControlNumber + i).ToString("0000", Inv));
                }

                Append(sb, Segment.Of("GE", docs.Count.ToString(Inv), groupControlNumber));
                Append(sb, Segment.Of("IEA", "1", _header.ControlNumber.PadLeft(9, '0')));
            }
            else
            {
                sb.Append(_delimiters.ToUna());
                if (_lineBreaks)
                {
                    sb.Append("\r\n");
                }

                Append(sb, new Segment("UNB", new[]
                {
                    new[] { "UNOC", "3" },
                    new[] { _header.SenderId, _header.SenderQualifier },
                    new[] { _header.ReceiverId, _header.ReceiverQualifier },
                    new[] { _header.Prepared.ToString("yyMMdd", Inv), _header.Prepared.ToString("HHmm", Inv) },
                    new[] { _header.ControlNumber },
                    new string[0],
                    new string[0],
                    new string[0],
                    new[] { _header.AckRequested ? "1" : string.Empty },
                    new string[0],
                    new[] { _header.Test ? "1" : string.Empty }
                }));
                for (var i = 0; i < docs.Count; i++)
                {
                    AppendSet(sb, docs[i], (firstSetControlNumber + i).ToString(Inv));
                }

                Append(sb, Segment.Of("UNZ", docs.Count.ToString(Inv), _header.ControlNumber));
            }

            return sb.ToString();
        }

        private void AppendSet(StringBuilder sb, XmlDocument doc, string controlNumber)
        {
            var segments = EdiXmlReader.Read(doc).ToList();
            var x12 = _header.Standard == EdiStandard.X12;
            var headerTag = x12 ? "ST" : "UNH";
            var trailerTag = x12 ? "SE" : "UNT";
            var def = EdiXmlReader.DefinitionFor(doc);

            segments.RemoveAll(s => s.Tag == headerTag || s.Tag == trailerTag);
            if (x12)
            {
                segments.Insert(0, Segment.Of("ST", def.Type, controlNumber));
            }
            else
            {
                var version = def.Version.Substring(1);
                segments.Insert(0, new Segment("UNH", new[] { new[] { controlNumber }, new[] { def.Type, "D", version, "UN", def.Type == "CONTRL" ? null : "EAN008" } }));
            }

            segments.Add(Segment.Of(trailerTag, (segments.Count + 1).ToString(Inv), controlNumber));
            foreach (var s in segments)
            {
                Append(sb, s);
            }
        }

        private void AppendIsa(StringBuilder sb)
        {
            var d = _delimiters;
            var e = d.Element;
            sb.Append("ISA").Append(e)
                .Append("00").Append(e).Append(new string(' ', 10)).Append(e)
                .Append("00").Append(e).Append(new string(' ', 10)).Append(e)
                .Append(Fixed(_header.SenderQualifier, 2)).Append(e).Append(Fixed(_header.SenderId, 15)).Append(e)
                .Append(Fixed(_header.ReceiverQualifier, 2)).Append(e).Append(Fixed(_header.ReceiverId, 15)).Append(e)
                .Append(_header.Prepared.ToString("yyMMdd", Inv)).Append(e).Append(_header.Prepared.ToString("HHmm", Inv)).Append(e)
                .Append(d.Repetition.HasValue ? d.Repetition.Value : 'U').Append(e)
                .Append("00401").Append(e)
                .Append(_header.ControlNumber.PadLeft(9, '0')).Append(e)
                .Append(_header.AckRequested ? '1' : '0').Append(e)
                .Append(_header.Test ? 'T' : 'P').Append(e)
                .Append(d.Component).Append(d.Segment);
            if (_lineBreaks)
            {
                sb.Append("\r\n");
            }
        }

        private void Append(StringBuilder sb, Segment s)
        {
            sb.Append(s.ToEdi(_delimiters));
            if (_lineBreaks)
            {
                sb.Append("\r\n");
            }
        }

        private static string Fixed(string value, int width)
        {
            value = value ?? string.Empty;
            return value.Length >= width ? value.Substring(0, width) : value.PadRight(width);
        }
    }
}
