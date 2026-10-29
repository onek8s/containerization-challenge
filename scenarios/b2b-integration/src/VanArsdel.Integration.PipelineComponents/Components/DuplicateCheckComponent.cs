using System;
using System.Runtime.InteropServices;
using Microsoft.BizTalk.Component.Interop;
using Microsoft.BizTalk.Message.Interop;
using VanArsdel.Integration.Contracts;
using VanArsdel.Integration.Data;
using VanArsdel.Integration.Messaging;
using VanArsdel.Integration.PipelineComponents.Common;
using VanArsdel.Integration.Runtime.Configuration;

namespace VanArsdel.Integration.PipelineComponents.Components
{
    /// <summary>
    /// Suspends a partner file that was already received (same interchange control number or same body hash
    /// within the window). Partners resend after network errors, and FTP polling occasionally picks a file twice.
    /// Place it before the disassembler so the interchange is rejected as a whole.
    /// </summary>
    [ComponentCategory(CategoryTypes.CATID_PipelineComponent)]
    [ComponentCategory(CategoryTypes.CATID_Any)]
    [Guid("A4E15C77-23B9-4F08-B6C1-8F7A2D0E3B33")]
    public class DuplicateCheckComponent : ComponentBase, IComponent
    {
        public DuplicateCheckComponent()
        {
            WindowHours = 72;
            Enabled = true;
        }

        public override string Name
        {
            get { return "VanArsdel Duplicate Check"; }
        }

        public override string Description
        {
            get { return "Suspends interchanges already received from the same partner."; }
        }

        public bool Enabled { get; set; }
        public int WindowHours { get; set; }

        public IBaseMessage Execute(IPipelineContext pContext, IBaseMessage pInMsg)
        {
            if (!Enabled)
            {
                return pInMsg;
            }

            var body = MessageBodies.ReadAll(pInMsg);
            var hash = ArchiveNaming.Hash(body);
            var context = new ContextAdapter(pInMsg.Context);
            context.WriteVanArsdel(ContextProperties.BodyHash, hash);

            var partnerId = context.ReadVanArsdel(ContextProperties.PartnerId) ?? "unknown";
            var control = context.ReadVanArsdel(ContextProperties.InterchangeControlNumber)
                          ?? context.ReadString(ContextProperties.Isa13, ContextProperties.EdiNamespace)
                          ?? context.ReadString(ContextProperties.UnbReference, ContextProperties.EdiNamespace);

            var detector = new DuplicateDetector(new InterchangeRepository(IntegrationDatabases.FromSso()), TimeSpan.FromHours(WindowHours));
            if (detector.IsDuplicate(partnerId, control, hash, DateTime.UtcNow))
            {
                throw new DuplicateInterchangeException(partnerId, control, hash);
            }

            MessageBodies.Replace(pContext, pInMsg, body);
            return pInMsg;
        }

        protected override void LoadProperties(IPropertyBag bag)
        {
            Enabled = ReadBool(bag, "Enabled", true);
            WindowHours = ReadInt(bag, "WindowHours", 72);
        }

        protected override void SaveProperties(IPropertyBag bag)
        {
            WriteProperty(bag, "Enabled", Enabled);
            WriteProperty(bag, "WindowHours", WindowHours);
        }
    }

    [Serializable]
    public class DuplicateInterchangeException : Exception
    {
        public DuplicateInterchangeException(string partnerId, string controlNumber, string hash)
            : base("Duplicate interchange from " + partnerId + " (control number " + (controlNumber ?? "-") + ", hash " + hash + ").")
        {
        }

        protected DuplicateInterchangeException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
            : base(info, context)
        {
        }
    }
}
