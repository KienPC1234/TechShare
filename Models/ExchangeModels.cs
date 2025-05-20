using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace LoginSystem.Models
{
    public class ExchangeItem
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required(ErrorMessage = "Tiêu đề là bắt buộc.")]
        [MaxLength(200, ErrorMessage = "Tiêu đề không được vượt quá 200 ký tự.")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mô tả là bắt buộc.")]
        [MaxLength(5000, ErrorMessage = "Mô tả không được vượt quá 5000 ký tự.")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Điều khoản là bắt buộc.")]
        [MaxLength(1000, ErrorMessage = "Điều khoản không được vượt quá 1000 ký tự.")]
        public string Terms { get; set; } = string.Empty;

        [Required(ErrorMessage = "Số lượng là bắt buộc.")]
        [Range(0, int.MaxValue, ErrorMessage = "Số lượng phải lớn hơn hoặc bằng 0.")]
        public int QuantityAvailable { get; set; }

        [Required(ErrorMessage = "Địa chỉ lấy hàng là bắt buộc.")]
        [MaxLength(500, ErrorMessage = "Địa chỉ không được vượt quá 500 ký tự.")]
        public string PickupAddress { get; set; } = string.Empty;

        [Required(ErrorMessage = "Người sở hữu là bắt buộc.")]
        public string OwnerId { get; set; } = string.Empty;

        public string? OrganizationId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn danh mục.")]
        public string? CategoryId { get; set; }

        public bool IsPrivate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public ApplicationUser? Owner { get; set; }
        public ItemCategory? Category { get; set; }
        public List<ItemTag> Tags { get; set; } = new List<ItemTag>();
        public List<ItemMedia> MediaItems { get; set; } = new List<ItemMedia>();
        public List<ItemReport> Reports { get; set; } = new List<ItemReport>();

        [NotMapped]
        public List<ItemMedia> Images => MediaItems?.Where(m => m.MediaType == MediaType.Image).ToList() ?? new List<ItemMedia>();

        [NotMapped]
        public List<ItemMedia> Videos => MediaItems?.Where(m => m.MediaType == MediaType.Video).ToList() ?? new List<ItemMedia>();
    }

    public enum MediaType
    {
        Image,
        Video
    }

    public class ItemMedia
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string ItemId { get; set; } = string.Empty;

        public ExchangeItem? Item { get; set; }

        [Required(ErrorMessage = "URL là bắt buộc.")]
        public string Url { get; set; } = string.Empty;

        public MediaType MediaType { get; set; }
    }

    public class ItemComment
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string ItemId { get; set; } = string.Empty;

        public ExchangeItem? Item { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public ApplicationUser? User { get; set; }

        [Required, MaxLength(1000)]
        public string Content { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class ItemRating
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string ItemId { get; set; } = string.Empty;

        public ExchangeItem? Item { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public ApplicationUser? User { get; set; }

        [Range(1, 5)]
        public int Score { get; set; }

        public DateTime RatedAt { get; set; } = DateTime.UtcNow;
    }

    public class ItemReport
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string ItemId { get; set; } = string.Empty;

        public ExchangeItem? Item { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public ApplicationUser? User { get; set; }

        [Required, MaxLength(1000)]
        public string Reason { get; set; } = string.Empty;

        public DateTime ReportedAt { get; set; } = DateTime.UtcNow;
    }

    public class ItemTag
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string ItemId { get; set; } = string.Empty;

        public ExchangeItem? Item { get; set; }

        [Required, MaxLength(50)]
        public string Tag { get; set; } = string.Empty;
    }

    public class ItemCategory
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}