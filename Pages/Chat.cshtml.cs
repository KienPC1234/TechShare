using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using LoginSystem.Models;

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

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                _logger.LogWarning("No user found for chat page access.");
                CurrentUserId = string.Empty;
            }
            else
            {
                CurrentUserId = user.Id;
            }
        }
    }
}