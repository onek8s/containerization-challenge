using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using log4net;
using Microsoft.BizTalk.Message.Interop;
using Microsoft.BizTalk.Operations;

namespace VanArsdel.Integration.Management.Operations
{
    /// <summary>
    /// IInstanceOperations over Microsoft.BizTalk.Operations. BizTalkOperations opens its own connections to the
    /// management database and the MessageBox, so an instance is created per call and disposed.
    /// </summary>
    public class BizTalkInstanceOperations : IInstanceOperations
    {
        private const string SystemNs = "http://schemas.microsoft.com/BizTalk/2003/system-properties";
        private const string FileNs = "http://schemas.microsoft.com/BizTalk/2003/file-properties";
        private const string PropertyNs = "https://VanArsdel.Integration.Schemas.PropertySchema";
        private const int MaxBodyBytes = 4 * 1024 * 1024;

        private static readonly ILog Log = LogManager.GetLogger(typeof(BizTalkInstanceOperations));
        private readonly BizTalkGroup _group;

        public BizTalkInstanceOperations(BizTalkGroup group)
        {
            _group = group;
        }

        public IList<SuspendedInstanceInfo> Suspended(InstanceFilter filter)
        {
            filter = filter ?? new InstanceFilter();
            var list = new List<SuspendedInstanceInfo>();
            using (var ops = Open())
            {
                foreach (var item in ops.GetServiceInstances())
                {
                    var instance = item as MessageBoxServiceInstance;
                    if (instance == null || !IsSuspended(instance.InstanceStatus))
                    {
                        continue;
                    }

                    var info = ToInfo(instance);
                    if (!Matches(info, filter))
                    {
                        continue;
                    }

                    list.Add(info);
                    if (list.Count >= filter.MaxRows)
                    {
                        break;
                    }
                }
            }

            return list.OrderByDescending(i => i.SuspendedUtc).ToList();
        }

        public SuspendedInstanceInfo Find(Guid instanceId)
        {
            using (var ops = Open())
            {
                var instance = ops.GetServiceInstance(instanceId);
                return instance == null ? null : ToInfo(instance);
            }
        }

        public InstanceMessage GetMessage(Guid instanceId, Guid messageId)
        {
            using (var ops = Open())
            {
                var message = ops.GetMessage(messageId, instanceId);
                if (message == null)
                {
                    return null;
                }

                var result = new InstanceMessage
                {
                    MessageId = message.MessageID,
                    InstanceId = instanceId,
                    MessageType = message.MessageType,
                    BodyPartName = message.BodyPartName
                };

                ReadContext(message.Context, result);
                result.ReceivePortName = result.Property(SystemNs, "ReceivePortName");
                result.InboundTransportLocation = result.Property(SystemNs, "InboundTransportLocation");
                result.PartnerId = result.Property(PropertyNs, "PartnerId");

                var body = message.BodyPart;
                if (body != null)
                {
                    result.Charset = body.Charset;
                    result.Body = ReadBody(body);
                }

                return result;
            }
        }

        public InstanceActionResult Resume(Guid instanceId)
        {
            using (var ops = Open())
            {
                return Result(instanceId, "resume", ops.ResumeInstance(instanceId));
            }
        }

        public InstanceActionResult Terminate(Guid instanceId)
        {
            using (var ops = Open())
            {
                return Result(instanceId, "terminate", ops.TerminateInstance(instanceId));
            }
        }

        internal static bool Matches(SuspendedInstanceInfo info, InstanceFilter filter)
        {
            if (!string.IsNullOrEmpty(filter.Application) && !string.Equals(info.Application, filter.Application, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(filter.ServiceContains) && (info.ServiceName ?? string.Empty).IndexOf(filter.ServiceContains, StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(filter.ErrorContains) && (info.ErrorDescription ?? string.Empty).IndexOf(filter.ErrorContains, StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            if (filter.Kind.HasValue && info.Kind != filter.Kind.Value)
            {
                return false;
            }

            return !filter.SuspendedAfterUtc.HasValue || info.SuspendedUtc >= filter.SuspendedAfterUtc.Value;
        }

        private BizTalkOperations Open()
        {
            return new BizTalkOperations(_group.ManagementServer, _group.ManagementDatabase);
        }

        private static bool IsSuspended(InstanceStatus status)
        {
            return status == InstanceStatus.Suspended || status == InstanceStatus.SuspendedNotResumable;
        }

        private static SuspendedInstanceInfo ToInfo(MessageBoxServiceInstance instance)
        {
            var info = new SuspendedInstanceInfo
            {
                InstanceId = instance.ID,
                Kind = KindOf(instance),
                Application = instance.Application,
                ServiceName = instance.ServiceType,
                HostName = instance.HostName,
                Adapter = instance.Adapter,
                Uri = instance.URI,
                Resumable = instance.InstanceStatus == InstanceStatus.Suspended,
                CreatedUtc = instance.CreationTime.ToUniversalTime(),
                SuspendedUtc = instance.SuspendTime.ToUniversalTime(),
                ErrorCode = instance.ErrorCode,
                ErrorDescription = instance.ErrorDescription,
                ProcessingServer = instance.ErrorProcessingServer
            };

            if (instance.Messages != null)
            {
                foreach (var m in instance.Messages)
                {
                    var message = m as BizTalkMessage;
                    if (message != null)
                    {
                        info.MessageIds.Add(message.MessageID);
                    }
                }
            }

            return info;
        }

        private static InstanceKind KindOf(MessageBoxServiceInstance instance)
        {
            if (instance is OrchestrationInstance)
            {
                return InstanceKind.Orchestration;
            }

            if (instance is SendPortInstance)
            {
                return InstanceKind.SendPort;
            }

            if (instance is RoutingFailureInstance)
            {
                return InstanceKind.RoutingFailure;
            }

            if (instance.Class == ServiceClass.Messaging)
            {
                return string.IsNullOrEmpty(instance.Adapter) ? InstanceKind.Isolated : InstanceKind.ReceiveLocation;
            }

            return InstanceKind.Other;
        }

        private static void ReadContext(IBaseMessageContext context, InstanceMessage target)
        {
            if (context == null)
            {
                return;
            }

            for (var i = 0; i < (int)context.CountProperties; i++)
            {
                string name;
                string ns;
                var value = context.ReadAt(i, out name, out ns);
                target.Context.Add(new ContextEntry
                {
                    Name = name,
                    Namespace = ns,
                    Value = Convert.ToString(value, CultureInfo.InvariantCulture),
                    Promoted = context.IsPromoted(name, ns)
                });
            }
        }

        private static byte[] ReadBody(IBaseMessagePart part)
        {
            var stream = part.GetOriginalDataStream() ?? part.Data;
            if (stream == null)
            {
                return new byte[0];
            }

            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            using (var buffer = new MemoryStream())
            {
                var chunk = new byte[81920];
                int read;
                while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
                {
                    if (buffer.Length + read > MaxBodyBytes)
                    {
                        throw new InvalidDataException("Message body is larger than " + MaxBodyBytes / 1024 / 1024 + " MB; save it from the Administration Console instead.");
                    }

                    buffer.Write(chunk, 0, read);
                }

                return buffer.ToArray();
            }
        }

        private static InstanceActionResult Result(Guid instanceId, string action, CompletionStatus status)
        {
            var ok = status == CompletionStatus.Succeeded || status == CompletionStatus.Pending;
            if (!ok)
            {
                Log.WarnFormat("Could not {0} instance {1}: {2}", action, instanceId, status);
            }

            return new InstanceActionResult
            {
                InstanceId = instanceId,
                Succeeded = ok,
                Message = string.Format(CultureInfo.InvariantCulture, "{0} {1}: {2}", action, instanceId, status)
            };
        }
    }
}
