using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;

namespace AcademicResourceApp.Hubs
{
    public class NotificationHub : Hub
    {
        // Called when a client connects
        public override Task OnConnectedAsync()
        {
            var userId = Context.UserIdentifier; // Should be the GUID of the user
            if (!string.IsNullOrEmpty(userId))
            {
                Groups.AddToGroupAsync(Context.ConnectionId, userId);
            }
            return base.OnConnectedAsync();
        }

        // Called when a client disconnects
        public override Task OnDisconnectedAsync(Exception exception)
        {
            var userId = Context.UserIdentifier;
            if (!string.IsNullOrEmpty(userId))
            {
                Groups.RemoveFromGroupAsync(Context.ConnectionId, userId);
            }
            return base.OnDisconnectedAsync(exception);
        }
    }
}
