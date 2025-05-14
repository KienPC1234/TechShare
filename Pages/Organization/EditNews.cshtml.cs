using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using LoginSystem.Data;
using LoginSystem.Models;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Ganss.Xss;

namespace LoginSystem.Pages.Organization
{
    [Authorize]
    public class EditNewsModel : PageModel
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly HtmlSanitizer _sanitizer;

        public EditNewsModel(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager)
        {
            _dbContext = dbContext;
            _userManager = userManager;
            _sanitizer = new HtmlSanitizer();
        }

        public LoginSystem.Models.Organization? Organization { get; set; }

        [BindProperty]
        public NewsInputModel NewsInput { get; set; } = new NewsInputModel();

        public class NewsInputModel
        {
            public string Id { get; set; } = string.Empty;
            public string OrganizationId { get; set; } = string.Empty;
            [Required(ErrorMessage = "Tiêu đề là bắt buộc")]
            [StringLength(200, ErrorMessage = "Tiêu đề không được vượt quá 200 ký tự")]
            public string Title { get; set; } = string.Empty;
            [Required(ErrorMessage = "Nội dung là bắt buộc")]
            public string Content { get; set; } = string.Empty;
            public string? ThumbnailUrl { get; set; }
            public bool IsPublished { get; set; } = true;
        }

        public async Task<IActionResult> OnGetAsync(string slug, string newsId)
        {
            if (string.IsNullOrEmpty(slug) || string.IsNullOrEmpty(newsId))
            {
                TempData["ErrorMessage"] = "Dữ liệu không hợp lệ.";
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

            var news = await _dbContext.OrganizationNews
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == newsId && n.OrganizationId == Organization.Id);

            if (news == null)
            {
                TempData["ErrorMessage"] = "Bài viết không tồn tại.";
                return NotFound();
            }

            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                TempData["ErrorMessage"] = "Không thể xác định người dùng.";
                return RedirectToPage("/Organization/NewsDetails", new { slug, newsId });
            }

            var isAdmin = await _dbContext.OrganizationMembers
                .AsNoTracking()
                .AnyAsync(m => m.OrganizationId == Organization.Id && m.UserId == userId && m.Role == "Admin");
            if (!isAdmin)
            {
                TempData["ErrorMessage"] = "Bạn không có quyền chỉnh sửa bài viết.";
                return RedirectToPage("/Organization/NewsDetails", new { slug, newsId });
            }

            NewsInput = new NewsInputModel
            {
                Id = news.Id,
                OrganizationId = news.OrganizationId,
                Title = news.Title,
                Content = news.Content,
                ThumbnailUrl = news.ThumbnailUrl,
                IsPublished = news.IsPublished
            };

            return Page();
        }
    }
}
