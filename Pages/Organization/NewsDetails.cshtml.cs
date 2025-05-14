using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using LoginSystem.Data;
using LoginSystem.Models;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using LoginSystem.Hubs;

using Ganss.Xss;

namespace LoginSystem.Pages.Organization
{
    [Authorize]
    public class NewsDetailsModel : PageModel
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly HtmlSanitizer _sanitizer;

        public NewsDetailsModel(
            ApplicationDbContext dbContext,
            UserManager<ApplicationUser> userManager,
            IHubContext<ChatHub> hubContext)
        {
            _dbContext = dbContext;
            _userManager = userManager;
            _hubContext = hubContext;
            _sanitizer = new HtmlSanitizer();
        }

        public LoginSystem.Models.Organization? Organization { get; set; }
        public OrganizationNews? News { get; set; }
        public bool IsMember { get; set; }
        public bool IsAdmin { get; set; }
        public string CurrentUserId { get; set; } = string.Empty;
        public IList<CommentViewModel> Comments { get; set; } = new List<CommentViewModel>();
        public int CurrentCommentPage { get; set; } = 1;
        public int TotalCommentPages { get; set; } = 1;
        private const int CommentPageSize = 10;

        public class CommentViewModel
        {
            public string Id { get; set; } = string.Empty;
            public string UserId { get; set; } = string.Empty;
            public string UserName { get; set; } = string.Empty;
            public string UserAvatar { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
        }

        [BindProperty]
        public CommentInputModel CommentInput { get; set; } = new CommentInputModel();

        public class CommentInputModel
        {
            [Required(ErrorMessage = "Vui lòng nhập nội dung bình luận")]
            [StringLength(500, ErrorMessage = "Bình luận không được vượt quá 500 ký tự")]
            public string Content { get; set; } = string.Empty;
        }

        public async Task<IActionResult> OnGetAsync(string slug, string newsId, int commentPage = 1)
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

            News = await _dbContext.OrganizationNews
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == newsId && n.OrganizationId == Organization.Id);

            if (News == null)
            {
                TempData["ErrorMessage"] = "Bài viết không tồn tại.";
                return NotFound();
            }

            // Sanitize HTML content
            News.Content = _sanitizer.Sanitize(News.Content);

            CurrentUserId = _userManager.GetUserId(User) ?? string.Empty;
            var membershipQuery = _dbContext.OrganizationMembers
                .Where(m => m.OrganizationId == Organization.Id && m.UserId == CurrentUserId);

            IsMember = await membershipQuery.AnyAsync();
            IsAdmin = IsMember && await membershipQuery.AnyAsync(m => m.Role == "Admin");

            CurrentCommentPage = commentPage < 1 ? 1 : commentPage;
            await LoadComments();

            return Page();
        }

        public async Task<IActionResult> OnPostCommentAsync(string slug, string newsId)
        {
            ModelState.Clear();
            TryValidateModel(CommentInput, nameof(CommentInput));

            Organization = await _dbContext.Organizations.FirstOrDefaultAsync(o => o.Slug == slug);
            if (Organization == null)
            {
                TempData["ErrorMessage"] = "Tổ chức không tồn tại.";
                return NotFound();
            }

            News = await _dbContext.OrganizationNews.FirstOrDefaultAsync(n => n.Id == newsId && n.OrganizationId == Organization.Id);
            if (News == null)
            {
                TempData["ErrorMessage"] = "Bài viết không tồn tại.";
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                CurrentUserId = _userManager.GetUserId(User) ?? string.Empty;
                await LoadComments();
                TempData["ErrorMessage"] = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return Page();
            }

            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                TempData["ErrorMessage"] = "Không thể xác định người dùng.";
                return RedirectToPage(new { slug, newsId });
            }

            if (!await _dbContext.OrganizationMembers.AnyAsync(m => m.OrganizationId == Organization.Id && m.UserId == userId))
            {
                TempData["ErrorMessage"] = "Bạn phải là thành viên để bình luận.";
                return RedirectToPage(new { slug, newsId });
            }

            var comment = new OrganizationNewsComment
            {
                NewsId = News.Id,
                UserId = userId,
                Content = _sanitizer.Sanitize(CommentInput.Content.Trim()),
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.OrganizationNewsComments.Add(comment);

            var admins = await _dbContext.OrganizationMembers
                .Where(m => m.OrganizationId == Organization.Id && m.Role == "Admin")
                .ToListAsync();
            var user = await _userManager.FindByIdAsync(userId);
            foreach (var admin in admins)
            {
                if (admin.UserId != userId)
                {
                    var notification = new Notification
                    {
                        UserId = admin.UserId,
                        Content = $"Bình luận mới trên bài viết {News.Title} từ {user?.DisplayName ?? user?.UserName}: {comment.Content.Substring(0, Math.Min(50, comment.Content.Length))}...",
                        OrganizationId = Organization.Id,
                        Type = "NewsComment",
                        CreatedAt = DateTime.UtcNow
                    };
                    _dbContext.Notifications.Add(notification);

                    await _hubContext.Clients.User(admin.UserId).SendAsync("ReceiveNotification",
                        notification.Id,
                        notification.Content,
                        notification.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                        $"/Organization/NewsDetails?slug={slug}&newsId={newsId}",
                        notification.Type);
                }
            }

            await _dbContext.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã thêm bình luận!";
            return RedirectToPage(new { slug, newsId });
        }

        public async Task<IActionResult> OnPostDeleteCommentAsync(string slug, string newsId, string commentId)
        {
            Organization = await _dbContext.Organizations.FirstOrDefaultAsync(o => o.Slug == slug);
            if (Organization == null)
            {
                TempData["ErrorMessage"] = "Tổ chức không tồn tại.";
                return NotFound();
            }

            News = await _dbContext.OrganizationNews.FirstOrDefaultAsync(n => n.Id == newsId && n.OrganizationId == Organization.Id);
            if (News == null)
            {
                TempData["ErrorMessage"] = "Bài viết không tồn tại.";
                return NotFound();
            }

            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                TempData["ErrorMessage"] = "Không thể xác định người dùng.";
                return RedirectToPage(new { slug, newsId });
            }

            var comment = await _dbContext.OrganizationNewsComments
                .FirstOrDefaultAsync(c => c.Id == commentId && c.NewsId == News.Id);
            if (comment == null)
            {
                TempData["ErrorMessage"] = "Bình luận không tồn tại.";
                return NotFound();
            }

            var isAdmin = await _dbContext.OrganizationMembers
                .AnyAsync(m => m.OrganizationId == Organization.Id && m.UserId == userId && m.Role == "Admin");
            if (!isAdmin && comment.UserId != userId)
            {
                TempData["ErrorMessage"] = "Bạn không có quyền xóa bình luận này.";
                return RedirectToPage(new { slug, newsId });
            }

            _dbContext.OrganizationNewsComments.Remove(comment);
            await _dbContext.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã xóa bình luận!";
            return RedirectToPage(new { slug, newsId });
        }

        private async Task LoadComments()
        {
            var commentsQuery = _dbContext.OrganizationNewsComments
                .Where(c => c.NewsId == News!.Id)
                .Join(_dbContext.Users,
                    c => c.UserId,
                    u => u.Id,
                    (c, u) => new CommentViewModel
                    {
                        Id = c.Id,
                        UserId = c.UserId,
                        UserName = u.DisplayName ?? u.UserName,
                        UserAvatar = u.AvatarUrl ?? string.Empty,
                        Content = c.Content,
                        CreatedAt = c.CreatedAt
                    })
                .OrderByDescending(c => c.CreatedAt)
                .AsNoTracking();

            var commentCount = await commentsQuery.CountAsync();
            TotalCommentPages = (int)Math.Ceiling((double)commentCount / CommentPageSize);

            Comments = await commentsQuery
                .Skip((CurrentCommentPage - 1) * CommentPageSize)
                .Take(CommentPageSize)
                .ToListAsync();
        }
    }
}
