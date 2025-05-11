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

namespace LoginSystem.Pages.Exchange
{
    [Authorize]
    public class UserActivityModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public UserActivityModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public List<BorrowOrder> BorrowingHistory { get; set; } = new List<BorrowOrder>();
        public List<BorrowOrder> OngoingOrders { get; set; } = new List<BorrowOrder>();
        public bool IsOngoingOrdersTab { get; set; }

        public async Task<IActionResult> OnGetAsync(string tab = "history")
        {
            var userId = _userManager.GetUserId(User);
            IsOngoingOrdersTab = tab == "ongoing";

            if (IsOngoingOrdersTab)
            {
                OngoingOrders = await _context.BorrowOrders
                    .Include(o => o.Item)
                    .Where(o => o.BorrowerId == userId && (o.Status == "Pending" || o.Status == "Shipped"))
                    .OrderByDescending(o => o.CreatedAt)
                    .ToListAsync();
            }
            else
            {
                BorrowingHistory = await _context.BorrowOrders
                    .Include(o => o.Item)
                    .Where(o => o.BorrowerId == userId && (o.Status == "Delivered" || o.Status == "Cancelled"))
                    .OrderByDescending(o => o.CreatedAt)
                    .ToListAsync();
            }

            return Page();
        }
    }
}