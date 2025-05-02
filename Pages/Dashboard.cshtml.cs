#nullable enable

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using LoginSystem.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LoginSystem.Pages
{
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class DashboardModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public DashboardModel(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        public UserManager<ApplicationUser> UserManager => _userManager;

        public IList<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [BindProperty(SupportsGet = true)]
        public int PageIndex { get; set; } = 1;

        public int TotalPages { get; set; }

        public IList<ApplicationUser> AdminRequests { get; set; } = new List<ApplicationUser>();

        public async Task OnGetAsync()
        {
            var all = _userManager.Users;

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                all = all.Where(u =>
                    u.UserName!.Contains(SearchTerm) ||
                    u.Email!.Contains(SearchTerm) ||
                    u.DisplayName!.Contains(SearchTerm));
            }

            int pageSize = 10;
            var count = await all.CountAsync();
            TotalPages = (int)Math.Ceiling(count / (double)pageSize);

            Users = await all
                .OrderBy(u => u.UserName)
                .Skip((PageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            if (User.IsInRole("SuperAdmin"))
            {
                AdminRequests = await _userManager.Users
                    .Where(u => u.AdminRequestPending)
                    .ToListAsync();
            }
        }

        public async Task<IActionResult> OnPostActionAsync(string id, string action, int pageIndex, string? searchTerm)
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
                case "edit":
                    return RedirectToPage("/EditUser", new { id });
                case "promote":
                    if (!await _userManager.IsInRoleAsync(user, "Admin"))
                        await _userManager.AddToRoleAsync(user, "Admin");
                    user.AdminRequestPending = false;
                    user.AdminRequestReason = null;
                    await _userManager.UpdateAsync(user);
                    TempData["SuccessMessage"] = "Thăng cấp người dùng thành công.";
                    break;
                case "demote":
                    if (await _userManager.IsInRoleAsync(user, "Admin"))
                        await _userManager.RemoveFromRoleAsync(user, "Admin");
                    TempData["SuccessMessage"] = "Giáng cấp người dùng thành công.";
                    break;
                case "approveRequest":
                    if (!User.IsInRole("SuperAdmin"))
                    {
                        TempData["ErrorMessage"] = "Bạn không có quyền chấp nhận yêu cầu admin.";
                        return RedirectToPage(new { pageIndex, searchTerm });
                    }
                    if (!await _userManager.IsInRoleAsync(user, "Admin"))
                        await _userManager.AddToRoleAsync(user, "Admin");
                    user.AdminRequestPending = false;
                    user.AdminRequestReason = null;
                    await _userManager.UpdateAsync(user);
                    TempData["SuccessMessage"] = "Chấp nhận yêu cầu admin thành công.";
                    break;
                case "rejectRequest":
                    if (!User.IsInRole("SuperAdmin"))
                    {
                        TempData["ErrorMessage"] = "Bạn không có quyền từ chối yêu cầu admin.";
                        return RedirectToPage(new { pageIndex, searchTerm });
                    }
                    user.AdminRequestPending = false;
                    user.AdminRequestReason = null;
                    await _userManager.UpdateAsync(user);
                    TempData["SuccessMessage"] = "Từ chối yêu cầu admin thành công.";
                    break;
            }

            // Cập nhật phiên nếu người dùng đang chỉnh sửa chính họ
            if (user.Id == _userManager.GetUserId(User))
            {
                await _signInManager.RefreshSignInAsync(user);
            }

            return RedirectToPage(new { pageIndex, searchTerm });
        }
    }
}