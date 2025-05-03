#nullable enable
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using LoginSystem.Data;
using LoginSystem.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LoginSystem.Pages
{
    [Authorize(Roles = "SuperAdmin")]
    public class DashboardModel : PageModel
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public ApplicationDbContext DbContext => _dbContext;
        public UserManager<ApplicationUser> UserManager => _userManager;

        public DashboardModel(
            ApplicationDbContext dbContext,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _signInManager = signInManager ?? throw new ArgumentNullException(nameof(signInManager));
        }

        public IList<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();
        public IList<LoginSystem.Models.Organization> Organizations { get; set; } = new List<LoginSystem.Models.Organization>();
        public IList<OrganizationReport> Reports { get; set; } = new List<OrganizationReport>();
        public IList<ApplicationUser> AdminRequests { get; set; } = new List<ApplicationUser>();

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [BindProperty(SupportsGet = true)]
        public int PageIndex { get; set; } = 1;

        public int TotalPages { get; set; }
        public int TotalOrgPages { get; set; }
        public int TotalUsers { get; set; }
        public int TotalOrganizations { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            // Ensure PageIndex is valid
            PageIndex = Math.Max(1, PageIndex);

            // Users
            var allUsers = _dbContext.Users.AsQueryable();
            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                allUsers = allUsers.Where(u =>
                    (u.UserName != null && u.UserName.Contains(SearchTerm)) ||
                    (u.Email != null && u.Email.Contains(SearchTerm)) ||
                    (u.DisplayName != null && u.DisplayName.Contains(SearchTerm)));
            }

            int pageSize = 10;
            TotalUsers = await allUsers.CountAsync();
            TotalPages = (int)Math.Ceiling(TotalUsers / (double)pageSize);

            Users = await allUsers
                .OrderBy(u => u.UserName)
                .Skip((PageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Admin Requests
            AdminRequests = await _dbContext.Users
                .Where(u => u.AdminRequestPending)
                .ToListAsync();

            // Organizations
            var allOrgs = _dbContext.Organizations.AsQueryable();
            TotalOrganizations = await allOrgs.CountAsync();
            TotalOrgPages = (int)Math.Ceiling(TotalOrganizations / (double)pageSize);

            Organizations = await allOrgs
                .OrderBy(o => o.Name)
                .Skip((PageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Reports
            Reports = await _dbContext.OrganizationReports
                .OrderByDescending(r => r.ReportedAt)
                .Take(50)
                .ToListAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostActionAsync(string id, string action, int pageIndex, string? searchTerm, string? entityType = "user")
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(action))
            {
                TempData["ErrorMessage"] = "Hành động không hợp lệ.";
                return RedirectToPage(new { pageIndex, searchTerm });
            }

            if (entityType == "user")
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy người dùng.";
                    return RedirectToPage(new { pageIndex, searchTerm });
                }

                switch (action)
                {
                    case "delete":
                        if (!User.IsInRole("SuperAdmin"))
                        {
                            TempData["ErrorMessage"] = "Bạn không có quyền xóa người dùng.";
                            return RedirectToPage(new { pageIndex, searchTerm });
                        }
                        await _userManager.DeleteAsync(user);
                        TempData["SuccessMessage"] = "Xóa người dùng thành công.";
                        break;
                    case "promote":
                        if (!await _userManager.IsInRoleAsync(user, "SuperAdmin"))
                        {
                            await _userManager.AddToRoleAsync(user, "SuperAdmin");
                            TempData["SuccessMessage"] = "Thăng cấp thành SuperAdmin thành công.";
                        }
                        else
                        {
                            TempData["ErrorMessage"] = "Người dùng đã là SuperAdmin.";
                        }
                        break;
                    case "demote":
                        if (await _userManager.IsInRoleAsync(user, "SuperAdmin") && user.Id != _userManager.GetUserId(User))
                        {
                            await _userManager.RemoveFromRoleAsync(user, "SuperAdmin");
                            TempData["SuccessMessage"] = "Giáng cấp SuperAdmin thành công.";
                        }
                        else
                        {
                            TempData["ErrorMessage"] = "Không thể giáng cấp SuperAdmin.";
                        }
                        break;
                    case "promoteAdmin":
                        if (!await _userManager.IsInRoleAsync(user, "Admin"))
                        {
                            await _userManager.AddToRoleAsync(user, "Admin");
                            TempData["SuccessMessage"] = "Thăng cấp thành Admin thành công.";
                        }
                        else
                        {
                            TempData["ErrorMessage"] = "Người dùng đã là Admin.";
                        }
                        break;
                    case "demoteAdmin":
                        if (await _userManager.IsInRoleAsync(user, "Admin"))
                        {
                            await _userManager.RemoveFromRoleAsync(user, "Admin");
                            TempData["SuccessMessage"] = "Giáng cấp Admin thành công.";
                        }
                        else
                        {
                            TempData["ErrorMessage"] = "Người dùng không phải Admin.";
                        }
                        break;
                    case "approveRequest":
                        if (!await _userManager.IsInRoleAsync(user, "Admin"))
                        {
                            await _userManager.AddToRoleAsync(user, "Admin");
                            user.AdminRequestPending = false;
                            user.AdminRequestReason = null;
                            await _userManager.UpdateAsync(user);
                            TempData["SuccessMessage"] = "Chấp nhận yêu cầu Admin thành công.";
                        }
                        else
                        {
                            TempData["ErrorMessage"] = "Người dùng đã là Admin.";
                        }
                        break;
                    case "rejectRequest":
                        user.AdminRequestPending = false;
                        user.AdminRequestReason = null;
                        await _userManager.UpdateAsync(user);
                        TempData["SuccessMessage"] = "Từ chối yêu cầu Admin thành công.";
                        break;
                    default:
                        TempData["ErrorMessage"] = "Hành động không được hỗ trợ.";
                        break;
                }

                if (user.Id == _userManager.GetUserId(User))
                {
                    await _signInManager.RefreshSignInAsync(user);
                }
            }
            else if (entityType == "organization")
            {
                var organization = await _dbContext.Organizations.FindAsync(id);
                if (organization == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy tổ chức.";
                    return RedirectToPage(new { pageIndex, searchTerm });
                }

                if (action == "delete")
                {
                    _dbContext.OrganizationMembers.RemoveRange(_dbContext.OrganizationMembers.Where(m => m.OrganizationId == id));
                    _dbContext.OrganizationJoinRequests.RemoveRange(_dbContext.OrganizationJoinRequests.Where(r => r.OrganizationId == id));
                    _dbContext.OrganizationComments.RemoveRange(_dbContext.OrganizationComments.Where(c => c.OrganizationId == id));
                    _dbContext.OrganizationReports.RemoveRange(_dbContext.OrganizationReports.Where(r => r.OrganizationId == id));
                    _dbContext.OrganizationRatings.RemoveRange(_dbContext.OrganizationRatings.Where(r => r.OrganizationId == id));
                    _dbContext.Organizations.Remove(organization);
                    await _dbContext.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Xóa tổ chức thành công.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Hành động không được hỗ trợ.";
                }
            }

            return RedirectToPage(new { pageIndex, searchTerm });
        }
    }
}