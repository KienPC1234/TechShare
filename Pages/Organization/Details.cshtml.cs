using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using LoginSystem.Data;
using LoginSystem.Models;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using LoginSystem.Hubs;

namespace LoginSystem.Pages.Organization
{
    [Authorize]
    public class DetailsModel : PageModel
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHubContext<ChatHub> _hubContext;

        public DetailsModel(
            ApplicationDbContext dbContext,
            UserManager<ApplicationUser> userManager,
            IHubContext<ChatHub> hubContext)
        {
            _dbContext = dbContext;
            _userManager = userManager;
            _hubContext = hubContext;
        }

        public LoginSystem.Models.Organization? Organization { get; set; }
        public bool IsMember { get; set; }
        public bool IsAdmin { get; set; }
        public bool HasRequested { get; set; }
        public double AverageRating { get; set; }
        public int MemberCount { get; set; }
        public string CurrentUserId { get; set; } = string.Empty;
        public IList<CommentViewModel> Comments { get; set; } = new List<CommentViewModel>();
        public IList<MemberViewModel> Members { get; set; } = new List<MemberViewModel>();
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        private const int PageSize = 20;

        public class CommentViewModel
        {
            public string Id { get; set; } = string.Empty;
            public string UserId { get; set; } = string.Empty;
            public string UserName { get; set; } = string.Empty;
            public string UserAvatar { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
        }

        public class MemberViewModel
        {
            public string UserId { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
            public string AvatarUrl { get; set; } = string.Empty;
            public string Role { get; set; } = string.Empty;
            public DateTime JoinedAt { get; set; }
        }

        [BindProperty]
        public CommentInputModel CommentInput { get; set; } = new CommentInputModel();

        [BindProperty]
        public ReportInputModel ReportInput { get; set; } = new ReportInputModel();

        [BindProperty]
        public RatingInputModel RatingInput { get; set; } = new RatingInputModel();

        [BindProperty]
        public JoinInputModel JoinInput { get; set; } = new JoinInputModel();

        public class CommentInputModel
        {
            [Required(ErrorMessage = "Vui lòng nhập nội dung bình luận")]
            [StringLength(500, ErrorMessage = "Bình luận không được vượt quá 500 ký tự")]
            public string Content { get; set; } = string.Empty;
        }

        public class ReportInputModel
        {
            [Required(ErrorMessage = "Vui lòng nhập lý do báo cáo")]
            [StringLength(500, ErrorMessage = "Lý do báo cáo không được vượt quá 500 ký tự")]
            public string Reason { get; set; } = string.Empty;
        }

        public class RatingInputModel
        {
            [Required(ErrorMessage = "Vui lòng chọn số sao đánh giá")]
            [Range(1, 5, ErrorMessage = "Đánh giá phải từ 1 đến 5 sao")]
            public int Score { get; set; }
        }

        public class JoinInputModel
        {
            [Required(ErrorMessage = "Vui lòng đồng ý với điều khoản để tham gia")]
            public bool TermsAccepted { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(string slug, int page = 1)
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

            CurrentPage = page < 1 ? 1 : page;
            CurrentUserId = _userManager.GetUserId(User) ?? string.Empty;
            await LoadOrganizationData(CurrentUserId);

            return Page();
        }

        public async Task<IActionResult> OnPostJoinAsync(string slug)
        {
            ModelState.Clear();
            TryValidateModel(JoinInput, nameof(JoinInput));

            Organization = await _dbContext.Organizations.FirstOrDefaultAsync(o => o.Slug == slug);
            if (Organization == null)
            {
                TempData["ErrorMessage"] = "Tổ chức không tồn tại.";
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                CurrentUserId = _userManager.GetUserId(User) ?? string.Empty;
                await LoadOrganizationData(CurrentUserId);
                TempData["ErrorMessage"] = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return Page();
            }

            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                TempData["ErrorMessage"] = "Không thể xác định người dùng.";
                return RedirectToPage(new { slug });
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null || user.OrganizationId != null)
            {
                TempData["ErrorMessage"] = "Bạn đã tham gia một tổ chức. Mỗi người dùng chỉ được tham gia 1 tổ chức.";
                return RedirectToPage(new { slug });
            }

            if (await _dbContext.OrganizationMembers.AnyAsync(m => m.OrganizationId == Organization.Id && m.UserId == userId))
            {
                TempData["ErrorMessage"] = "Bạn đã là thành viên của tổ chức này.";
                return RedirectToPage(new { slug });
            }

            if (Organization.IsPrivate)
            {
                if (!await _dbContext.OrganizationJoinRequests.AnyAsync(r => r.OrganizationId == Organization.Id && r.UserId == userId))
                {
                    var request = new OrganizationJoinRequest
                    {
                        OrganizationId = Organization.Id,
                        UserId = userId,
                        Status = "Pending",
                        RequestedAt = DateTime.UtcNow
                    };
                    _dbContext.OrganizationJoinRequests.Add(request);

                    var admins = await _dbContext.OrganizationMembers
                        .Where(m => m.OrganizationId == Organization.Id && m.Role == "Admin")
                        .ToListAsync();

                    foreach (var admin in admins)
                    {
                        var notification = new Notification
                        {
                            UserId = admin.UserId,
                            Content = $"Người dùng {user.DisplayName ?? user.UserName} đã yêu cầu tham gia tổ chức {Organization.Name}.",
                            OrganizationId = Organization.Id,
                            Type = "OrganizationJoin",
                            CreatedAt = DateTime.UtcNow
                        };
                        _dbContext.Notifications.Add(notification);

                        // Send real-time notification
                        await _hubContext.Clients.User(admin.UserId).SendAsync("ReceiveNotification",
                            notification.Id,
                            notification.Content,
                            notification.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                            $"/Organization/Details/{slug}",
                            notification.Type);
                    }
                }
                TempData["SuccessMessage"] = "Yêu cầu tham gia đã được gửi!";
            }
            else
            {
                var member = new OrganizationMember
                {
                    OrganizationId = Organization.Id,
                    UserId = userId,
                    Role = "Member",
                    JoinedAt = DateTime.UtcNow
                };
                _dbContext.OrganizationMembers.Add(member);
                user.OrganizationId = Organization.Id;
                await _userManager.UpdateAsync(user);

                var notification = new Notification
                {
                    UserId = userId,
                    Content = $"Bạn đã tham gia tổ chức {Organization.Name} thành công.",
                    OrganizationId = Organization.Id,
                    Type = "OrganizationJoin",
                    CreatedAt = DateTime.UtcNow
                };
                _dbContext.Notifications.Add(notification);

                // Send real-time notification
                await _hubContext.Clients.User(userId).SendAsync("ReceiveNotification",
                    notification.Id,
                    notification.Content,
                    notification.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                    $"/Organization/Details/{slug}",
                    notification.Type);

                TempData["SuccessMessage"] = "Tham gia tổ chức thành công!";
            }

            await _dbContext.SaveChangesAsync();
            return RedirectToPage(new { slug });
        }

        public async Task<IActionResult> OnPostLeaveAsync(string slug)
        {
            ModelState.Clear();
            Organization = await _dbContext.Organizations.FirstOrDefaultAsync(o => o.Slug == slug);
            if (Organization == null)
            {
                TempData["ErrorMessage"] = "Tổ chức không tồn tại.";
                return NotFound();
            }

            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                TempData["ErrorMessage"] = "Không thể xác định người dùng.";
                return RedirectToPage(new { slug });
            }

            if (Organization.CreatorId == userId)
            {
                TempData["ErrorMessage"] = "Người tạo tổ chức không thể rời khỏi tổ chức.";
                return RedirectToPage(new { slug });
            }

            var membership = await _dbContext.OrganizationMembers
                .FirstOrDefaultAsync(m => m.OrganizationId == Organization.Id && m.UserId == userId);
            if (membership == null)
            {
                TempData["ErrorMessage"] = "Bạn không phải là thành viên của tổ chức này.";
                return RedirectToPage(new { slug });
            }

            _dbContext.OrganizationMembers.Remove(membership);
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                user.OrganizationId = null;
                await _userManager.UpdateAsync(user);
            }

            var notification = new Notification
            {
                UserId = userId,
                Content = $"Bạn đã rời khỏi tổ chức {Organization.Name}.",
                OrganizationId = Organization.Id,
                Type = "OrganizationJoin",
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.Notifications.Add(notification);

            // Send real-time notification
            await _hubContext.Clients.User(userId).SendAsync("ReceiveNotification",
                notification.Id,
                notification.Content,
                notification.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                $"/Organization/Details/{slug}",
                notification.Type);

            await _dbContext.SaveChangesAsync();
            TempData["SuccessMessage"] = "Bạn đã rời khỏi tổ chức thành công!";
            return RedirectToPage(new { slug });
        }

        public async Task<IActionResult> OnPostCommentAsync(string slug)
        {
            ModelState.Clear();
            TryValidateModel(CommentInput, nameof(CommentInput));

            Organization = await _dbContext.Organizations.FirstOrDefaultAsync(o => o.Slug == slug);
            if (Organization == null)
            {
                TempData["ErrorMessage"] = "Tổ chức không tồn tại.";
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                CurrentUserId = _userManager.GetUserId(User) ?? string.Empty;
                await LoadOrganizationData(CurrentUserId);
                TempData["ErrorMessage"] = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return Page();
            }

            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                TempData["ErrorMessage"] = "Không thể xác định người dùng.";
                return RedirectToPage(new { slug });
            }

            if (!await _dbContext.OrganizationMembers.AnyAsync(m => m.OrganizationId == Organization.Id && m.UserId == userId))
            {
                TempData["ErrorMessage"] = "Bạn phải là thành viên để bình luận.";
                return RedirectToPage(new { slug });
            }

            var comment = new OrganizationComment
            {
                OrganizationId = Organization.Id,
                UserId = userId,
                Content = CommentInput.Content.Trim(),
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.OrganizationComments.Add(comment);

            // Notify admins about the new comment
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
                        Content = $"Bình luận mới trên tổ chức {Organization.Name} từ {user?.DisplayName ?? user?.UserName}: {CommentInput.Content.Substring(0, Math.Min(50, CommentInput.Content.Length))}...",
                        OrganizationId = Organization.Id,
                        Type = "OrganizationComment",
                        CreatedAt = DateTime.UtcNow
                    };
                    _dbContext.Notifications.Add(notification);

                    // Send real-time notification
                    await _hubContext.Clients.User(admin.UserId).SendAsync("ReceiveNotification",
                        notification.Id,
                        notification.Content,
                        notification.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                        $"/Organization/Details/{slug}",
                        notification.Type);
                }
            }

            await _dbContext.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã thêm bình luận!";
            return RedirectToPage(new { slug });
        }

        public async Task<IActionResult> OnPostDeleteCommentAsync(string slug, string commentId)
        {
            ModelState.Clear();
            Organization = await _dbContext.Organizations.FirstOrDefaultAsync(o => o.Slug == slug);
            if (Organization == null)
            {
                TempData["ErrorMessage"] = "Tổ chức không tồn tại.";
                return NotFound();
            }

            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                TempData["ErrorMessage"] = "Không thể xác định người dùng.";
                return RedirectToPage(new { slug });
            }

            var comment = await _dbContext.OrganizationComments
                .FirstOrDefaultAsync(c => c.Id == commentId && c.OrganizationId == Organization.Id);

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
                return RedirectToPage(new { slug });
            }

            _dbContext.OrganizationComments.Remove(comment);
            await _dbContext.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã xóa bình luận!";
            return RedirectToPage(new { slug });
        }

        public async Task<IActionResult> OnPostReportAsync(string slug)
        {
            ModelState.Clear();
            TryValidateModel(ReportInput, nameof(ReportInput));

            Organization = await _dbContext.Organizations.FirstOrDefaultAsync(o => o.Slug == slug);
            if (Organization == null)
            {
                TempData["ErrorMessage"] = "Tổ chức không tồn tại.";
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                CurrentUserId = _userManager.GetUserId(User) ?? string.Empty;
                await LoadOrganizationData(CurrentUserId);
                TempData["ErrorMessage"] = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return Page();
            }

            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                TempData["ErrorMessage"] = "Không thể xác định người dùng.";
                return RedirectToPage(new { slug });
            }

            if (Organization.CreatorId == userId)
            {
                TempData["ErrorMessage"] = "Bạn không thể báo cáo tổ chức do chính mình tạo.";
                return RedirectToPage(new { slug });
            }

            var report = new OrganizationReport
            {
                Id = Guid.NewGuid().ToString(),
                OrganizationId = Organization.Id,
                UserId = userId,
                Reason = ReportInput.Reason.Trim(),
                ReportedAt = DateTime.UtcNow
            };
            _dbContext.OrganizationReports.Add(report);

            var superAdmins = await _userManager.GetUsersInRoleAsync("SuperAdmin");
            var user = await _userManager.FindByIdAsync(userId);
            foreach (var superAdmin in superAdmins)
            {
                var notification = new Notification
                {
                    UserId = superAdmin.Id,
                    Content = $"Tổ chức {Organization.Name} bị báo cáo bởi {user?.DisplayName ?? user?.UserName}: {ReportInput.Reason.Substring(0, Math.Min(50, ReportInput.Reason.Length))}...",
                    OrganizationId = Organization.Id,
                    Type = "OrganizationReport",
                    CreatedAt = DateTime.UtcNow
                };
                _dbContext.Notifications.Add(notification);

                // Send real-time notification
                await _hubContext.Clients.User(superAdmin.Id).SendAsync("ReceiveNotification",
                    notification.Id,
                    notification.Content,
                    notification.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                    $"/Organization/Details/{slug}",
                    notification.Type);
            }

            await _dbContext.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã gửi báo cáo!";
            return RedirectToPage(new { slug });
        }

        public async Task<IActionResult> OnPostRateAsync(string slug)
        {
            ModelState.Clear();
            TryValidateModel(RatingInput, nameof(RatingInput));

            Organization = await _dbContext.Organizations.FirstOrDefaultAsync(o => o.Slug == slug);
            if (Organization == null)
            {
                TempData["ErrorMessage"] = "Tổ chức không tồn tại.";
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                CurrentUserId = _userManager.GetUserId(User) ?? string.Empty;
                await LoadOrganizationData(CurrentUserId);
                TempData["ErrorMessage"] = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return Page();
            }

            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                TempData["ErrorMessage"] = "Không thể xác định người dùng.";
                return RedirectToPage(new { slug });
            }

            if (!await _dbContext.OrganizationMembers.AnyAsync(m => m.OrganizationId == Organization.Id && m.UserId == userId))
            {
                TempData["ErrorMessage"] = "Bạn phải là thành viên để đánh giá.";
                return RedirectToPage(new { slug });
            }

            await using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                var existingRating = await _dbContext.OrganizationRatings
                    .FirstOrDefaultAsync(r => r.OrganizationId == Organization.Id && r.UserId == userId);

                if (existingRating != null)
                {
                    existingRating.Score = RatingInput.Score;
                    existingRating.RatedAt = DateTime.UtcNow;
                }
                else
                {
                    var rating = new OrganizationRating
                    {
                        OrganizationId = Organization.Id,
                        UserId = userId,
                        Score = RatingInput.Score,
                        RatedAt = DateTime.UtcNow
                    };
                    _dbContext.OrganizationRatings.Add(rating);
                }

                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();
                TempData["SuccessMessage"] = "Đã đánh giá tổ chức!";
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["ErrorMessage"] = $"Có lỗi xảy ra khi lưu đánh giá: {ex.Message}. Vui lòng thử lại.";
            }

            return RedirectToPage(new { slug });
        }

        private async Task LoadOrganizationData(string userId)
        {
            if (string.IsNullOrEmpty(userId) || Organization == null)
            {
                return;
            }

            var membershipQuery = _dbContext.OrganizationMembers
                .Where(m => m.OrganizationId == Organization.Id && m.UserId == userId);

            IsMember = await membershipQuery.AnyAsync();
            IsAdmin = IsMember && await membershipQuery.AnyAsync(m => m.Role == "Admin");
            HasRequested = await _dbContext.OrganizationJoinRequests
                .AnyAsync(r => r.OrganizationId == Organization.Id && r.UserId == userId && r.Status == "Pending");

            MemberCount = await _dbContext.OrganizationMembers
                .CountAsync(m => m.OrganizationId == Organization.Id);

            var ratings = await _dbContext.OrganizationRatings
                .Where(r => r.OrganizationId == Organization.Id)
                .AsNoTracking()
                .Select(r => r.Score)
                .ToListAsync();
            AverageRating = ratings.Any() ? ratings.Average() : 0;

            Comments = await _dbContext.OrganizationComments
                .Where(c => c.OrganizationId == Organization.Id)
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
                .Take(50)
                .AsNoTracking()
                .ToListAsync();

            var membersQuery = _dbContext.OrganizationMembers
                .Where(m => m.OrganizationId == Organization.Id)
                .Join(_dbContext.Users,
                    m => m.UserId,
                    u => u.Id,
                    (m, u) => new MemberViewModel
                    {
                        UserId = m.UserId,
                        DisplayName = u.DisplayName ?? u.UserName,
                        AvatarUrl = u.AvatarUrl ?? string.Empty,
                        Role = m.Role,
                        JoinedAt = m.JoinedAt
                    })
                .OrderBy(m => m.JoinedAt)
                .AsNoTracking();

            Members = await membersQuery
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            TotalPages = (int)Math.Ceiling((double)MemberCount / PageSize);
        }
    }
}