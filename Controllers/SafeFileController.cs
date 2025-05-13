using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LoginSystem.Controllers
{
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    public class SafeFileController : Controller
    {
        private readonly string _uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "PrivateUploads");

        [HttpGet("GetFile")]
        public IActionResult GetFile(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return BadRequest("File name is required.");

            var safeFileName = Path.GetFileName(name); // tránh path traversal
            var filePath = Path.Combine(_uploadPath, safeFileName);

            if (!System.IO.File.Exists(filePath))
                return NotFound("File not found.");

            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            var mime = ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".mp4" => "video/mp4",
                ".webm" => "video/webm",
                ".ogg" => "video/ogg",
                _ => "application/octet-stream"
            };

            var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            return File(fileStream, mime);
        }
    }

}
