using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Memory;
using LoginSystem.Data;
using LoginSystem.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace LoginSystem.Pages.Exchange
{
    [Authorize]
    public class AddCategoryModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IMemoryCache _cache;

        public AddCategoryModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IMemoryCache cache)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        [BindProperty]
        public NewCategoryInput NewCategory { get; set; } = new NewCategoryInput();

        public bool IsAuthorized { get; private set; }
        public string? ErrorMessage { get; private set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            IsAuthorized = user != null && await _userManager.IsInRoleAsync(user, "SuperAdmin");
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            IsAuthorized = user != null && await _userManager.IsInRoleAsync(user, "SuperAdmin");

            if (!IsAuthorized)
            {
                ErrorMessage = "Bạn không có quyền tạo danh mục mới. Vui lòng liên hệ SuperAdmin.";
                return Page();
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.SelectMany(kvp => kvp.Value.Errors.Select(e => e.ErrorMessage));
                ErrorMessage = "Dữ liệu không hợp lệ: " + string.Join("; ", errors);
                return Page();
            }

            // Check for duplicate category name (case-insensitive)
            var existingCategory = await _context.ItemCategories
                .AnyAsync(c => c.Name.ToLower() == NewCategory.Name.Trim().ToLower());
            if (existingCategory)
            {
                ErrorMessage = "Tên danh mục đã tồn tại. Vui lòng chọn tên khác.";
                return Page();
            }

            try
            {
                var newCategory = new ItemCategory
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = NewCategory.Name.Trim(),
                    Description = NewCategory.Description?.Trim() ?? string.Empty,
                    CreatedAt = DateTime.UtcNow
                };

                await _context.ItemCategories.AddAsync(newCategory);
                await _context.SaveChangesAsync();
                _cache.Remove("ItemCategories");

                NewCategory.Id = newCategory.Id; // Store ID for JavaScript
                return Page(); // Stay on page to allow JavaScript to handle closing
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Lỗi khi thêm danh mục: {ex.Message}";
                return Page();
            }
        }

        public class NewCategoryInput
        {
            public string? Id { get; set; } // For sending back to parent window

            [Required(ErrorMessage = "Tên danh mục là bắt buộc.")]
            [MaxLength(100, ErrorMessage = "Tên danh mục không được vượt quá 100 ký tự.")]
            public string Name { get; set; } = string.Empty;

            [MaxLength(500, ErrorMessage = "Mô tả không được vượt quá 500 ký tự.")]
            public string? Description { get; set; }
        }
    }
}