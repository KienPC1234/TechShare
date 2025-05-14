using System.ComponentModel.DataAnnotations;

namespace LoginSystem.Models
{
    public class OrganizationNews
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string OrganizationId { get; set; } = string.Empty;

        [Required]
        [StringLength(200, ErrorMessage = "Tiêu đề không được vượt quá 200 ký tự")]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Content { get; set; } = string.Empty; // HTML từ Summernote

        public string? ThumbnailUrl { get; set; } // Ảnh đại diện bài viết

        public string AuthorId { get; set; } = string.Empty; // ID của Admin tạo bài

        public bool IsPublished { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }

    public class OrganizationNewsComment
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string NewsId { get; set; } = string.Empty;

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [StringLength(500, ErrorMessage = "Bình luận không được vượt quá 500 ký tự")]
        public string Content { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}