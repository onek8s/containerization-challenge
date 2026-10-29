using System.Runtime.InteropServices;
using System.Xml;
using Microsoft.BizTalk.Component.Interop;
using VanArsdel.Integration.Contracts;
using VanArsdel.Integration.Messaging;

namespace VanArsdel.Integration.PipelineComponents.Components
{
    /// <summary>
    /// IDoc files from the ERP outbound folder hold several IDOC elements (one per delivery or billing document).
    /// Each becomes its own ORDERS05 / DESADV01 / INVOIC02 message; the control record's message type and
    /// document number go to the context.
    /// </summary>
    [ComponentCategory(CategoryTypes.CATID_PipelineComponent)]
    [ComponentCategory(CategoryTypes.CATID_DisassemblingParser)]
    [Guid("C7A8B9D0-1E2F-4A3B-9C4D-5E6F7A8B9C66")]
    public class IDocSplitterComponent : XmlDebatcherComponent
    {
        public IDocSplitterComponent()
        {
            BodyElement = "IDOC";
            EnvelopeMessageType = null;
            WrapInRoot = true;
        }

        public override string Name
        {
            get { return "VanArsdel IDoc Splitter"; }
        }

        public override string Description
        {
            get { return "Splits ERP IDoc XML files into one message per IDoc."; }
        }

        protected override void OnPartCreated(IMessageContext context, DebatchedPart part)
        {
            var doc = new XmlDocument();
            doc.Load(new System.IO.MemoryStream(part.Body));
            var dc = doc.SelectSingleNode("/*/IDOC/EDI_DC40");
            context.WriteVanArsdel(ContextProperties.SourceSystem, "ERP");
            if (dc == null)
            {
                return;
            }

            var mestyp = Text(dc, "MESTYP");
            context.PromoteVanArsdel(ContextProperties.MessageKind, mestyp);
            context.WriteVanArsdel("IDocNumber", Text(dc, "DOCNUM"));
            context.WriteVanArsdel("IDocPartner", Text(dc, "RCVPRN"));
        }

        private static string Text(XmlNode node, string child)
        {
            var c = node.SelectSingleNode(child);
            return c == null ? null : c.InnerText;
        }
    }
}
