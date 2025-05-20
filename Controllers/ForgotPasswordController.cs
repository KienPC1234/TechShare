using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using LoginSystem.Models;
using System.ComponentModel.DataAnnotations;
using TechShare.Classes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using LoginSystem.Security;

namespace LoginSystem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ForgotPasswordController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly MailToolBox _mailToolBox;
        private readonly ILogger<ForgotPasswordController> _logger;
        private readonly IMemoryCache _cache;
        private readonly IRecaptchaService _recaptcha;
        private const int RESET_TOKEN_VALIDITY_MINUTES = 30;

        public ForgotPasswordController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            MailToolBox mailToolBox,
            IRecaptchaService recaptcha,
            ILogger<ForgotPasswordController> logger,
            IMemoryCache cache)
        {
            _recaptcha = recaptcha;
            _userManager = userManager;
            _signInManager = signInManager;
            _mailToolBox = mailToolBox;
            _logger = logger;
            _cache = cache;
        }

        public class EmailInputModel
        {
            [Required(ErrorMessage = "Email là bắt buộc")]
            [EmailAddress(ErrorMessage = "Định dạng email không hợp lệ")]
            public string Email { get; set; }

            [Required(ErrorMessage = "Recaptcha là bắt buộc")]
            public string RecaptchaToken { get; set; }
        }

        public class CodeInputModel
        {
            [Required(ErrorMessage = "Email là bắt buộc")]
            [EmailAddress(ErrorMessage = "Định dạng email không hợp lệ")]
            public string Email { get; set; }

            [Required(ErrorMessage = "Mã xác minh là bắt buộc")]
            [StringLength(6, MinimumLength = 6, ErrorMessage = "Mã xác minh phải là 6 chữ số")]
            [RegularExpression(@"^\d{6}$", ErrorMessage = "Mã xác minh phải là 6 chữ số")]
            public string Code { get; set; }
        }

        public class PasswordInputModel
        {
            [Required(ErrorMessage = "Email là bắt buộc")]
            [EmailAddress(ErrorMessage = "Định dạng email không hợp lệ")]
            public string Email { get; set; }

            [Required(ErrorMessage = "Mã xác minh là bắt buộc")]
            [StringLength(6, MinimumLength = 6, ErrorMessage = "Mã xác minh phải là 6 chữ số")]
            [RegularExpression(@"^\d{6}$", ErrorMessage = "Mã xác minh phải là 6 chữ số")]
            public string Code { get; set; }

            [Required(ErrorMessage = "Mật khẩu mới là bắt buộc")]
            [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu mới phải từ 6 đến 100 ký tự")]
            [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$", ErrorMessage = "Mật khẩu mới phải chứa ít nhất một chữ cái in hoa, một chữ cái thường và một số")]
            public string NewPassword { get; set; }

            [Required(ErrorMessage = "Xác nhận mật khẩu là bắt buộc")]
            [Compare("NewPassword", ErrorMessage = "Mật khẩu xác nhận không khớp")]
            public string ConfirmPassword { get; set; }
        }

        [HttpPost("send-code")]
        public async Task<IActionResult> SendCode([FromBody] EmailInputModel model)
        {
            if (!await _recaptcha.VerifyAsync(model.RecaptchaToken))
                return BadRequest(new { error = "Recaptcha failed" });

            if (!ModelState.IsValid)
            {
                var errors = ModelState.ToDictionary(
                    kvp => kvp.Key.ToLower(),
                    kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToList()
                );
                return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ.", errors });
            }

            try
            {
                _logger.LogInformation("Processing forgot password request for email: {Email}", model.Email);

                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user == null)
                {
                    _logger.LogWarning("No user found for email: {Email}", model.Email);
                    return Ok(new { success = true, message = "Nếu email tồn tại, mã xác minh đã được gửi." });
                }

                // Generate secure 6-digit code
                byte[] randomBytes = new byte[4];
                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(randomBytes);
                }
                var code = (BitConverter.ToUInt32(randomBytes, 0) % 900000 + 100000).ToString(); // 100000-999999

                var cacheKey = $"PasswordReset_{user.Id}";
                _cache.Set(cacheKey, code, TimeSpan.FromMinutes(RESET_TOKEN_VALIDITY_MINUTES));
                _cache.Set($"PasswordReset_Email_{user.Id}", model.Email, TimeSpan.FromMinutes(RESET_TOKEN_VALIDITY_MINUTES));

                var emailResult = _mailToolBox.SendEmail(
                    model.Email,
                    "Đặt lại mật khẩu",
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
            <h2>Đặt lại mật khẩu</h2>
            <p>Vui lòng sử dụng mã sau để đặt lại mật khẩu của bạn:</p>
            <div class='code'>{code}</div>
            <p>Mã này có hiệu lực trong {RESET_TOKEN_VALIDITY_MINUTES} phút.</p>
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
                    _logger.LogWarning("Failed to send reset code to {Email}: {Status}", model.Email, emailResult);
                    return BadRequest(new { success = false, message = "Không thể gửi mã xác minh." });
                }

                _logger.LogInformation("Reset code sent to {Email}", model.Email);
                return Ok(new { success = true, message = "Mã xác minh đã được gửi đến email của bạn." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing forgot password request for email: {Email}", model.Email);
                return StatusCode(500, new { success = false, message = "Lỗi server khi gửi mã xác minh." });
            }
        }

        [HttpPost("verify-code")]
        public async Task<IActionResult> VerifyCode([FromBody] CodeInputModel model)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.ToDictionary(
                    kvp => kvp.Key.ToLower(),
                    kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToList()
                );
                return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ.", errors });
            }

            try
            {
                _logger.LogInformation("Verifying reset code for email: {Email}", model.Email);

                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user == null)
                {
                    _logger.LogWarning("No user found for email: {Email}", model.Email);
                    return BadRequest(new { success = false, message = "Mã xác minh không hợp lệ." });
                }

                var cacheKey = $"PasswordReset_{user.Id}";
                if (!_cache.TryGetValue(cacheKey, out string storedCode) || storedCode != model.Code)
                {
                    _logger.LogWarning("Invalid or expired code for email: {Email}", model.Email);
                    return BadRequest(new { success = false, message = "Mã xác minh không hợp lệ hoặc đã hết hạn." });
                }

                _logger.LogInformation("Code verified for email: {Email}", model.Email);
                return Ok(new { success = true, message = "Mã xác minh hợp lệ." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying reset code for email: {Email}", model.Email);
                return StatusCode(500, new { success = false, message = "Lỗi server khi xác minh mã." });
            }
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] PasswordInputModel model)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.ToDictionary(
                    kvp => kvp.Key.ToLower(),
                    kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToList()
                );
                return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ.", errors });
            }

            try
            {
                _logger.LogInformation("Resetting password for email: {Email}", model.Email);

                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user == null)
                {
                    _logger.LogWarning("No user found for email: {Email}", model.Email);
                    return BadRequest(new { success = false, message = "Không thể đặt lại mật khẩu." });
                }

                var cacheKey = $"PasswordReset_{user.Id}";
                if (!_cache.TryGetValue(cacheKey, out string storedCode) || storedCode != model.Code)
                {
                    _logger.LogWarning("Invalid or expired code for email: {Email}", model.Email);
                    return BadRequest(new { success = false, message = "Mã xác minh không hợp lệ hoặc đã hết hạn." });
                }

                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);
                if (!result.Succeeded)
                {
                    var errors = result.Errors.ToDictionary(e => e.Code.ToLower(), e => e.Description);
                    _logger.LogWarning("Password reset failed for email: {Email}, errors: {Errors}", model.Email, errors);
                    return BadRequest(new { success = false, message = "Không thể đặt lại mật khẩu.", errors });
                }

                _cache.Remove(cacheKey);
                _cache.Remove($"PasswordReset_Email_{user.Id}");
                await _signInManager.RefreshSignInAsync(user);
                _logger.LogInformation("Password reset successfully for email: {Email}", model.Email);
                return Ok(new { success = true, message = "Mật khẩu đã được đặt lại thành công." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting password for email: {Email}", model.Email);
                return StatusCode(500, new { success = false, message = "Lỗi server khi đặt lại mật khẩu." });
            }
        }
    }
}