#nullable enable
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using LoginSystem.Data;
using LoginSystem.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
        private readonly ILogger<DashboardModel> _logger;

        public DashboardModel(
            ApplicationDbContext dbContext,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<DashboardModel> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _signInManager = signInManager ?? throw new ArgumentNullException(nameof(signInManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public IList<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();
        public IList<LoginSystem.Models.Organization> Organizations { get; set; } = new List<LoginSystem.Models.Organization>();
        public IList<LoginSystem.Models.OrganizationReport> OrganizationReports { get; set; } = new List<LoginSystem.Models.OrganizationReport>();
        public IList<LoginSystem.Models.ItemReport> ItemReports { get; set; } = new List<LoginSystem.Models.ItemReport>();
        public IList<ApplicationUser> AdminRequests { get; set; } = new List<ApplicationUser>();

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [BindProperty(SupportsGet = true)]
        public int PageIndex { get; set; } = 1;

        [BindProperty(SupportsGet = true)]
        public int PageSize { get; set; } = 10;

        [BindProperty(SupportsGet = true)]
        public string? SortColumn { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SortDirection { get; set; }

        public int TotalUsers { get; set; }
        public int TotalOrganizations { get; set; }
        public int TotalOrgReports { get; set; }
        public int TotalItemReports { get; set; }
        public int TotalAdminRequests { get; set; }
        public int TotalUserPages { get; set; }
        public int TotalOrgPages { get; set; }
        public int TotalOrgReportPages { get; set; }
        public int TotalItemReportPages { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                PageIndex = Math.Max(1, PageIndex);
                PageSize = Math.Clamp(PageSize, 5, 50);

                // Users
                var usersQuery = _dbContext.Users.AsQueryable();
                if (!string.IsNullOrWhiteSpace(SearchTerm))
                {
                    usersQuery = usersQuery.Where(u =>
                        (u.UserName != null && u.UserName.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase)) ||
                        (u.Email != null && u.Email.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase)) ||
                        (u.DisplayName != null && u.DisplayName.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase)));
                }
                usersQuery = ApplyUserSorting(usersQuery, SortColumn, SortDirection);
                TotalUsers = await usersQuery.CountAsync();
                TotalUserPages = (int)Math.Ceiling(TotalUsers / (double)PageSize);
                Users = await usersQuery
                    .Skip((PageIndex - 1) * PageSize)
                    .Take(PageSize)
                    .ToListAsync();

                // Admin Requests
                var adminRequestsQuery = _dbContext.Users.Where(u => u.AdminRequestPending);
                TotalAdminRequests = await adminRequestsQuery.CountAsync();
                AdminRequests = await adminRequestsQuery
                    .OrderBy(u => u.UserName)
                    .ToListAsync();

                // Organizations
                var orgsQuery = _dbContext.Organizations.AsQueryable();
                if (!string.IsNullOrWhiteSpace(SearchTerm))
                {
                    orgsQuery = orgsQuery.Where(o => o.Name != null && o.Name.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase));
                }
                orgsQuery = ApplyOrganizationSorting(orgsQuery, SortColumn, SortDirection);
                TotalOrganizations = await orgsQuery.CountAsync();
                TotalOrgPages = (int)Math.Ceiling(TotalOrganizations / (double)PageSize);
                Organizations = await orgsQuery
                    .Skip((PageIndex - 1) * PageSize)
                    .Take(PageSize)
                    .ToListAsync();

                // Organization Reports
                var orgReportsQuery = _dbContext.OrganizationReports.AsQueryable();
                if (!string.IsNullOrWhiteSpace(SearchTerm))
                {
                    orgReportsQuery = orgReportsQuery.Where(r => r.Reason.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase));
                }
                orgReportsQuery = ApplyReportSorting(orgReportsQuery, SortColumn, SortDirection);
                TotalOrgReports = await orgReportsQuery.CountAsync();
                TotalOrgReportPages = (int)Math.Ceiling(TotalOrgReports / (double)PageSize);
                OrganizationReports = await orgReportsQuery
                    .Skip((PageIndex - 1) * PageSize)
                    .Take(PageSize)
                    .ToListAsync();

                // Item Reports
                var itemReportsQuery = _dbContext.ItemReports.AsQueryable();
                if (!string.IsNullOrWhiteSpace(SearchTerm))
                {
                    itemReportsQuery = itemReportsQuery.Where(r =>
                        r.ItemId.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                        r.Reason.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase));
                }
                itemReportsQuery = ApplyReportSorting(itemReportsQuery, SortColumn, SortDirection);
                TotalItemReports = await itemReportsQuery.CountAsync();
                TotalItemReportPages = (int)Math.Ceiling(TotalItemReports / (double)PageSize);
                ItemReports = await itemReportsQuery
                    .Skip((PageIndex - 1) * PageSize)
                    .Take(PageSize)
                    .ToListAsync();

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard data");
                TempData["ErrorMessage"] = "Đã xảy ra lỗi khi tải dữ liệu. Vui lòng thử lại.";
                return Page();
            }
        }

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostActionAsync(
            [FromForm] string id,
            [FromForm] string action,
            [FromForm] int pageIndex,
            [FromForm] string? searchTerm,
            [FromForm] int pageSize,
            [FromForm] string? entityType)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Invalid model state for POST action: {Errors}", string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
                    TempData["ErrorMessage"] = "Dữ liệu không hợp lệ. Vui lòng thử lại.";
                    return RedirectToPage(new { pageIndex, searchTerm, pageSize });
                }

                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(action) || string.IsNullOrEmpty(entityType))
                {
                    _logger.LogWarning("Missing required parameters: id={Id}, action={Action}, entityType={EntityType}", id, action, entityType);
                    TempData["ErrorMessage"] = "Hành động không hợp lệ.";
                    return RedirectToPage(new { pageIndex, searchTerm, pageSize });
                }

                _logger.LogInformation("Processing POST action: id={Id}, action={Action}, entityType={EntityType}, pageIndex={PageIndex}, pageSize={PageSize}, searchTerm={SearchTerm}", id, action, entityType, pageIndex, pageSize, searchTerm);

                if (entityType == "user")
                {
                    var user = await _userManager.FindByIdAsync(id);
                    if (user == null)
                    {
                        _logger.LogWarning("User not found: id={Id}", id);
                        TempData["ErrorMessage"] = "Không tìm thấy người dùng.";
                        return RedirectToPage(new { pageIndex, searchTerm, pageSize });
                    }

                    switch (action)
                    {
                        case "delete":
                            if (!User.IsInRole("SuperAdmin"))
                            {
                                _logger.LogWarning("Unauthorized delete attempt by user {UserId}", _userManager.GetUserId(User));
                                TempData["ErrorMessage"] = "Bạn không có quyền xóa người dùng.";
                                return RedirectToPage(new { pageIndex, searchTerm, pageSize });
                            }
                            var deleteResult = await _userManager.DeleteAsync(user);
                            if (!deleteResult.Succeeded)
                            {
                                _logger.LogError("Failed to delete user {UserId}: {Errors}", user.Id, string.Join("; ", deleteResult.Errors.Select(e => e.Description)));
                                TempData["ErrorMessage"] = "Xóa người dùng thất bại.";
                                return RedirectToPage(new { pageIndex, searchTerm, pageSize });
                            }
                            _logger.LogInformation("User deleted: id={UserId}", user.Id);
                            TempData["SuccessMessage"] = "Xóa người dùng thành công.";
                            break;
                        case "promote":
                            if (!await _userManager.IsInRoleAsync(user, "SuperAdmin"))
                            {
                                await _userManager.AddToRoleAsync(user, "SuperAdmin");
                                _logger.LogInformation("User promoted to SuperAdmin: id={UserId}", user.Id);
                                TempData["SuccessMessage"] = "Thăng cấp thành SuperAdmin thành công.";
                            }
                            else
                            {
                                _logger.LogWarning("User already SuperAdmin: id={UserId}", user.Id);
                                TempData["ErrorMessage"] = "Người dùng đã là SuperAdmin.";
                            }
                            break;
                        case "demote":
                            if (await _userManager.IsInRoleAsync(user, "SuperAdmin") && user.Id != _userManager.GetUserId(User))
                            {
                                await _userManager.RemoveFromRoleAsync(user, "SuperAdmin");
                                _logger.LogInformation("User demoted from SuperAdmin: id={UserId}", user.Id);
                                TempData["SuccessMessage"] = "Giáng cấp SuperAdmin thành công.";
                            }
                            else
                            {
                                _logger.LogWarning("Cannot demote SuperAdmin: id={UserId}", user.Id);
                                TempData["ErrorMessage"] = "Không thể giáng cấp SuperAdmin.";
                            }
                            break;
                        case "promoteAdmin":
                            if (!await _userManager.IsInRoleAsync(user, "Admin"))
                            {
                                await _userManager.AddToRoleAsync(user, "Admin");
                                user.AdminRequestPending = false;
                                user.AdminRequestReason = null;
                                await _userManager.UpdateAsync(user);
                                _logger.LogInformation("User promoted to Admin: id={UserId}", user.Id);
                                TempData["SuccessMessage"] = "Thăng cấp thành Admin thành công.";
                            }
                            else
                            {
                                _logger.LogWarning("User already Admin: id={UserId}", user.Id);
                                TempData["ErrorMessage"] = "Người dùng đã là Admin.";
                            }
                            break;
                        case "demoteAdmin":
                            if (await _userManager.IsInRoleAsync(user, "Admin"))
                            {
                                await _userManager.RemoveFromRoleAsync(user, "Admin");
                                _logger.LogInformation("User demoted from Admin: id={UserId}", user.Id);
                                TempData["SuccessMessage"] = "Giáng cấp Admin thành công.";
                            }
                            else
                            {
                                _logger.LogWarning("User not Admin: id={UserId}", user.Id);
                                TempData["ErrorMessage"] = "Người dùng không phải Admin.";
                            }
                            break;
                        case "promoteDelivery":
                            if (!await _userManager.IsInRoleAsync(user, "Delivery"))
                            {
                                await _userManager.AddToRoleAsync(user, "Delivery");
                                _logger.LogInformation("User promoted to Delivery: id={UserId}", user.Id);
                                TempData["SuccessMessage"] = "Thăng cấp thành Delivery thành công.";
                            }
                            else
                            {
                                _logger.LogWarning("User already Delivery: id={UserId}", user.Id);
                                TempData["ErrorMessage"] = "Người dùng đã là Delivery.";
                            }
                            break;
                        case "demoteDelivery":
                            if (await _userManager.IsInRoleAsync(user, "Delivery"))
                            {
                                await _userManager.RemoveFromRoleAsync(user, "Delivery");
                                _logger.LogInformation("User demoted from Delivery: id={UserId}", user.Id);
                                TempData["SuccessMessage"] = "Giáng cấp Delivery thành công.";
                            }
                            else
                            {
                                _logger.LogWarning("User not Delivery: id={UserId}", user.Id);
                                TempData["ErrorMessage"] = "Người dùng không phải Delivery.";
                            }
                            break;
                        case "approveRequest":
                            if (!await _userManager.IsInRoleAsync(user, "Admin"))
                            {
                                await _userManager.AddToRoleAsync(user, "Admin");
                                user.AdminRequestPending = false;
                                user.AdminRequestReason = null;
                                await _userManager.UpdateAsync(user);
                                _logger.LogInformation("Admin request approved: id={UserId}", user.Id);
                                TempData["SuccessMessage"] = "Chấp nhận yêu cầu Admin thành công.";
                            }
                            else
                            {
                                _logger.LogWarning("User already Admin for request: id={UserId}", user.Id);
                                TempData["ErrorMessage"] = "Người dùng đã là Admin.";
                            }
                            break;
                        case "rejectRequest":
                            user.AdminRequestPending = false;
                            user.AdminRequestReason = null;
                            await _userManager.UpdateAsync(user);
                            _logger.LogInformation("Admin request rejected: id={UserId}", user.Id);
                            TempData["SuccessMessage"] = "Từ chối yêu cầu Admin thành công.";
                            break;
                        default:
                            _logger.LogWarning("Unsupported action: {Action}", action);
                            TempData["ErrorMessage"] = "Hành động không được hỗ trợ.";
                            break;
                    }

                    if (user.Id == _userManager.GetUserId(User))
                    {
                        await _signInManager.RefreshSignInAsync(user);
                        _logger.LogInformation("Refreshed sign-in for user: id={UserId}", user.Id);
                    }
                }
                else if (entityType == "organization")
                {
                    var organization = await _dbContext.Organizations.FindAsync(id);
                    if (organization == null)
                    {
                        _logger.LogWarning("Organization not found: id={Id}", id);
                        TempData["ErrorMessage"] = "Không tìm thấy tổ chức.";
                        return RedirectToPage(new { pageIndex, searchTerm, pageSize });
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
                        _logger.LogInformation("Organization deleted: id={Id}", id);
                        TempData["SuccessMessage"] = "Xóa tổ chức thành công.";
                    }
                    else
                    {
                        _logger.LogWarning("Unsupported organization action: {Action}", action);
                        TempData["ErrorMessage"] = "Hành động không được hỗ trợ.";
                    }
                }
                else if (entityType == "itemReport")
                {
                    var itemReport = await _dbContext.ItemReports.FindAsync(id);
                    if (itemReport == null)
                    {
                        _logger.LogWarning("Item report not found: id={Id}", id);
                        TempData["ErrorMessage"] = "Không tìm thấy báo cáo.";
                        return RedirectToPage(new { pageIndex, searchTerm, pageSize });
                    }

                    if (action == "accept")
                    {
                        _dbContext.ItemReports.Remove(itemReport);
                        await _dbContext.SaveChangesAsync();
                        _logger.LogInformation("Item report accepted and deleted: id={Id}", id);
                        TempData["SuccessMessage"] = "Chấp nhận báo cáo và xóa thành công.";
                    }
                    else
                    {
                        _logger.LogWarning("Unsupported item report action: {Action}", action);
                        TempData["ErrorMessage"] = "Hành động không được hỗ trợ.";
                    }
                }
                else
                {
                    _logger.LogWarning("Invalid entity type: {EntityType}", entityType);
                    TempData["ErrorMessage"] = "Loại thực thể không hợp lệ.";
                }

                return RedirectToPage(new { pageIndex, searchTerm, pageSize });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing POST action: id={Id}, action={Action}, entityType={EntityType}", id, action, entityType);
                TempData["ErrorMessage"] = "Đã xảy ra lỗi khi xử lý hành động. Vui lòng thử lại.";
                return RedirectToPage(new { pageIndex, searchTerm, pageSize });
            }
        }

        private IQueryable<ApplicationUser> ApplyUserSorting(IQueryable<ApplicationUser> query, string? sortColumn, string? sortDirection)
        {
            if (string.IsNullOrEmpty(sortColumn) || string.IsNullOrEmpty(sortDirection))
                return query;

            bool isAscending = sortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase);

            return sortColumn.ToLower() switch
            {
                "username" => isAscending ? query.OrderBy(u => u.UserName) : query.OrderByDescending(u => u.UserName),
                "email" => isAscending ? query.OrderBy(u => u.Email) : query.OrderByDescending(u => u.Email),
                "displayname" => isAscending ? query.OrderBy(u => u.DisplayName) : query.OrderByDescending(u => u.DisplayName),
                _ => query
            };
        }

        private IQueryable<LoginSystem.Models.Organization> ApplyOrganizationSorting(IQueryable<LoginSystem.Models.Organization> query, string? sortColumn, string? sortDirection)
        {
            if (string.IsNullOrEmpty(sortColumn) || string.IsNullOrEmpty(sortDirection))
                return query;

            bool isAscending = sortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase);

            return sortColumn.ToLower() == "name"
                ? isAscending ? query.OrderBy(o => o.Name) : query.OrderByDescending(o => o.Name)
                : query;
        }

        private IQueryable<LoginSystem.Models.OrganizationReport> ApplyReportSorting(IQueryable<LoginSystem.Models.OrganizationReport> query, string? sortColumn, string? sortDirection)
        {
            if (string.IsNullOrEmpty(sortColumn) || string.IsNullOrEmpty(sortDirection))
                return query;

            bool isAscending = sortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase);

            return sortColumn.ToLower() == "reportedat"
                ? isAscending ? query.OrderBy(r => r.ReportedAt) : query.OrderByDescending(r => r.ReportedAt)
                : query;
        }

        private IQueryable<LoginSystem.Models.ItemReport> ApplyReportSorting(IQueryable<LoginSystem.Models.ItemReport> query, string? sortColumn, string? sortDirection)
        {
            if (string.IsNullOrEmpty(sortColumn) || string.IsNullOrEmpty(sortDirection))
                return query;

            bool isAscending = sortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase);

            return sortColumn.ToLower() == "reportedat"
                ? isAscending ? query.OrderBy(r => r.ReportedAt) : query.OrderByDescending(r => r.ReportedAt)
                : query;
        }
    }
}