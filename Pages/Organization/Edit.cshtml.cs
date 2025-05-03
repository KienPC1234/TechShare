#nullable enable

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using LoginSystem.Data;
using LoginSystem.Models;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using Microsoft.Extensions.Logging;

namespace LoginSystem.Pages.Organization
{
    [Authorize]
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<EditModel> _logger;

        public ApplicationDbContext DbContext => _dbContext;
        public UserManager<ApplicationUser> UserManager => _userManager;

        public EditModel(
            ApplicationDbContext dbContext,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment environment,
            ILogger<EditModel> logger)
        {
            _dbContext = dbContext;
            _userManager = userManager;
            _environment = environment;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new InputModel();

        public LoginSystem.Models.Organization? Organization { get; set; }

        public class InputModel
        {
            [Required]
            [StringLength(100, MinimumLength = 3)]
            [Display(Name = "Tên tổ chức")]
            public string Name { get; set; } = string.Empty;

            [Required]
            [Display(Name = "Điều khoản tham gia")]
            public string Terms { get; set; } = string.Empty;

            [Display(Name = "Tổ chức riêng tư")]
            public bool IsPrivate { get; set; }

            [Display(Name = "Mô tả tổ chức")]
            public string? Description { get; set; }

            [Display(Name = "Avatar tổ chức")]
            [DataType(DataType.Upload)]
            public IFormFile? Avatar { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(string slug)
        {
            Organization = await _dbContext.Organizations.FirstOrDefaultAsync(o => o.Slug == slug);
            if (Organization == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            var isAdmin = await _dbContext.OrganizationMembers
                .AnyAsync(m => m.OrganizationId == Organization.Id && m.UserId == userId && m.Role == "Admin");
            if (!isAdmin)
            {
                TempData["ErrorMessage"] = "Bạn không có quyền chỉnh sửa tổ chức này.";
                return RedirectToPage("/Organization/Details", new { slug });
            }

            Input = new InputModel
            {
                Name = Organization.Name,
                Terms = Organization.Terms,
                IsPrivate = Organization.IsPrivate,
                Description = Organization.Description
            };
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(string slug)
        {
            Organization = await _dbContext.Organizations.FirstOrDefaultAsync(o => o.Slug == slug);
            if (Organization == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            var isAdmin = await _dbContext.OrganizationMembers
                .AnyAsync(m => m.OrganizationId == Organization.Id && m.UserId == userId && m.Role == "Admin");
            if (!isAdmin)
            {
                TempData["ErrorMessage"] = "Bạn không có quyền chỉnh sửa tổ chức này.";
                return RedirectToPage("/Organization/Details", new { slug });
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            Organization.Name = Input.Name;
            Organization.Terms = Input.Terms;
            Organization.IsPrivate = Input.IsPrivate;
            Organization.Description = Input.Description;

            if (Input.Avatar != null)
            {
                var extension = Path.GetExtension(Input.Avatar.FileName).ToLowerInvariant();
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                if (!allowedExtensions.Contains(extension))
                {
                    ModelState.AddModelError("Input.Avatar", "Chỉ chấp nhận các định dạng ảnh: .jpg, .png, .jpeg, .webp.");
                    return Page();
                }

                var uploadsFolder = Path.Combine(_environment.WebRootPath, "Uploads");
                Directory.CreateDirectory(uploadsFolder);
                var fileName = Guid.NewGuid() + ".jpg";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using var imageStream = Input.Avatar.OpenReadStream();
                using var image = await Image.LoadAsync(imageStream);
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(300, 300),
                    Mode = ResizeMode.Max
                }).Pad(300, 300, Color.White));
                using var outputStream = new FileStream(filePath, FileMode.Create);
                await image.SaveAsync(outputStream, new JpegEncoder { Quality = 85 });

                Organization.AvatarUrl = "/Uploads/" + fileName;
            }

            await _dbContext.SaveChangesAsync();
            TempData["SuccessMessage"] = "Cập nhật tổ chức thành công!";
            return RedirectToPage("/Organization/Details", new { slug });
        }

        public async Task<IActionResult> OnPostUploadImageAsync(IFormFile file)
        {
            _logger.LogInformation("Received image upload request. File: {FileName}, Size: {FileSize}", file?.FileName, file?.Length);

            if (file == null || file.Length == 0)
            {
                _logger.LogWarning("No file uploaded.");
                return new JsonResult(new { error = "Không có file được upload." }) { StatusCode = 400 };
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            if (!allowedExtensions.Contains(extension))
            {
                _logger.LogWarning("Invalid file extension: {Extension}", extension);
                return new JsonResult(new { error = "Chỉ chấp nhận các định dạng ảnh: .jpg, .png, .jpeg, .webp." }) { StatusCode = 400 };
            }

            var uploadsFolder = Path.Combine(_environment.WebRootPath, "Uploads");
            Directory.CreateDirectory(uploadsFolder);
            var fileName = Guid.NewGuid() + ".jpg";
            var filePath = Path.Combine(uploadsFolder, fileName);

            try
            {
                using var imageStream = file.OpenReadStream();
                using var image = await Image.LoadAsync(imageStream);
                // Không resize hay padding, chỉ nén JPEG 85%
                using var outputStream = new FileStream(filePath, FileMode.Create);
                await image.SaveAsync(outputStream, new JpegEncoder { Quality = 85 });

                var url = "/Uploads/" + fileName;
                _logger.LogInformation("Image uploaded successfully. URL: {Url}", url);
                return new JsonResult(new { location = url });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading image: {Message}", ex.Message);
                return new JsonResult(new { error = "Không thể xử lý ảnh. Vui lòng thử lại." }) { StatusCode = 500 };
            }
        }
    }
}