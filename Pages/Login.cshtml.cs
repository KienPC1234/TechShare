#nullable disable

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using LoginSystem.Models;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using System;

namespace LoginSystem.Pages
{
    public class LoginModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;

        public LoginModel(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập hoặc email.")]
            public string UsernameOrEmail { get; set; }

            [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            public bool RememberMe { get; set; }
        }

        public void OnGet(string returnUrl = null)
        {
            ReturnUrl = returnUrl ?? Url.Content("~/");
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            HttpContext.Response.Cookies.Delete("TechShareAuth");
            returnUrl ??= Url.Content("~/");

            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByNameAsync(Input.UsernameOrEmail) ??
                           await _userManager.FindByEmailAsync(Input.UsernameOrEmail);

                if (user == null)
                {
                    ModelState.AddModelError(string.Empty, "Sai tên đăng nhập hoặc mật khẩu.");
                    return Page();
                }

                var result = await _signInManager.PasswordSignInAsync(
                    user.UserName,
                    Input.Password,
                    Input.RememberMe,
                    lockoutOnFailure: true);

                if (result.Succeeded)
                {
                    user.LastLoginTime = DateTime.UtcNow;
                    await _userManager.UpdateAsync(user);

                    if (await _userManager.IsInRoleAsync(user, "Admin") || await _userManager.IsInRoleAsync(user, "SuperAdmin"))
                    {
                        return RedirectToPage("/Dashboard");
                    }

                    return LocalRedirect(returnUrl);
                }

                if (result.IsLockedOut)
                {
                    ModelState.AddModelError(string.Empty, "Tài khoản đã bị khóa tạm thời do đăng nhập sai nhiều lần.");
                }
                else if (result.RequiresTwoFactor)
                {
                    return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Sai tên đăng nhập hoặc mật khẩu.");
                }
            }

            return Page();
        }
    }
}
