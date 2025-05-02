using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using LoginSystem.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace LoginSystem.Pages
{
    [Authorize(Roles = "SuperAdmin")]
    public class AdminRequestsModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminRequestsModel(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public IList<ApplicationUser> AdminRequests { get; set; } = new List<ApplicationUser>();
        public int TotalRequests { get; set; }
        public int CurrentPage { get; set; }
        public string SearchTerm { get; set; }

        private const int PageSize = 10;

        public async Task OnGetAsync(int page = 1, string searchTerm = "")
        {
            CurrentPage = page;
            SearchTerm = searchTerm;

            var query = _userManager.Users
                .Where(u => u.AdminRequestPending);

            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(u => u.UserName.Contains(searchTerm) || u.Email.Contains(searchTerm));
            }

            TotalRequests = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(TotalRequests / (double)PageSize);

            if (CurrentPage > totalPages && totalPages > 0)
            {
                CurrentPage = totalPages;
            }
            else if (totalPages == 0)
            {
                CurrentPage = 1;
            }

            AdminRequests = await query
                .OrderBy(u => u.UserName)
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostApproveAsync(string id, int page, string searchTerm)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            if (!await _userManager.IsInRoleAsync(user, "Admin"))
                await _userManager.AddToRoleAsync(user, "Admin");

            user.AdminRequestPending = false;
            user.AdminRequestReason = null;

            await _userManager.UpdateAsync(user);
            return RedirectToPage(new { page, searchTerm });
        }

        public async Task<IActionResult> OnPostRejectAsync(string id, int page, string searchTerm)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            user.AdminRequestPending = false;
            user.AdminRequestReason = null;

            await _userManager.UpdateAsync(user);
            return RedirectToPage(new { page, searchTerm });
        }
    }
}
