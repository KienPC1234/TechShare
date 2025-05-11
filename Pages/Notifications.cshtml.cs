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
using Microsoft.Extensions.Logging;

namespace LoginSystem.Pages
{
    [Authorize]
    public class NotificationsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<NotificationsModel> _logger;

        public NotificationsModel(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<NotificationsModel> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        public List<Notification> Notifications { get; set; } = new List<Notification>();
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; }
        private const int PageSize = 10;

        public async Task<IActionResult> OnGetAsync(int page = 1)
        {
            try
            {
                var userId = _userManager.GetUserId(User);
                _logger.LogInformation("Loading notifications for user {UserId}, page {Page}", userId, page);
                var query = _context.Notifications
                    .Where(n => n.UserId == userId)
                    .OrderByDescending(n => n.CreatedAt);

                var totalNotifications = await query.CountAsync();
                TotalPages = (int)Math.Ceiling(totalNotifications / (double)PageSize);
                CurrentPage = Math.Max(1, Math.Min(page, TotalPages));

                Notifications = await query
                    .Skip((CurrentPage - 1) * PageSize)
                    .Take(PageSize)
                    .ToListAsync();

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading notifications for user {UserId}", _userManager.GetUserId(User));
                ModelState.AddModelError("", $"Lỗi khi tải thông báo: {ex.Message}");
                return Page();
            }
        }

        public async Task<IActionResult> OnPostMarkAsReadAsync(string id)
        {
            try
            {
                var userId = _userManager.GetUserId(User);
                var notification = await _context.Notifications
                    .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);

                if (notification == null)
                {
                    _logger.LogWarning("Notification {NotificationId} not found for user {UserId}", id, userId);
                    return new JsonResult(new { success = false, message = "Thông báo không tồn tại." });
                }

                notification.IsRead = true;
                await _context.SaveChangesAsync();
                _logger.LogInformation("Marked notification {NotificationId} as read for user {UserId}", id, userId);
                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking notification {NotificationId} as read for user {UserId}", id, _userManager.GetUserId(User));
                return new JsonResult(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        public async Task<IActionResult> OnPostDeleteAllAsync()
        {
            try
            {
                var userId = _userManager.GetUserId(User);
                _logger.LogInformation("Deleting all notifications for user {UserId}", userId);
                var notifications = await _context.Notifications
                    .Where(n => n.UserId == userId)
                    .ToListAsync();

                if (!notifications.Any())
                {
                    _logger.LogInformation("No notifications to delete for user {UserId}", userId);
                    return new JsonResult(new { success = true, message = "Không có thông báo để xóa." });
                }

                _context.Notifications.RemoveRange(notifications);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Deleted {Count} notifications for user {UserId}", notifications.Count, userId);
                return new JsonResult(new { success = true, message = "Đã xóa tất cả thông báo." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting all notifications for user {UserId}", _userManager.GetUserId(User));
                return new JsonResult(new { success = false, message = $"Lỗi khi xóa thông báo: {ex.Message}" });
            }
        }

        public async Task<string> GetOrganizationSlugAsync(string? organizationId)
        {
            if (string.IsNullOrEmpty(organizationId))
                return "#";

            var slug = await _context.Organizations
                .AsNoTracking()
                .Where(o => o.Id == organizationId)
                .Select(o => o.Slug)
                .FirstOrDefaultAsync();

            return slug ?? "#";
        }
    }
}