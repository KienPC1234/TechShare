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
    public class DeliverySearchModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public DeliverySearchModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        }

        [BindProperty(SupportsGet = true)]
        public string Query { get; set; } = string.Empty;

        [BindProperty(SupportsGet = true)]
        public string Status { get; set; } = "Pending";

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

                var query = _context.BorrowOrders
                    .Include(o => o.Item)
                    .Include(o => o.Borrower)
                    .Include(o => o.DeliveryAgent)
                    .Where(o => o.Status != "Cancelled" && o.Status != "Delivered" &&
                                (o.DeliveryAgentId == user.Id || string.IsNullOrEmpty(o.DeliveryAgentId)));

                if (!string.IsNullOrEmpty(Status))
                {
                    query = query.Where(o => o.Status == Status);
                }

                if (!string.IsNullOrWhiteSpace(Query))
                {
                    var cleanedQuery = Query.Trim().ToLower();
                    query = query.Where(o => o.Id.ToLower().Contains(cleanedQuery) ||
                                            o.Item.Title.ToLower().Contains(cleanedQuery) ||
                                            o.ShippingAddress.ToLower().Contains(cleanedQuery));
                }

                Orders = await query
                    .OrderByDescending(o => o.CreatedAt)
                    .Take(50)
                    .ToListAsync();

                return Page();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Lỗi khi tìm kiếm đơn hàng: {ex.Message}";
                return Page();
            }
        }
    }
}