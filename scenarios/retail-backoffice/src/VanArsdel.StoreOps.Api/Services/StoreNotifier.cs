using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using VanArsdel.StoreOps.Api.Hubs;

namespace VanArsdel.StoreOps.Api.Services
{
    /// <summary>Pushes changes to the store manager app. Event names are part of the client contract.</summary>
    public class StoreNotifier
    {
        public const string TaskCreated = "taskCreated";
        public const string TaskChanged = "taskChanged";
        public const string CollectionReady = "collectionReady";
        public const string CollectionChanged = "collectionChanged";
        public const string SalesUpdated = "salesUpdated";

        private readonly IHubContext<StoreHub> _hub;

        public StoreNotifier(IHubContext<StoreHub> hub)
        {
            _hub = hub;
        }

        public Task SendAsync(string storeCode, string eventName, object payload) =>
            _hub.Clients.Groups(StoreHub.Group(storeCode), StoreHub.AllStoresGroup).SendAsync(eventName, payload);
    }
}
