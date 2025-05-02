#nullable enable

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using LoginSystem.Models;
using System.ComponentModel.DataAnnotations;

namespace LoginSystem.Pages;

[Authorize]
public class EditProfileModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWebHostEnvironment _environment;

    public EditProfileModel(UserManager<ApplicationUser> userManager, IWebHostEnvironment environment)
    {
        _userManager = userManager;
        _environment = environment;
    }

    [BindProperty]
    public InputModel? Input { get; set; }

    public ApplicationUser? CurrentUser { get; set; }

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

        [StringLength(300)]
        public string? RequestAdminReason { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();
        CurrentUser = user;

        Input = new InputModel
        {
            Username = user.UserName ?? string.Empty,
            DisplayName = user.DisplayName ?? string.Empty,
            Email = user.Email ?? string.Empty
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid || Input == null)
            return Page();

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();
        CurrentUser = user;

        // Check trùng username
        var existingUserByUsername = await _userManager.FindByNameAsync(Input.Username);
        if (existingUserByUsername != null && existingUserByUsername.Id != user.Id)
        {
            ModelState.AddModelError("Input.Username", "Tên người dùng đã tồn tại.");
            return Page();
        }

        // Check trùng email
        var existingUserByEmail = await _userManager.FindByEmailAsync(Input.Email);
        if (existingUserByEmail != null && existingUserByEmail.Id != user.Id)
        {
            ModelState.AddModelError("Input.Email", "Email đã được sử dụng.");
            return Page();
        }

        user.UserName = Input.Username;
        user.Email = Input.Email;
        user.DisplayName = Input.DisplayName;
        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            foreach (var error in updateResult.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return Page();
        }

        if (Input.Avatar != null)
        {
            var extension = Path.GetExtension(Input.Avatar.FileName).ToLowerInvariant();
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            if (!allowedExtensions.Contains(extension))
            {
                ModelState.AddModelError("Input.Avatar", "Chỉ chấp nhận các định dạng ảnh: .jpg, .png, .jpeg, .webp.");
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
                return Page();
            }
        }

        return RedirectToPage("/Index");
    }

    public async Task<IActionResult> OnPostRequestAdminAsync()
    {
        if (Input?.RequestAdminReason == null || string.IsNullOrWhiteSpace(Input.RequestAdminReason))
        {
            ModelState.AddModelError("Input.RequestAdminReason", "Vui lòng nhập lý do.");
            return await OnGetAsync();
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        user.AdminRequestPending = true;
        user.AdminRequestReason = Input.RequestAdminReason;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
        }

        return RedirectToPage();
    }
}
