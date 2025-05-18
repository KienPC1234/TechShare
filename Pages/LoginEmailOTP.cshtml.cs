using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using LoginSystem.Models;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using TechShare.Classes;
using Microsoft.Extensions.Caching.Memory;

namespace LoginSystem.Pages
{
    public class LoginEmailOTPModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<LoginEmailOTPModel> _logger;
        private readonly IWebHostEnvironment _environment;
        private readonly MailToolBox _mailToolBox;
        private readonly IMemoryCache _cache;

        public LoginEmailOTPModel(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            ILogger<LoginEmailOTPModel> logger,
            IWebHostEnvironment environment,
            MailToolBox mailToolBox,
            IMemoryCache cache)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
            _environment = environment;
            _mailToolBox = mailToolBox;
            _cache = cache;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Vui lòng nhập mã OTP.")]
            [StringLength(6, MinimumLength = 6, ErrorMessage = "Mã OTP phải có 6 chữ số.")]
            [RegularExpression(@"^\d{6}$", ErrorMessage = "Mã OTP chỉ được chứa số.")]
            public string Code { get; set; }

            public string UserId { get; set; }
            public string Email { get; set; }
            public bool RememberMe { get; set; }
            public string ReturnUrl { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(string userId, string email, bool rememberMe, string returnUrl = null)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(email))
            {
                _logger.LogWarning("Thiếu userId hoặc email khi truy cập LoginEmailOTP");
                return RedirectToPage("/Login");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("Không tìm thấy người dùng với userId {UserId}", userId);
                return RedirectToPage("/Login");
            }

            Input = new InputModel
            {
                UserId = userId,
                Email = email,
                RememberMe = rememberMe,
                ReturnUrl = returnUrl ?? Url.Content("~/")
            };

            // Send OTP email automatically
            var sendResult = await SendEmailOTPAsync(user, email);
            if (!sendResult.Success)
            {
                _logger.LogWarning("Không thể gửi mã OTP đến {Email}: {Message}", email, sendResult.Message);
                ModelState.AddModelError(string.Empty, sendResult.Message);
                return Page();
            }

            _logger.LogInformation("Mã OTP đã được gửi đến {Email} cho người dùng {UserId}", email, userId);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("ModelState không hợp lệ khi xác thực OTP email");
                return Page();
            }

            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null)
            {
                _logger.LogWarning("Không tìm thấy người dùng trong phiên xác thực 2FA");
                ModelState.AddModelError(string.Empty, "Phiên đăng nhập không hợp lệ.");
                return RedirectToPage("/Login");
            }

            try
            {
                _logger.LogInformation("Xác thực mã OTP email cho người dùng {UserId}, email {Email}, mã: {Code}", user.Id, Input.Email, Input.Code);

                var cacheKey = $"TwoFactorEmail_{user.Id}";
                if (!_cache.TryGetValue(cacheKey, out (string Code, DateTime Expires) cachedToken) ||
                    cachedToken.Expires < DateTime.UtcNow ||
                    cachedToken.Code != Input.Code)
                {
                    _logger.LogWarning("Mã OTP email không hợp lệ hoặc đã hết hạn cho người dùng {UserId}, mã: {Code}", user.Id, Input.Code);
                    ModelState.AddModelError(string.Empty, "Mã OTP không hợp lệ hoặc đã hết hạn.");
                    return Page();
                }

                // Debug: Log token providers
                _logger.LogInformation("Available token providers: {Providers}", string.Join(", ", _userManager.GetValidTwoFactorProvidersAsync(user).Result));

                // Validate OTP using CustomEmailTokenProvider
                var isValid = await _userManager.VerifyTwoFactorTokenAsync(user, "CustomEmail", Input.Code);
                if (!isValid)
                {
                    _logger.LogWarning("Mã OTP email không hợp lệ qua CustomEmailTokenProvider cho người dùng {UserId}, mã: {Code}", user.Id, Input.Code);
                    ModelState.AddModelError(string.Empty, "Mã OTP không hợp lệ.");
                    return Page();
                }

                // Manually sign in after successful OTP verification
                await _signInManager.SignInAsync(user, Input.RememberMe);
                _cache.Remove(cacheKey); // Remove token after successful verification
                user.LastLoginTime = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);
                _logger.LogInformation("Người dùng {UserId} đã đăng nhập với OTP email thành công", user.Id);
                return await RedirectAfterLogin(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xác thực OTP email cho người dùng {UserId}", user.Id);
                ModelState.AddModelError(string.Empty, "Lỗi xác thực OTP.");
            }

            return Page();
        }

        private async Task<(bool Success, string Message)> SendEmailOTPAsync(ApplicationUser user, string email)
        {
            try
            {
                var sessionId = Guid.NewGuid().ToString();
                var verificationCode = new Random().Next(100000, 999999).ToString();
                var cacheKey = $"TwoFactorEmail_{user.Id}";
                var expires = DateTime.UtcNow.AddMinutes(15);

                _cache.Set(cacheKey, (Code: verificationCode, Expires: expires), TimeSpan.FromMinutes(15));

                var emailResult = _mailToolBox.SendEmail(
                    email,
                    "Xác thực OTP Đăng nhập",
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
            <h2>Xác thực OTP Đăng nhập</h2>
            <p>Vui lòng sử dụng mã sau để xác thực đăng nhập của bạn:</p>
            <div class='code'>{verificationCode}</div>
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
                    return (false, "Không thể gửi mã OTP. Vui lòng thử lại.");
                }

                _logger.LogInformation("Mã OTP đã được gửi đến {Email}, sessionId: {SessionId}", email, sessionId);
                return (true, "Mã OTP đã được gửi.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gửi mã OTP đến {Email}", email);
                return (false, "Lỗi khi gửi mã OTP: " + ex.Message);
            }
        }

        private async Task<IActionResult> RedirectAfterLogin(ApplicationUser user)
        {
            if (await _userManager.IsInRoleAsync(user, "SuperAdmin"))
            {
                _logger.LogInformation("Chuyển hướng SuperAdmin {UserId} đến Dashboard", user.Id);
                return RedirectToPage("/Dashboard");
            }
            var redirectUrl = Input.ReturnUrl ?? Url.Content("~/");
            if (!Url.IsLocalUrl(redirectUrl))
            {
                _logger.LogWarning("ReturnUrl không hợp lệ: {ReturnUrl}. Chuyển hướng đến trang chủ.", redirectUrl);
                redirectUrl = Url.Content("~/");
            }
            _logger.LogInformation("Chuyển hướng người dùng {UserId} đến {RedirectUrl}", user.Id, redirectUrl);
            return LocalRedirect(redirectUrl);
        }
    }
}