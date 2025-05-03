#nullable enable

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
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly UserManager<ApplicationUser> _userManager;

        public ApplicationDbContext DbContext => _dbContext;
        public UserManager<ApplicationUser> UserManager => _userManager;

        public IndexModel(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager)
        {
            _dbContext = dbContext;
            _userManager = userManager;
        }

        public IList<LoginSystem.Models.Organization> Organizations { get; set; } = new List<LoginSystem.Models.Organization>();
        public bool IsAdmin { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var userId = _userManager.GetUserId(User);
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null) return NotFound();

            IsAdmin = await _userManager.IsInRoleAsync(user, "Admin");

            if (IsAdmin)
            {
                // Admin thấy tổ chức do mình tạo
                Organizations = await _dbContext.Organizations
                    .Where(o => o.CreatorId == userId)
                    .ToListAsync();
            }
            else if (user.OrganizationId != null)
            {
                // User thường thấy tổ chức mình tham gia
                var org = await _dbContext.Organizations
                    .Where(o => o.Id == user.OrganizationId)
                    .FirstOrDefaultAsync();
                if (org != null)
                {
                    Organizations.Add(org);
                }
            }

            return Page();
        }

        // Phương thức tính số thành viên
        public int GetMemberCount(string organizationId)
        {
            return _dbContext.OrganizationMembers
                .Count(m => m.OrganizationId == organizationId);
        }

        // Phương thức tính đánh giá trung bình
        public double GetAverageRating(string organizationId)
        {
            var average = _dbContext.OrganizationRatings
                .Where(r => r.OrganizationId == organizationId)
                .Average(r => (double?)r.Score) ?? 0;
            return average;
        }
    }
}