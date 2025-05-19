using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using LoginSystem.Models;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using LoginSystem.Security;

namespace LoginSystem.Pages
{
    public class LoginModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<LoginModel> _logger;
        private readonly IRecaptchaService _recaptcha;
        private readonly IWebHostEnvironment _environment;

        public LoginModel(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            ILogger<LoginModel> logger,
            IRecaptchaService recaptcha,
            IWebHostEnvironment environment)
        {
            _recaptcha = recaptcha;
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
            _environment = environment;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        [BindProperty(Name = "g-recaptcha-response")]
        public string RecaptchaToken { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập hoặc email.")]
            public string UsernameOrEmail { get; set; }

            [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
            [DataType(DataType.Password)]
            [MinLength(6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự.")]
            public string Password { get; set; }

            public bool RememberMe { get; set; }

            public string ReturnUrl { get; set; }
        }

        public IActionResult OnGet(string returnUrl = null)
        {
            if (User.Identity.IsAuthenticated)
            {
                _logger.LogInformation("Người dùng đã đăng nhập, chuyển hướng đến trang chủ với ReturnUrl: {ReturnUrl}", returnUrl);
                return LocalRedirect(returnUrl ?? Url.Content("~/"));
            }

            Input = new InputModel
            {
                ReturnUrl = returnUrl
            };
            _logger.LogInformation("OnGet: Đặt Input.ReturnUrl = {ReturnUrl}", returnUrl);
            return Page();
        }

        public async Task<IActionResult> OnPostLoginAsync()
        {
            // Kiểm tra reCAPTCHA
            if (!await _recaptcha.VerifyAsync(RecaptchaToken))
            {
                ModelState.AddModelError(string.Empty, "Xác thực reCAPTCHA thất bại");
                return Page();
            }

            ModelState.Remove("Input.ReturnUrl");

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("ModelState không hợp lệ khi đăng nhập");
                return Page();
            }

            var user = await _userManager.FindByNameAsync(Input.UsernameOrEmail) ??
                       await _userManager.FindByEmailAsync(Input.UsernameOrEmail);

            if (user == null)
            {
                _logger.LogWarning("Không tìm thấy người dùng với UsernameOrEmail: {UsernameOrEmail}", Input.UsernameOrEmail);
                ModelState.AddModelError(string.Empty, "Tên đăng nhập hoặc email không tồn tại.");
                return Page();
            }

            try
            {
                var result = await _signInManager.PasswordSignInAsync(
                    user.UserName,
                    Input.Password,
                    Input.RememberMe,
                    lockoutOnFailure: !_environment.IsDevelopment());

                _logger.LogInformation("Kết quả đăng nhập cho người dùng {UserId}: Succeeded={Succeeded}, IsLockedOut={IsLockedOut}, RequiresTwoFactor={RequiresTwoFactor}, IsNotAllowed={IsNotAllowed}",
                    user.Id, result.Succeeded, result.IsLockedOut, result.RequiresTwoFactor, result.IsNotAllowed);

                if (result.Succeeded)
                {
                    user.LastLoginTime = DateTime.UtcNow;
                    await _userManager.UpdateAsync(user);
                    _logger.LogInformation("Người dùng {UserId} đã đăng nhập thành công", user.Id);
                    return await RedirectAfterLogin(user);
                }
                else if (result.RequiresTwoFactor)
                {
                    var twoFactorUser = await _signInManager.GetTwoFactorAuthenticationUserAsync();
                    if (twoFactorUser == null)
                    {
                        _logger.LogError("Không thể lấy người dùng trong phiên 2FA sau PasswordSignInAsync cho người dùng {UserId}", user.Id);
                        ModelState.AddModelError(string.Empty, "Lỗi phiên xác thực. Vui lòng đăng nhập lại.");
                        return Page();
                    }

                    var twoFactorMethod = user.TwoFactorMethod ?? "TOTP";
                    _logger.LogInformation("Người dùng {UserId} yêu cầu xác thực hai yếu tố ({TwoFactorMethod})", user.Id, twoFactorMethod);
                    return RedirectToPage(twoFactorMethod == "Email" ? "/LoginEmailOTP" : "/LoginTOTP",
                        new { userId = user.Id, email = user.Email, rememberMe = Input.RememberMe, returnUrl = Input.ReturnUrl });
                }
                else if (!_environment.IsDevelopment() && result.IsLockedOut)
                {
                    var remainingMinutes = Math.Ceiling((user.LockoutEnd.Value - DateTime.UtcNow).TotalMinutes);
                    _logger.LogWarning("Tài khoản {UserId} bị khóa đến {LockoutEnd}", user.Id, user.LockoutEnd);
                    ModelState.AddModelError(string.Empty, $"Tài khoản của bạn đã bị khóa. Vui lòng thử lại sau {remainingMinutes} phút.");
                }
                else if (result.IsNotAllowed)
                {
                    _logger.LogWarning("Người dùng {UserId} không được phép đăng nhập", user.Id);
                    ModelState.AddModelError(string.Empty, "Tài khoản không được phép đăng nhập. Vui lòng xác minh email hoặc liên hệ quản trị viên.");
                }
                else
                {
                    _logger.LogWarning("Đăng nhập thất bại cho người dùng {UserId}", user.Id);
                    ModelState.AddModelError(string.Empty, "Tên đăng nhập hoặc mật khẩu không đúng.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi đăng nhập cho người dùng {UsernameOrEmail}", Input.UsernameOrEmail);
                ModelState.AddModelError(string.Empty, "Lỗi đăng nhập. Vui lòng thử lại.");
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