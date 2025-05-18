using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using LoginSystem.Models;
using System.ComponentModel.DataAnnotations;
using System.IO;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading.Tasks;
using TechShare.Classes;
using Microsoft.Extensions.Caching.Memory;

namespace LoginSystem.Controllers
{
    [Authorize]
    [Route("api/profile")]
    [ApiController]
    public class ProfileApiController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<ProfileApiController> _logger;
        private readonly MailToolBox _mailToolBox;
        private readonly IMemoryCache _cache;
        private const string DefaultAvatar = "/images/default-avatar.png";
        private const long MaxFileSize = 2 * 1024 * 1024; // 2MB
        private const int MaxImageDimension = 2048;
        private const int TWO_FACTOR_TOKEN_VALIDITY_MINUTES = 20;
        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png" };

        public ProfileApiController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IWebHostEnvironment environment,
            ILogger<ProfileApiController> logger,
            MailToolBox mailToolBox,
            IMemoryCache cache)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _environment = environment;
            _logger = logger;
            _mailToolBox = mailToolBox;
            _cache = cache;
        }

        public class UpdateProfileInputModel
        {
            [Required(ErrorMessage = "Tên đăng nhập là bắt buộc")]
            [StringLength(100, MinimumLength = 3, ErrorMessage = "Tên đăng nhập phải từ 3 đến 100 ký tự")]
            [RegularExpression(@"^[a-zA-Z0-9_-]+$", ErrorMessage = "Tên đăng nhập chỉ được chứa chữ, số, dấu gạch dưới và dấu gạch ngang")]
            public string Username { get; set; }

            [Required(ErrorMessage = "Tên hiển thị là bắt buộc")]
            [StringLength(100, MinimumLength = 3, ErrorMessage = "Tên hiển thị phải từ 3 đến 100 ký tự")]
            public string DisplayName { get; set; }

            public IFormFile Avatar { get; set; } // Không yêu cầu bắt buộc

            [StringLength(20, ErrorMessage = "AvatarAction không hợp lệ")]
            public string AvatarAction { get; set; } // "NoProfileUpdate" hoặc "UpdateRequest"

            public bool Enable2FA { get; set; }

            [StringLength(10)]
            public string TwoFactorMethod { get; set; }
        }

        public class ChangeEmailInputModel
        {
            [Required(ErrorMessage = "Email mới là bắt buộc")]
            [EmailAddress(ErrorMessage = "Định dạng email không hợp lệ")]
            public string NewEmail { get; set; }
        }

        public class RequestAdminInputModel
        {
            [Required(ErrorMessage = "Lý do là bắt buộc")]
            [StringLength(300, ErrorMessage = "Lý do không được vượt quá 300 ký tự")]
            public string RequestAdminReason { get; set; }
        }

        public class ChangePasswordInputModel
        {
            [Required(ErrorMessage = "Mật khẩu hiện tại là bắt buộc")]
            [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu hiện tại phải từ 6 đến 100 ký tự")]
            public string CurrentPassword { get; set; }

            [Required(ErrorMessage = "Mật khẩu mới là bắt buộc")]
            [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu mới phải từ 6 đến 100 ký tự")]
            [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$", ErrorMessage = "Mật khẩu mới phải chứa ít nhất một chữ cái in hoa, một chữ cái thường và một số")]
            public string NewPassword { get; set; }

            [Required(ErrorMessage = "Xác nhận mật khẩu là bắt buộc")]
            [Compare("NewPassword", ErrorMessage = "Mật khẩu xác nhận không khớp")]
            public string ConfirmPassword { get; set; }
        }


        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromForm] ChangePasswordInputModel input)
        {
            try
            {
                _logger.LogInformation("Processing ChangePassword request");

                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    _logger.LogWarning("Không tìm thấy người dùng để thay đổi mật khẩu");
                    return NotFound(new { success = false, message = "Không tìm thấy người dùng." });
                }

                // Validate input
                var validationContext = new ValidationContext(input);
                var validationResults = new List<ValidationResult>();
                if (!Validator.TryValidateObject(input, validationContext, validationResults, true))
                {
                    var errors = validationResults.ToDictionary(v => char.ToLower(v.MemberNames.First()[0]) + v.MemberNames.First().Substring(1), v => v.ErrorMessage);
                    _logger.LogWarning("Validation errors: {Errors}", errors);
                    return BadRequest(new { success = false, errors });
                }

                // Verify current password
                var passwordCheck = await _userManager.CheckPasswordAsync(user, input.CurrentPassword);
                if (!passwordCheck)
                {
                    _logger.LogWarning("Current password is incorrect for user: {Username}", user.UserName);
                    return BadRequest(new { success = false, errors = new { currentPassword = "Mật khẩu hiện tại không đúng." } });
                }

                // Change password
                var result = await _userManager.ChangePasswordAsync(user, input.CurrentPassword, input.NewPassword);
                if (!result.Succeeded)
                {
                    var errors = result.Errors.ToDictionary(e => e.Code, e => e.Description);
                    _logger.LogWarning("Password change failed: {Errors}", errors);
                    return BadRequest(new { success = false, errors });
                }

                await _signInManager.RefreshSignInAsync(user);
                _logger.LogInformation("Password changed successfully for user: {Username}", user.UserName);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password");
                return StatusCode(500, new { success = false, message = "Lỗi server khi thay đổi mật khẩu: " + ex.Message });
            }
        }

        [HttpPost("update")]
        public async Task<IActionResult> UpdateProfile([FromForm] UpdateProfileInputModel input)
        {
            try
            {
                _logger.LogInformation("Processing UpdateProfile request: Username={Username}, Enable2FA={Enable2FA}, TwoFactorMethod={TwoFactorMethod}, HasAvatar={HasAvatar}, AvatarAction={AvatarAction}",
                    input.Username, input.Enable2FA, input.TwoFactorMethod, input.Avatar != null, input.AvatarAction);

                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    _logger.LogWarning("Không tìm thấy người dùng để cập nhật hồ sơ");
                    return NotFound(new { success = false, message = "Không tìm thấy người dùng." });
                }

                // Validate input
                var validationContext = new ValidationContext(input);
                var validationResults = new List<ValidationResult>();
                if (!Validator.TryValidateObject(input, validationContext, validationResults, true))
                {
                    var errors = validationResults.ToDictionary(v => char.ToLower(v.MemberNames.First()[0]) + v.MemberNames.First().Substring(1), v => v.ErrorMessage);
                    _logger.LogWarning("Validation errors: {Errors}", errors);
                    return BadRequest(new { success = false, errors });
                }

                // Validate AvatarAction
                if (!string.IsNullOrEmpty(input.AvatarAction) && input.AvatarAction != "NoProfileUpdate" && input.AvatarAction != "UpdateRequest")
                {
                    _logger.LogWarning("Invalid AvatarAction: {AvatarAction}", input.AvatarAction);
                    return BadRequest(new { success = false, errors = new { avatarAction = "AvatarAction phải là 'NoProfileUpdate' hoặc 'UpdateRequest'." } });
                }

                // Check username uniqueness
                var existingUserByUsername = await _userManager.FindByNameAsync(input.Username);
                if (existingUserByUsername != null && existingUserByUsername.Id != user.Id)
                {
                    _logger.LogWarning("Username {Username} already exists", input.Username);
                    return BadRequest(new { success = false, errors = new { username = "Tên đăng nhập đã tồn tại." } });
                }

                // Validate 2FA
                if (input.Enable2FA)
                {
                    if (string.IsNullOrEmpty(input.TwoFactorMethod) || (input.TwoFactorMethod != "TOTP" && input.TwoFactorMethod != "Email"))
                    {
                        _logger.LogWarning("Invalid TwoFactorMethod: {TwoFactorMethod}", input.TwoFactorMethod);
                        return BadRequest(new { success = false, errors = new { twoFactorMethod = "Vui lòng chọn phương thức 2FA." } });
                    }
                }
                else
                {
                    input.TwoFactorMethod = "None";
                }

                // Check for changes
                bool hasChanges = user.UserName != input.Username ||
                                 user.DisplayName != input.DisplayName ||
                                 input.AvatarAction == "UpdateRequest" ||
                                 await _userManager.GetTwoFactorEnabledAsync(user) != input.Enable2FA ||
                                 user.TwoFactorMethod != input.TwoFactorMethod;

                // Update user
                user.UserName = input.Username;
                user.DisplayName = input.DisplayName;

                if (input.Enable2FA)
                {
                    user.TwoFactorMethod = input.TwoFactorMethod;
                    await _userManager.SetTwoFactorEnabledAsync(user, true);
                }
                else
                {
                    user.TwoFactorMethod = "None";
                    user.TwoFactorSecretKey = null;
                    await _userManager.SetTwoFactorEnabledAsync(user, false);
                }

                // Process avatar
                if (input.AvatarAction == "UpdateRequest" && input.Avatar != null && input.Avatar.Length > 0)
                {
                    _logger.LogInformation("Processing avatar upload for user: {Username}", user.UserName);
                    var avatarResult = await ProcessAvatarUpload(user, input.Avatar);
                    if (!avatarResult.Success)
                    {
                        _logger.LogWarning("Avatar processing failed: {Message}", avatarResult.Message);
                        return BadRequest(new { success = false, errors = new { avatar = avatarResult.Message } });
                    }
                    hasChanges = true;
                }
                else if (input.AvatarAction == "NoProfileUpdate")
                {
                    _logger.LogInformation("No avatar update requested, keeping current AvatarUrl: {AvatarUrl}", user.AvatarUrl ?? DefaultAvatar);
                    user.AvatarUrl = user.AvatarUrl ?? DefaultAvatar; // Giữ avatar hiện tại hoặc dùng mặc định
                }
                else
                {
                    _logger.LogWarning("Invalid or missing AvatarAction, keeping current AvatarUrl: {AvatarUrl}", user.AvatarUrl ?? DefaultAvatar);
                    user.AvatarUrl = user.AvatarUrl ?? DefaultAvatar;
                    return BadRequest(new { success = false, errors = new { avatarAction = "AvatarAction không hợp lệ hoặc thiếu." } });
                }

                // Save changes
                if (hasChanges)
                {
                    var updateResult = await _userManager.UpdateAsync(user);
                    if (!updateResult.Succeeded)
                    {
                        var errors = updateResult.Errors.ToDictionary(e => e.Code, e => e.Description);
                        _logger.LogWarning("User update failed: {Errors}", errors);
                        return BadRequest(new { success = false, errors });
                    }

                    await _signInManager.RefreshSignInAsync(user);
                }

                _logger.LogInformation("Profile updated successfully for user: {Username}", user.UserName);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile");
                return StatusCode(500, new { success = false, message = "Lỗi server khi cập nhật hồ sơ: " + ex.Message });
            }
        }

        [HttpPost("change-email")]
        public async Task<IActionResult> ChangeEmail([FromForm] ChangeEmailInputModel input)
        {
            try
            {
                _logger.LogInformation("Processing ChangeEmail request: {NewEmail}", input.NewEmail);

                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    _logger.LogWarning("Không tìm thấy người dùng để thay đổi email");
                    return NotFound(new { success = false, message = "Không tìm thấy người dùng." });
                }

                var validationContext = new ValidationContext(input);
                var validationResults = new List<ValidationResult>();
                if (!Validator.TryValidateObject(input, validationContext, validationResults, true))
                {
                    var errors = validationResults.ToDictionary(v => char.ToLower(v.MemberNames.First()[0]) + v.MemberNames.First().Substring(1), v => v.ErrorMessage);
                    _logger.LogWarning("Validation errors: {Errors}", errors);
                    return BadRequest(new { success = false, errors });
                }

                var existingUserByEmail = await _userManager.FindByEmailAsync(input.NewEmail);
                if (existingUserByEmail != null && existingUserByEmail.Id != user.Id)
                {
                    _logger.LogWarning("Email {NewEmail} already exists", input.NewEmail);
                    return BadRequest(new { success = false, errors = new { newEmail = "Email đã được sử dụng." } });
                }

                var sessionId = Guid.NewGuid().ToString();
                var verificationCode = new Random().Next(100000, 999999).ToString();
                var cacheKey = $"EmailVerification_{user.Id}_{sessionId}";
                _cache.Set(cacheKey, (verificationCode, input.NewEmail, DateTime.UtcNow.AddMinutes(TWO_FACTOR_TOKEN_VALIDITY_MINUTES)), TimeSpan.FromMinutes(TWO_FACTOR_TOKEN_VALIDITY_MINUTES));

                var emailResult = _mailToolBox.SendEmail(
                    input.NewEmail,
                    "Xác thực Email mới",
                    $@"<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; background-color: #f4f4f4; margin: 0; padding: 0; }}
        .container {{ max-width: 600px; margin: 20px auto; background: #ffffff; padding: 20px; border-radius: 8px; box-shadow: 0 0 10px rgba(0,0,0,0.1); }}
        .header {{ text-align: center; padding: 20px 0; background: #007bff; color: white; border-radius: 8px 8px 0 0; }}
        .content {{ padding: 20px; text-align: center; }}
        .code {{ font-size: 24px; font-weight: bold; color: #007bff; margin: 20px 0; padding: 10px; background: #f8f9fa; border-radius: 4px; }}
        .footer {{ text-align: center; padding: 20px; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>TechShare</h1>
        </div>
        <div class='content'>
            <h2>Xác thực Email mới</h2>
            <p>Vui lòng sử dụng mã sau để xác thực địa chỉ email mới của bạn:</p>
            <div class='code'>{verificationCode}</div>
            <p>Mã này có hiệu lực trong {TWO_FACTOR_TOKEN_VALIDITY_MINUTES} phút.</p>
        </div>
        <div class='footer'>
            <p>Nếu bạn không yêu cầu, vui lòng bỏ qua email này.</p>
            <p>© 2025 TechShare. Mọi quyền được bảo lưu.</p>
        </div>
    </div>
</body>
</html>");

                if (emailResult != EmailStatus.Success)
                {
                    _logger.LogWarning("Failed to send verification email to {NewEmail}: {Status}", input.NewEmail, emailResult);
                    return BadRequest(new { success = false, message = "Không thể gửi mã xác thực." });
                }

                _logger.LogInformation("Verification email sent to {NewEmail}, sessionId: {SessionId}", input.NewEmail, sessionId);
                return Ok(new { success = true, userId = user.Id, sessionId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing email");
                return StatusCode(500, new { success = false, message = "Lỗi server khi thay đổi email: " + ex.Message });
            }
        }

        [HttpPost("request-admin")]
        public async Task<IActionResult> RequestAdmin([FromForm] RequestAdminInputModel input)
        {
            try
            {
                _logger.LogInformation("Processing RequestAdmin request: {Reason}", input.RequestAdminReason);

                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    _logger.LogWarning("Không tìm thấy người dùng để yêu cầu admin");
                    return NotFound(new { success = false, message = "Không tìm thấy người dùng." });
                }

                var validationContext = new ValidationContext(input);
                var validationResults = new List<ValidationResult>();
                if (!Validator.TryValidateObject(input, validationContext, validationResults, true))
                {
                    var errors = validationResults.ToDictionary(v => char.ToLower(v.MemberNames.First()[0]) + v.MemberNames.First().Substring(1), v => v.ErrorMessage);
                    _logger.LogWarning("Validation errors: {Errors}", errors);
                    return BadRequest(new { success = false, errors });
                }

                user.AdminRequestPending = true;
                user.AdminRequestReason = input.RequestAdminReason;
                var result = await _userManager.UpdateAsync(user);
                if (!result.Succeeded)
                {
                    var errors = result.Errors.ToDictionary(e => e.Code, e => e.Description);
                    _logger.LogWarning("Admin request update failed: {Errors}", errors);
                    return BadRequest(new { success = false, errors });
                }

                await _signInManager.RefreshSignInAsync(user);
                _logger.LogInformation("Admin request submitted successfully for user: {Username}", user.UserName);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing admin request");
                return StatusCode(500, new { success = false, message = "Lỗi server khi xử lý yêu cầu admin: " + ex.Message });
            }
        }

        private async Task<(bool Success, string Message)> ProcessAvatarUpload(ApplicationUser user, IFormFile avatar)
        {
            try
            {
                if (avatar == null || avatar.Length == 0)
                {
                    _logger.LogWarning("Avatar file is null or empty");
                    return (false, "File avatar không hợp lệ.");
                }

                var extension = Path.GetExtension(avatar.FileName).ToLower();
                if (!AllowedExtensions.Contains(extension))
                {
                    return (false, "Chỉ cho phép file JPG hoặc PNG.");
                }

                if (avatar.Length > MaxFileSize)
                {
                    return (false, $"Kích thước file không được vượt quá {MaxFileSize / (1024 * 1024)}MB.");
                }

                using var memoryStream = new MemoryStream();
                await avatar.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                using var image = System.Drawing.Image.FromStream(memoryStream);
                if (image.Width > MaxImageDimension || image.Height > MaxImageDimension)
                {
                    return (false, $"Kích thước ảnh không được vượt quá {MaxImageDimension}x{MaxImageDimension} pixel.");
                }

                var uploadsFolder = Path.Combine(_environment.WebRootPath, "Uploads", "Avatars");
                Directory.CreateDirectory(uploadsFolder);
                var fileName = $"{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                memoryStream.Position = 0;
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await memoryStream.CopyToAsync(stream);
                }

                if (!string.IsNullOrEmpty(user.AvatarUrl) && user.AvatarUrl != DefaultAvatar)
                {
                    var oldAvatarPath = Path.Combine(_environment.WebRootPath, user.AvatarUrl.TrimStart('/'));
                    if (System.IO.File.Exists(oldAvatarPath))
                    {
                        try
                        {
                            System.IO.File.Delete(oldAvatarPath);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("Failed to delete old avatar: {Message}", ex.Message);
                        }
                    }
                }

                user.AvatarUrl = $"/Uploads/Avatars/{fileName}";
                _logger.LogInformation("Avatar uploaded successfully: {FileName}", fileName);
                return (true, "Avatar uploaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing avatar upload");
                return (false, "Lỗi khi tải lên avatar: " + ex.Message);
            }
        }
    }
}