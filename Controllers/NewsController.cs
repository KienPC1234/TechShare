
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LoginSystem.Data;
using LoginSystem.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace LoginSystem.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class NewsController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly UserManager<ApplicationUser> _userManager;

        public NewsController(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager)
        {
            _dbContext = dbContext;
            _userManager = userManager;
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateNews([FromBody] CreateNewsDto model)
        {
            if (model == null)
            {
                return BadRequest(new { message = "Dữ liệu không hợp lệ." });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    message = "Dữ liệu không hợp lệ.",
                    errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)
                });
            }

            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "Không thể xác định người dùng." });
            }

            var isAdmin = await _dbContext.OrganizationMembers
                .AnyAsync(m => m.OrganizationId == model.OrganizationId && m.UserId == userId && m.Role == "Admin");
            if (!isAdmin)
            {
                return Forbid("Chỉ Admin mới có thể tạo bài viết.");
            }

            var news = new OrganizationNews
            {
                Id = Guid.NewGuid().ToString(),
                OrganizationId = model.OrganizationId,
                Title = model.Title.Trim(),
                Content = model.Content.Trim(),
                ThumbnailUrl = model.ThumbnailUrl ?? string.Empty,
                AuthorId = userId,
                IsPublished = model.IsPublished,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.OrganizationNews.Add(news);
            await _dbContext.SaveChangesAsync();

            return Ok(new { newsId = news.Id, message = "Tạo bài viết thành công." });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateNews(string id, [FromBody] UpdateNewsDto model)
        {
            if (string.IsNullOrEmpty(id))
            {
                return BadRequest(new { message = "ID bài viết không hợp lệ." });
            }

            if (model == null)
            {
                return BadRequest(new { message = "Dữ liệu không hợp lệ." });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    message = "Dữ liệu không hợp lệ.",
                    errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)
                });
            }

            var news = await _dbContext.OrganizationNews.FirstOrDefaultAsync(n => n.Id == id);
            if (news == null)
            {
                return NotFound(new { message = "Bài viết không tồn tại." });
            }

            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "Không thể xác định người dùng." });
            }

            var isAdmin = await _dbContext.OrganizationMembers
                .AnyAsync(m => m.OrganizationId == news.OrganizationId && m.UserId == userId && m.Role == "Admin");
            if (!isAdmin)
            {
                return Forbid("Chỉ Admin mới có thể chỉnh sửa bài viết.");
            }

            news.Title = model.Title.Trim();
            news.Content = model.Content.Trim();
            news.ThumbnailUrl = model.ThumbnailUrl ?? string.Empty;
            news.IsPublished = model.IsPublished;
            news.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
            return Ok(new { message = "Cập nhật bài viết thành công." });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteNews(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return BadRequest(new { message = "ID bài viết không hợp lệ." });
            }

            var news = await _dbContext.OrganizationNews.FirstOrDefaultAsync(n => n.Id == id);
            if (news == null)
            {
                return NotFound(new { message = "Bài viết không tồn tại." });
            }

            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "Không thể xác định người dùng." });
            }

            var isAdmin = await _dbContext.OrganizationMembers
                .AnyAsync(m => m.OrganizationId == news.OrganizationId && m.UserId == userId && m.Role == "Admin");
            if (!isAdmin)
            {
                return Forbid("Chỉ Admin mới có thể xóa bài viết.");
            }

            _dbContext.OrganizationNews.Remove(news);
            await _dbContext.SaveChangesAsync();
            return Ok(new { message = "Xóa bài viết thành công." });
        }

        public class CreateNewsDto
        {
            [Required(ErrorMessage = "OrganizationId là bắt buộc")]
            public string OrganizationId { get; set; } = string.Empty;

            [Required(ErrorMessage = "Tiêu đề là bắt buộc")]
            [StringLength(200, ErrorMessage = "Tiêu đề không được vượt quá 200 ký tự")]
            public string Title { get; set; } = string.Empty;

            [Required(ErrorMessage = "Nội dung là bắt buộc")]
            [StringLength(5000, ErrorMessage = "Nội dung không được vượt quá 5000 ký tự")]
            public string Content { get; set; } = string.Empty;

            public string? ThumbnailUrl { get; set; }

            public bool IsPublished { get; set; } = true;
        }

        public class UpdateNewsDto
        {
            [Required(ErrorMessage = "Tiêu đề là bắt buộc")]
            [StringLength(200, ErrorMessage = "Tiêu đề không được vượt quá 200 ký tự")]
            public string Title { get; set; } = string.Empty;

            [Required(ErrorMessage = "Nội dung là bắt buộc")]
            [StringLength(5000, ErrorMessage = "Nội dung không được vượt quá 5000 ký tự")]
            public string Content { get; set; } = string.Empty;

            public string? ThumbnailUrl { get; set; }

            public bool IsPublished { get; set; } = true;
        }
    }
}
