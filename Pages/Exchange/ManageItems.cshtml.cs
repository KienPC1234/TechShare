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
    [Authorize]
    public class ManageItemsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ManageItemsModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        }

        public List<ExchangeItem> Items { get; set; } = new List<ExchangeItem>();
        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return StatusCode(401, "Người dùng không được xác thực.");
                }

                Items = await _context.ExchangeItems
                    .Include(i => i.Category)
                    .Where(i => i.OwnerId == user.Id)
                    .OrderByDescending(i => i.CreatedAt)
                    .ToListAsync();

                return Page();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Lỗi khi tải danh sách mặt hàng: {ex.Message}";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostDeleteAsync(string id)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return StatusCode(401, "Người dùng không được xác thực.");
                }

                var item = await _context.ExchangeItems
                    .Include(i => i.MediaItems)
                    .Include(i => i.Tags)
                    .FirstOrDefaultAsync(i => i.Id == id && i.OwnerId == user.Id);

                if (item == null)
                {
                    return NotFound("Mặt hàng không tồn tại hoặc bạn không có quyền xóa.");
                }

                var hasOrders = await _context.BorrowOrders
                    .AnyAsync(o => o.ItemId == id && o.Status != "Cancelled" && o.Status != "Delivered");
                if (hasOrders)
                {
                    ErrorMessage = "Không thể xóa mặt hàng vì còn đơn mượn đang xử lý.";
                    await OnGetAsync();
                    return Page();
                }

                _context.ExchangeItems.Remove(item);
                await _context.SaveChangesAsync();

                SuccessMessage = "Mặt hàng đã được xóa thành công.";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Lỗi khi xóa mặt hàng: {ex.Message}";
                await OnGetAsync();
                return Page();
            }
        }
    }
}