using System;
using System.Runtime.InteropServices;
using Microsoft.BizTalk.Component.Interop;
using Microsoft.BizTalk.Message.Interop;
using VanArsdel.Integration.Contracts;
using VanArsdel.Integration.Messaging;
using VanArsdel.Integration.PipelineComponents.Common;

namespace VanArsdel.Integration.PipelineComponents.Components
{
    /// <summary>
    /// BAM activity InterchangeTracking (bam\InterchangeTracking.xml) from the pipeline's messaging event stream:
    /// one activity per file received from or sent to a partner, written in the pipeline transaction.
    /// Inbound orders also enable the OrderLifecycle continuation on the interchange id so the orchestration
    /// can relate the order to the file.
    /// </summary>
    [ComponentCategory(CategoryTypes.CATID_PipelineComponent)]
    [ComponentCategory(CategoryTypes.CATID_Any)]
    [Guid("E9F0A1B2-3C4D-4E5F-9A81-92B3C4D5E688")]
    public class InterchangeTrackingComponent : ComponentBase, IComponent
    {
        public const string ActivityName = "InterchangeTracking";

        public InterchangeTrackingComponent()
        {
            Direction = "Inbound";
            Enabled = true;
        }

        public override string Name
        {
            get { return "VanArsdel Interchange Tracking"; }
        }

        public override string Description
        {
            get { return "Writes the InterchangeTracking BAM activity."; }
        }

        public bool Enabled { get; set; }
        public string Direction { get; set; }

        public IBaseMessage Execute(IPipelineContext pContext, IBaseMessage pInMsg)
        {
            if (!Enabled)
            {
                return pInMsg;
            }

            var context = new ContextAdapter(pInMsg.Context);
            var stream = pContext.GetEventStream();
            var id = pInMsg.MessageID.ToString("D");
            ulong size;
            bool known;
            pInMsg.GetSize(out size, out known);

            stream.BeginActivity(ActivityName, id);
            stream.UpdateActivity(ActivityName, id, new object[]
            {
                "Direction", Direction,
                "PartnerId", context.ReadVanArsdel(ContextProperties.PartnerId),
                "MessageKind", context.ReadVanArsdel(ContextProperties.MessageKind),
                "MessageType", context.MessageType(),
                "ControlNumber", context.ReadString(ContextProperties.Isa13, ContextProperties.EdiNamespace)
                                 ?? context.ReadString(ContextProperties.UnbReference, ContextProperties.EdiNamespace)
                                 ?? context.ReadVanArsdel(ContextProperties.InterchangeControlNumber),
                "FileName", context.ReadString(ContextProperties.ReceivedFileName, ContextProperties.FileNamespace),
                "ArchivePath", context.ReadVanArsdel(ContextProperties.ArchivePath),
                "Port", context.ReadString(ContextProperties.ReceivePortName, ContextProperties.SystemNamespace),
                "SizeBytes", known ? (object)(long)size : null,
                "TrackedUtc", DateTime.UtcNow
            });

            var interchangeId = context.ReadString(ContextProperties.InterchangeID, ContextProperties.SystemNamespace);
            if (!string.IsNullOrEmpty(interchangeId))
            {
                stream.AddReference(ActivityName, id, "Interchange", "InterchangeID", interchangeId);
            }

            stream.EndActivity(ActivityName, id);
            return pInMsg;
        }

        protected override void LoadProperties(IPropertyBag bag)
        {
            Enabled = ReadBool(bag, "Enabled", true);
            Direction = ReadString(bag, "Direction", "Inbound");
        }

        protected override void SaveProperties(IPropertyBag bag)
        {
            WriteProperty(bag, "Enabled", Enabled);
            WriteProperty(bag, "Direction", Direction);
        }
    }
}
