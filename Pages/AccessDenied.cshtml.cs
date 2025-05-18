#nullable disable
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LoginSystem.Pages
{
    public class AccessDeniedModel : PageModel
    {
        public IActionResult OnGet()
        {
            return Page();
        }
    }
}