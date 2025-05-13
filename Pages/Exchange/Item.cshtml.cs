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
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using LoginSystem.Hubs;
using Microsoft.Extensions.Logging;

namespace LoginSystem.Pages.Exchange
{
    [Authorize]
    public class ItemDetailsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly ILogger<ItemDetailsModel> _logger;

        public ItemDetailsModel(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IHubContext<ChatHub> hubContext,
            ILogger<ItemDetailsModel> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public ExchangeItem? Item { get; set; }
        public List<ItemComment> Comments { get; set; } = new List<ItemComment>();
        public double? AverageRating { get; set; }
        public bool CanBorrow { get; set; }
        public string SavedAddress { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }
        public string? OrganizationName { get; set; }

        public async Task<IActionResult> OnGetAsync(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                _logger.LogWarning("OnGetAsync: Invalid item ID provided.");
                return NotFound("ID mặt hàng không hợp lệ.");
            }

            try
            {
                Item = await _context.ExchangeItems
                    .AsNoTracking()
                    .Include(i => i.Tags)
                    .Include(i => i.Category)
                    .Include(i => i.Owner)
                    .Include(i => i.MediaItems)
                    .FirstOrDefaultAsync(i => i.Id == id);

                if (Item == null)
                {
                    _logger.LogWarning("OnGetAsync: Item {ItemId} not found.", id);
                    return NotFound("Mặt hàng không tồn tại hoặc đã bị xóa.");
                }

                Comments = await _context.ItemComments
                    .AsNoTracking()
                    .Include(c => c.User)
                    .Where(c => c.ItemId == id)
                    .OrderByDescending(c => c.CreatedAt)
                    .Take(50)
                    .ToListAsync();

                var ratings = await _context.ItemRatings
                    .AsNoTracking()
                    .Where(r => r.ItemId == id)
                    .Select(r => r.Score)
                    .ToListAsync();
                AverageRating = ratings.Any() ? ratings.Average() : null;

                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    _logger.LogWarning("OnGetAsync: User not authenticated for item {ItemId}.", id);
                    return StatusCode(401, "Người dùng không được xác thực.");
                }

                CanBorrow = Item.QuantityAvailable > 0 &&
                            (Item.IsPrivate && Item.OrganizationId != null
                                ? await _context.OrganizationMembers
                                    .AnyAsync(m => m.OrganizationId == Item.OrganizationId && m.UserId == user.Id)
                                : true);

                SavedAddress = Request.Cookies["SavedAddress"]?.Trim() ?? string.Empty;

                OrganizationName = Item.OrganizationId == null
                    ? "Không Tổ Chức"
                    : await _context.Organizations
                        .Where(o => o.Id == Item.OrganizationId)
                        .Select(o => o.Name)
                        .FirstOrDefaultAsync() ?? "Không xác định";

                return Page();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Lỗi khi tải chi tiết mặt hàng: {ex.Message}";
                _logger.LogError(ex, "Error in OnGetAsync for item {ItemId}", id);
                return Page();
            }
        }

        public async Task<IActionResult> OnPostBorrowAsync(string id, string shippingAddress, bool saveAddress, bool termsAccepted)
        {
            if (string.IsNullOrEmpty(id))
            {
                _logger.LogWarning("OnPostBorrowAsync: Invalid item ID provided.");
                return NotFound("ID mặt hàng không hợp lệ.");
            }

            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    _logger.LogWarning("OnPostBorrowAsync: User not authenticated for item {ItemId}.", id);
                    return StatusCode(401, "Người dùng không được xác thực.");
                }

                var item = await _context.ExchangeItems
                    .Include(i => i.Owner)
                    .FirstOrDefaultAsync(i => i.Id == id);

                if (item == null)
                {
                    _logger.LogWarning("OnPostBorrowAsync: Item {ItemId} not found.", id);
                    return NotFound("Mặt hàng không tồn tại hoặc đã bị xóa.");
                }

                if (item.QuantityAvailable <= 0)
                {
                    ErrorMessage = "Mặt hàng hiện không khả dụng để mượn.";
                    await OnGetAsync(id);
                    return Page();
                }

                if (item.IsPrivate && item.OrganizationId != null)
                {
                    var isMember = await _context.OrganizationMembers
                        .AnyAsync(m => m.OrganizationId == item.OrganizationId && m.UserId == user.Id);
                    if (!isMember)
                    {
                        ErrorMessage = "Bạn không có quyền mượn mặt hàng này (chỉ thành viên tổ chức được phép).";
                        await OnGetAsync(id);
                        return Page();
                    }
                }

                if (string.IsNullOrWhiteSpace(shippingAddress) || shippingAddress.Length > 500)
                {
                    ErrorMessage = "Địa chỉ giao hàng không hợp lệ hoặc vượt quá 500 ký tự.";
                    await OnGetAsync(id);
                    return Page();
                }

                if (!termsAccepted)
                {
                    ErrorMessage = "Bạn phải đồng ý với điều khoản mượn.";
                    await OnGetAsync(id);
                    return Page();
                }

                var order = new BorrowOrder
                {
                    Id = Guid.NewGuid().ToString(),
                    ItemId = item.Id,
                    BorrowerId = user.Id,
                    ShippingAddress = shippingAddress.Trim(),
                    TermsAccepted = termsAccepted,
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow,
                    StatusHistory = new List<OrderStatusHistory>
                    {
                        new OrderStatusHistory
                        {
                            Id = Guid.NewGuid().ToString(),
                            OrderId = string.Empty, // Will be set after order is saved
                            Status = "Pending",
                            ChangedAt = DateTime.UtcNow,
                            ChangedByUserId = user.Id,
                            Note = "Đơn mượn được tạo."
                        }
                    }
                };

                item.QuantityAvailable--;

                if (saveAddress)
                {
                    Response.Cookies.Append("SavedAddress", shippingAddress.Trim(), new CookieOptions
                    {
                        Expires = DateTimeOffset.UtcNow.AddYears(1),
                        HttpOnly = true,
                        Secure = true
                    });
                }

                _context.BorrowOrders.Add(order);
                order.StatusHistory.First().OrderId = order.Id;

                var notification = new Notification
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = item.OwnerId,
                    Content = $"Yêu cầu mượn mới cho mặt hàng '{item.Title}' từ {user.DisplayName}.",
                    OrderId = order.Id,
                    Type = "BorrowRequest",
                    CreatedAt = DateTime.UtcNow
                };
                _context.Notifications.Add(notification);

                await _context.SaveChangesAsync();

                await _hubContext.Clients.User(item.OwnerId).SendAsync("ReceiveNotification",
                    notification.Id,
                    notification.Content,
                    notification.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                    $"/Exchange/OrderDetails/{order.Id}",
                    notification.Type);

                _logger.LogInformation("Borrow order {OrderId} created for item {ItemId} by user {UserId}.", order.Id, item.Id, user.Id);

                return RedirectToPage("/Exchange/OrderDetails", new { orderId = order.Id });
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Lỗi khi gửi yêu cầu mượn: {ex.Message}";
                _logger.LogError(ex, "Error in OnPostBorrowAsync for item {ItemId}", id);
                await OnGetAsync(id);
                return Page();
            }
        }

        public async Task<IActionResult> OnPostCommentAsync(string id, string content)
        {
            if (string.IsNullOrEmpty(id))
            {
                _logger.LogWarning("OnPostCommentAsync: Invalid item ID provided.");
                return NotFound("ID mặt hàng không hợp lệ.");
            }

            try
            {
                if (string.IsNullOrWhiteSpace(content) || content.Trim().Length > 1000)
                {
                    ErrorMessage = "Bình luận không được để trống hoặc vượt quá 1000 ký tự.";
                    await OnGetAsync(id);
                    return Page();
                }

                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    _logger.LogWarning("OnPostCommentAsync: User not authenticated for item {ItemId}.", id);
                    return StatusCode(401, "Người dùng không được xác thực.");
                }

                var item = await _context.ExchangeItems.FindAsync(id);
                if (item == null)
                {
                    _logger.LogWarning("OnPostCommentAsync: Item {ItemId} not found.", id);
                    return NotFound("Mặt hàng không tồn tại.");
                }

                var comment = new ItemComment
                {
                    Id = Guid.NewGuid().ToString(),
                    ItemId = id,
                    UserId = user.Id,
                    Content = content.Trim(),
                    CreatedAt = DateTime.UtcNow
                };

                _context.ItemComments.Add(comment);
                await _context.SaveChangesAsync();

                if (item.OwnerId != user.Id)
                {
                    var notification = new Notification
                    {
                        Id = Guid.NewGuid().ToString(),
                        UserId = item.OwnerId,
                        Content = $"Bình luận mới trên mặt hàng '{item.Title}': {content.Substring(0, Math.Min(50, content.Length))}...",
                        ItemId = item.Id,
                        Type = "ItemComment",
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Notifications.Add(notification);
                    await _context.SaveChangesAsync();

                    await _hubContext.Clients.User(item.OwnerId).SendAsync("ReceiveNotification",
                        notification.Id,
                        notification.Content,
                        notification.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                        $"/Exchange/Item/{item.Id}",
                        notification.Type);
                }

                SuccessMessage = "Bình luận đã được gửi thành công.";
                return RedirectToPage(new { id });
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Lỗi khi gửi bình luận: {ex.Message}";
                _logger.LogError(ex, "Error in OnPostCommentAsync for item {ItemId}", id);
                await OnGetAsync(id);
                return Page();
            }
        }

        public async Task<IActionResult> OnPostRateAsync(string id, int score)
        {
            if (string.IsNullOrEmpty(id))
            {
                _logger.LogWarning("OnPostRateAsync: Invalid item ID provided.");
                return NotFound("ID mặt hàng không hợp lệ.");
            }

            try
            {
                if (score < 1 || score > 5)
                {
                    ErrorMessage = "Điểm đánh giá phải từ 1 đến 5.";
                    await OnGetAsync(id);
                    return Page();
                }

                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    _logger.LogWarning("OnPostRateAsync: User not authenticated for item {ItemId}.", id);
                    return StatusCode(401, "Người dùng không được xác thực.");
                }

                var item = await _context.ExchangeItems.FindAsync(id);
                if (item == null)
                {
                    _logger.LogWarning("OnPostRateAsync: Item {ItemId} not found.", id);
                    return NotFound("Mặt hàng không tồn tại.");
                }

                var existingRating = await _context.ItemRatings
                    .FirstOrDefaultAsync(r => r.ItemId == id && r.UserId == user.Id);

                if (existingRating != null)
                {
                    ErrorMessage = "Bạn đã đánh giá mặt hàng này rồi.";
                    await OnGetAsync(id);
                    return Page();
                }

                var rating = new ItemRating
                {
                    Id = Guid.NewGuid().ToString(),
                    ItemId = id,
                    UserId = user.Id,
                    Score = score,
                    RatedAt = DateTime.UtcNow
                };

                _context.ItemRatings.Add(rating);
                await _context.SaveChangesAsync();

                if (item.OwnerId != user.Id)
                {
                    var notification = new Notification
                    {
                        Id = Guid.NewGuid().ToString(),
                        UserId = item.OwnerId,
                        Content = $"Mặt hàng '{item.Title}' nhận được đánh giá {score} sao từ {user.DisplayName}.",
                        ItemId = item.Id,
                        Type = "ItemRating",
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Notifications.Add(notification);
                    await _context.SaveChangesAsync();

                    await _hubContext.Clients.User(item.OwnerId).SendAsync("ReceiveNotification",
                        notification.Id,
                        notification.Content,
                        notification.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                        $"/Exchange/Item/{item.Id}",
                        notification.Type);
                }

                SuccessMessage = "Đánh giá đã được gửi thành công.";
                return RedirectToPage(new { id });
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Lỗi khi gửi đánh giá: {ex.Message}";
                _logger.LogError(ex, "Error in OnPostRateAsync for item {ItemId}", id);
                await OnGetAsync(id);
                return Page();
            }
        }

        public async Task<IActionResult> OnPostReportAsync(string id, string reason)
        {
            if (string.IsNullOrEmpty(id))
            {
                _logger.LogWarning("OnPostReportAsync: Invalid item ID provided.");
                return NotFound("ID mặt hàng không hợp lệ.");
            }

            try
            {
                if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1000)
                {
                    ErrorMessage = "Lý do báo cáo không được để trống hoặc vượt quá 1000 ký tự.";
                    await OnGetAsync(id);
                    return Page();
                }

                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    _logger.LogWarning("OnPostReportAsync: User not authenticated for item {ItemId}.", id);
                    return StatusCode(401, "Người dùng không được xác thực.");
                }

                var item = await _context.ExchangeItems.FindAsync(id);
                if (item == null)
                {
                    _logger.LogWarning("OnPostReportAsync: Item {ItemId} not found.", id);
                    return NotFound("Mặt hàng không tồn tại.");
                }

                var existingReport = await _context.ItemReports
                    .FirstOrDefaultAsync(r => r.ItemId == id && r.UserId == user.Id);

                if (existingReport != null)
                {
                    ErrorMessage = "Bạn đã báo cáo mặt hàng này rồi.";
                    await OnGetAsync(id);
                    return Page();
                }

                var report = new ItemReport
                {
                    Id = Guid.NewGuid().ToString(),
                    ItemId = id,
                    UserId = user.Id,
                    Reason = reason.Trim(),
                    ReportedAt = DateTime.UtcNow
                };

                _context.ItemReports.Add(report);
                await _context.SaveChangesAsync();

                if (item.OwnerId != user.Id)
                {
                    var notification = new Notification
                    {
                        Id = Guid.NewGuid().ToString(),
                        UserId = item.OwnerId,
                        Content = $"Mặt hàng '{item.Title}' đã bị báo cáo bởi {user.DisplayName}.",
                        ItemId = item.Id,
                        Type = "ItemReport",
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Notifications.Add(notification);
                    await _context.SaveChangesAsync();

                    await _hubContext.Clients.User(item.OwnerId).SendAsync("ReceiveNotification",
                        notification.Id,
                        notification.Content,
                        notification.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                        $"/Exchange/Item/{item.Id}",
                        notification.Type);
                }

                SuccessMessage = "Báo cáo đã được gửi thành công.";
                return RedirectToPage(new { id });
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Lỗi khi gửi báo cáo: {ex.Message}";
                _logger.LogError(ex, "Error in OnPostReportAsync for item {ItemId}", id);
                await OnGetAsync(id);
                return Page();
            }
        }
    }
}