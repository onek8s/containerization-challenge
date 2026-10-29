using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNet.SignalR;
using Tailspin.CustomerCare.Web.Security;

namespace Tailspin.CustomerCare.Web.Realtime
{
    /// <summary>
    /// Live updates for the queue board, the case list and the case page.
    /// Clients join a group per queue ("queue:BILLING") or per case ("case:123").
    /// </summary>
    [Authorize]
    public class QueueHub : Hub
    {
        public Task WatchQueue(string queueCode)
        {
            return Groups.Add(Context.ConnectionId, "queue:" + (queueCode ?? string.Empty).ToUpperInvariant());
        }

        public Task UnwatchQueue(string queueCode)
        {
            return Groups.Remove(Context.ConnectionId, "queue:" + (queueCode ?? string.Empty).ToUpperInvariant());
        }

        public Task WatchAll()
        {
            return Groups.Add(Context.ConnectionId, "all");
        }

        /// <summary>Called by the case page; returns who else has the case open.</summary>
        public IList<string> OpenCase(int caseId)
        {
            var name = DisplayName();
            Groups.Add(Context.ConnectionId, "case:" + caseId);
            var viewers = CasePresence.Enter(caseId, Context.ConnectionId, name);
            Clients.OthersInGroup("case:" + caseId).viewersChanged(caseId, viewers);
            return viewers;
        }

        public override Task OnDisconnected(bool stopCalled)
        {
            int caseId;
            if (CasePresence.Leave(Context.ConnectionId, out caseId))
            {
                Clients.Group("case:" + caseId).viewersChanged(caseId, CasePresence.Viewers(caseId));
            }

            return base.OnDisconnected(stopCalled);
        }

        private string DisplayName()
        {
            var name = Context.User != null ? Context.User.Identity.Name : null;
            return string.IsNullOrEmpty(name) ? "?" : UserDirectory.DisplayName(name);
        }
    }
}
