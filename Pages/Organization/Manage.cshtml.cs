﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using LoginSystem.Data;
using LoginSystem.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using LoginSystem.Hubs;
using System.Collections.Generic;

namespace LoginSystem.Pages.Organization
{
    [Authorize]
    public class ManageModel : PageModel
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHubContext<ChatHub> _hubContext;

        public ApplicationDbContext DbContext => _dbContext;
        public UserManager<ApplicationUser> UserManager => _userManager;

        public ManageModel(
            ApplicationDbContext dbContext,
            UserManager<ApplicationUser> userManager,
            IHubContext<ChatHub> hubContext)
        {
            _dbContext = dbContext;
            _userManager = userManager;
            _hubContext = hubContext;
        }

        public LoginSystem.Models.Organization? Organization { get; set; }
        public IList<MemberViewModel> Members { get; set; } = new List<MemberViewModel>();
        public IList<JoinRequestViewModel> JoinRequests { get; set; } = new List<JoinRequestViewModel>();
        public IList<NewsViewModel> News { get; set; } = new List<NewsViewModel>();
        public IList<ExchangeItemViewModel> ExchangeItems { get; set; } = new List<ExchangeItemViewModel>();
        public List<SelectListItem> Categories { get; set; } = new List<SelectListItem>();
        public int TotalMembers { get; set; }
        public int TotalComments { get; set; }
        public double AverageRating { get; set; }
        [BindProperty(SupportsGet = true)]
        public string? ItemTitleFilter { get; set; }
        [BindProperty(SupportsGet = true)]
        public string? ItemCategoryFilter { get; set; }
        [BindProperty(SupportsGet = true)]
        public string? ItemCreatorFilter { get; set; }
        [BindProperty(SupportsGet = true)]
        public string? ItemStatusFilter { get; set; }

        public class MemberViewModel
        {
            public string UserId { get; set; } = string.Empty;
            public string UserName { get; set; } = string.Empty;
            public string AvatarUrl { get; set; } = string.Empty;
            public string Role { get; set; } = "Member";
            public DateTime JoinedAt { get; set; }
        }

        public class JoinRequestViewModel
        {
            public string Id { get; set; } = string.Empty;
            public string UserId { get; set; } = string.Empty;
            public string UserName { get; set; } = string.Empty;
            public string UserAvatar { get; set; } = string.Empty;
            public DateTime RequestedAt { get; set; }
        }

        public class NewsViewModel
        {
            public string Id { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
        }

        public class ExchangeItemViewModel
        {
            public string Id { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public string CreatorId { get; set; } = string.Empty;
            public string CreatorName { get; set; } = string.Empty;
            public string CreatorAvatar { get; set; } = string.Empty;
            public string CategoryId { get; set; } = string.Empty;
            public string CategoryName { get; set; } = string.Empty;
            public int QuantityAvailable { get; set; }
            public bool IsPrivate { get; set; }
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

            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                TempData["ErrorMessage"] = "Không thể xác định người dùng.";
                return RedirectToPage("/Organization/Details", new { slug });
            }

            var isAdmin = await _dbContext.OrganizationMembers
                .AsNoTracking()
                .AnyAsync(m => m.OrganizationId == Organization.Id && m.UserId == userId && m.Role == "Admin");
            if (!isAdmin)
            {
                TempData["ErrorMessage"] = "Bạn không có quyền quản lý tổ chức này.";
                return RedirectToPage("/Organization/Details", new { slug });
            }

            // Load members
            Members = await _dbContext.OrganizationMembers
                .Where(m => m.OrganizationId == Organization.Id)
                .Join(_dbContext.Users,
                    m => m.UserId,
                    u => u.Id,
                    (m, u) => new MemberViewModel
                    {
                        UserId = m.UserId,
                        UserName = u.DisplayName ?? u.UserName,
                        AvatarUrl = u.AvatarUrl ?? string.Empty,
                        Role = m.Role,
                        JoinedAt = m.JoinedAt
                    })
                .AsNoTracking()
                .ToListAsync();

            // Load join requests
            JoinRequests = await _dbContext.OrganizationJoinRequests
                .Where(r => r.OrganizationId == Organization.Id && r.Status == "Pending")
                .Join(_dbContext.Users,
                    r => r.UserId,
                    u => u.Id,
                    (r, u) => new JoinRequestViewModel
                    {
                        Id = r.Id,
                        UserId = r.UserId,
                        UserName = u.DisplayName ?? u.UserName,
                        UserAvatar = u.AvatarUrl ?? string.Empty,
                        RequestedAt = r.RequestedAt
                    })
                .AsNoTracking()
                .ToListAsync();

            // Load news
            News = await _dbContext.OrganizationNews
                .Where(n => n.OrganizationId == Organization.Id)
                .Select(n => new NewsViewModel
                {
                    Id = n.Id,
                    Title = n.Title,
                    Content = n.Content,
                    CreatedAt = n.CreatedAt
                })
                .OrderByDescending(n => n.CreatedAt)
                .AsNoTracking()
                .ToListAsync();

            // Load exchange items with filters
            var itemsQuery = _dbContext.ExchangeItems
                .Where(i => i.OrganizationId == Organization.Id)
                .Join(_dbContext.Users,
                    i => i.OwnerId,
                    u => u.Id,
                    (i, u) => new { Item = i, User = u })
                .Join(_dbContext.ItemCategories,
                    x => x.Item.CategoryId,
                    c => c.Id,
                    (x, c) => new ExchangeItemViewModel
                    {
                        Id = x.Item.Id,
                        Title = x.Item.Title,
                        CreatorId = x.Item.OwnerId,
                        CreatorName = x.User.DisplayName ?? x.User.UserName,
                        CreatorAvatar = x.User.AvatarUrl ?? string.Empty,
                        CategoryId = x.Item.CategoryId,
                        CategoryName = c.Name,
                        QuantityAvailable = x.Item.QuantityAvailable,
                        IsPrivate = x.Item.IsPrivate,
                        CreatedAt = x.Item.CreatedAt
                    });

            if (!string.IsNullOrEmpty(ItemTitleFilter))
            {
                itemsQuery = itemsQuery.Where(i => i.Title.Contains(ItemTitleFilter));
            }
            if (!string.IsNullOrEmpty(ItemCategoryFilter))
            {
                itemsQuery = itemsQuery.Where(i => i.CategoryId == ItemCategoryFilter);
            }
            if (!string.IsNullOrEmpty(ItemCreatorFilter))
            {
                itemsQuery = itemsQuery.Where(i => i.CreatorName.Contains(ItemCreatorFilter));
            }
            if (!string.IsNullOrEmpty(ItemStatusFilter) && bool.TryParse(ItemStatusFilter, out bool isPrivate))
            {
                itemsQuery = itemsQuery.Where(i => i.IsPrivate == isPrivate);
            }

            ExchangeItems = await itemsQuery
                .OrderByDescending(i => i.CreatedAt)
                .AsNoTracking()
                .ToListAsync();

            // Load categories for filter
            Categories = await _dbContext.ItemCategories
                .Select(c => new SelectListItem { Value = c.Id, Text = c.Name })
                .OrderBy(c => c.Text)
                .AsNoTracking()
                .ToListAsync();

            // Load statistics
            TotalMembers = Members.Count;
            TotalComments = await _dbContext.OrganizationNewsComments
                .AsNoTracking()
                .CountAsync(c => c.NewsId == _dbContext.OrganizationNews
                    .Where(n => n.OrganizationId == Organization.Id)
                    .Select(n => n.Id)
                    .FirstOrDefault());
            var ratings = await _dbContext.OrganizationRatings
                .Where(r => r.OrganizationId == Organization.Id)
                .AsNoTracking()
                .Select(r => r.Score)
                .ToListAsync();
            AverageRating = ratings.Any() ? ratings.Average() : 0;

            return Page();
        }

        public async Task<IActionResult> OnPostPromoteAsync(string slug, string memberId, string role)
        {
            if (string.IsNullOrEmpty(slug) || string.IsNullOrEmpty(memberId) || string.IsNullOrEmpty(role))
            {
                TempData["ErrorMessage"] = "Dữ liệu không hợp lệ.";
                return RedirectToPage(new { slug });
            }

            var organization = await _dbContext.Organizations.FirstOrDefaultAsync(o => o.Slug == slug);
            if (organization == null)
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

            var isCreator = organization.CreatorId == userId;
            if (!isCreator)
            {
                TempData["ErrorMessage"] = "Chỉ người tạo tổ chức mới có quyền thăng chức.";
                return RedirectToPage(new { slug });
            }

            var member = await _dbContext.OrganizationMembers
                .FirstOrDefaultAsync(m => m.OrganizationId == organization.Id && m.UserId == memberId);
            if (member == null)
            {
                TempData["ErrorMessage"] = "Thành viên không tồn tại.";
                return RedirectToPage(new { slug });
            }

            if (member.Role == "Admin" || member.UserId == organization.CreatorId)
            {
                TempData["ErrorMessage"] = "Không thể thăng chức cho thành viên này.";
                return RedirectToPage(new { slug });
            }

            member.Role = "Admin";
            await _dbContext.SaveChangesAsync();

            var user = await _userManager.FindByIdAsync(memberId);
            var notification = new Notification
            {
                UserId = memberId,
                Content = $"Bạn đã được thăng chức thành {member.Role} trong tổ chức {organization.Name}.",
                OrganizationId = organization.Id,
                Type = "OrganizationRoleChange",
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.Notifications.Add(notification);
            await _dbContext.SaveChangesAsync();

            await _hubContext.Clients.User(memberId).SendAsync("ReceiveNotification",
                notification.Id,
                notification.Content,
                notification.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                $"/Organization/Details/{slug}",
                notification.Type);

            TempData["SuccessMessage"] = $"Đã thăng chức {user?.DisplayName ?? user?.UserName} thành {member.Role}!";
            return RedirectToPage(new { slug });
        }

        public async Task<IActionResult> OnPostDemoteAsync(string slug, string memberId)
        {
            if (string.IsNullOrEmpty(slug) || string.IsNullOrEmpty(memberId))
            {
                TempData["ErrorMessage"] = "Dữ liệu không hợp lệ.";
                return RedirectToPage(new { slug });
            }

            var organization = await _dbContext.Organizations.FirstOrDefaultAsync(o => o.Slug == slug);
            if (organization == null)
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

            var isCreator = organization.CreatorId == userId;
            if (!isCreator)
            {
                TempData["ErrorMessage"] = "Chỉ người tạo tổ chức mới có quyền giáng chức.";
                return RedirectToPage(new { slug });
            }

            var member = await _dbContext.OrganizationMembers
                .FirstOrDefaultAsync(m => m.OrganizationId == organization.Id && m.UserId == memberId);
            if (member == null || member.UserId == userId)
            {
                TempData["ErrorMessage"] = "Không thể giáng chức hoặc thành viên không tồn tại.";
                return RedirectToPage(new { slug });
            }

            if (member.Role == "Member" || member.UserId == organization.CreatorId)
            {
                TempData["ErrorMessage"] = "Không thể giáng chức cho thành viên này.";
                return RedirectToPage(new { slug });
            }

            member.Role = "Member";
            await _dbContext.SaveChangesAsync();

            var user = await _userManager.FindByIdAsync(memberId);
            var notification = new Notification
            {
                UserId = memberId,
                Content = $"Bạn đã bị giáng chức xuống Member trong tổ chức {organization.Name}.",
                OrganizationId = organization.Id,
                Type = "OrganizationRoleChange",
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.Notifications.Add(notification);
            await _dbContext.SaveChangesAsync();

            await _hubContext.Clients.User(memberId).SendAsync("ReceiveNotification",
                notification.Id,
                notification.Content,
                notification.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                $"/Organization/Details/{slug}",
                notification.Type);

            TempData["SuccessMessage"] = $"Đã giáng chức {user?.DisplayName ?? user?.UserName} thành Member!";
            return RedirectToPage(new { slug });
        }

        public async Task<IActionResult> OnPostRemoveMemberAsync(string slug, string memberId)
        {
            if (string.IsNullOrEmpty(slug) || string.IsNullOrEmpty(memberId))
            {
                TempData["ErrorMessage"] = "Dữ liệu không hợp lệ.";
                return RedirectToPage(new { slug });
            }

            var organization = await _dbContext.Organizations.FirstOrDefaultAsync(o => o.Slug == slug);
            if (organization == null)
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

            var isAdmin = await _dbContext.OrganizationMembers
                .AnyAsync(m => m.OrganizationId == organization.Id && m.UserId == userId && m.Role == "Admin");
            if (!isAdmin)
            {
                TempData["ErrorMessage"] = "Bạn không có quyền xóa thành viên.";
                return RedirectToPage(new { slug });
            }

            var member = await _dbContext.OrganizationMembers
                .FirstOrDefaultAsync(m => m.OrganizationId == organization.Id && m.UserId == memberId);
            if (member == null || member.UserId == userId)
            {
                TempData["ErrorMessage"] = "Không thể xóa hoặc thành viên không tồn tại.";
                return RedirectToPage(new { slug });
            }

            if (member.UserId == organization.CreatorId)
            {
                TempData["ErrorMessage"] = "Không thể xóa người tạo tổ chức.";
                return RedirectToPage(new { slug });
            }

            _dbContext.OrganizationMembers.Remove(member);

            var joinRequests = await _dbContext.OrganizationJoinRequests
                .Where(r => r.UserId == memberId && r.OrganizationId == organization.Id)
                .ToListAsync();
            if (joinRequests.Any())
            {
                _dbContext.OrganizationJoinRequests.RemoveRange(joinRequests);
            }

            var ratings = await _dbContext.OrganizationRatings
                .Where(r => r.UserId == memberId && r.OrganizationId == organization.Id)
                .ToListAsync();
            if (ratings.Any())
            {
                _dbContext.OrganizationRatings.RemoveRange(ratings);
            }

            var user = await _userManager.FindByIdAsync(memberId);
            if (user != null)
            {
                user.OrganizationId = null;
                await _userManager.UpdateAsync(user);
            }

            var notification = new Notification
            {
                UserId = memberId,
                Content = $"Bạn đã bị xóa khỏi tổ chức {organization.Name}.",
                OrganizationId = organization.Id,
                Type = "OrganizationRoleChange",
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.Notifications.Add(notification);
            await _dbContext.SaveChangesAsync();

            await _hubContext.Clients.User(memberId).SendAsync("ReceiveNotification",
                notification.Id,
                notification.Content,
                notification.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                $"/Organization/Details/{slug}",
                notification.Type);

            TempData["SuccessMessage"] = $"Đã xóa thành viên {user?.DisplayName ?? user?.UserName}!";
            return RedirectToPage(new { slug });
        }

        public async Task<IActionResult> OnPostApproveRequestAsync(string slug, string requestId)
        {
            if (string.IsNullOrEmpty(slug) || string.IsNullOrEmpty(requestId))
            {
                TempData["ErrorMessage"] = "Dữ liệu không hợp lệ.";
                return RedirectToPage(new { slug });
            }

            var organization = await _dbContext.Organizations.FirstOrDefaultAsync(o => o.Slug == slug);
            if (organization == null)
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

            var isAdmin = await _dbContext.OrganizationMembers
                .AnyAsync(m => m.OrganizationId == organization.Id && m.UserId == userId && m.Role == "Admin");
            if (!isAdmin)
            {
                TempData["ErrorMessage"] = "Bạn không có quyền duyệt yêu cầu.";
                return RedirectToPage(new { slug });
            }

            var request = await _dbContext.OrganizationJoinRequests
                .FirstOrDefaultAsync(r => r.Id == requestId && r.OrganizationId == organization.Id);
            if (request == null)
            {
                TempData["ErrorMessage"] = "Yêu cầu không tồn tại.";
                return RedirectToPage(new { slug });
            }

            if (request.Status != "Pending")
            {
                TempData["ErrorMessage"] = "Yêu cầu đã được xử lý trước đó.";
                return RedirectToPage(new { slug });
            }

            var user = await _userManager.FindByIdAsync(request.UserId);
            if (user == null)
            {
                _dbContext.OrganizationJoinRequests.Remove(request);
                await _dbContext.SaveChangesAsync();
                TempData["ErrorMessage"] = "Người dùng không tồn tại.";
                return RedirectToPage(new { slug });
            }

            var existingMember = await _dbContext.OrganizationMembers
                .FirstOrDefaultAsync(m => m.UserId == user.Id && m.OrganizationId == organization.Id);
            if (existingMember != null)
            {
                _dbContext.OrganizationJoinRequests.Remove(request);
                await _dbContext.SaveChangesAsync();
                TempData["ErrorMessage"] = "Người dùng đã là thành viên của tổ chức.";
                return RedirectToPage(new { slug });
            }

            if (user.OrganizationId != null)
            {
                _dbContext.OrganizationJoinRequests.Remove(request);
                await _dbContext.SaveChangesAsync();
                TempData["ErrorMessage"] = "Người dùng đã tham gia một tổ chức khác.";
                return RedirectToPage(new { slug });
            }

            request.Status = "Approved";
            var member = new OrganizationMember
            {
                OrganizationId = organization.Id,
                UserId = request.UserId,
                Role = "Member",
                JoinedAt = DateTime.UtcNow
            };
            _dbContext.OrganizationMembers.Add(member);
            user.OrganizationId = organization.Id;
            await _userManager.UpdateAsync(user);

            var notification = new Notification
            {
                UserId = request.UserId,
                Content = $"Yêu cầu tham gia tổ chức {organization.Name} của bạn đã được chấp nhận.",
                OrganizationId = organization.Id,
                Type = "OrganizationJoin",
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.Notifications.Add(notification);
            await _dbContext.SaveChangesAsync();

            await _hubContext.Clients.User(request.UserId).SendAsync("ReceiveNotification",
                notification.Id,
                notification.Content,
                notification.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                $"/Organization/Details/{slug}",
                notification.Type);

            TempData["SuccessMessage"] = $"Đã chấp nhận yêu cầu tham gia của {user.DisplayName ?? user.UserName}!";
            return RedirectToPage(new { slug });
        }

        public async Task<IActionResult> OnPostRejectRequestAsync(string slug, string requestId)
        {
            if (string.IsNullOrEmpty(slug) || string.IsNullOrEmpty(requestId))
            {
                TempData["ErrorMessage"] = "Dữ liệu không hợp lệ.";
                return RedirectToPage(new { slug });
            }

            var organization = await _dbContext.Organizations.FirstOrDefaultAsync(o => o.Slug == slug);
            if (organization == null)
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

            var isAdmin = await _dbContext.OrganizationMembers
                .AnyAsync(m => m.OrganizationId == organization.Id && m.UserId == userId && m.Role == "Admin");
            if (!isAdmin)
            {
                TempData["ErrorMessage"] = "Bạn không có quyền từ chối yêu cầu.";
                return RedirectToPage(new { slug });
            }

            var request = await _dbContext.OrganizationJoinRequests
                .FirstOrDefaultAsync(r => r.Id == requestId && r.OrganizationId == organization.Id);
            if (request == null)
            {
                TempData["ErrorMessage"] = "Yêu cầu không tồn tại.";
                return RedirectToPage(new { slug });
            }

            if (request.Status != "Pending")
            {
                TempData["ErrorMessage"] = "Yêu cầu đã được xử lý trước đó.";
                return RedirectToPage(new { slug });
            }

            var user = await _userManager.FindByIdAsync(request.UserId);
            if (user == null)
            {
                _dbContext.OrganizationJoinRequests.Remove(request);
                await _dbContext.SaveChangesAsync();
                TempData["ErrorMessage"] = "Người dùng không tồn tại.";
                return RedirectToPage(new { slug });
            }

            var existingMember = await _dbContext.OrganizationMembers
                .FirstOrDefaultAsync(m => m.UserId == user.Id && m.OrganizationId == organization.Id);
            if (existingMember != null)
            {
                _dbContext.OrganizationJoinRequests.Remove(request);
                await _dbContext.SaveChangesAsync();
                TempData["ErrorMessage"] = "Người dùng vẫn là thành viên của tổ chức.";
                return RedirectToPage(new { slug });
            }

            request.Status = "Rejected";
            await _dbContext.SaveChangesAsync();

            var notification = new Notification
            {
                UserId = request.UserId,
                Content = $"Yêu cầu tham gia tổ chức {organization.Name} của bạn đã bị từ chối.",
                OrganizationId = organization.Id,
                Type = "OrganizationJoin",
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.Notifications.Add(notification);
            await _dbContext.SaveChangesAsync();

            await _hubContext.Clients.User(request.UserId).SendAsync("ReceiveNotification",
                notification.Id,
                notification.Content,
                notification.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                $"/Organization/Details/{slug}",
                notification.Type);

            TempData["SuccessMessage"] = $"Đã từ chối yêu cầu tham gia của {user.DisplayName ?? user.UserName}!";
            return RedirectToPage(new { slug });
        }

        public async Task<IActionResult> OnPostDeleteItemAsync(string slug, string itemId)
        {
            if (string.IsNullOrEmpty(slug) || string.IsNullOrEmpty(itemId))
            {
                TempData["ErrorMessage"] = "Dữ liệu không hợp lệ.";
                return RedirectToPage(new { slug });
            }

            var organization = await _dbContext.Organizations.FirstOrDefaultAsync(o => o.Slug == slug);
            if (organization == null)
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

            var isAdmin = await _dbContext.OrganizationMembers
                .AnyAsync(m => m.OrganizationId == organization.Id && m.UserId == userId && m.Role == "Admin");
            if (!isAdmin)
            {
                TempData["ErrorMessage"] = "Bạn không có quyền xóa mặt hàng.";
                return RedirectToPage(new { slug });
            }

            var item = await _dbContext.ExchangeItems
                .FirstOrDefaultAsync(i => i.Id == itemId && i.OrganizationId == organization.Id);
            if (item == null)
            {
                TempData["ErrorMessage"] = "Mặt hàng không tồn tại hoặc không thuộc tổ chức này.";
                return RedirectToPage(new { slug });
            }

            try
            {
                // Remove associated data
                var mediaItems = await _dbContext.ItemMedia
                    .Where(m => m.ItemId == itemId)
                    .ToListAsync();
                if (mediaItems.Any())
                {
                    _dbContext.ItemMedia.RemoveRange(mediaItems);
                }

                var tags = await _dbContext.ItemTags
                    .Where(t => t.ItemId == itemId)
                    .ToListAsync();
                if (tags.Any())
                {
                    _dbContext.ItemTags.RemoveRange(tags);
                }

                var ratings = await _dbContext.ItemRatings
                    .Where(r => r.ItemId == itemId)
                    .ToListAsync();
                if (ratings.Any())
                {
                    _dbContext.ItemRatings.RemoveRange(ratings);
                }

                var comments = await _dbContext.ItemComments
                    .Where(c => c.ItemId == itemId)
                    .ToListAsync();
                if (comments.Any())
                {
                    _dbContext.ItemComments.RemoveRange(comments);
                }

                var reports = await _dbContext.ItemReports
                    .Where(r => r.ItemId == itemId)
                    .ToListAsync();
                if (reports.Any())
                {
                    _dbContext.ItemReports.RemoveRange(reports);
                }

                _dbContext.ExchangeItems.Remove(item);
                await _dbContext.SaveChangesAsync();

                var user = await _userManager.FindByIdAsync(item.OwnerId);
                var notification = new Notification
                {
                    UserId = item.OwnerId,
                    Content = $"Mặt hàng '{item.Title}' của bạn đã bị xóa bởi quản trị viên tổ chức {organization.Name}.",
                    OrganizationId = organization.Id,
                    Type = "ExchangeItemDeletion",
                    CreatedAt = DateTime.UtcNow
                };
                _dbContext.Notifications.Add(notification);
                await _dbContext.SaveChangesAsync();

                await _hubContext.Clients.User(item.OwnerId).SendAsync("ReceiveNotification",
                    notification.Id,
                    notification.Content,
                    notification.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                    $"/Exchange/Item/{itemId}",
                    notification.Type);

                TempData["SuccessMessage"] = $"Đã xóa mặt hàng '{item.Title}' thành công!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi xóa mặt hàng: {ex.Message}";
            }

            return RedirectToPage(new { slug });
        }
    }
}