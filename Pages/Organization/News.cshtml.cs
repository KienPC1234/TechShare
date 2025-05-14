using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using LoginSystem.Data;
using LoginSystem.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LoginSystem.Pages.Organization
{
    [Authorize]
    public class NewsModel : PageModel
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly UserManager<ApplicationUser> _userManager;

        public NewsModel(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager)
        {
            _dbContext = dbContext;
            _userManager = userManager;
        }

        public LoginSystem.Models.Organization? Organization { get; set; }
        public IList<NewsViewModel> NewsList { get; set; } = new List<NewsViewModel>();
        public bool IsAdmin { get; set; }
        public bool IsMember { get; set; }

        public class NewsViewModel
        {
            public string Id { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public string? ThumbnailUrl { get; set; }
            public string AuthorName { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(string slug)
        {
            if (string.IsNullOrEmpty(slug))
            {
                TempData["ErrorMessage"] = "Slug không hợp lệ.";
                return NotFound();
            }

            Organization = await _dbContext.Organizations
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Slug == slug);

            if (Organization == null)
            {
                TempData["ErrorMessage"] = "Tổ chức không tồn tại.";
                return NotFound();
            }

            var userId = _userManager.GetUserId(User) ?? string.Empty;
            var membershipQuery = _dbContext.OrganizationMembers
                .Where(m => m.OrganizationId == Organization.Id && m.UserId == userId);

            IsMember = await membershipQuery.AnyAsync();
            IsAdmin = IsMember && await membershipQuery.AnyAsync(m => m.Role == "Admin");

            NewsList = await _dbContext.OrganizationNews
                .Where(n => n.OrganizationId == Organization.Id && n.IsPublished)
                .Join(_dbContext.Users,
                    n => n.AuthorId,
                    u => u.Id,
                    (n, u) => new NewsViewModel
                    {
                        Id = n.Id,
                        Title = n.Title,
                        ThumbnailUrl = n.ThumbnailUrl,
                        AuthorName = u.DisplayName ?? u.UserName,
                        CreatedAt = n.CreatedAt
                    })
                .OrderByDescending(n => n.CreatedAt)
                .AsNoTracking()
                .ToListAsync();

            return Page();
        }
    }
}