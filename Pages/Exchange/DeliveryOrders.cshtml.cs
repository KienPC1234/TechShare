using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using LoginSystem.Data;
using LoginSystem.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LoginSystem.Pages.Exchange
{
    [Authorize(Roles = "Delivery")]
    public class DeliveryOrdersModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public DeliveryOrdersModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        }

        public List<BorrowOrder> Orders { get; set; } = new List<BorrowOrder>();
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return StatusCode(401, "Người dùng không được xác thực.");
                }

                // Load all pending orders (not assigned) and orders assigned to the current delivery agent
                Orders = await _context.BorrowOrders
                    .Include(o => o.Item)
                    .Include(o => o.Borrower)
                    .Include(o => o.DeliveryAgent)
                    .Where(o => o.Status != "Cancelled" && o.Status != "Delivered" &&
                                (o.DeliveryAgentId == user.Id || string.IsNullOrEmpty(o.DeliveryAgentId)))
                    .OrderByDescending(o => o.CreatedAt)
                    .ToListAsync();

                return Page();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Lỗi khi tải danh sách đơn hàng: {ex.Message}";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostAcceptAsync(string orderId, string paymentInfo)
        {
            try
            {
                var order = await _context.BorrowOrders
                    .Include(o => o.Item)
                    .Include(o => o.Borrower)
                    .FirstOrDefaultAsync(o => o.Id == orderId);

                if (order == null)
                {
                    return NotFound("Đơn hàng không tồn tại.");
                }

                if (order.Status != "Pending" || !string.IsNullOrEmpty(order.DeliveryAgentId))
                {
                    ErrorMessage = "Đơn hàng đã được nhận hoặc không ở trạng thái có thể chấp nhận.";
                    await OnGetAsync();
                    return Page();
                }

                if (string.IsNullOrWhiteSpace(paymentInfo) || paymentInfo.Length > 500)
                {
                    ErrorMessage = "Thông tin thanh toán không hợp lệ (tối đa 500 ký tự).";
                    await OnGetAsync();
                    return Page();
                }

                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return StatusCode(401, "Người dùng không được xác thực.");
                }

                order.DeliveryAgentId = user.Id;
                order.Status = "Accepted";
                order.PaymentInfo = paymentInfo.Trim();
                order.UpdatedAt = DateTime.UtcNow;

                order.StatusHistory.Add(new OrderStatusHistory
                {
                    Id = Guid.NewGuid().ToString(),
                    OrderId = order.Id,
                    Status = "Accepted",
                    ChangedAt = DateTime.UtcNow,
                    ChangedByUserId = user.Id,
                    Note = "Đơn hàng được chấp nhận bởi người chuyển phát."
                });

                _context.Notifications.Add(new Notification
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = order.BorrowerId,
                    Content = $"Đơn hàng {order.Id} cho mặt hàng '{order.Item.Title}' đã được chấp nhận vận chuyển. Vui lòng thanh toán theo thông tin: {paymentInfo.Substring(0, Math.Min(50, paymentInfo.Length))}...",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow,
                    OrderId = order.Id
                });

                await _context.SaveChangesAsync();
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Lỗi khi chấp nhận đơn hàng: {ex.Message}";
                await OnGetAsync();
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
                    return StatusCode(401, "Người dùng không được xác thực.");
                }

                var order = await _context.BorrowOrders
                    .Include(o => o.Item)
                    .Include(o => o.Borrower)
                    .FirstOrDefaultAsync(o => o.Id == orderId && o.DeliveryAgentId == user.Id);

                if (order == null)
                {
                    return NotFound("Đơn hàng không tồn tại hoặc bạn không có quyền hủy.");
                }

                if (order.Status != "Pending")
                {
                    ErrorMessage = "Chỉ có thể hủy đơn hàng ở trạng thái 'Pending'.";
                    await OnGetAsync();
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
                    Note = "Đơn hàng được hủy bởi người chuyển phát."
                });

                order.Item.QuantityAvailable++;

                _context.Notifications.Add(new Notification
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = order.BorrowerId,
                    Content = $"Đơn hàng {order.Id} cho mặt hàng '{order.Item.Title}' đã bị hủy bởi người chuyển phát.",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow,
                    OrderId = order.Id
                });

                await _context.SaveChangesAsync();
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Lỗi khi hủy đơn hàng: {ex.Message}";
                await OnGetAsync();
                return Page();
            }
        }

        public async Task<IActionResult> OnPostUpdateStatusAsync(string orderId, string status)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return StatusCode(401, "Người dùng không được xác thực.");
                }

                var order = await _context.BorrowOrders
                    .Include(o => o.Item)
                    .Include(o => o.Borrower)
                    .FirstOrDefaultAsync(o => o.Id == orderId && o.DeliveryAgentId == user.Id);

                if (order == null)
                {
                    return NotFound("Đơn hàng không tồn tại hoặc bạn không có quyền cập nhật.");
                }

                if ((status == "Shipped" && order.Status != "Accepted") ||
                    (status == "Delivered" && order.Status != "Shipped"))
                {
                    ErrorMessage = "Không thể cập nhật trạng thái không hợp lệ.";
                    await OnGetAsync();
                    return Page();
                }

                order.Status = status;
                order.UpdatedAt = DateTime.UtcNow;

                order.StatusHistory.Add(new OrderStatusHistory
                {
                    Id = Guid.NewGuid().ToString(),
                    OrderId = order.Id,
                    Status = status,
                    ChangedAt = DateTime.UtcNow,
                    ChangedByUserId = user.Id,
                    Note = $"Đơn hàng được cập nhật thành '{status}'."
                });

                _context.Notifications.Add(new Notification
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = order.BorrowerId,
                    Content = $"Đơn hàng {order.Id} cho mặt hàng '{order.Item.Title}' đã được cập nhật thành '{status}'.",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow,
                    OrderId = order.Id
                });

                await _context.SaveChangesAsync();
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Lỗi khi cập nhật trạng thái đơn hàng: {ex.Message}";
                await OnGetAsync();
                return Page();
            }
        }
    }
}