using System;
using System.Collections.Generic;

namespace VanArsdel.Integration.Management.Operations
{
    public enum InstanceKind
    {
        Other = 0,
        Orchestration = 1,
        SendPort = 2,
        ReceiveLocation = 3,
        RoutingFailure = 4,
        Isolated = 5
    }

    /// <summary>A suspended service instance as the operations portal lists it.</summary>
    public class SuspendedInstanceInfo
    {
        public Guid InstanceId { get; set; }
        public InstanceKind Kind { get; set; }
        public string Application { get; set; }
        public string ServiceName { get; set; }
        public string HostName { get; set; }
        public string Adapter { get; set; }
        public string Uri { get; set; }
        public bool Resumable { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime SuspendedUtc { get; set; }
        public string ErrorCode { get; set; }
        public string ErrorDescription { get; set; }
        public string ProcessingServer { get; set; }
        public List<Guid> MessageIds { get; } = new List<Guid>();

        /// <summary>Short service name without the assembly-qualified namespace (VanArsdel.Integration.Orchestrations.X).</summary>
        public string ShortServiceName
        {
            get
            {
                if (string.IsNullOrEmpty(ServiceName))
                {
                    return string.Empty;
                }

                var dot = ServiceName.LastIndexOf('.');
                return dot < 0 ? ServiceName : ServiceName.Substring(dot + 1);
            }
        }
    }

    /// <summary>One message of a suspended instance with its promoted and written context.</summary>
    public class InstanceMessage
    {
        public Guid MessageId { get; set; }
        public Guid InstanceId { get; set; }
        public string MessageType { get; set; }
        public string ReceivePortName { get; set; }
        public string InboundTransportLocation { get; set; }
        public string PartnerId { get; set; }
        public string BodyPartName { get; set; }
        public string Charset { get; set; }
        public byte[] Body { get; set; }
        public List<ContextEntry> Context { get; } = new List<ContextEntry>();

        public string Property(string ns, string name)
        {
            foreach (var c in Context)
            {
                if (c.Name == name && c.Namespace == ns)
                {
                    return c.Value;
                }
            }

            return null;
        }
    }

    public class ContextEntry
    {
        public string Name { get; set; }
        public string Namespace { get; set; }
        public string Value { get; set; }
        public bool Promoted { get; set; }
    }

    public class InstanceActionResult
    {
        public Guid InstanceId { get; set; }
        public bool Succeeded { get; set; }
        public string Message { get; set; }
    }

    public class InstanceFilter
    {
        public string Application { get; set; }
        public string ServiceContains { get; set; }
        public string ErrorContains { get; set; }
        public InstanceKind? Kind { get; set; }
        public DateTime? SuspendedAfterUtc { get; set; }
        public int MaxRows { get; set; } = 500;
    }
}
