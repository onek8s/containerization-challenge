using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.BizTalk.Component.Interop;
using Microsoft.BizTalk.Message.Interop;
using VanArsdel.Integration.Contracts;
using VanArsdel.Integration.Messaging;
using VanArsdel.Integration.PipelineComponents.Common;

namespace VanArsdel.Integration.PipelineComponents.Components
{
    /// <summary>
    /// Disassemble stage for XML envelopes: gateway batches (OrderBatch/Order) become one message per order, so a
    /// bad order does not hold up the others. The envelope attributes (BatchId, SubmittedBy) go to every part's
    /// context; MessageType is promoted per part.
    /// </summary>
    [ComponentCategory(CategoryTypes.CATID_PipelineComponent)]
    [ComponentCategory(CategoryTypes.CATID_DisassemblingParser)]
    [Guid("B1D2E3F4-5A6B-4C7D-8E9F-0A1B2C3D4E55")]
    public class XmlDebatcherComponent : ComponentBase, IDisassemblerComponent, IProbeMessage
    {
        private readonly Queue<IBaseMessage> _output = new Queue<IBaseMessage>();

        public XmlDebatcherComponent()
        {
            BodyElement = "Order";
            EnvelopeMessageType = MessageTypes.PartnerOrderBatch;
        }

        public override string Name
        {
            get { return "VanArsdel XML Debatcher"; }
        }

        public override string Description
        {
            get { return "Splits an XML envelope into one message per body element."; }
        }

        /// <summary>Local name of the repeating element.</summary>
        public string BodyElement { get; set; }

        /// <summary>Only messages of this type are split (probe); others pass through untouched.</summary>
        public string EnvelopeMessageType { get; set; }

        /// <summary>Wrap each part in a copy of the envelope root (IDoc files).</summary>
        public bool WrapInRoot { get; set; }

        public bool Probe(IPipelineContext pContext, IBaseMessage pInMsg)
        {
            var stream = MessageBodies.MakeSeekable(pContext, pInMsg);
            var head = new byte[Math.Min(4096, stream.CanSeek ? (int)Math.Min(stream.Length, 4096) : 4096)];
            var read = stream.Read(head, 0, head.Length);
            stream.Position = 0;
            Array.Resize(ref head, read);
            var probe = MessageTypeProbe.Probe(head);
            return probe.Format == PayloadFormat.Xml && (string.IsNullOrEmpty(EnvelopeMessageType) || probe.MessageType == EnvelopeMessageType);
        }

        public void Disassemble(IPipelineContext pContext, IBaseMessage pInMsg)
        {
            var body = MessageBodies.ReadAll(pInMsg);
            var parts = XmlDebatcher.Split(new MemoryStream(body), BodyElement, WrapInRoot);
            foreach (var part in parts)
            {
                var message = MessageBodies.CreateMessage(pContext, pInMsg, part.Body);
                var context = new ContextAdapter(message.Context);
                context.Promote(ContextProperties.MessageType, ContextProperties.SystemNamespace, part.MessageType);
                context.WriteVanArsdel("BatchIndex", part.Index);
                context.WriteVanArsdel("BatchCount", part.Count);
                if (part.EnvelopeAttributes != null)
                {
                    foreach (var a in part.EnvelopeAttributes)
                    {
                        context.WriteVanArsdel("Envelope." + a.Key, a.Value);
                    }
                }

                OnPartCreated(context, part);
                _output.Enqueue(message);
            }
        }

        public IBaseMessage GetNext(IPipelineContext pContext)
        {
            return _output.Count == 0 ? null : _output.Dequeue();
        }

        protected virtual void OnPartCreated(IMessageContext context, DebatchedPart part)
        {
        }

        protected override void LoadProperties(IPropertyBag bag)
        {
            BodyElement = ReadString(bag, "BodyElement", BodyElement);
            EnvelopeMessageType = ReadString(bag, "EnvelopeMessageType", EnvelopeMessageType);
            WrapInRoot = ReadBool(bag, "WrapInRoot", WrapInRoot);
        }

        protected override void SaveProperties(IPropertyBag bag)
        {
            WriteProperty(bag, "BodyElement", BodyElement);
            WriteProperty(bag, "EnvelopeMessageType", EnvelopeMessageType ?? string.Empty);
            WriteProperty(bag, "WrapInRoot", WrapInRoot);
        }

        protected override IList<string> ValidateConfiguration()
        {
            return string.IsNullOrEmpty(BodyElement) ? new[] { "BodyElement is required." } : null;
        }
    }
}
