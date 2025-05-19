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
        public bool HasUnreadNotifications { get; set; }
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

                HasUnreadNotifications = await query.AnyAsync(n => !n.IsRead);

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading notifications for user {UserId}", _userManager.GetUserId(User));
                ModelState.AddModelError("", $"Lỗi khi tải thông báo: {ex.Message}");
                return Page();
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