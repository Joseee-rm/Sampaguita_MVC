using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using System.Security.Claims;

namespace SeniorManagement.Hubs
{
    public class ActivityHub : Hub
    {
        public async Task JoinActivityGroup()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "ActivityLogGroup");
        }

        public async Task JoinAdminDashboard()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "AdminDashboard");
        }

        public async Task LeaveActivityGroup()
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "ActivityLogGroup");
        }

        public async Task LeaveAdminDashboard()
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "AdminDashboard");
        }

        public async Task RequestInitialData()
        {
            // Client can request initial data when connecting
            await Clients.Caller.SendAsync("ConnectionEstablished", new
            {
                message = "Activity Hub connected",
                timestamp = DateTime.Now,
                connectionId = Context.ConnectionId
            });
        }

        public async Task SubscribeToUser(string userId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId}");
        }

        public async Task UnsubscribeFromUser(string userId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"User_{userId}");
        }

        public override async Task OnConnectedAsync()
        {
            var user = Context.User;
            var userId = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId}");
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var user = Context.User;
            var userId = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"User_{userId}");
            }

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "ActivityLogGroup");
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "AdminDashboard");

            await base.OnDisconnectedAsync(exception);
        }

        // Add these methods to ActivityHub.cs

        public async Task SendNotificationToUser(string userId, string message, string type = "info")
        {
            await Clients.User(userId).SendAsync("ReceiveNotification", new
            {
                message,
                type,
                timestamp = DateTime.Now
            });
        }

        public async Task SendNotificationToRole(string role, string message, string type = "info")
        {
            await Clients.Group(role).SendAsync("ReceiveNotification", new
            {
                message,
                type,
                timestamp = DateTime.Now
            });
        }

        public async Task SendNotificationToAll(string message, string type = "info")
        {
            await Clients.All.SendAsync("ReceiveNotification", new
            {
                message,
                type,
                timestamp = DateTime.Now
            });
        }
    }
}