using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Fabrikam.Housing.BackOffice.Hubs
{
    /// <summary>
    /// Live repairs board. Clients join the group for their patch ("ALL" for the repairs desk).
    /// </summary>
    public class RepairsHub : Hub
    {
        public const string AllPatches = "ALL";

        public Task JoinPatch(string patch)
        {
            return Groups.AddToGroupAsync(Context.ConnectionId, string.IsNullOrEmpty(patch) ? AllPatches : patch.ToUpperInvariant());
        }

        public Task LeavePatch(string patch)
        {
            return Groups.RemoveFromGroupAsync(Context.ConnectionId, string.IsNullOrEmpty(patch) ? AllPatches : patch.ToUpperInvariant());
        }
    }

    public class BoardUpdate
    {
        public int EventId { get; set; }
        public int WorkOrderId { get; set; }
        public string Reference { get; set; }
        public string Patch { get; set; }
        public string Address { get; set; }
        public string Priority { get; set; }
        public string Status { get; set; }
        public string Contractor { get; set; }
        public string Actor { get; set; }
        public string Text { get; set; }
        public string At { get; set; }
        public bool Overdue { get; set; }
    }
}
