using VanArsdel.Integration.Contracts;
using VanArsdel.Integration.Domain.Partners;

namespace VanArsdel.Integration.Messaging
{
    /// <summary>
    /// Works out which trading partner sent a message from what the adapters and the EDI disassembler put in the
    /// context: AS2-From, ISA06, UNB sender, the SFTP/FTP folder or the gateway user. Promotes VanArsdel.PartnerId.
    /// </summary>
    public static class PartnerResolution
    {
        public static TradingPartner Resolve(IMessageContext context, PartnerDirectory directory)
        {
            var existing = context.ReadVanArsdel(ContextProperties.PartnerId);
            if (!string.IsNullOrEmpty(existing))
            {
                var known = directory.Find(existing);
                if (known != null)
                {
                    return known;
                }
            }

            var as2 = context.ReadString(ContextProperties.As2From, ContextProperties.As2Namespace);
            var partner = directory.FindByAs2(as2);
            if (partner != null)
            {
                return partner;
            }

            var isa06 = context.ReadString(ContextProperties.Isa06, ContextProperties.EdiNamespace);
            if (!string.IsNullOrEmpty(isa06))
            {
                partner = directory.FindByInterchange(context.ReadString("ISA05", ContextProperties.EdiNamespace), isa06);
                if (partner != null)
                {
                    return partner;
                }
            }

            var unb = context.ReadString(ContextProperties.UnbSenderId, ContextProperties.EdiNamespace);
            if (!string.IsNullOrEmpty(unb))
            {
                partner = directory.FindByInterchange(context.ReadString("UNB2_2", ContextProperties.EdiNamespace), unb);
                if (partner != null)
                {
                    return partner;
                }
            }

            // Folder per partner on the SFTP server: /inbound/<PartnerId>/...
            var location = context.ReadString(ContextProperties.InboundTransportLocation, ContextProperties.SystemNamespace);
            if (!string.IsNullOrEmpty(location))
            {
                var parts = location.Replace('\\', '/').Split('/');
                for (var i = 0; i < parts.Length - 1; i++)
                {
                    if (string.Equals(parts[i], "inbound", System.StringComparison.OrdinalIgnoreCase))
                    {
                        partner = directory.Find(parts[i + 1]);
                        if (partner != null)
                        {
                            return partner;
                        }
                    }
                }
            }

            return null;
        }

        public static void Promote(IMessageContext context, TradingPartner partner)
        {
            context.PromoteVanArsdel(ContextProperties.PartnerId, partner.PartnerId);
        }
    }
}
