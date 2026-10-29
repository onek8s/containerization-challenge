using System;
using System.Collections.Generic;

namespace VanArsdel.Integration.Management.Operations
{
    /// <summary>Suspended instance handling for the group (BizTalkOperations on the servers).</summary>
    public interface IInstanceOperations
    {
        IList<SuspendedInstanceInfo> Suspended(InstanceFilter filter);

        SuspendedInstanceInfo Find(Guid instanceId);

        InstanceMessage GetMessage(Guid instanceId, Guid messageId);

        InstanceActionResult Resume(Guid instanceId);

        InstanceActionResult Terminate(Guid instanceId);
    }
}
