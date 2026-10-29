using System;
using System.Runtime.InteropServices;
using Microsoft.BizTalk.Component.Interop;
using Microsoft.BizTalk.Message.Interop;
using VanArsdel.Integration.Contracts;
using VanArsdel.Integration.Messaging;
using VanArsdel.Integration.PipelineComponents.Common;
using VanArsdel.Integration.Runtime.Partners;

namespace VanArsdel.Integration.PipelineComponents.Components
{
    /// <summary>
    /// ResolveParty stage: finds our trading partner from AS2-From, ISA06, UNB sender or the per-partner SFTP folder
    /// and promotes VanArsdel.PartnerId, which every subscription and the ack correlation use.
    /// </summary>
    [ComponentCategory(CategoryTypes.CATID_PipelineComponent)]
    [ComponentCategory(CategoryTypes.CATID_PartyResolver)]
    [ComponentCategory(CategoryTypes.CATID_Any)]
    [Guid("0C93F2B4-7E61-4B0A-8C25-5D4E8A1B9F22")]
    public class PartnerResolverComponent : ComponentBase, IComponent
    {
        public override string Name
        {
            get { return "VanArsdel Partner Resolver"; }
        }

        public override string Description
        {
            get { return "Promotes VanArsdel.PartnerId from the AS2, EDI envelope or folder information."; }
        }

        /// <summary>Unknown senders are suspended when true; otherwise the message continues without PartnerId (and fails routing).</summary>
        public bool FailWhenUnknown { get; set; }

        public IBaseMessage Execute(IPipelineContext pContext, IBaseMessage pInMsg)
        {
            var context = new ContextAdapter(pInMsg.Context);
            var partner = PartnerResolution.Resolve(context, PartnerCache.Directory);
            if (partner == null)
            {
                if (FailWhenUnknown)
                {
                    throw new UnknownSenderException(Describe(context));
                }

                return pInMsg;
            }

            if (!partner.Active)
            {
                throw new UnknownSenderException("Partner " + partner.PartnerId + " is inactive. " + Describe(context));
            }

            PartnerResolution.Promote(context, partner);
            return pInMsg;
        }

        private static string Describe(IMessageContext context)
        {
            return "AS2From=" + context.ReadString(ContextProperties.As2From, ContextProperties.As2Namespace)
                   + " ISA06=" + context.ReadString(ContextProperties.Isa06, ContextProperties.EdiNamespace)
                   + " UNB2.1=" + context.ReadString(ContextProperties.UnbSenderId, ContextProperties.EdiNamespace)
                   + " Location=" + context.ReadString(ContextProperties.InboundTransportLocation, ContextProperties.SystemNamespace);
        }

        protected override void LoadProperties(IPropertyBag bag)
        {
            FailWhenUnknown = ReadBool(bag, "FailWhenUnknown", true);
        }

        protected override void SaveProperties(IPropertyBag bag)
        {
            WriteProperty(bag, "FailWhenUnknown", FailWhenUnknown);
        }
    }

    [Serializable]
    public class UnknownSenderException : Exception
    {
        public UnknownSenderException(string message)
            : base("The sender could not be resolved to a trading partner: " + message)
        {
        }

        protected UnknownSenderException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
            : base(info, context)
        {
        }
    }
}
