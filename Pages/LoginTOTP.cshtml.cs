using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using LoginSystem.Models;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

namespace LoginSystem.Pages
{
    public class LoginTOTPModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<LoginTOTPModel> _logger;
        private readonly IWebHostEnvironment _environment;

        public LoginTOTPModel(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            ILogger<LoginTOTPModel> logger,
            IWebHostEnvironment environment)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
            _environment = environment;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Vui lòng nhập mã TOTP.")]
            [StringLength(6, MinimumLength = 6, ErrorMessage = "Mã TOTP phải có 6 chữ số.")]
            [RegularExpression(@"^\d{6}$", ErrorMessage = "Mã TOTP chỉ được chứa số.")]
            public string Code { get; set; }

            public string UserId { get; set; }
            public string Email { get; set; }
            public bool RememberMe { get; set; }
            public string ReturnUrl { get; set; }
        }

        public IActionResult OnGet(string userId, string email, bool rememberMe, string returnUrl = null)
        {
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Thiếu userId khi truy cập LoginTOTP");
                return RedirectToPage("/Login");
            }

            Input = new InputModel
            {
                UserId = userId,
                Email = email,
                RememberMe = rememberMe,
                ReturnUrl = returnUrl ?? Url.Content("~/")
            };

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("ModelState không hợp lệ khi xác thực TOTP");
                return Page();
            }

            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null)
            {
                _logger.LogWarning("Không tìm thấy người dùng trong phiên xác thực 2FA.");
                return RedirectToPage("/Login");
            }

            try
            {
                _logger.LogInformation("Bắt đầu xác thực TOTP cho người dùng {UserId}, mã: {Code}, thời gian server: {ServerTime}",
                    user.Id, Input.Code, DateTime.UtcNow);
                _logger.LogInformation("TwoFactorSecretKey: {SecretKey}, TwoFactorEnabled: {TwoFactorEnabled}",
                    user.TwoFactorSecretKey ?? "null", await _userManager.GetTwoFactorEnabledAsync(user));

                if (string.IsNullOrEmpty(user.TwoFactorSecretKey) || !await _userManager.GetTwoFactorEnabledAsync(user))
                {
                    _logger.LogError("Cấu hình TOTP không hợp lệ cho người dùng {UserId}", user.Id);
                    ModelState.AddModelError(string.Empty, "Cấu hình TOTP không hợp lệ. Vui lòng cài đặt lại 2FA.");
                    return Page();
                }

                // Kiểm tra mã TOTP bằng UserManager
                var isValid = await _userManager.VerifyTwoFactorTokenAsync(user, "TOTP", Input.Code);
                _logger.LogInformation("VerifyTwoFactorTokenAsync: IsValid={IsValid}, UserId: {UserId}, Code: {Code}",
                    isValid, user.Id, Input.Code);

                if (!isValid)
                {
                    _logger.LogWarning("Mã TOTP không hợp lệ cho người dùng {UserId}, mã: {Code}", user.Id, Input.Code);
                    ModelState.AddModelError(string.Empty, "Mã TOTP không hợp lệ hoặc thời gian thiết bị không đồng bộ.");
                    return Page();
                }

                // Xác thực 2FA bằng SignInManager
                var result = await _signInManager.TwoFactorSignInAsync("TOTP", Input.Code, Input.RememberMe, false);
                _logger.LogInformation("TwoFactorSignInAsync: Succeeded={Succeeded}, IsLockedOut={IsLockedOut}, IsNotAllowed={IsNotAllowed}, UserId: {UserId}",
                    result.Succeeded, result.IsLockedOut, result.IsNotAllowed, user.Id);

                if (result.Succeeded)
                {
                    user.LastLoginTime = DateTime.UtcNow;
                    await _userManager.UpdateAsync(user);
                    _logger.LogInformation("Người dùng {UserId} đăng nhập thành công với TOTP", user.Id);
                    return await RedirectAfterLogin(user);
                }
                else if (!_environment.IsDevelopment() && result.IsLockedOut)
                {
                    _logger.LogWarning("Tài khoản {UserId} bị khóa", user.Id);
                    ModelState.AddModelError(string.Empty, "Tài khoản bị khóa.");
                }
                else if (result.IsNotAllowed)
                {
                    _logger.LogWarning("Người dùng {UserId} không được phép đăng nhập", user.Id);
                    ModelState.AddModelError(string.Empty, "Tài khoản không được phép đăng nhập.");
                }
                else
                {
                    _logger.LogWarning("Xác thực TOTP thất bại qua SignInManager cho người dùng {UserId}", user.Id);
                    ModelState.AddModelError(string.Empty, "Xác thực TOTP thất bại. Vui lòng thử lại.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xác thực TOTP cho người dùng {UserId}", user.Id);
                ModelState.AddModelError(string.Empty, "Đã xảy ra lỗi khi xác thực.");
            }

            return Page();
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