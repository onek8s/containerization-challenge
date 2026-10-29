using System;

namespace VanArsdel.Integration.OpsPortal.Models
{
    public class InstanceRow
    {
        public Guid InstanceId { get; set; }
        public string Kind { get; set; }
        public string Application { get; set; }
        public string Service { get; set; }
        public string HostName { get; set; }
        public string Adapter { get; set; }
        public string Uri { get; set; }
        public bool Resumable { get; set; }
        public DateTime SuspendedLocal { get; set; }
        public string ErrorCode { get; set; }
        public string ErrorShort { get; set; }
        public int Messages { get; set; }
    }

    public class PartnerRow
    {
        public string PartnerId { get; set; }
        public string Name { get; set; }
        public string Role { get; set; }
        public string Standard { get; set; }
        public string Transport { get; set; }
        public string Interchange { get; set; }
        public string ErpNumber { get; set; }
        public bool Active { get; set; }
        public bool TestIndicator { get; set; }
        public string PartyState { get; set; }
    }

    public class XrefRow
    {
        public string PartnerId { get; set; }
        public string Kind { get; set; }
        public string PartnerValue { get; set; }
        public string InternalValue { get; set; }
        public DateTime? ValidFrom { get; set; }
        public DateTime? ValidTo { get; set; }
    }

    public class HostRow
    {
        public string HostName { get; set; }
        public string ServerName { get; set; }
        public string State { get; set; }
        public bool Disabled { get; set; }
        public bool Running { get; set; }

        public string Name
        {
            get { return HostName + " @ " + ServerName; }
        }
    }

    public class ContextRow
    {
        public string Name { get; set; }
        public string Namespace { get; set; }
        public string Value { get; set; }
        public bool Promoted { get; set; }
    }
}
