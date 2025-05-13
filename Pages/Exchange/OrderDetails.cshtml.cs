using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using LoginSystem.Data;
using LoginSystem.Models;
using Microsoft.AspNetCore.Identity;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LoginSystem.Pages.Exchange
{
    [Authorize]
    public class OrderDetailsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<OrderDetailsModel> _logger;

        public OrderDetailsModel(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<OrderDetailsModel> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public BorrowOrder Order { get; set; } = null!;
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(string orderId)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    _logger.LogWarning("OnGetAsync: User not authenticated for order {OrderId}.", orderId);
                    return StatusCode(401, "Người dùng không được xác thực.");
                }

                Order = await _context.BorrowOrders
                    .Include(o => o.Item)
                    .Include(o => o.Borrower)
                    .Include(o => o.DeliveryAgent)
                    .Include(o => o.StatusHistory)
                    .FirstOrDefaultAsync(o => o.Id == orderId &&
                                             (o.BorrowerId == user.Id || o.DeliveryAgentId == user.Id || o.Item.OwnerId == user.Id));

                if (Order == null)
                {
                    _logger.LogWarning("OnGetAsync: Order {OrderId} not found or user {UserId} lacks permission.", orderId, user.Id);
                    return NotFound("Đơn hàng không tồn tại hoặc bạn không có quyền xem.");
                }

                return Page();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Lỗi khi tải chi tiết đơn hàng: {ex.Message}";
                _logger.LogError(ex, "Error in OnGetAsync for order {OrderId}", orderId);
                return Page();
            }
        }

        public async Task<IActionResult> OnPostCancelAsync(string orderId)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    _logger.LogWarning("OnPostCancelAsync: User not authenticated for order {OrderId}.", orderId);
                    return StatusCode(401, "Người dùng không được xác thực.");
                }

                var order = await _context.BorrowOrders
                    .Include(o => o.Item)
                    .Include(o => o.Borrower)
                    .Include(o => o.DeliveryAgent)
                    .FirstOrDefaultAsync(o => o.Id == orderId &&
                                             (o.BorrowerId == user.Id || o.DeliveryAgentId == user.Id));

                if (order == null)
                {
                    _logger.LogWarning("OnPostCancelAsync: Order {OrderId} not found or user {UserId} lacks permission.", orderId, user.Id);
                    return NotFound("Đơn hàng không tồn tại hoặc bạn không có quyền hủy.");
                }

                if (order.Status != "Pending")
                {
                    ErrorMessage = "Chỉ có thể hủy đơn hàng ở trạng thái 'Pending'.";
                    await OnGetAsync(orderId);
                    return Page();
                }

                order.Status = "Cancelled";
                order.UpdatedAt = DateTime.UtcNow;

                order.StatusHistory.Add(new OrderStatusHistory
                {
                    Id = Guid.NewGuid().ToString(),
                    OrderId = order.Id,
                    Status = "Cancelled",
                    ChangedAt = DateTime.UtcNow,
                    ChangedByUserId = user.Id,
                    Note = $"Đơn hàng được hủy bởi {(order.BorrowerId == user.Id ? "người mượn" : "người chuyển phát")}."
                });

                order.Item.QuantityAvailable++;

                var notification = new Notification
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = order.BorrowerId == user.Id ? order.Item.OwnerId : order.BorrowerId,
                    Content = $"Đơn hàng {order.Id} cho mặt hàng '{order.Item.Title}' đã bị hủy.",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow,
                    OrderId = order.Id
                };
                _context.Notifications.Add(notification);

                await _context.SaveChangesAsync();
                _logger.LogInformation("Order {OrderId} cancelled by user {UserId}.", orderId, user.Id);

                return RedirectToPage();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Lỗi khi hủy đơn hàng: {ex.Message}";
                _logger.LogError(ex, "Error in OnPostCancelAsync for order {OrderId}", orderId);
                await OnGetAsync(orderId);
                return Page();
            }
        }

        public async Task<IActionResult> OnPostAcceptAsync(string orderId)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    _logger.LogWarning("OnPostAcceptAsync: User not authenticated for order {OrderId}.", orderId);
                    return StatusCode(401, "Người dùng không được xác thực.");
                }

                var order = await _context.BorrowOrders
                    .Include(o => o.Item)
                    .Include(o => o.Borrower)
                    .FirstOrDefaultAsync(o => o.Id == orderId && o.Item.OwnerId == user.Id);

                if (order == null)
                {
                    _logger.LogWarning("OnPostAcceptAsync: Order {OrderId} not found or user {UserId} lacks permission.", orderId, user.Id);
                    return NotFound("Đơn hàng không tồn tại hoặc bạn không có quyền chấp nhận.");
                }

                if (order.Status != "Pending")
                {
                    ErrorMessage = "Chỉ có thể chấp nhận đơn hàng ở trạng thái 'Pending'.";
                    await OnGetAsync(orderId);
                    return Page();
                }

                order.Status = "Accepted";
                order.UpdatedAt = DateTime.UtcNow;

                order.StatusHistory.Add(new OrderStatusHistory
                {
                    Id = Guid.NewGuid().ToString(),
                    OrderId = order.Id,
                    Status = "Accepted",
                    ChangedAt = DateTime.UtcNow,
                    ChangedByUserId = user.Id,
                    Note = "Đơn hàng được chấp nhận bởi chủ sở hữu mặt hàng."
                });

                var notification = new Notification
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = order.BorrowerId,
                    Content = $"Đơn hàng {order.Id} cho mặt hàng '{order.Item.Title}' đã được chấp nhận.",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow,
                    OrderId = order.Id
                };
                _context.Notifications.Add(notification);

                await _context.SaveChangesAsync();
                _logger.LogInformation("Order {OrderId} accepted by user {UserId}.", orderId, user.Id);

                return RedirectToPage();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Lỗi khi chấp nhận đơn hàng: {ex.Message}";
                _logger.LogError(ex, "Error in OnPostAcceptAsync for order {OrderId}", orderId);
                await OnGetAsync(orderId);
                return Page();
            }
        }

        public async Task<IActionResult> OnPostShipAsync(string orderId, string deliveryAgentId)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    _logger.LogWarning("OnPostShipAsync: User not authenticated for order {OrderId}.", orderId);
                    return StatusCode(401, "Người dùng không được xác thực.");
                }

                var order = await _context.BorrowOrders
                    .Include(o => o.Item)
                    .Include(o => o.Borrower)
                    .FirstOrDefaultAsync(o => o.Id == orderId && o.Item.OwnerId == user.Id);

                if (order == null)
                {
                    _logger.LogWarning("OnPostShipAsync: Order {OrderId} not found or user {UserId} lacks permission.", orderId, user.Id);
                    return NotFound("Đơn hàng không tồn tại hoặc bạn không có quyền cập nhật.");
                }

                if (order.Status != "Accepted")
                {
                    ErrorMessage = "Chỉ có thể giao hàng cho đơn hàng ở trạng thái 'Accepted'.";
                    await OnGetAsync(orderId);
                    return Page();
                }

                var deliveryAgent = await _userManager.FindByIdAsync(deliveryAgentId);
                if (deliveryAgent == null)
                {
                    ErrorMessage = "Người chuyển phát không tồn tại.";
                    await OnGetAsync(orderId);
                    return Page();
                }

                order.Status = "Shipped";
                order.DeliveryAgentId = deliveryAgentId;
                order.UpdatedAt = DateTime.UtcNow;

                order.StatusHistory.Add(new OrderStatusHistory
                {
                    Id = Guid.NewGuid().ToString(),
                    OrderId = order.Id,
                    Status = "Shipped",
                    ChangedAt = DateTime.UtcNow,
                    ChangedByUserId = user.Id,
                    Note = $"Đơn hàng được giao bởi {deliveryAgent.DisplayName}."
                });

                var notification = new Notification
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = order.BorrowerId,
                    Content = $"Đơn hàng {order.Id} cho mặt hàng '{order.Item.Title}' đã được giao.",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow,
                    OrderId = order.Id
                };
                _context.Notifications.Add(notification);

                await _context.SaveChangesAsync();
                _logger.LogInformation("Order {OrderId} shipped by user {UserId} with delivery agent {DeliveryAgentId}.", orderId, user.Id, deliveryAgentId);

                return RedirectToPage();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Lỗi khi cập nhật trạng thái giao hàng: {ex.Message}";
                _logger.LogError(ex, "Error in OnPostShipAsync for order {OrderId}", orderId);
                await OnGetAsync(orderId);
                return Page();
            }
        }
    }
}