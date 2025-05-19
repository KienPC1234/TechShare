using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using LoginSystem.Data;
using LoginSystem.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LoginSystem.Security;

namespace LoginSystem.Pages.Exchange
{
    [Authorize]
    public class CreateItemModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IMemoryCache _cache;
        private readonly ILogger<CreateItemModel> _logger;
        private readonly IRecaptchaService _recaptcha;

        public CreateItemModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IMemoryCache cache, ILogger<CreateItemModel> logger, IRecaptchaService recaptcha)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _recaptcha = recaptcha ?? throw new ArgumentNullException(nameof(recaptcha));
        }

        [BindProperty(Name = "g-recaptcha-response")]
        public string RecaptchaToken { get; set; }

        [BindProperty]
        public ExchangeItem Item { get; set; } = new ExchangeItem();

        [BindProperty]
        public List<IFormFile> Images { get; set; } = new List<IFormFile>();

        [BindProperty]
        public IFormFile? Video { get; set; }

        [BindProperty]
        public string Tags { get; set; } = string.Empty;

        public List<SelectListItem> Categories { get; private set; } = new List<SelectListItem>();

        public string? ErrorMessage { get; private set; }

        public bool HasOrganization { get; private set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                _logger.LogWarning("User not authenticated.");
                ErrorMessage = "Người dùng không được xác thực.";
                return Page();
            }

            Item.OwnerId = user.Id;
            HasOrganization = await ValidateUserOrganizationAsync(user);
            Item.OrganizationId = HasOrganization ? user.OrganizationId : null;
            Item.IsPrivate = false; // Default to false, only allow true if HasOrganization

            _logger.LogInformation("OnGet: User {UserId}, OrganizationId: {OrgId}, HasOrganization: {HasOrg}", user.Id, Item.OrganizationId, HasOrganization);

            await LoadSelectListsAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!await _recaptcha.VerifyAsync(RecaptchaToken))
            {
                ErrorMessage = "Xác thực reCAPTCHA thất bại";
                await LoadSelectListsAsync();
                return Page();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                _logger.LogWarning("User not authenticated on POST.");
                ErrorMessage = "Người dùng không được xác thực.";
                HasOrganization = false;
                await LoadSelectListsAsync();
                return Page();
            }

            if (Item.OwnerId != user.Id)
            {
                _logger.LogWarning("Invalid OwnerId: {OwnerId} does not match user {UserId}", Item.OwnerId, user.Id);
                ErrorMessage = "ID người sở hữu không hợp lệ.";
                HasOrganization = false;
                await LoadSelectListsAsync();
                return Page();
            }

            HasOrganization = await ValidateUserOrganizationAsync(user);
            if (HasOrganization)
            {
                // Ensure OrganizationId is set to user's valid OrganizationId
                Item.OrganizationId = user.OrganizationId;
            }
            else
            {
                // Clear OrganizationId and IsPrivate if no valid organization
                Item.OrganizationId = null;
                Item.IsPrivate = false;
            }

            _logger.LogInformation("OnPost: User {UserId}, OrganizationId: {OrgId}, HasOrganization: {HasOrg}, IsPrivate: {IsPrivate}",
                user.Id, Item.OrganizationId, HasOrganization, Item.IsPrivate);

            Item.Id = Guid.NewGuid().ToString();
            Item.CreatedAt = DateTime.UtcNow;
            Item.MediaItems = new List<ItemMedia>();
            Item.Tags = new List<ItemTag>();

            if (!ModelState.IsValid)
            {
                var errors = ModelState.SelectMany(kvp => kvp.Value.Errors.Select(e => $"{kvp.Key}: {e.ErrorMessage}"));
                ErrorMessage = "Dữ liệu không hợp lệ: " + string.Join("; ", errors);
                await LoadSelectListsAsync();
                return Page();
            }

            if (string.IsNullOrWhiteSpace(Item.Title) || Item.Title.Length > 200)
            {
                ErrorMessage = "Tiêu đề không hợp lệ hoặc vượt quá 200 ký tự.";
                await LoadSelectListsAsync();
                return Page();
            }

            if (string.IsNullOrWhiteSpace(Item.Description) || Item.Description.Length > 5000)
            {
                ErrorMessage = "Mô tả không hợp lệ hoặc vượt quá 5000 ký tự.";
                await LoadSelectListsAsync();
                return Page();
            }

            if (string.IsNullOrWhiteSpace(Item.Terms) || Item.Terms.Length > 1000)
            {
                ErrorMessage = "Điều khoản không hợp lệ hoặc vượt quá 1000 ký tự.";
                await LoadSelectListsAsync();
                return Page();
            }

            if (string.IsNullOrWhiteSpace(Item.PickupAddress) || Item.PickupAddress.Length > 500)
            {
                ErrorMessage = "Địa chỉ lấy hàng không hợp lệ hoặc vượt quá 500 ký tự.";
                await LoadSelectListsAsync();
                return Page();
            }

            if (Item.QuantityAvailable < 0)
            {
                ErrorMessage = "Số lượng phải lớn hơn hoặc bằng 0.";
                await LoadSelectListsAsync();
                return Page();
            }

            if (string.IsNullOrEmpty(Item.CategoryId))
            {
                ErrorMessage = "Danh mục là bắt buộc.";
                await LoadSelectListsAsync();
                return Page();
            }

            var categoryExists = await _context.ItemCategories.AnyAsync(c => c.Id == Item.CategoryId);
            if (!categoryExists)
            {
                ErrorMessage = "Danh mục không tồn tại.";
                await LoadSelectListsAsync();
                return Page();
            }

            var mediaResult = await ProcessMediaFilesAsync();
            if (!mediaResult.Success)
            {
                ErrorMessage = mediaResult.ErrorMessage;
                await LoadSelectListsAsync();
                return Page();
            }

            if (!mediaResult.HasMedia)
            {
                ErrorMessage = "Vui lòng tải lên ít nhất một hình ảnh hoặc video.";
                await LoadSelectListsAsync();
                return Page();
            }

            if (!string.IsNullOrWhiteSpace(Tags))
            {
                var tagList = Tags.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim().ToLower())
                    .Distinct()
                    .Take(10)
                    .ToList();

                if (tagList.Any(t => t.Length > 50))
                {
                    ErrorMessage = "Mỗi tag không được vượt quá 50 ký tự.";
                    await LoadSelectListsAsync();
                    return Page();
                }

                Item.Tags = tagList.Select(t => new ItemTag
                {
                    Id = Guid.NewGuid().ToString(),
                    Tag = t,
                    ItemId = Item.Id
                }).ToList();
            }

            try
            {
                foreach (var media in Item.MediaItems)
                {
                    media.ItemId = Item.Id;
                }

                await _context.ExchangeItems.AddAsync(Item);
                await _context.SaveChangesAsync();
                _cache.Remove("ItemCategories");

                _logger.LogInformation("Item created successfully: {ItemId}, OrganizationId: {OrgId}", Item.Id, Item.OrganizationId);

                return RedirectToPage("/Exchange/Item", new { id = Item.Id });
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error saving item: {Message}", ex.InnerException?.Message ?? ex.Message);
                ErrorMessage = $"Lỗi khi lưu mặt hàng: {ex.InnerException?.Message ?? ex.Message}";
                await LoadSelectListsAsync();
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error: {Message}", ex.Message);
                ErrorMessage = $"Lỗi không xác định: {ex.Message}";
                await LoadSelectListsAsync();
                return Page();
            }
        }

        private async Task<bool> ValidateUserOrganizationAsync(ApplicationUser user)
        {
            if (string.IsNullOrWhiteSpace(user.OrganizationId))
            {
                _logger.LogDebug("User {UserId} has no OrganizationId.", user.Id);
                return false;
            }

            if (!Guid.TryParse(user.OrganizationId, out _))
            {
                _logger.LogWarning("User {UserId} has invalid OrganizationId format: {OrgId}", user.Id, user.OrganizationId);
                return false;
            }

            var orgExists = await _context.Organizations.AnyAsync(o => o.Id == user.OrganizationId);
            _logger.LogDebug("Organization {OrgId} exists for user {UserId}: {Exists}", user.OrganizationId, user.Id, orgExists);
            return orgExists;
        }

        private async Task<(bool Success, bool HasMedia, string? ErrorMessage)> ProcessMediaFilesAsync()
        {
            try
            {
                if (Images.Any(f => f != null))
                {
                    if (Images.Count > 5)
                        return (false, false, "Chỉ được tải lên tối đa 5 hình ảnh.");

                    foreach (var image in Images.Where(f => f != null))
                    {
                        if (image.Length > 5 * 1024 * 1024)
                            return (false, false, "Ảnh phải nhỏ hơn 5MB.");

                        var ext = Path.GetExtension(image.FileName).ToLower();
                        if (!(ext == ".jpg" || ext == ".jpeg" || ext == ".png"))
                            return (false, false, "Ảnh phải là định dạng JPG hoặc PNG.");

                        var url = await SaveFileAsync(image);
                        Item.MediaItems.Add(new ItemMedia
                        {
                            Id = Guid.NewGuid().ToString(),
                            Url = url,
                            MediaType = MediaType.Image,
                            ItemId = Item.Id
                        });
                    }
                }

                if (Video != null)
                {
                    if (Video.Length > 50 * 1024 * 1024)
                        return (false, false, "Video phải nhỏ hơn 50MB.");

                    var ext = Path.GetExtension(Video.FileName).ToLower();
                    if (ext != ".mp4")
                        return (false, false, "Video phải là định dạng MP4.");

                    var url = await SaveFileAsync(Video);
                    Item.MediaItems.Add(new ItemMedia
                    {
                        Id = Guid.NewGuid().ToString(),
                        Url = url,
                        MediaType = MediaType.Video,
                        ItemId = Item.Id
                    });
                }

                return (true, Item.MediaItems.Any(), null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing media files: {Message}", ex.Message);
                return (false, false, $"Lỗi khi xử lý tệp: {ex.Message}");
            }
        }

        private async Task<string> SaveFileAsync(IFormFile file)
        {
            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Uploads");

            try
            {
                Directory.CreateDirectory(uploadPath);
                var fullPath = Path.Combine(uploadPath, fileName);
                using var stream = new FileStream(fullPath, FileMode.Create);
                await file.CopyToAsync(stream);
                return $"/Uploads/{fileName}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving file: {Message}", ex.Message);
                throw new Exception($"Lỗi khi lưu tệp: {ex.Message}");
            }
        }

        private async Task LoadSelectListsAsync()
        {
            Categories = await _cache.GetOrCreateAsync("ItemCategories", async entry =>
            {
                entry.SlidingExpiration = TimeSpan.FromMinutes(30);
                return await _context.ItemCategories
                    .Select(c => new SelectListItem { Value = c.Id, Text = c.Name })
                    .OrderBy(c => c.Text)
                    .ToListAsync();
            }) ?? new List<SelectListItem>();
        }
    }
}