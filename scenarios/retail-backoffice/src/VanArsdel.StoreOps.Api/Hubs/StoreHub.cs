using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using VanArsdel.BuildingBlocks.Security;
using VanArsdel.Contracts;

namespace VanArsdel.StoreOps.Api.Hubs
{
    /// <summary>Live updates for the store manager app and the back office store dashboards.</summary>
    [Authorize]
    public class StoreHub : Hub
    {
        public static string Group(string storeCode) => "store-" + storeCode.ToUpperInvariant();

        public const string AllStoresGroup = "stores-all";

        public async Task JoinStore(string storeCode)
        {
            var own = Context.User.StoreCode();
            if (!string.Equals(own, storeCode, StringComparison.OrdinalIgnoreCase)
                && !Context.User.HasAnyRole(Roles.Admin, Roles.Merchandiser, Roles.CustomerService))
            {
                throw new HubException("Not allowed to follow this store.");
            }
            await Groups.AddToGroupAsync(Context.ConnectionId, Group(storeCode));
        }

        public async Task JoinAllStores()
        {
            if (!Context.User.HasAnyRole(Roles.Admin, Roles.Merchandiser, Roles.Finance))
            {
                throw new HubException("Not allowed.");
            }
            await Groups.AddToGroupAsync(Context.ConnectionId, AllStoresGroup);
        }

        public Task LeaveStore(string storeCode) => Groups.RemoveFromGroupAsync(Context.ConnectionId, Group(storeCode));
    }
}
