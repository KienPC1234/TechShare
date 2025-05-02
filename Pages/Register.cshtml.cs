#nullable disable

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using LoginSystem.Models;
using System.ComponentModel.DataAnnotations;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace LoginSystem.Pages
{
    public class RegisterModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _environment;

        public RegisterModel(UserManager<ApplicationUser> userManager, IWebHostEnvironment environment)
        {
            _userManager = userManager;
            _environment = environment;
        }

        [BindProperty]
        public InputModel Input { get; set; }  // Nullable property

        public class InputModel
        {
            [Required]
            [StringLength(100, MinimumLength = 3)]
            public string Username { get; set; } = string.Empty;

            [Required]
            [StringLength(100, MinimumLength = 3)]
            public string DisplayName { get; set; } = string.Empty;

            [Required]
            [EmailAddress]
            [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Invalid email format.")]
            public string Email { get; set; } = string.Empty;

            [Required]
            [DataType(DataType.Password)]
            [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{6,}$", ErrorMessage = "Password must be at least 6 characters, including upper, lower, and number.")]
            public string Password { get; set; } = string.Empty;

            [DataType(DataType.Password)]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; } = string.Empty;

            [DataType(DataType.Upload)]
            public IFormFile Avatar { get; set; }  // Nullable property
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            // Kiểm tra username/email đã tồn tại
            if (await _userManager.FindByNameAsync(Input.Username) != null)
            {
                ModelState.AddModelError("Input.Username", "Username already exists.");
            }
            if (await _userManager.FindByEmailAsync(Input.Email) != null)
            {
                ModelState.AddModelError("Input.Email", "Email already exists.");
            }

            if (!ModelState.IsValid)
                return Page();

            var user = new ApplicationUser
            {
                UserName = Input.Username,
                Email = Input.Email,
                DisplayName = Input.DisplayName
            };

            // Xử lý avatar
            if (Input.Avatar != null)
            {
                try
                {
                    using var image = Image.Load(Input.Avatar.OpenReadStream());

                    var uploadsFolder = Path.Combine(_environment.WebRootPath, "Uploads");
                    Directory.CreateDirectory(uploadsFolder);
                    var fileName = Guid.NewGuid().ToString() + ".jpg";
                    var filePath = Path.Combine(uploadsFolder, fileName);

                    // Nén và lưu ảnh JPG với thư viện ImageSharp
                    image.Mutate(x => x.Resize(500, 500));  // Resize ảnh nếu cần
                    image.Save(filePath, new JpegEncoder { Quality = 85 });

                    user.AvatarUrl = "/Uploads/" + fileName;
                }
                catch
                {
                    ModelState.AddModelError("Input.Avatar", "The uploaded file is not a valid image.");
                    return Page();
                }
            }
            else
            {
                user.AvatarUrl = "/images/default-avatar.png";
            }

            var result = await _userManager.CreateAsync(user, Input.Password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "User");
                return RedirectToPage("/Login");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return Page();
        }
    }
}
