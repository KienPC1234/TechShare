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
}