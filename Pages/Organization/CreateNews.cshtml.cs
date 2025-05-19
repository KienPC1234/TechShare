using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using LoginSystem.Data;
using LoginSystem.Models;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using LoginSystem.Security;

namespace LoginSystem.Pages.Organization
{
    [Authorize]
    public class CreateNewsModel : PageModel
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly UserManager<ApplicationUser> _userManager;

        public CreateNewsModel(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager)
        {
            _dbContext = dbContext;
            _userManager = userManager;
        }

        public NewsInputModel NewsInput { get; set; } = new NewsInputModel();

        public string Slug { get; set; } = string.Empty;

        public class NewsInputModel
        {
            public string OrganizationId { get; set; } = string.Empty;
        }

        public async Task<IActionResult> OnGetAsync(string slug)
        {
            if (string.IsNullOrEmpty(slug))
            {
                TempData["ErrorMessage"] = "Slug không hợp lệ.";
                return NotFound();
            }

            var organization = await _dbContext.Organizations
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Slug == slug);

            if (organization == null)
            {
                TempData["ErrorMessage"] = "Tổ chức không tồn tại.";
                return NotFound();
            }

            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                TempData["ErrorMessage"] = "Không thể xác định người dùng.";
                return RedirectToPage("/Organization/Details", new { slug });
            }

            var isAdmin = await _dbContext.OrganizationMembers
                .AsNoTracking()
                .AnyAsync(m => m.OrganizationId == organization.Id && m.UserId == userId && m.Role == "Admin");
            if (!isAdmin)
            {
                TempData["ErrorMessage"] = "Bạn không có quyền tạo tin tức cho tổ chức này.";
                return RedirectToPage("/Organization/Details", new { slug });
            }

            Slug = slug;
            NewsInput.OrganizationId = organization.Id;
            return Page();
        }
    }
}
