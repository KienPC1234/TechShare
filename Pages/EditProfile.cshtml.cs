using LoginSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Razor;


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
            public string Username { get; set; }
            public string DisplayName { get; set; }
            public IFormFile Avatar { get; set; }
            public bool Enable2FA { get; set; }
            public string TwoFactorMethod { get; set; }
        }

        public class ChangeEmailInputModel
        {
            public string NewEmail { get; set; }
        }

        public class RequestAdminInputModel
        {
            public string RequestAdminReason { get; set; }
        }

        public class ChangePasswordInputModel
        {
            public string CurrentPassword { get; set; }
            public string NewPassword { get; set; }
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