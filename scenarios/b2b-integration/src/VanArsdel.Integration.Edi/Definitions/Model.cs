using System;
using System.Collections.Generic;
using System.Linq;

namespace VanArsdel.Integration.Edi.Definitions
{
    /// <summary>Data element types: AN alphanumeric, ID code, Nn implied decimals, R real, DT date, TM time.</summary>
    public enum ElementType
    {
        AN,
        ID,
        N0,
        N2,
        R,
        DT,
        TM
    }

    public sealed class ElementDef
    {
        public ElementDef(string name, ElementType type, int minLength, int maxLength, bool mandatory, IEnumerable<string> codes = null)
        {
            Name = name;
            Type = type;
            MinLength = minLength;
            MaxLength = maxLength;
            Mandatory = mandatory;
            Codes = codes == null ? null : new HashSet<string>(codes, StringComparer.Ordinal);
            Components = new ElementDef[0];
        }

        private ElementDef(string name, bool mandatory, ElementDef[] components)
        {
            Name = name;
            Mandatory = mandatory;
            Type = ElementType.AN;
            Components = components;
        }

        public static ElementDef Composite(string name, bool mandatory, params ElementDef[] components)
        {
            return new ElementDef(name, mandatory, components);
        }

        public string Name { get; private set; }
        public ElementType Type { get; private set; }
        public int MinLength { get; private set; }
        public int MaxLength { get; private set; }
        public bool Mandatory { get; private set; }
        public ISet<string> Codes { get; private set; }
        public ElementDef[] Components { get; private set; }

        public bool IsComposite
        {
            get { return Components.Length > 0; }
        }
    }

    public sealed class SegmentDef
    {
        public SegmentDef(string tag, string description, params ElementDef[] elements)
        {
            Tag = tag;
            Description = description;
            Elements = elements;
        }

        public string Tag { get; private set; }
        public string Description { get; private set; }
        public ElementDef[] Elements { get; private set; }

        public int IndexOf(string elementName)
        {
            for (var i = 0; i < Elements.Length; i++)
            {
                if (Elements[i].Name == elementName)
                {
                    return i;
                }
            }

            return -1;
        }
    }

    public abstract class Node
    {
        protected Node(int minOccurs, int maxOccurs)
        {
            MinOccurs = minOccurs;
            MaxOccurs = maxOccurs;
        }

        public int MinOccurs { get; private set; }
        public int MaxOccurs { get; private set; }

        /// <summary>Tag of the segment that starts this node.</summary>
        public abstract string TriggerTag { get; }
    }

    public sealed class SegmentUse : Node
    {
        public SegmentUse(SegmentDef segment, int minOccurs, int maxOccurs) : base(minOccurs, maxOccurs)
        {
            Segment = segment;
        }

        public SegmentDef Segment { get; private set; }

        public override string TriggerTag
        {
            get { return Segment.Tag; }
        }
    }

    public sealed class LoopDef : Node
    {
        public LoopDef(string name, int minOccurs, int maxOccurs, params Node[] children) : base(minOccurs, maxOccurs)
        {
            if (children == null || children.Length == 0 || !(children[0] is SegmentUse))
            {
                throw new ArgumentException("A loop must start with a segment.", "children");
            }

            Name = name;
            Children = children;
        }

        public string Name { get; private set; }
        public Node[] Children { get; private set; }

        public override string TriggerTag
        {
            get { return Children[0].TriggerTag; }
        }
    }

    public sealed class TransactionDefinition
    {
        public TransactionDefinition(EdiStandard standard, string version, string type, string functionalId, params Node[] nodes)
        {
            Standard = standard;
            Version = version;
            Type = type;
            FunctionalId = functionalId;
            Nodes = nodes;
        }

        public EdiStandard Standard { get; private set; }

        /// <summary>00401 for X12, D96A or D3 for EDIFACT.</summary>
        public string Version { get; private set; }

        public string Type { get; private set; }

        /// <summary>X12 GS01 functional identifier code (PO, IN, SH, FA).</summary>
        public string FunctionalId { get; private set; }

        public Node[] Nodes { get; private set; }

        /// <summary>Root element name of the BizTalk EDI schema, e.g. X12_00401_850 or EFACT_D96A_ORDERS.</summary>
        public string RootName
        {
            get { return Standard == EdiStandard.X12 ? "X12_" + Version + "_" + Type : "EFACT_" + Version + "_" + Type; }
        }

        public string Namespace
        {
            get { return Standard == EdiStandard.X12 ? EdiXmlNames.X12Namespace : EdiXmlNames.EdifactNamespace; }
        }

        public IEnumerable<SegmentDef> AllSegments()
        {
            return Flatten(Nodes).Select(n => n.Segment).Distinct();
        }

        public SegmentDef FindSegment(string tag)
        {
            return AllSegments().FirstOrDefault(s => s.Tag == tag);
        }

        private static IEnumerable<SegmentUse> Flatten(IEnumerable<Node> nodes)
        {
            foreach (var n in nodes)
            {
                var use = n as SegmentUse;
                if (use != null)
                {
                    yield return use;
                    continue;
                }

                foreach (var inner in Flatten(((LoopDef)n).Children))
                {
                    yield return inner;
                }
            }
        }
    }

    public static class EdiXmlNames
    {
        public const string X12Namespace = "http://schemas.microsoft.com/BizTalk/EDI/X12/2006";
        public const string EdifactNamespace = "http://schemas.microsoft.com/BizTalk/EDI/EDIFACT/2006";
    }
}
