using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using LoginSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace LoginSystem.Pages
{
    [Authorize]
    public class ChatModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<ChatModel> _logger;

        public ChatModel(UserManager<ApplicationUser> userManager, ILogger<ChatModel> logger)
        {
            _userManager = userManager;
            _logger = logger;
        }

        public string CurrentUserId { get; set; } = string.Empty;
        public string InitialUserId { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync(string userId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                _logger.LogWarning("No user found for chat page access");
                return Unauthorized();
            }
            CurrentUserId = currentUser.Id;
            _logger.LogInformation("Chat page accessed by user {UserId}", CurrentUserId);

            if (!string.IsNullOrEmpty(userId))
            {
                var targetUser = await _userManager.Users
                    .Where(u => u.Id == userId)
                    .Select(u => new { u.Id })
                    .FirstOrDefaultAsync();
                if (targetUser != null)
                {
                    InitialUserId = targetUser.Id;
                    _logger.LogInformation("Pre-selecting user {TargetUserId} for chat", InitialUserId);
                }
                else
                {
                    _logger.LogWarning("Target user {UserId} not found", userId);
                }
            }

            return Page();
        }
    }
}