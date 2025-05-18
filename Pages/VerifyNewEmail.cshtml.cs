using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity;
using LoginSystem.Models;
using TechShare.Classes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;

namespace LoginSystem.Pages
{
    [Authorize]
    public class VerifyNewEmailModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly MailToolBox _mailToolBox;
        private readonly ILogger<VerifyNewEmailModel> _logger;
        private readonly IMemoryCache _cache;

        public VerifyNewEmailModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            MailToolBox mailToolBox,
            ILogger<VerifyNewEmailModel> logger,
            IMemoryCache cache)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _mailToolBox = mailToolBox;
            _logger = logger;
            _cache = cache;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Vui lòng nhập mã xác thực.")]
            [StringLength(6, MinimumLength = 6, ErrorMessage = "Mã xác thực phải có 6 chữ số.")]
            [RegularExpression(@"^\d{6}$", ErrorMessage = "Mã xác thực chỉ được chứa số.")]
            public string Code { get; set; }

            [Required]
            public string NewEmail { get; set; }

            [Required]
            public string UserId { get; set; }

            [Required]
            public string SessionId { get; set; }
        }

        public IActionResult OnGet(string email, string userId, string sessionId)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(sessionId))
            {
                _logger.LogWarning("Thiếu email, userId hoặc sessionId khi truy cập VerifyNewEmail");
                TempData["ErrorMessage"] = "Thông tin không hợp lệ.";
                return RedirectToPage("/EditProfile");
            }

            var user = _userManager.GetUserAsync(User).Result;
            if (user == null || user.Id != userId)
            {
                _logger.LogWarning("Người dùng không hợp lệ hoặc không khớp userId: {UserId}", userId);
                TempData["ErrorMessage"] = "Không có quyền truy cập.";
                return Unauthorized();
            }

            Input = new InputModel
            {
                NewEmail = email,
                UserId = userId,
                SessionId = sessionId
            };

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("ModelState không hợp lệ khi xác thực email mới: {Errors}", string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
                return Page();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.Id != Input.UserId)
            {
                _logger.LogWarning("Người dùng không hợp lệ hoặc không khớp userId khi xác thực email mới: {UserId}", Input.UserId);
                TempData["ErrorMessage"] = "Không có quyền truy cập.";
                return Unauthorized();
            }

            try
            {
                _logger.LogInformation("Xác thực mã OTP cho người dùng {UserId}, email {NewEmail}, mã: {Code}, sessionId: {SessionId}", user.Id, Input.NewEmail, Input.Code, Input.SessionId);

                // Retrieve token from cache
                var cacheKey = $"EmailVerification_{Input.UserId}_{Input.SessionId}";
                if (!_cache.TryGetValue(cacheKey, out (string Token, string Email, DateTime Expires) cachedData))
                {
                    _logger.LogWarning("Không tìm thấy mã xác thực trong cache cho người dùng {UserId}, sessionId: {SessionId}", user.Id, Input.SessionId);
                    ModelState.AddModelError("Input.Code", "Mã xác thực không tồn tại hoặc đã hết hạn. Vui lòng thử gửi lại mã mới.");
                    return Page();
                }

                // Verify token and session
                if (cachedData.Token != Input.Code || cachedData.Email != Input.NewEmail || DateTime.UtcNow > cachedData.Expires)
                {
                    _logger.LogWarning("Mã xác thực không hợp lệ hoặc đã hết hạn cho người dùng {UserId}, email {NewEmail}, mã: {Code}, sessionId: {SessionId}, stored: {StoredToken}, expires: {Expires}",
                        user.Id, Input.NewEmail, Input.Code, Input.SessionId, cachedData.Token, cachedData.Expires);
                    ModelState.AddModelError("Input.Code", "Mã xác thực không hợp lệ hoặc đã hết hạn. Vui lòng thử gửi lại mã mới.");
                    return Page();
                }

                // Update email
                var oldEmail = user.Email;
                user.Email = Input.NewEmail;
                user.NormalizedEmail = _userManager.NormalizeEmail(Input.NewEmail);
                user.EmailConfirmed = true;

                var updateResult = await _userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    _logger.LogWarning("Lỗi khi cập nhật email cho người dùng {UserId}: {Errors}", user.Id, string.Join(", ", updateResult.Errors.Select(e => e.Description)));
                    foreach (var error in updateResult.Errors)
                        ModelState.AddModelError(string.Empty, error.Description);
                    return Page();
                }

                // Remove token from cache
                _cache.Remove(cacheKey);

                await _signInManager.RefreshSignInAsync(user);
                _logger.LogInformation("Người dùng {UserId} đã xác thực email mới {NewEmail} từ {OldEmail}", user.Id, Input.NewEmail, oldEmail);
                TempData["SuccessMessage"] = "Email đã được cập nhật thành công.";
                return RedirectToPage("/EditProfile");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xác thực email mới cho người dùng {UserId}, email {NewEmail}, sessionId: {SessionId}", user.Id, Input.NewEmail, Input.SessionId);
                TempData["ErrorMessage"] = "Đã xảy ra lỗi khi xác thực email. Vui lòng thử lại.";
                return Page();
            }
        }
    }
}