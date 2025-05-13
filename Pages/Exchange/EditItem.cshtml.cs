using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using LoginSystem.Data;
using LoginSystem.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace LoginSystem.Pages.Exchange
{
    [Authorize]
    public class EditItemModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IMemoryCache _cache;

        public EditItemModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IMemoryCache cache)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        [BindProperty]
        public ExchangeItem Item { get; set; } = new ExchangeItem();

        [BindProperty]
        public List<IFormFile> Images { get; set; } = new List<IFormFile>();

        [BindProperty]
        public IFormFile? Video { get; set; }

        [BindProperty]
        public string Tags { get; set; } = string.Empty;

        [BindProperty]
        public List<string> DeleteMedia { get; set; } = new List<string>();

        public List<SelectListItem> Categories { get; private set; } = new List<SelectListItem>();

        public string? ErrorMessage { get; private set; }

        public bool HasOrganization { get; private set; }

        public async Task<IActionResult> OnGetAsync(string? id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound("ID mặt hàng không được cung cấp.");
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Forbid();
            }

            Item = await _context.ExchangeItems
                .Include(i => i.MediaItems)
                .Include(i => i.Tags)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (Item == null || Item.OwnerId != user.Id)
            {
                return NotFound("Mặt hàng không tồn tại hoặc bạn không có quyền chỉnh sửa.");
            }

            Tags = string.Join(", ", Item.Tags.Select(t => t.Tag));
            HasOrganization = !string.IsNullOrWhiteSpace(user.OrganizationId) && await _context.Organizations.AnyAsync(o => o.Id == user.OrganizationId);
            await LoadSelectListsAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(string? id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound("ID mặt hàng không được cung cấp.");
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Forbid();
            }

            var itemToUpdate = await _context.ExchangeItems
                .Include(i => i.MediaItems)
                .Include(i => i.Tags)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (itemToUpdate == null || itemToUpdate.OwnerId != user.Id)
            {
                return NotFound("Mặt hàng không tồn tại hoặc bạn không có quyền chỉnh sửa.");
            }

            // Update basic properties
            itemToUpdate.Title = Item.Title;
            itemToUpdate.Description = Item.Description;
            itemToUpdate.Terms = Item.Terms;
            itemToUpdate.QuantityAvailable = Item.QuantityAvailable;
            itemToUpdate.PickupAddress = Item.PickupAddress;
            itemToUpdate.CategoryId = Item.CategoryId;
            itemToUpdate.IsPrivate = Item.IsPrivate;
            itemToUpdate.UpdatedAt = DateTime.UtcNow;

            // Normalize OrganizationId and handle invalid cases
            if (!string.IsNullOrWhiteSpace(itemToUpdate.OrganizationId))
            {
                var orgExists = await _context.Organizations.AnyAsync(o => o.Id == itemToUpdate.OrganizationId);
                if (!orgExists)
                {
                    itemToUpdate.OrganizationId = null;
                }
            }
            else
            {
                itemToUpdate.OrganizationId = null;
            }

            // Ensure IsPrivate is false if no organization
            if (itemToUpdate.IsPrivate && itemToUpdate.OrganizationId == null)
            {
                itemToUpdate.IsPrivate = false;
            }

            // Validate model state
            if (!ModelState.IsValid)
            {
                var errors = ModelState.SelectMany(kvp => kvp.Value.Errors.Select(e => $"{kvp.Key}: {e.ErrorMessage}"));
                ErrorMessage = "Dữ liệu không hợp lệ: " + string.Join("; ", errors);
                HasOrganization = !string.IsNullOrWhiteSpace(user.OrganizationId) && await _context.Organizations.AnyAsync(o => o.Id == user.OrganizationId);
                await LoadSelectListsAsync();
                return Page();
            }

            // Validate required fields
            if (string.IsNullOrWhiteSpace(itemToUpdate.Title) || itemToUpdate.Title.Length > 200)
            {
                ErrorMessage = "Tiêu đề không hợp lệ hoặc vượt quá 200 ký tự.";
                HasOrganization = !string.IsNullOrWhiteSpace(user.OrganizationId) && await _context.Organizations.AnyAsync(o => o.Id == user.OrganizationId);
                await LoadSelectListsAsync();
                return Page();
            }

            if (string.IsNullOrWhiteSpace(itemToUpdate.Description) || itemToUpdate.Description.Length > 5000)
            {
                ErrorMessage = "Mô tả không hợp lệ hoặc vượt quá 5000 ký tự.";
                HasOrganization = !string.IsNullOrWhiteSpace(user.OrganizationId) && await _context.Organizations.AnyAsync(o => o.Id == user.OrganizationId);
                await LoadSelectListsAsync();
                return Page();
            }

            if (string.IsNullOrWhiteSpace(itemToUpdate.Terms) || itemToUpdate.Terms.Length > 1000)
            {
                ErrorMessage = "Điều khoản không hợp lệ hoặc vượt quá 1000 ký tự.";
                HasOrganization = !string.IsNullOrWhiteSpace(user.OrganizationId) && await _context.Organizations.AnyAsync(o => o.Id == user.OrganizationId);
                await LoadSelectListsAsync();
                return Page();
            }

            if (string.IsNullOrWhiteSpace(itemToUpdate.PickupAddress) || itemToUpdate.PickupAddress.Length > 500)
            {
                ErrorMessage = "Địa chỉ lấy hàng không hợp lệ hoặc vượt quá 500 ký tự.";
                HasOrganization = !string.IsNullOrWhiteSpace(user.OrganizationId) && await _context.Organizations.AnyAsync(o => o.Id == user.OrganizationId);
                await LoadSelectListsAsync();
                return Page();
            }

            if (itemToUpdate.QuantityAvailable < 0)
            {
                ErrorMessage = "Số lượng phải lớn hơn hoặc bằng 0.";
                HasOrganization = !string.IsNullOrWhiteSpace(user.OrganizationId) && await _context.Organizations.AnyAsync(o => o.Id == user.OrganizationId);
                await LoadSelectListsAsync();
                return Page();
            }

            if (string.IsNullOrEmpty(itemToUpdate.CategoryId))
            {
                ErrorMessage = "Danh mục là bắt buộc.";
                HasOrganization = !string.IsNullOrWhiteSpace(user.OrganizationId) && await _context.Organizations.AnyAsync(o => o.Id == user.OrganizationId);
                await LoadSelectListsAsync();
                return Page();
            }

            var categoryExists = await _context.ItemCategories.AnyAsync(c => c.Id == itemToUpdate.CategoryId);
            if (!categoryExists)
            {
                ErrorMessage = "Danh mục không tồn tại.";
                HasOrganization = !string.IsNullOrWhiteSpace(user.OrganizationId) && await _context.Organizations.AnyAsync(o => o.Id == user.OrganizationId);
                await LoadSelectListsAsync();
                return Page();
            }

            // Process media deletions
            if (DeleteMedia.Any())
            {
                var mediaToDelete = itemToUpdate.MediaItems.Where(m => DeleteMedia.Contains(m.Id)).ToList();
                foreach (var media in mediaToDelete)
                {
                    itemToUpdate.MediaItems.Remove(media);
                    _context.ItemMedia.Remove(media);
                }
            }

            // Process new media uploads
            var mediaResult = await ProcessMediaFilesAsync(itemToUpdate);
            if (!mediaResult.Success)
            {
                ErrorMessage = mediaResult.ErrorMessage;
                HasOrganization = !string.IsNullOrWhiteSpace(user.OrganizationId) && await _context.Organizations.AnyAsync(o => o.Id == user.OrganizationId);
                await LoadSelectListsAsync();
                return Page();
            }

            if (!itemToUpdate.MediaItems.Any())
            {
                ErrorMessage = "Vui lòng giữ hoặc tải lên ít nhất một hình ảnh hoặc video.";
                HasOrganization = !string.IsNullOrWhiteSpace(user.OrganizationId) && await _context.Organizations.AnyAsync(o => o.Id == user.OrganizationId);
                await LoadSelectListsAsync();
                return Page();
            }

            // Process tags
            itemToUpdate.Tags.Clear();
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
                    HasOrganization = !string.IsNullOrWhiteSpace(user.OrganizationId) && await _context.Organizations.AnyAsync(o => o.Id == user.OrganizationId);
                    await LoadSelectListsAsync();
                    return Page();
                }

                itemToUpdate.Tags = tagList.Select(t => new ItemTag
                {
                    Id = Guid.NewGuid().ToString(),
                    Tag = t,
                    ItemId = itemToUpdate.Id
                }).ToList();
            }

            try
            {
                foreach (var media in itemToUpdate.MediaItems)
                {
                    media.ItemId = itemToUpdate.Id;
                }

                await _context.SaveChangesAsync();
                return RedirectToPage("/Exchange/Item", new { id = itemToUpdate.Id });
            }
            catch (DbUpdateException ex)
            {
                ErrorMessage = $"Lỗi khi lưu thay đổi: {ex.InnerException?.Message ?? ex.Message}";
                HasOrganization = !string.IsNullOrWhiteSpace(user.OrganizationId) && await _context.Organizations.AnyAsync(o => o.Id == user.OrganizationId);
                await LoadSelectListsAsync();
                return Page();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Lỗi không xác định: {ex.Message}";
                HasOrganization = !string.IsNullOrWhiteSpace(user.OrganizationId) && await _context.Organizations.AnyAsync(o => o.Id == user.OrganizationId);
                await LoadSelectListsAsync();
                return Page();
            }
        }

        private async Task<(bool Success, string? ErrorMessage)> ProcessMediaFilesAsync(ExchangeItem item)
        {
            try
            {
                if (Images.Any(f => f != null))
                {
                    var currentImageCount = item.MediaItems.Count(m => m.MediaType == MediaType.Image);
                    if (currentImageCount + Images.Count > 5)
                        return (false, "Tổng số hình ảnh không được vượt quá 5.");

                    foreach (var image in Images.Where(f => f != null))
                    {
                        if (image.Length > 5 * 1024 * 1024)
                            return (false, "Ảnh phải nhỏ hơn 5MB.");

                        var ext = Path.GetExtension(image.FileName).ToLower();
                        if (!(ext == ".jpg" || ext == ".jpeg" || ext == ".png"))
                            return (false, "Ảnh phải là định dạng JPG hoặc PNG.");

                        var url = await SaveFileAsync(image);
                        item.MediaItems.Add(new ItemMedia
                        {
                            Id = Guid.NewGuid().ToString(),
                            Url = url,
                            MediaType = MediaType.Image,
                            ItemId = item.Id
                        });
                    }
                }

                if (Video != null)
                {
                    if (Video.Length > 50 * 1024 * 1024)
                        return (false, "Video phải nhỏ hơn 50MB.");

                    var ext = Path.GetExtension(Video.FileName).ToLower();
                    if (ext != ".mp4")
                        return (false, "Video phải là định dạng MP4.");

                    var existingVideo = item.MediaItems.FirstOrDefault(m => m.MediaType == MediaType.Video);
                    if (existingVideo != null && !DeleteMedia.Contains(existingVideo.Id))
                    {
                        return (false, "Chỉ được phép có tối đa 1 video. Vui lòng xóa video hiện tại trước khi tải lên video mới.");
                    }

                    var url = await SaveFileAsync(Video);
                    item.MediaItems.Add(new ItemMedia
                    {
                        Id = Guid.NewGuid().ToString(),
                        Url = url,
                        MediaType = MediaType.Video,
                        ItemId = item.Id
                    });
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, $"Lỗi khi xử lý tệp: {ex.Message}");
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