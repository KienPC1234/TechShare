using Microsoft.AspNetCore.SignalR;
using LoginSystem.Data;
using LoginSystem.Models;
using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;
using System;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace LoginSystem.Hubs
{
    public class MessHub : Hub
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<MessHub> _logger;
        private static readonly ConcurrentDictionary<string, string> _onlineUsers = new ConcurrentDictionary<string, string>();

        public MessHub(ApplicationDbContext context, UserManager<ApplicationUser> userManager, ILogger<MessHub> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            var user = await _userManager.GetUserAsync(Context.User);
            if (user != null)
            {
                _onlineUsers.TryAdd(user.Id, Context.ConnectionId);
                _logger.LogInformation("User connected: {UserId}, {DisplayName}, ConnectionId: {ConnectionId}", user.Id, user.DisplayName ?? user.UserName, Context.ConnectionId);
                await Clients.All.SendAsync("UserConnected", user.Id, user.DisplayName ?? user.UserName);
                await UpdateOnlineUsers();
            }
            else
            {
                _logger.LogWarning("No user found for connection: {ConnectionId}", Context.ConnectionId);
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var user = await _userManager.GetUserAsync(Context.User);
            if (user != null)
            {
                _onlineUsers.TryRemove(user.Id, out _);
                _logger.LogInformation("User disconnected: {UserId}, {DisplayName}", user.Id, user.DisplayName ?? user.UserName);
                await Clients.All.SendAsync("UserDisconnected", user.Id, user.DisplayName ?? user.UserName);
                await UpdateOnlineUsers();
            }
            await base.OnDisconnectedAsync(exception);
        }

        private async Task UpdateOnlineUsers()
        {
            var onlineUserIds = _onlineUsers.Keys.ToList();
            _logger.LogInformation("Updating online users: {UserIds}", string.Join(", ", onlineUserIds));
            await Clients.All.SendAsync("UpdateOnlineUsers", onlineUserIds);
        }

        public async Task SendMessage(string senderId, string receiverId, string content, string contentType)
        {
            try
            {
                _logger.LogInformation("Sending message: Sender={SenderId}, Receiver={ReceiverId}, Content={Content}, Type={ContentType}", senderId, receiverId, content, contentType);

                if (string.IsNullOrEmpty(senderId) || string.IsNullOrEmpty(receiverId))
                {
                    _logger.LogWarning("Invalid sender or receiver ID: Sender={SenderId}, Receiver={ReceiverId}", senderId, receiverId);
                    throw new HubException("Sender or receiver ID is empty.");
                }

                if (string.IsNullOrEmpty(content) || string.IsNullOrEmpty(contentType))
                {
                    _logger.LogWarning("Invalid content or content type: Content={Content}, ContentType={ContentType}", content, contentType);
                    throw new HubException("Content or content type is empty.");
                }

                var sender = await _userManager.FindByIdAsync(senderId);
                var receiver = await _userManager.FindByIdAsync(receiverId);
                if (sender == null || receiver == null)
                {
                    _logger.LogWarning("Sender or receiver not found: Sender={SenderId}, Receiver={ReceiverId}", senderId, receiverId);
                    throw new HubException("Invalid sender or receiver.");
                }

                var message = new Message
                {
                    SenderId = senderId,
                    ReceiverId = receiverId,
                    Content = content,
                    ContentType = contentType,
                    CreatedAt = DateTime.UtcNow
                };

                _logger.LogInformation("Saving message to database: Sender={SenderId}, Receiver={ReceiverId}", senderId, receiverId);
                await _context.Messages.AddAsync(message);
                await _context.SaveChangesAsync();

                var messageDto = new
                {
                    id = message.Id,
                    senderId = message.SenderId,
                    senderName = sender.DisplayName ?? sender.UserName,
                    senderAvatar = sender.AvatarUrl,
                    receiverId = message.ReceiverId,
                    content = message.Content,
                    contentType = message.ContentType,
                    createdAt = message.CreatedAt
                };

                _logger.LogInformation("Message saved and sending to users: {SenderId}, {ReceiverId}", senderId, receiverId);

                await Clients.User(senderId).SendAsync("ReceiveMessage", messageDto);
                await Clients.User(receiverId).SendAsync("ReceiveMessage", messageDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message: Sender={SenderId}, Receiver={ReceiverId}, Message={Message}", senderId, receiverId, ex.Message);
                throw;
            }
        }
    }
}