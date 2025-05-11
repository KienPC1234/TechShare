using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace LoginSystem.Hubs
{
    public class ChatHub : Hub
    {
        public async Task SendMessage(string senderId, string receiverId, string message)
        {
            var senderName = Context.User?.Identity?.Name ?? "Unknown";
            var createdAt = DateTime.UtcNow.ToString("dd/MM/yyyy HH:mm");
            await Clients.User(receiverId).SendAsync("ReceiveMessage", senderName, message, createdAt);
        }

        public async Task SendNotification(string userId, string notificationId, string content, string createdAt, string redirectUrl, string type)
        {
            await Clients.User(userId).SendAsync("ReceiveNotification", notificationId, content, createdAt, redirectUrl, type);
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.UserIdentifier;
            if (!string.IsNullOrEmpty(userId))
            {
                // Log connection or perform other initialization
            }
            await base.OnConnectedAsync();
        }
    }
}