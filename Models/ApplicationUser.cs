using Microsoft.AspNetCore.Identity;

namespace LoginSystem.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string? DisplayName { get; set; }
        public string? AvatarUrl { get; set; }
        public DateTime? LastLoginTime { get; set; }

        // Dành cho yêu cầu lên Admin
        public bool AdminRequestPending { get; set; }
        public string? AdminRequestReason { get; set; }

        // Liên kết với tổ chức
        public string? OrganizationId { get; set; } // ID của tổ chức mà user tham gia
    }
}