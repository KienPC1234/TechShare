#nullable enable

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using LoginSystem.Models;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication; // Added for SignInAsync/SignOutAsync

namespace LoginSystem.Pages
{
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class EditUserModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly SignInManager<ApplicationUser> _signInManager; // Added for session management
        private readonly IWebHostEnvironment _environment;

        public EditUserModel(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            SignInManager<ApplicationUser> signInManager,
            IWebHostEnvironment environment)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _signInManager = signInManager;
            _environment = environment;
        }

        [BindProperty]
        public InputModel? Input { get; set; }

        public ApplicationUser? CurrentUser { get; set; }

        public string[]? AvailableRoles { get; set; }

        public class InputModel
        {
            [Required]
            [StringLength(100, MinimumLength = 3)]
            public string Username { get; set; } = string.Empty;

            [Required]
            [StringLength(100, MinimumLength = 3)]
            public string DisplayName { get; set; } = string.Empty;

            [Required]
            [EmailAddress]
            public string Email { get; set; } = string.Empty;

            [DataType(DataType.Upload)]
            public IFormFile? Avatar { get; set; }

            public string[]? SelectedRoles { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();
            CurrentUser = user;

            AvailableRoles = _roleManager.Roles.Select(r => r.Name!).ToArray();
            var userRoles = await _userManager.GetRolesAsync(user);

            Input = new InputModel
            {
                Username = user.UserName ?? string.Empty,
                DisplayName = user.DisplayName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                SelectedRoles = userRoles.ToArray()
            };

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(string id)
        {
            if (!ModelState.IsValid || Input == null)
                return Page();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();
            CurrentUser = user;

            // Check trùng username
            var existingUserByUsername = await _userManager.FindByNameAsync(Input.Username);
            if (existingUserByUsername != null && existingUserByUsername.Id != user.Id)
            {
                ModelState.AddModelError("Input.Username", "Tên người dùng đã tồn tại.");
                AvailableRoles = _roleManager.Roles.Select(r => r.Name!).ToArray();
                return Page();
            }

            // Check trùng email
            var existingUserByEmail = await _userManager.FindByEmailAsync(Input.Email);
            if (existingUserByEmail != null && existingUserByEmail.Id != user.Id)
            {
                ModelState.AddModelError("Input.Email", "Email đã được sử dụng.");
                AvailableRoles = _roleManager.Roles.Select(r => r.Name!).ToArray();
                return Page();
            }

            // Cập nhật thông tin cơ bản
            user.UserName = Input.Username;
            user.Email = Input.Email;
            user.DisplayName = Input.DisplayName;
            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                foreach (var error in updateResult.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                AvailableRoles = _roleManager.Roles.Select(r => r.Name!).ToArray();
                return Page();
            }

            // Cập nhật avatar
            if (Input.Avatar != null)
            {
                var extension = Path.GetExtension(Input.Avatar.FileName).ToLowerInvariant();
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                if (!allowedExtensions.Contains(extension))
                {
                    ModelState.AddModelError("Input.Avatar", "Chỉ chấp nhận các định dạng ảnh: .jpg, .png, .jpeg, .webp.");
                    AvailableRoles = _roleManager.Roles.Select(r => r.Name!).ToArray();
                    return Page();
                }

                var uploadsFolder = Path.Combine(_environment.WebRootPath, "Uploads");
                Directory.CreateDirectory(uploadsFolder);
                var fileName = Guid.NewGuid() + extension;
                var filePath = Path.Combine(uploadsFolder, fileName);

                using var stream = new FileStream(filePath, FileMode.Create);
                await Input.Avatar.CopyToAsync(stream);

                user.AvatarUrl = "/Uploads/" + fileName;
                var avatarResult = await _userManager.UpdateAsync(user);
                if (!avatarResult.Succeeded)
                {
                    foreach (var error in avatarResult.Errors)
                        ModelState.AddModelError(string.Empty, error.Description);
                    AvailableRoles = _roleManager.Roles.Select(r => r.Name!).ToArray();
                    return Page();
                }
            }

            // Cập nhật roles
            if (User.IsInRole("SuperAdmin")) // Chỉ SuperAdmin được thay đổi roles
            {
                var currentRoles = await _userManager.GetRolesAsync(user);
                await _userManager.RemoveFromRolesAsync(user, currentRoles);
                if (Input.SelectedRoles != null && Input.SelectedRoles.Any())
                {
                    var roleResult = await _userManager.AddToRolesAsync(user, Input.SelectedRoles);
                    if (!roleResult.Succeeded)
                    {
                        foreach (var error in roleResult.Errors)
                            ModelState.AddModelError(string.Empty, error.Description);
                        AvailableRoles = _roleManager.Roles.Select(r => r.Name!).ToArray();
                        return Page();
                    }
                }

                // Cập nhật phiên nếu người dùng đang chỉnh sửa là chính họ
                if (user.Id == _userManager.GetUserId(User))
                {
                    await _signInManager.SignOutAsync();
                    await _signInManager.SignInAsync(user, isPersistent: true); // Persistent to match cookie settings
                }
            }

            TempData["SuccessMessage"] = "Cập nhật thông tin người dùng thành công.";
            return RedirectToPage("/Dashboard", new { pageIndex = 1 });
        }
    }
}