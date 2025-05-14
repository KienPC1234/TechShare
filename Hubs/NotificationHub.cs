using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LoginSystem.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly ILogger<ChatHub> _logger;

        public ChatHub(ILogger<ChatHub> logger)
        {
            _logger = logger;
        }

        public async Task SendNotification(string userId, string notificationId, string content, string createdAt, string redirectUrl, string type)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(notificationId) || string.IsNullOrEmpty(content))
            {
                _logger.LogWarning("Invalid notification data: userId={UserId}, notificationId={NotificationId}, content={Content}", userId, notificationId, content);
                return;
            }

            if (!DateTime.TryParse(createdAt, out _))
            {
                _logger.LogWarning("Invalid createdAt format: {CreatedAt}", createdAt);
                return;
            }

            _logger.LogInformation("Sending notification {NotificationId} to user {UserId}", notificationId, userId);
            await Clients.User(userId).SendAsync("ReceiveNotification", notificationId, content, createdAt, redirectUrl, type);
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.UserIdentifier;
            if (!string.IsNullOrEmpty(userId))
            {
                _logger.LogInformation("User {UserId} connected to NotificationHub", userId);
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.UserIdentifier;
            if (!string.IsNullOrEmpty(userId))
            {
                _logger.LogInformation("User {UserId} disconnected from NotificationHub", userId);
            }
            await base.OnDisconnectedAsync(exception);
        }
    }
}