using LoginSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace LoginSystem.Pages
{
    [Authorize]
    public class EditProfileModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger<EditProfileModel> _logger;

        public EditProfileModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<EditProfileModel> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
        }

        public ApplicationUser CurrentUser { get; set; }

        public UpdateProfileInputModel UpdateProfileInput { get; set; }

        public ChangeEmailInputModel ChangeEmailInput { get; set; }

        public RequestAdminInputModel RequestAdminInput { get; set; }

        public class UpdateProfileInputModel
        {
            [Required(ErrorMessage = "Tên đăng nhập là bắt buộc")]
            [StringLength(100, MinimumLength = 3, ErrorMessage = "Tên đăng nhập phải từ 3 đến 100 ký tự")]
            [RegularExpression(@"^[a-zA-Z0-9_-]+$", ErrorMessage = "Tên đăng nhập chỉ được chứa chữ, số, dấu gạch dưới và dấu gạch ngang")]
            public string Username { get; set; }

            [Required(ErrorMessage = "Tên hiển thị là bắt buộc")]
            [StringLength(100, MinimumLength = 3, ErrorMessage = "Tên hiển thị phải từ 3 đến 100 ký tự")]
            public string DisplayName { get; set; }

            public IFormFile Avatar { get; set; }

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

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                _logger.LogWarning("Không tìm thấy người dùng để chỉnh sửa hồ sơ");
                return NotFound();
            }
            CurrentUser = user;

            UpdateProfileInput = new UpdateProfileInputModel
            {
                Username = user.UserName,
                DisplayName = user.DisplayName,
                Enable2FA = await _userManager.GetTwoFactorEnabledAsync(user),
                TwoFactorMethod = user.TwoFactorMethod ?? "None"
            };

            ChangeEmailInput = new ChangeEmailInputModel
            {
                NewEmail = user.Email
            };

            RequestAdminInput = new RequestAdminInputModel();
            return Page();
        }
    }
}