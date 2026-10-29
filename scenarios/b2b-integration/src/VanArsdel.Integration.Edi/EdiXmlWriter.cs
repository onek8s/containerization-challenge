using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using VanArsdel.Integration.Edi.Definitions;
using VanArsdel.Integration.Edi.Envelopes;
using VanArsdel.Integration.Edi.Validation;

namespace VanArsdel.Integration.Edi
{
    /// <summary>
    /// Turns the segments of one transaction set into the XML shape of the BizTalk EDI schema: root and loops in the
    /// EDI namespace, one element per segment, one child per data element (TAG01) or composite (C507/C50701).
    /// Structure errors (unexpected, missing, repeated segments) are added to the transaction set.
    /// </summary>
    public class EdiXmlWriter
    {
        private readonly TransactionDefinition _definition;
        private XmlDocument _doc;
        private IList<Segment> _segments;
        private int _cursor;
        private List<EdiError> _errors;

        public EdiXmlWriter(TransactionDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException("definition");
            }

            _definition = definition;
        }

        public XmlDocument Write(TransactionSet set)
        {
            _doc = new XmlDocument();
            _segments = set.Segments;
            _cursor = 0;
            _errors = set.Errors;

            var root = _doc.CreateElement("ns0", _definition.RootName, _definition.Namespace);
            _doc.AppendChild(root);

            while (_cursor < _segments.Count)
            {
                ParseLevel(_definition.Nodes, root, true);
                if (_cursor < _segments.Count)
                {
                    var s = _segments[_cursor];
                    var known = _definition.FindSegment(s.Tag) != null;
                    _errors.Add(new EdiError(s.Tag, s.Position, 0, known ? EdiErrorCodes.UnexpectedSegment : EdiErrorCodes.UnrecognizedSegment,
                        (known ? "Unexpected segment " : "Unrecognized segment ") + s.Tag + " at position " + s.Position + "."));
                    _cursor++;
                }
            }

            return _doc;
        }

        private void ParseLevel(Node[] nodes, XmlElement parent, bool topLevel)
        {
            var counts = new int[nodes.Length];
            var index = 0;
            while (_cursor < _segments.Count)
            {
                var segment = _segments[_cursor];
                var match = -1;
                for (var j = index; j < nodes.Length; j++)
                {
                    if (nodes[j].TriggerTag == segment.Tag)
                    {
                        match = j;
                        break;
                    }
                }

                if (match < 0)
                {
                    break;
                }

                var node = nodes[match];
                if (counts[match] >= node.MaxOccurs)
                {
                    _errors.Add(new EdiError(segment.Tag, segment.Position, 0,
                        node is LoopDef ? EdiErrorCodes.LoopTooManyTimes : EdiErrorCodes.SegmentExceedsMaximum,
                        segment.Tag + " occurs more than " + node.MaxOccurs + " times."));
                }

                CheckMissing(nodes, counts, index, match, segment);
                counts[match]++;
                index = match;

                var use = node as SegmentUse;
                if (use != null)
                {
                    parent.AppendChild(WriteSegment(use.Segment, segment));
                    _cursor++;
                    if (counts[match] >= node.MaxOccurs)
                    {
                        // A repeat of a loop's trigger segment starts the next iteration of the loop in the parent.
                        index = match + 1;
                    }
                }
                else
                {
                    var loop = (LoopDef)node;
                    var loopElement = _doc.CreateElement("ns0", loop.Name, _definition.Namespace);
                    parent.AppendChild(loopElement);
                    ParseLevel(loop.Children, loopElement, false);
                }
            }

            if (!topLevel || _cursor >= _segments.Count)
            {
                var at = _cursor < _segments.Count ? _segments[_cursor] : _segments.Last();
                CheckMissing(nodes, counts, index, nodes.Length, at);
            }
        }

        private void CheckMissing(Node[] nodes, int[] counts, int from, int to, Segment at)
        {
            for (var k = from; k < to; k++)
            {
                if (counts[k] == 0 && nodes[k].MinOccurs > 0 && (k != from || counts[from] == 0))
                {
                    var name = nodes[k] is LoopDef ? ((LoopDef)nodes[k]).Name : nodes[k].TriggerTag;
                    if (_errors.Any(e => e.Code == EdiErrorCodes.MissingSegment && e.SegmentTag == nodes[k].TriggerTag && e.SegmentPosition == at.Position))
                    {
                        continue;
                    }

                    _errors.Add(new EdiError(nodes[k].TriggerTag, at.Position, 0, EdiErrorCodes.MissingSegment, "Mandatory " + name + " is missing before position " + at.Position + "."));
                }
            }
        }

        private XmlElement WriteSegment(SegmentDef def, Segment segment)
        {
            var element = _doc.CreateElement("ns0", def.Tag, _definition.Namespace);
            SegmentValidator.Validate(def, segment, _errors);

            var max = Math.Min(def.Elements.Length, segment.ElementCount);
            for (var i = 0; i < max; i++)
            {
                var ed = def.Elements[i];
                var values = segment.Elements[i];
                if (ed.IsComposite)
                {
                    if (values.All(string.IsNullOrEmpty))
                    {
                        continue;
                    }

                    var composite = _doc.CreateElement(ed.Name);
                    for (var c = 0; c < ed.Components.Length && c < values.Length; c++)
                    {
                        if (string.IsNullOrEmpty(values[c]))
                        {
                            continue;
                        }

                        var child = _doc.CreateElement(ed.Components[c].Name);
                        child.InnerText = values[c];
                        composite.AppendChild(child);
                    }

                    element.AppendChild(composite);
                }
                else
                {
                    var value = values.Length > 0 ? values[0] : null;
                    if (string.IsNullOrEmpty(value))
                    {
                        continue;
                    }

                    var child = _doc.CreateElement(ed.Name);
                    child.InnerText = value;
                    element.AppendChild(child);
                }
            }

            return element;
        }
    }
}
