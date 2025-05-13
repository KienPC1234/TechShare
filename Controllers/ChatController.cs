using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LoginSystem.Data;
using LoginSystem.Models;
using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.Logging;
using System.IO;
using Microsoft.AspNetCore.Http;

namespace LoginSystem.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<ChatController> _logger;

        public ChatController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, ILogger<ChatController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetDefaultUsers()
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                var query = _userManager.Users.AsQueryable();
                if (currentUser != null)
                {
                    query = query.Where(u => u.Id != currentUser.Id);
                }
                var users = await query
                    .OrderBy(u => u.DisplayName ?? u.UserName)
                    .Take(50)
                    .Select(u => new
                    {
                        id = u.Id,
                        userName = u.UserName,
                        displayName = u.DisplayName,
                        avatarUrl = u.AvatarUrl
                    })
                    .ToListAsync();

                _logger.LogInformation("Loaded {Count} default users.", users.Count);
                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading default users: {Message}", ex.Message);
                return StatusCode(500, new { error = "Failed to load users." });
            }
        }

        [HttpGet("users/search")]
        public async Task<IActionResult> SearchUsers([FromQuery] string query)
        {
            try
            {
                _logger.LogInformation("Searching users with query: {Query}", query);
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                {
                    _logger.LogWarning("Current user is null for search query: {Query}", query);
                    return Unauthorized(new { error = "User not authenticated." });
                }

                if (string.IsNullOrWhiteSpace(query))
                {
                    _logger.LogInformation("Empty query, loading default users.");
                    return await GetDefaultUsers();
                }

                var queryable = _userManager.Users.AsQueryable();
                queryable = queryable.Where(u => u.Id != currentUser.Id);
                var users = await queryable
                    .Where(u => EF.Functions.Like(u.UserName, $"%{query}%") ||
                                EF.Functions.Like(u.DisplayName, $"%{query}%"))
                    .OrderBy(u => u.DisplayName ?? u.UserName)
                    .Take(50)
                    .Select(u => new
                    {
                        id = u.Id,
                        userName = u.UserName,
                        displayName = u.DisplayName,
                        avatarUrl = u.AvatarUrl
                    })
                    .ToListAsync();

                _logger.LogInformation("Search returned {Count} users for query: {Query}.", users.Count, query);
                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching users with query: {Query}, Message: {Message}, StackTrace: {StackTrace}", query, ex.Message, ex.StackTrace);
                return StatusCode(500, new { error = "Search failed: " + ex.Message });
            }
        }

        [HttpGet("messages")]
        public async Task<IActionResult> GetMessages([FromQuery] string receiverId)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null || string.IsNullOrEmpty(receiverId))
                {
                    _logger.LogWarning("Invalid message load request: User={UserId}, Receiver={ReceiverId}.", currentUser?.Id, receiverId);
                    return BadRequest(new { error = "Invalid user or receiver ID." });
                }

                var messages = await _context.Messages
                    .Where(m => (m.SenderId == currentUser.Id && m.ReceiverId == receiverId) ||
                                (m.SenderId == receiverId && m.ReceiverId == currentUser.Id))
                    .OrderBy(m => m.CreatedAt)
                    .Select(m => new
                    {
                        id = m.Id,
                        senderId = m.SenderId,
                        senderName = m.Sender.DisplayName ?? m.Sender.UserName,
                        senderAvatar = m.Sender.AvatarUrl,
                        receiverId = m.ReceiverId,
                        content = m.Content,
                        contentType = m.ContentType,
                        createdAt = m.CreatedAt
                    })
                    .Take(100)
                    .ToListAsync();

                _logger.LogInformation("Loaded {Count} messages for user {UserId} and receiver {ReceiverId}.", messages.Count, currentUser.Id, receiverId);
                return Ok(messages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading messages for receiver: {ReceiverId}, Message: {Message}", receiverId, ex.Message);
                return StatusCode(500, new { error = "Failed to load messages." });
            }
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadFile(IFormFile file)
        {
            try
            {
                _logger.LogInformation("Received file upload request: FileName={FileName}, ContentLength={Length}", file?.FileName, file?.Length);

                if (file == null || file.Length == 0)
                {
                    _logger.LogWarning("No file uploaded or file is empty.");
                    return BadRequest(new { error = "No file uploaded or file is empty." });
                }

                // 1. Giới hạn dung lượng
                const int maxSize = 10 * 1024 * 1024;
                if (file.Length > maxSize)
                {
                    _logger.LogWarning("File too large: {FileSize} bytes.", file.Length);
                    return BadRequest(new { error = "File size exceeds 10MB limit." });
                }

                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".mp4", ".webm", ".ogg" };
                var allowedMimeTypes = new[] {
            "image/jpeg", "image/png", "image/gif", "image/webp",
            "video/mp4", "video/webm", "video/ogg"
        };

                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(extension))
                {
                    _logger.LogWarning("Invalid file extension: {Extension}.", extension);
                    return BadRequest(new { error = "Invalid file type." });
                }

                if (!allowedMimeTypes.Contains(file.ContentType))
                {
                    _logger.LogWarning("Invalid MIME type: {MimeType}.", file.ContentType);
                    return BadRequest(new { error = "Invalid file content." });
                }

                using (var memStream = new MemoryStream())
                {
                    await file.CopyToAsync(memStream);
                    var buffer = memStream.ToArray();

                    if (extension == ".jpg" && !(buffer.Length > 2 && buffer[0] == 0xFF && buffer[1] == 0xD8))
                    {
                        _logger.LogWarning("Fake JPG file detected.");
                        return BadRequest(new { error = "Invalid JPG file content." });
                    }

                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "PrivateUploads"); 
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    var fileName = $"{Guid.NewGuid()}{extension}";
                    var filePath = Path.Combine(uploadsFolder, fileName);

                    await System.IO.File.WriteAllBytesAsync(filePath, buffer);

                    var safeAccessUrl = Url.Action("GetFile", "SafeFile", new { name = fileName });

                    _logger.LogInformation("File uploaded safely: {FileName}", fileName);
                    return Ok(new { fileUrl = safeAccessUrl });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file: {Message}", ex.Message);
                return StatusCode(500, new { error = "Upload failed: " + ex.Message });
            }
        }
    }
}