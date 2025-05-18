using Microsoft.AspNetCore.Identity;

namespace LoginSystem.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string? DisplayName { get; set; }
        public string? AvatarUrl { get; set; }
        public DateTime? LastLoginTime { get; set; }
        public bool AdminRequestPending { get; set; }
        public string? AdminRequestReason { get; set; }
        public string? OrganizationId { get; set; }
        public string? TwoFactorMethod { get; set; } // "None", "TOTP", "Email"
        public string? TwoFactorSecretKey { get; set; }
    }
}