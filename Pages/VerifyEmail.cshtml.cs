using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using LoginSystem.Models;
using TechShare.Classes;

namespace LoginSystem.Pages
{
    public class VerifyEmailModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IMemoryCache _cache;
        private readonly MailToolBox _mailToolBox;
        private readonly ILogger<VerifyEmailModel> _logger;

        public VerifyEmailModel(
            UserManager<ApplicationUser> userManager,
            IMemoryCache cache,
            MailToolBox mailToolBox,
            ILogger<VerifyEmailModel> logger)
        {
            _userManager = userManager;
            _cache = cache;
            _mailToolBox = mailToolBox;
            _logger = logger;
        }

        public class InputModel
        {
            [Required(ErrorMessage = "Email là bắt buộc")]
            [EmailAddress(ErrorMessage = "Định dạng email không hợp lệ")]
            public string Email { get; set; }

            [Required(ErrorMessage = "Mã xác thực là bắt buộc")]
            [StringLength(6, MinimumLength = 6, ErrorMessage = "Mã xác thực phải có 6 ký tự")]
            [RegularExpression(@"^[A-Za-z0-9+/]{6}$", ErrorMessage = "Mã xác thực phải là 6 ký tự chữ cái hoặc số")]
            public string Code { get; set; }
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public async Task<IActionResult> OnGetAsync(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                _logger.LogWarning("Thiếu email khi truy cập VerifyEmail");
                return RedirectToPage("/Login");
            }

            var user = await _userManager.FindByEmailAsync(email);
            if (user != null && user.EmailConfirmed)
            {
                _logger.LogWarning("Email đã được xác thực cho email {Email}", email);
                return RedirectToPage("/Login");
            }

            var cacheKey = $"PendingUser_{email}";
            if (!_cache.TryGetValue(cacheKey, out dynamic userData))
            {
                _logger.LogWarning("Không tìm thấy dữ liệu đăng ký cho email {Email}", email);
                ModelState.AddModelError(string.Empty, "Không tìm thấy thông tin đăng ký. Vui lòng đăng ký lại.");
                return Page();
            }

            var verificationKey = $"Verification_{email}";
            if (!_cache.TryGetValue(verificationKey, out (string Code, DateTime Expires) cachedToken) ||
                cachedToken.Expires < DateTime.UtcNow)
            {
                var sendResult = await SendVerificationEmailAsync(email, null);
                if (!sendResult.Success)
                {
                    _logger.LogWarning("Không thể gửi mã xác thực đến {Email}: {Message}", email, sendResult.Message);
                    ModelState.AddModelError(string.Empty, sendResult.Message);
                    return Page();
                }
                _logger.LogInformation("Mã xác thực đã được gửi đến {Email}", email);
            }
            else
            {
                _logger.LogInformation("Tìm thấy mã xác thực hợp lệ trong cache cho email {Email}: {Code}", email, cachedToken.Code);
                if (!_cache.TryGetValue($"EmailSent_{email}", out bool emailSent) || !emailSent)
                {
                    var sendResult = await SendVerificationEmailAsync(email, cachedToken.Code);
                    if (!sendResult.Success)
                    {
                        _logger.LogWarning("Không thể gửi mã xác thực đến {Email}: {Message}", email, sendResult.Message);
                        ModelState.AddModelError(string.Empty, sendResult.Message);
                        return Page();
                    }
                    _cache.Set($"EmailSent_{email}", true, TimeSpan.FromMinutes(15));
                    _logger.LogInformation("Mã xác thực đã được gửi đến {Email}", email);
                }
            }

            Input = new InputModel { Email = email };
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("ModelState không hợp lệ khi xác thực email");
                return Page();
            }

            var cacheKey = $"PendingUser_{Input.Email}";
            if (!_cache.TryGetValue(cacheKey, out dynamic userData))
            {
                _logger.LogWarning("Không tìm thấy dữ liệu đăng ký cho email {Email}", Input.Email);
                ModelState.AddModelError(string.Empty, "Không tìm thấy thông tin đăng ký. Vui lòng đăng ký lại.");
                return Page();
            }

            var verificationKey = $"Verification_{Input.Email}";
            if (!_cache.TryGetValue(verificationKey, out (string Code, DateTime Expires) cachedToken) ||
                cachedToken.Expires < DateTime.UtcNow ||
                cachedToken.Code != Input.Code)
            {
                _logger.LogWarning("Mã xác thực không hợp lệ hoặc đã hết hạn cho email {Email}, mã: {Code}", Input.Email, Input.Code);
                ModelState.AddModelError(string.Empty, "Mã xác thực không hợp lệ hoặc đã hết hạn.");
                return Page();
            }

            try
            {
                var user = new ApplicationUser
                {
                    UserName = userData.Username,
                    Email = userData.Email,
                    DisplayName = userData.DisplayName,
                    AvatarUrl = userData.AvatarUrl,
                    EmailConfirmed = true
                };

                var result = await _userManager.CreateAsync(user, userData.Password);
                if (!result.Succeeded)
                {
                    foreach (var error in result.Errors)
                    {
                        //_logger.LogWarning("Lỗi tạo người dùng: {Error}", error.Description);
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                    return Page();
                }

                await _userManager.AddToRoleAsync(user, "User");
                _cache.Remove(cacheKey);
                _cache.Remove(verificationKey);
                _cache.Remove($"EmailSent_{Input.Email}");
                _logger.LogInformation("Người dùng {UserId} đã được tạo và xác thực email thành công", user.Id);
                return RedirectToPage("/Login");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tạo người dùng hoặc xác thực email cho {Email}", Input.Email);
                ModelState.AddModelError(string.Empty, "Lỗi khi tạo tài khoản.");
                return Page();
            }
        }

        public async Task<IActionResult> OnPostResendAsync()
        {
            if (string.IsNullOrEmpty(Input?.Email))
            {
                _logger.LogWarning("Thiếu email khi gửi lại mã xác thực");
                ModelState.AddModelError(string.Empty, "Email là bắt buộc.");
                return Page();
            }

            var cacheKey = $"PendingUser_{Input.Email}";
            if (!_cache.TryGetValue(cacheKey, out dynamic userData))
            {
                _logger.LogWarning("Không tìm thấy dữ liệu đăng ký cho email {Email}", Input.Email);
                ModelState.AddModelError(string.Empty, "Không tìm thấy thông tin đăng ký. Vui lòng đăng ký lại.");
                return Page();
            }

            var verificationKey = $"Verification_{Input.Email}";
            var sendResult = await SendVerificationEmailAsync(Input.Email, null);
            if (!sendResult.Success)
            {
                _logger.LogWarning("Không thể gửi lại mã xác thực đến {Email}: {Message}", Input.Email, sendResult.Message);
                ModelState.AddModelError(string.Empty, sendResult.Message);
                return Page();
            }

            _logger.LogInformation("Mã xác thực đã được gửi lại đến {Email}", Input.Email);
            TempData["SuccessMessage"] = "Mã xác thực đã được gửi lại.";
            return Page();
        }

        private async Task<(bool Success, string Message)> SendVerificationEmailAsync(string email, string verificationCode)
        {
            try
            {
                var sessionId = Guid.NewGuid().ToString();
                var code = verificationCode ?? new Random().Next(100000, 999999).ToString();
                var verificationKey = $"Verification_{email}";
                if (verificationCode == null)
                {
                    var expires = DateTime.UtcNow.AddMinutes(15);
                    _cache.Set(verificationKey, (Code: code, Expires: expires), TimeSpan.FromMinutes(15));
                }

                var emailResult = _mailToolBox.SendEmail(
                    email,
                    "Xác thực Email",
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
            <h2>Xác thực Email</h2>
            <p>Vui lòng sử dụng mã sau để xác thực email của bạn:</p>
            <div class='code'>{code}</div>
            <p>Mã này có hiệu lực trong 15 phút.</p>
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
                    _logger.LogWarning("Không thể gửi email xác thực: {Status}", emailResult);
                    return (false, "Không thể gửi mã xác thực. Vui lòng thử lại.");
                }

                _logger.LogInformation("Mã xác thực đã được gửi đến {Email}, sessionId: {SessionId}, mã: {Code}", email, sessionId, code);
                return (true, "Mã xác thực đã được gửi.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gửi mã xác thực đến {Email}", email);
                return (false, "Lỗi khi gửi mã xác thực.");
            }
        }
    }
}