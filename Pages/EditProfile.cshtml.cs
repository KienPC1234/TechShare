#nullable enable
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using LoginSystem.Models;
using System.ComponentModel.DataAnnotations;
using System.IO;
using Microsoft.Extensions.Logging;
using MimeDetective;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using Microsoft.AspNetCore.Authentication;

namespace LoginSystem.Pages;

[Authorize]
[ValidateAntiForgeryToken]
public class EditProfileModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<EditProfileModel> _logger;
    private readonly IContentInspector _mimeInspector;
    private const string DefaultAvatar = "/images/default-avatar.png";
    private const long MaxFileSize = 2 * 1024 * 1024; // 2MB
    private const int MaxImageDimension = 2048; // Max width/height in pixels
    private static readonly string[] AllowedMimeTypes = { "image/jpeg", "image/png" };

    public EditProfileModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IWebHostEnvironment environment,
        ILogger<EditProfileModel> logger)
    {
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _signInManager = signInManager ?? throw new ArgumentNullException(nameof(signInManager));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Initialize ContentInspector with default definitions
        _mimeInspector = new ContentInspectorBuilder
        {
            Definitions = MimeDetective.Definitions.DefaultDefinitions.All(),
            Parallel = false // Single-threaded for simplicity
        }.Build();
    }

    [BindProperty]
    public InputModel? Input { get; set; }

    public ApplicationUser? CurrentUser { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Username is required")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 100 characters")]
        [RegularExpression(@"^[a-zA-Z0-9_-]+$", ErrorMessage = "Username can only contain letters, numbers, underscores, and hyphens")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Display name is required")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "Display name must be between 3 and 100 characters")]
        public string DisplayName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        public string Email { get; set; } = string.Empty;

        [DataType(DataType.Upload)]
        public IFormFile? Avatar { get; set; }

        [StringLength(300, ErrorMessage = "Reason must not exceed 300 characters")]
        public string? RequestAdminReason { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            _logger.LogWarning("User not found for profile edit");
            return NotFound();
        }
        CurrentUser = user;

        Input = new InputModel
        {
            Username = user.UserName ?? string.Empty,
            DisplayName = user.DisplayName ?? string.Empty,
            Email = user.Email ?? string.Empty
        };

        return Page();
    }

    [ValidateAntiForgeryToken]
    public async Task<IActionResult> OnPostAsync()
    {
        try
        {
            if (!ModelState.IsValid || Input == null)
            {
                _logger.LogWarning("Invalid model state for profile update");
                CurrentUser = await _userManager.GetUserAsync(User);
                return Page();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                _logger.LogWarning("User not found for profile update");
                return NotFound();
            }
            CurrentUser = user;

            // Validate username uniqueness
            var existingUserByUsername = await _userManager.FindByNameAsync(Input.Username);
            if (existingUserByUsername != null && existingUserByUsername.Id != user.Id)
            {
                ModelState.AddModelError("Input.Username", "Username already exists.");
                return Page();
            }

            // Validate email uniqueness
            var existingUserByEmail = await _userManager.FindByEmailAsync(Input.Email);
            if (existingUserByEmail != null && existingUserByEmail.Id != user.Id)
            {
                ModelState.AddModelError("Input.Email", "Email already in use.");
                return Page();
            }

            // Check if any changes were made
            bool hasChanges = user.UserName != Input.Username ||
                             user.Email != Input.Email ||
                             user.DisplayName != Input.DisplayName ||
                             Input.Avatar != null;

            // Update basic profile info
            user.UserName = Input.Username;
            user.Email = Input.Email;
            user.DisplayName = Input.DisplayName;

            // Handle avatar upload
            if (Input.Avatar != null)
            {
                if (!await ProcessAvatarUpload(user))
                {
                    return Page();
                }
            }

            // Update user only if there are changes
            if (hasChanges)
            {
                var updateResult = await _userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    foreach (var error in updateResult.Errors)
                        ModelState.AddModelError(string.Empty, error.Description);
                    return Page();
                }

                // Refresh claims
                await _signInManager.RefreshSignInAsync(user);
                _logger.LogInformation($"User {user.Id} updated profile and claims refreshed");
            }
            else
            {
                _logger.LogInformation($"User {user.Id} submitted profile with no changes");
            }

            TempData["SuccessMessage"] = "Profile updated successfully.";
            return RedirectToPage("/Index");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user profile");
            ModelState.AddModelError(string.Empty, "An error occurred while updating your profile.");
            CurrentUser = await _userManager.GetUserAsync(User);
            return Page();
        }
    }

    
    public async Task<IActionResult> OnPostRequestAdminAsync()
    {
        try
        {
            if (Input?.RequestAdminReason == null || string.IsNullOrWhiteSpace(Input.RequestAdminReason))
            {
                ModelState.AddModelError("Input.RequestAdminReason", "Please provide a reason.");
                CurrentUser = await _userManager.GetUserAsync(User);
                return await OnGetAsync();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                _logger.LogWarning("User not found for admin request");
                return NotFound();
            }

            user.AdminRequestPending = true;
            user.AdminRequestReason = Input.RequestAdminReason;
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                CurrentUser = user;
                return Page();
            }

            // Refresh claims to reflect admin request status
            await _signInManager.RefreshSignInAsync(user);
            _logger.LogInformation($"User {user.Id} submitted admin request and claims refreshed");
            TempData["SuccessMessage"] = "Admin request submitted successfully.";
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing admin request");
            ModelState.AddModelError(string.Empty, "An error occurred while processing your admin request.");
            CurrentUser = await _userManager.GetUserAsync(User);
            return Page();
        }
    }

    private async Task<bool> ProcessAvatarUpload(ApplicationUser user)
    {
        try
        {
            if (Input?.Avatar == null) return false;

            // Validate file size
            if (Input.Avatar.Length > MaxFileSize)
            {
                ModelState.AddModelError("Input.Avatar", $"File size must not exceed {MaxFileSize / (1024 * 1024)}MB.");
                _logger.LogWarning($"User {user.Id} attempted to upload oversized file: {Input.Avatar.Length} bytes");
                return false;
            }

            // Validate MIME type using MimeDetective
            using var memoryStream = new MemoryStream();
            await Input.Avatar.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            var mimeResult = _mimeInspector.Inspect(memoryStream.ToArray());
            var detectedFileType = mimeResult.FirstOrDefault();
            var detectedMimeType = detectedFileType?.Definition.File.MimeType;

            if (string.IsNullOrEmpty(detectedMimeType) || !AllowedMimeTypes.Contains(detectedMimeType))
            {
                ModelState.AddModelError("Input.Avatar", "Invalid file type. Only JPG and PNG images are allowed.");
                _logger.LogWarning($"User {user.Id} attempted to upload invalid file type: {detectedMimeType ?? "unknown"}");
                return false;
            }

            // Validate image dimensions
            try
            {
                memoryStream.Position = 0;
                using var image = Image.FromStream(memoryStream);
                if (image.Width > MaxImageDimension || image.Height > MaxImageDimension)
                {
                    ModelState.AddModelError("Input.Avatar", $"Image dimensions must not exceed {MaxImageDimension}x{MaxImageDimension} pixels.");
                    _logger.LogWarning($"User {user.Id} uploaded image with invalid dimensions: {image.Width}x{image.Height}");
                    return false;
                }

                // Verify image format
                if (!IsValidImageFormat(image.RawFormat))
                {
                    ModelState.AddModelError("Input.Avatar", "Invalid image format detected.");
                    _logger.LogWarning($"User {user.Id} uploaded image with invalid format");
                    return false;
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("Input.Avatar", "Invalid or corrupted image file.");
                _logger.LogWarning(ex, $"User {user.Id} uploaded potentially corrupted image");
                return false;
            }

            // Generate secure file path
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "Uploads");
            Directory.CreateDirectory(uploadsFolder);
            var extension = detectedMimeType switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                _ => ".jpg" // Fallback (shouldn't happen due to prior validation)
            };
            var fileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            // Save new avatar
            memoryStream.Position = 0;
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await memoryStream.CopyToAsync(stream);
            }

            // Delete old avatar if not default
            if (!string.IsNullOrEmpty(user.AvatarUrl) && user.AvatarUrl != DefaultAvatar)
            {
                var oldAvatarPath = Path.Combine(_environment.WebRootPath, user.AvatarUrl.TrimStart('/'));
                if (System.IO.File.Exists(oldAvatarPath))
                {
                    try
                    {
                        System.IO.File.Delete(oldAvatarPath);
                        _logger.LogInformation($"Deleted old avatar: {oldAvatarPath}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Failed to delete old avatar: {oldAvatarPath}");
                    }
                }
            }

            // Update avatar URL
            user.AvatarUrl = $"/Uploads/{fileName}";
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error processing avatar upload for user {user.Id}");
            ModelState.AddModelError("Input.Avatar", "Error uploading avatar.");
            return false;
        }
    }

    private bool IsValidImageFormat(ImageFormat format)
    {
        return format.Equals(ImageFormat.Jpeg) || format.Equals(ImageFormat.Png);
    }
}