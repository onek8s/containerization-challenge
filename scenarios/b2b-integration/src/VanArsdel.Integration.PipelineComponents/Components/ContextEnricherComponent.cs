using System.IO;
using System.Runtime.InteropServices;
using System.Xml.XPath;
using Microsoft.BizTalk.Component.Interop;
using Microsoft.BizTalk.Message.Interop;
using VanArsdel.Integration.Contracts;
using VanArsdel.Integration.Messaging;
using VanArsdel.Integration.PipelineComponents.Common;

namespace VanArsdel.Integration.PipelineComponents.Components
{
    /// <summary>
    /// Writes the routing properties the platform's subscriptions use: MessageKind and SourceSystem from the
    /// pipeline configuration, OrderNumber from an XPath on the body, and the body hash. Used in the XML receive
    /// and send pipelines (the EDI properties come from the EDI disassembler).
    /// </summary>
    [ComponentCategory(CategoryTypes.CATID_PipelineComponent)]
    [ComponentCategory(CategoryTypes.CATID_Any)]
    [Guid("D8E9F0A1-2B3C-4D5E-8F70-81A2B3C4D577")]
    public class ContextEnricherComponent : ComponentBase, IComponent
    {
        public override string Name
        {
            get { return "VanArsdel Context Enricher"; }
        }

        public override string Description
        {
            get { return "Promotes MessageKind, SourceSystem and OrderNumber for routing and tracking."; }
        }

        public string MessageKind { get; set; }
        public string SourceSystem { get; set; }

        /// <summary>local-name() based XPath, e.g. /*[local-name()='Order']/*[local-name()='Header']/*[local-name()='OrderNumber'].</summary>
        public string OrderNumberXPath { get; set; }

        public IBaseMessage Execute(IPipelineContext pContext, IBaseMessage pInMsg)
        {
            var context = new ContextAdapter(pInMsg.Context);
            if (!string.IsNullOrEmpty(MessageKind))
            {
                context.PromoteVanArsdel(ContextProperties.MessageKind, MessageKind);
            }

            if (!string.IsNullOrEmpty(SourceSystem))
            {
                context.WriteVanArsdel(ContextProperties.SourceSystem, SourceSystem);
            }

            var body = MessageBodies.ReadAll(pInMsg);
            context.WriteVanArsdel(ContextProperties.BodyHash, ArchiveNaming.Hash(body));
            if (!string.IsNullOrEmpty(OrderNumberXPath) && MessageTypeProbe.Probe(body).Format == PayloadFormat.Xml)
            {
                var nav = new XPathDocument(new MemoryStream(body)).CreateNavigator();
                var value = nav.Evaluate("string(" + OrderNumberXPath + ")") as string;
                if (!string.IsNullOrEmpty(value))
                {
                    context.PromoteVanArsdel(ContextProperties.OrderNumber, value);
                }
            }

            MessageBodies.Replace(pContext, pInMsg, body);
            return pInMsg;
        }

        protected override void LoadProperties(IPropertyBag bag)
        {
            MessageKind = ReadString(bag, "MessageKind", null);
            SourceSystem = ReadString(bag, "SourceSystem", null);
            OrderNumberXPath = ReadString(bag, "OrderNumberXPath", null);
        }

        protected override void SaveProperties(IPropertyBag bag)
        {
            WriteProperty(bag, "MessageKind", MessageKind ?? string.Empty);
            WriteProperty(bag, "SourceSystem", SourceSystem ?? string.Empty);
            WriteProperty(bag, "OrderNumberXPath", OrderNumberXPath ?? string.Empty);
        }
    }
}
