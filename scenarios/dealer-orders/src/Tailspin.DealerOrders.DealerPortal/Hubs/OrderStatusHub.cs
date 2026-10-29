using System.Security.Claims;
using System.Threading.Tasks;
using log4net;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;
using Newtonsoft.Json.Linq;
using Tailspin.DealerOrders.Data;
using Tailspin.DealerOrders.Infrastructure;

namespace Tailspin.DealerOrders.DealerPortal.Hubs
{
    /// <summary>
    /// Order and quote status pushed to the dealer's open browsers. Browsers join their dealer's group; the
    /// notification service connects with the service key and calls Publish.
    /// </summary>
    [HubName("OrderStatusHub")]
    [ServiceOrUserAuthorize]
    public class OrderStatusHub : Hub
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(OrderStatusHub));

        public override Task OnConnected()
        {
            var dealerId = DealerIdOf(Context.User);
            return dealerId.HasValue ? Groups.Add(Context.ConnectionId, Group(dealerId.Value)) : base.OnConnected();
        }

        public void Publish(int dealerId, string payloadJson)
        {
            if (!ServiceOrUserAuthorizeAttribute.IsService(Context.Request))
                throw new HubException("Only the notification service may publish.");
            Clients.Group(Group(dealerId)).statusChanged(JObject.Parse(payloadJson));
            Log.DebugFormat("Pushed to dealer {0}", dealerId);
        }

        private static string Group(int dealerId) => "dealer-" + dealerId;

        private static int? DealerIdOf(System.Security.Principal.IPrincipal user)
        {
            var claim = (user?.Identity as ClaimsIdentity)?.FindFirst(PortalUser.DealerIdClaim);
            return claim != null && int.TryParse(claim.Value, out var id) ? id : (int?)null;
        }
    }

    /// <summary>Signed-in dealer users, or the notification service presenting Push.ServiceKey.</summary>
    public class ServiceOrUserAuthorizeAttribute : AuthorizeAttribute
    {
        private static readonly string ServiceKey = Settings.Get("Push.ServiceKey");

        public static bool IsService(IRequest request)
        {
            var key = request.Headers["X-Tailspin-Service-Key"];
            return !string.IsNullOrEmpty(ServiceKey) && key == ServiceKey;
        }

        public override bool AuthorizeHubConnection(HubDescriptor hubDescriptor, IRequest request) =>
            IsService(request) || base.AuthorizeHubConnection(hubDescriptor, request);

        public override bool AuthorizeHubMethodInvocation(IHubIncomingInvokerContext hubIncomingInvokerContext, bool appliesToMethod) =>
            IsService(hubIncomingInvokerContext.Hub.Context.Request) || base.AuthorizeHubMethodInvocation(hubIncomingInvokerContext, appliesToMethod);
    }
}
