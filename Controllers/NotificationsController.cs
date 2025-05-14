using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LoginSystem.Data;
using LoginSystem.Models;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LoginSystem.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class NotificationsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<NotificationsController> _logger;

        public NotificationsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<NotificationsController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetNotifications([FromQuery] int page = 1, [FromQuery] bool? isRead = null)
        {
            try
            {
                var userId = _userManager.GetUserId(User);
                _logger.LogInformation("Fetching notifications for user {UserId}, page {Page}, isRead {IsRead}", userId, page, isRead);

                var query = _context.Notifications
                    .Where(n => n.UserId == userId)
                    .AsNoTracking();

                if (isRead.HasValue)
                {
                    query = query.Where(n => n.IsRead == isRead.Value);
                }

                const int pageSize = 10;
                var total = await query.CountAsync();
                var notifications = await query
                    .OrderByDescending(n => n.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(n => new
                    {
                        n.Id,
                        n.Content,
                        n.IsRead,
                        CreatedAt = n.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                        n.Type,
                        n.ItemId,
                        n.OrderId,
                        n.OrganizationId
                    })
                    .ToListAsync();

                return Ok(new
                {
                    Notifications = notifications,
                    TotalPages = (int)Math.Ceiling(total / (double)pageSize),
                    CurrentPage = page
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching notifications for user {UserId}", _userManager.GetUserId(User));
                return StatusCode(500, new { Error = "Lỗi khi tải thông báo" });
            }
        }

        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            try
            {
                var userId = _userManager.GetUserId(User);
                var count = await _context.Notifications
                    .AsNoTracking()
                    .CountAsync(n => n.UserId == userId && !n.IsRead);

                return Ok(count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching unread count for user {UserId}", _userManager.GetUserId(User));
                return StatusCode(500, new { Error = "Lỗi khi lấy số thông báo chưa đọc" });
            }
        }

        [HttpPost("mark-read/{id}")]
        public async Task<IActionResult> MarkAsRead(string id)
        {
            try
            {
                var userId = _userManager.GetUserId(User);
                var notification = await _context.Notifications
                    .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);

                if (notification == null)
                {
                    _logger.LogWarning("Notification {NotificationId} not found for user {UserId}", id, userId);
                    return NotFound(new { Error = "Thông báo không tồn tại" });
                }

                notification.IsRead = true;
                await _context.SaveChangesAsync();
                _logger.LogInformation("Marked notification {NotificationId} as read for user {UserId}", id, userId);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking notification {NotificationId} as read for user {UserId}", id, _userManager.GetUserId(User));
                return StatusCode(500, new { Error = "Lỗi khi đánh dấu thông báo đã đọc" });
            }
        }

        [HttpPost("mark-all-read")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            try
            {
                var userId = _userManager.GetUserId(User);
                var notifications = await _context.Notifications
                    .Where(n => n.UserId == userId && !n.IsRead)
                    .ToListAsync();

                if (!notifications.Any())
                {
                    return Ok();
                }

                notifications.ForEach(n => n.IsRead = true);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Marked {Count} notifications as read for user {UserId}", notifications.Count, userId);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking all notifications as read for user {UserId}", _userManager.GetUserId(User));
                return StatusCode(500, new { Error = "Lỗi khi đánh dấu tất cả thông báo đã đọc" });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            try
            {
                var userId = _userManager.GetUserId(User);
                var notification = await _context.Notifications
                    .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);

                if (notification == null)
                {
                    _logger.LogWarning("Notification {NotificationId} not found for user {UserId}", id, userId);
                    return NotFound(new { Error = "Thông báo không tồn tại" });
                }

                _context.Notifications.Remove(notification);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Deleted notification {NotificationId} for user {UserId}", id, userId);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting notification {NotificationId} for user {UserId}", id, _userManager.GetUserId(User));
                return StatusCode(500, new { Error = "Lỗi khi xóa thông báo" });
            }
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteAll()
        {
            try
            {
                var userId = _userManager.GetUserId(User);
                var notifications = await _context.Notifications
                    .Where(n => n.UserId == userId)
                    .ToListAsync();

                if (!notifications.Any())
                {
                    return Ok();
                }

                _context.Notifications.RemoveRange(notifications);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Deleted {Count} notifications for user {UserId}", notifications.Count, userId);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting all notifications for user {UserId}", _userManager.GetUserId(User));
                return StatusCode(500, new { Error = "Lỗi khi xóa tất cả thông báo" });
            }
        }
    }
}