using System.ComponentModel.DataAnnotations;

namespace LoginSystem.Models
{
    public class Organization
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string Name { get; set; } = string.Empty; // Tên tổ chức, hỗ trợ tiếng Việt

        [Required]
        public string Slug { get; set; } = string.Empty; // Slug dùng cho URL (ASCII)

        public string? AvatarUrl { get; set; } // Avatar của tổ chức

        [Required]
        public string Terms { get; set; } = string.Empty; // Điều khoản tham gia

        public bool IsPrivate { get; set; } // Riêng tư hay cộng đồng

        public string? Description { get; set; } // Mô tả tổ chức (HTML từ Summernote)

        public string CreatorId { get; set; } = string.Empty; // ID của người tạo (Admin)

        public bool IsVerified { get; set; } // Trạng thái xác minh bởi SuperAdmin

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class OrganizationRating
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string OrganizationId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public int Score { get; set; } // Điểm đánh giá từ 1-5
        public DateTime RatedAt { get; set; } = DateTime.UtcNow;
    }

    public class OrganizationReport
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string OrganizationId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public DateTime ReportedAt { get; set; } = DateTime.UtcNow;
    }

    public class OrganizationMember
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string OrganizationId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Role { get; set; } = "Member"; // Vai trò: Member, Admin
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    }

    public class OrganizationJoinRequest
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string OrganizationId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    }

    public class OrganizationComment
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string OrganizationId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}