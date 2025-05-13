using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using LoginSystem.Data;
using LoginSystem.Models;
using Microsoft.AspNetCore.Identity;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LoginSystem.Pages.Exchange
{
    [Authorize]
    public class UserActivityModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<UserActivityModel> _logger;

        public UserActivityModel(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<UserActivityModel> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public List<BorrowOrder> BorrowingHistory { get; set; } = new List<BorrowOrder>();
        public List<BorrowOrder> OngoingOrders { get; set; } = new List<BorrowOrder>();
        public bool IsOngoingOrdersTab { get; set; }

        public async Task<IActionResult> OnGetAsync(string tab = "history")
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null)
            {
                _logger.LogWarning("OnGetAsync: User not authenticated.");
                return StatusCode(401, "Người dùng không được xác thực.");
            }

            IsOngoingOrdersTab = tab == "ongoing";

            try
            {
                if (IsOngoingOrdersTab)
                {
                    OngoingOrders = await _context.BorrowOrders
                        .Include(o => o.Item)
                        .Include(o => o.StatusHistory)
                        .Where(o => o.BorrowerId == userId &&
                                   (o.Status == "Pending" || o.Status == "Accepted"))
                        .OrderByDescending(o => o.CreatedAt)
                        .ToListAsync();
                    _logger.LogInformation("OnGetAsync: Loaded {Count} ongoing orders for user {UserId}.", OngoingOrders.Count, userId);
                }
                else
                {
                    BorrowingHistory = await _context.BorrowOrders
                        .Include(o => o.Item)
                        .Include(o => o.StatusHistory)
                        .Where(o => o.BorrowerId == userId &&
                                   (o.Status == "Shipped" || o.Status == "Delivered" || o.Status == "Cancelled"))
                        .OrderByDescending(o => o.CreatedAt)
                        .ToListAsync();
                    _logger.LogInformation("OnGetAsync: Loaded {Count} borrowing history orders for user {UserId}.", BorrowingHistory.Count, userId);
                }

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnGetAsync for user {UserId}, tab {Tab}", userId, tab);
                return StatusCode(500, "Lỗi khi tải hoạt động của người dùng.");
            }
        }
    }
}