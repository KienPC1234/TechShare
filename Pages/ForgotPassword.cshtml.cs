using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace LoginSystem.Pages
{
    public class ForgotPasswordModel : PageModel
    {
        public class EmailInputModel
        {
            [Required(ErrorMessage = "Email là bắt buộc")]
            [EmailAddress(ErrorMessage = "Định dạng email không hợp lệ")]
            public string Email { get; set; }
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

        public EmailInputModel EmailInput { get; set; }
        public CodeInputModel CodeInput { get; set; }
        public PasswordInputModel PasswordInput { get; set; }

        public bool ShowCodeInput { get; set; }
        public bool ShowPasswordInput { get; set; }

        public IActionResult OnGet(bool showCode = false, bool showPassword = false)
        {
            EmailInput = new EmailInputModel();
            CodeInput = new CodeInputModel();
            PasswordInput = new PasswordInputModel();

            ShowCodeInput = showCode;
            ShowPasswordInput = showPassword;

            return Page();
        }
    }
}