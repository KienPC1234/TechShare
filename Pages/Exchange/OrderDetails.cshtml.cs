using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using LoginSystem.Data;
using LoginSystem.Models;
using Microsoft.AspNetCore.Identity;
using System;
using System.Threading.Tasks;

namespace LoginSystem.Pages.Exchange
{
    [Authorize]
    public class OrderDetailsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public OrderDetailsModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
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
                    return StatusCode(401, "Người dùng không được xác thực.");
                }

                Order = await _context.BorrowOrders
                    .Include(o => o.Item)
                    .Include(o => o.Borrower)
                    .Include(o => o.DeliveryAgent)
                    .Include(o => o.StatusHistory)
                    .FirstOrDefaultAsync(o => o.Id == orderId && (o.BorrowerId == user.Id || o.DeliveryAgentId == user.Id));

                if (Order == null)
                {
                    return NotFound("Đơn hàng không tồn tại hoặc bạn không có quyền xem.");
                }

                return Page();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Lỗi khi tải chi tiết đơn hàng: {ex.Message}";
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
                    .Include(o => o.DeliveryAgent)
                    .FirstOrDefaultAsync(o => o.Id == orderId && (o.BorrowerId == user.Id || o.DeliveryAgentId == user.Id));

                if (order == null)
                {
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
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Lỗi khi hủy đơn hàng: {ex.Message}";
                await OnGetAsync(orderId);
                return Page();
            }
        }
    }
}