using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using LoginSystem.Data;
using LoginSystem.Models;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using LoginSystem.Hubs;

namespace LoginSystem.Pages.Messages
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHubContext<ChatHub> _hubContext;

        public IndexModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IHubContext<ChatHub> hubContext)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        }

        public List<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();
        public List<Message> Messages { get; set; } = new List<Message>();
        public string? SelectedUserId { get; set; }
        public ApplicationUser? SelectedUser { get; set; }
        public string? ErrorMessage { get; set; }
        public ApplicationDbContext DbContext => _context;

        public async Task<IActionResult> OnGetAsync(string userId)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                {
                    return StatusCode(401, "Người dùng không được xác thực.");
                }

                // Load all users except the current user
                Users = await _context.Users
                    .Where(u => u.Id != currentUser.Id)
                    .OrderBy(u => u.DisplayName)
                    .ToListAsync();

                if (!string.IsNullOrEmpty(userId))
                {
                    SelectedUserId = userId;
                    SelectedUser = await _context.Users.FindAsync(userId);
                    if (SelectedUser == null)
                    {
                        ErrorMessage = "Người dùng không tồn tại.";
                        return Page();
                    }

                    // Load messages between current user and selected user
                    Messages = await _context.Messages
                        .Include(m => m.Sender)
                        .Include(m => m.Receiver)
                        .Where(m => (m.SenderId == currentUser.Id && m.ReceiverId == userId) ||
                                    (m.SenderId == userId && m.ReceiverId == currentUser.Id))
                        .OrderBy(m => m.CreatedAt)
                        .Take(50)
                        .ToListAsync();

                    // Mark messages from selected user as read
                    var unreadMessages = Messages
                        .Where(m => m.ReceiverId == currentUser.Id && !m.IsRead)
                        .ToList();
                    foreach (var message in unreadMessages)
                    {
                        message.IsRead = true;
                    }
                    await _context.SaveChangesAsync();
                }

                return Page();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Lỗi khi tải tin nhắn: {ex.Message}";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostSendAsync(string receiverId, string content)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(receiverId))
                {
                    ErrorMessage = "Không xác định được người nhận.";
                    await OnGetAsync(null);
                    return Page();
                }

                if (string.IsNullOrWhiteSpace(content) || content.Trim().Length > 1000)
                {
                    ErrorMessage = "Tin nhắn không được để trống hoặc vượt quá 1000 ký tự.";
                    await OnGetAsync(receiverId);
                    return Page();
                }

                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                {
                    return StatusCode(401, "Người dùng không được xác thực.");
                }

                var receiver = await _context.Users.FindAsync(receiverId);
                if (receiver == null)
                {
                    ErrorMessage = "Người nhận không tồn tại.";
                    await OnGetAsync(null);
                    return Page();
                }

                var message = new Message
                {
                    Id = Guid.NewGuid().ToString(),
                    SenderId = currentUser.Id,
                    ReceiverId = receiverId,
                    Content = content.Trim(),
                    CreatedAt = DateTime.UtcNow
                };

                _context.Messages.Add(message);
                await _context.SaveChangesAsync();

                // Send message via SignalR
                await _hubContext.Clients.User(receiverId).SendAsync("ReceiveMessage", currentUser.DisplayName, message.Content, message.CreatedAt.ToString("dd/MM/yyyy HH:mm"));

                return RedirectToPage(new { userId = receiverId });
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Lỗi khi gửi tin nhắn: {ex.Message}";
                await OnGetAsync(receiverId);
                return Page();
            }
        }

        public async Task<IActionResult> OnPostMarkAsReadAsync(string id)
        {
            try
            {
                var message = await _context.Messages.FindAsync(id);
                if (message == null)
                {
                    return NotFound("Tin nhắn không tồn tại.");
                }

                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null || message.ReceiverId != currentUser.Id)
                {
                    return StatusCode(403, "Không có quyền đánh dấu tin nhắn này.");
                }

                message.IsRead = true;
                await _context.SaveChangesAsync();
                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, error = ex.Message });
            }
        }
    }
}